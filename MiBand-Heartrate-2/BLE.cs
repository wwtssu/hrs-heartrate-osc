using System;
using System.ComponentModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace MiBand_Heartrate_2
{
    public class BLE : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        DeviceWatcher[] _watchers;

        public DeviceWatcher[] Watchers
        {
            get { return _watchers; }
            set
            {
                _watchers = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Watcher"));
            }
        }

        // --------------------------------------

        public BLE(string[] filters)
        {
            Watchers = [
                DeviceInformation.CreateWatcher(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                    filters,
                    DeviceInformationKind.AssociationEndpoint
                ),
                DeviceInformation.CreateWatcher(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                    filters,
                    DeviceInformationKind.AssociationEndpoint
                )
            ];
        }

        ~BLE()
        {
            if (Watchers != null)
            {
                StopWatcher();
            }
            Watchers = null;
        }

        public void StartWatcher()
        {
            if (Watchers != null && Watchers.Length != 0)
            {
                foreach (DeviceWatcher Watcher in Watchers) {
                    Watcher.Start();
                }
            }
        }

        public void StopWatcher()
        {
            if (Watchers != null)
            {
                foreach (DeviceWatcher Watcher in Watchers)
                {
                    if (Watcher.Status == DeviceWatcherStatus.Started) {
                        Watcher.Stop();
                    }
                }
            }
        }

        // --------------------------------------

        static async public void Write(GattCharacteristic characteristic, byte[] data)
        {
            using (var stream = new DataWriter())
            {
                stream.WriteBytes(data);

                try
                {
                    GattCommunicationStatus r = await characteristic.WriteValueAsync(stream.DetachBuffer());

                    if (r != GattCommunicationStatus.Success)
                    {
                        Console.WriteLine(string.Format("Unable to write on {0} - {1}", characteristic.Uuid, r));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
