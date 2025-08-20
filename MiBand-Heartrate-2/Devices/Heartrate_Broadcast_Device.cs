using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace MiBand_Heartrate_2.Devices
{
    public class Heartrate_Broadcast_Device : Device
    {
        const string HEARTRATE_SRV_ID = "0000180d-0000-1000-8000-00805f9b34fb";
        const string HEARTRATE_NOTIFY_CHAR_ID = "00002a37-0000-1000-8000-00805f9b34fb";

        // --------------------------------------

        byte[] _key;

        BluetoothLEDevice _connectedDevice;

        GattDeviceService _heartrateService = null;

        GattCharacteristic _heartrateNotifyCharacteristic = null;

        string _deviceId = "";

        bool _continuous = false;

        public Heartrate_Broadcast_Device(DeviceInformation d)
        {
            _deviceId = d.Id;

            Name = d.Name;
            Model = DeviceModel.HEARTRATE_BROADCAST;
        }

        public override void Authenticate()
        {
            if (Status == Devices.DeviceStatus.ONLINE_UNAUTH)
            {
                Status = Devices.DeviceStatus.ONLINE_AUTH;
            }
        }

        public override void Connect()
        {
            Disconnect();

            if (_connectedDevice == null)
            {
                var task = Task.Run(async () => await BluetoothLEDevice.FromIdAsync(_deviceId));

                _connectedDevice = task.Result;
                _connectedDevice.ConnectionStatusChanged += OnDeviceConnectionChanged;

                Status = Devices.DeviceStatus.ONLINE_UNAUTH;

                Authenticate();
            }
        }

        private void OnDeviceConnectionChanged(BluetoothLEDevice sender, object args)
        {
            if (_connectedDevice != null && _connectedDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                Status = Devices.DeviceStatus.OFFLINE;
            }
        }

        public override void Disconnect()
        {
            StopHeartrateMonitor();

            if (_connectedDevice != null)
            {
                _connectedDevice.Dispose();
                _connectedDevice = null;
            }

            Status = Devices.DeviceStatus.OFFLINE;
        }

        public override void Dispose()
        {
            Disconnect();
        }


        public override void StartHeartrateMonitor(bool continuous = false)
        {
            if (HeartrateMonitorStarted)
                return;


            _continuous = continuous;

            var task = Task.Run(async () =>
            {
                GattDeviceServicesResult heartrateService = await _connectedDevice.GetGattServicesForUuidAsync(new Guid(HEARTRATE_SRV_ID));

                if (heartrateService.Status == GattCommunicationStatus.Success && heartrateService.Services.Count > 0)
                {
                    _heartrateService = heartrateService.Services[0];

                    GattCharacteristicsResult heartrateNotifyCharacteristic = await _heartrateService.GetCharacteristicsForUuidAsync(new Guid(HEARTRATE_NOTIFY_CHAR_ID));

                    if (heartrateNotifyCharacteristic.Status == GattCommunicationStatus.Success && heartrateNotifyCharacteristic.Characteristics.Count > 0)
                    {
                        GattCommunicationStatus notify = await heartrateNotifyCharacteristic.Characteristics[0].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                        if (notify == GattCommunicationStatus.Success)
                        {
                            _heartrateNotifyCharacteristic = heartrateNotifyCharacteristic.Characteristics[0];
                            _heartrateNotifyCharacteristic.ValueChanged += OnHeartrateNotify;
                        }
                    }
                }

                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
                    HeartrateMonitorStarted = true;
                });
            });
        }

        public override void StopHeartrateMonitor()
        {
            if (!HeartrateMonitorStarted)
                return;

            _heartrateNotifyCharacteristic = null;

            if (_heartrateService != null)
            {
                _heartrateService.Dispose();
                _heartrateService = null;
            }

            System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
                HeartrateMonitorStarted = false;
            });

            GC.Collect();
        }


        void OnHeartrateNotify(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            using (DataReader reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                int value = (ushort)reader.ReadUInt16() & (ushort)0xFF;

                if (value > 0) // when sensor fail to retrieve heartrate it send a 0 value
                {
                    Heartrate = (ushort)value;
                }

                if (!_continuous)
                {
                    StopHeartrateMonitor();
                }
            }
        }
    }
}
