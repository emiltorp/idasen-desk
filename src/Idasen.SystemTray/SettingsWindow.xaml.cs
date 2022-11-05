using Idasen.BluetoothLE.Core;
using Idasen.SystemTray.Interfaces;
using Idasen.SystemTray.Utils;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Idasen.SystemTray
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class SettingsWindow
        : ISettingsWindow
    {
        public SettingsWindow(
            ILogger logger,
            ISettingsManager manager,
            IVersionProvider provider)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(manager, nameof(manager));
            Guard.ArgumentNotNull(provider, nameof(provider));

            _logger = logger;
            _manager = manager;

            InitializeComponent();

            this.Title = $"Idasen Desk Settings ({provider.GetVersion()})";

            _ = Task.Run(Initialize);
        }

        public event EventHandler AdvancedSettingsChanged;
        public event EventHandler<LockSettingsChangedEventArgs> LockSettingsChanged;

        private async void Initialize()
        {
            try
            {
                await _manager.Load();

                Update(_manager.CurrentSettings);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to initialize");
            }
        }

        private void ImageClose_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _logger.Debug($"Closing {GetType().Name}...");

            Close();
        }

        private void StoreSettings()
        {
            if (_storingSettingsTask?.Status == TaskStatus.Running)
            {
                _logger.Warning("Storing Settings already in progress");

                return;
            }

            ISettings settings = _manager.CurrentSettings;

            string newDeviceName = _nameConverter.DefaultIfEmpty(DeskName.Text);
            ulong newDeviceAddress = _addressConverter.DefaultIfEmpty(DeskAddress.Text);
            bool newDeviceLocked = Locked.IsChecked ?? false;

            bool advancedChanged = settings.DeviceName != newDeviceName ||
                                  settings.DeviceAddress != newDeviceAddress;

            bool lockChanged = settings.DeviceLocked != newDeviceLocked;

            settings.StandingHeightInCm = _doubleConverter.ConvertToUInt(Standing.Value,
                                                                           Constants.DefaultHeightStandingInCm);
            settings.SeatingHeightInCm = _doubleConverter.ConvertToUInt(Seating.Value,
                                                                          Constants.DefaultHeightSeatingInCm);
            settings.DeviceName = newDeviceName;
            settings.DeviceAddress = newDeviceAddress;
            settings.DeviceLocked = newDeviceLocked;

            _storingSettingsTask = Task.Run(async () =>
                                              {
                                                  await DoStoreSettings(settings,
                                                                          advancedChanged,
                                                                          lockChanged);
                                              });
        }

        private async Task DoStoreSettings(ISettings settings, bool advancedChanged, bool lockChanged)
        {
            try
            {
                _logger.Debug($"Storing new settings: {settings}");

                await _manager.Save();

                if (advancedChanged)
                {
                    _logger.Information("Advanced settings have changed, reconnecting...");

                    AdvancedSettingsChanged?.Invoke(this, EventArgs.Empty);
                }

                if (lockChanged)
                {
                    _logger.Information("Advanced Locked settings have changed...");

                    LockSettingsChanged?.Invoke(this, new LockSettingsChangedEventArgs(settings.DeviceLocked));
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to store settings");
            }
        }

        private void SettingsWindow_OnClosed(object sender, EventArgs e)
        {
            _logger.Debug("Handling 'Closed' event");

            StoreSettings();
        }

        private void Update(ISettings settings)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => Update(settings)));

                return;
            }

            _logger.Debug($"Update settings: {settings}");

            Standing.Value = settings.StandingHeightInCm;
            Standing.Minimum = settings.DeskMinHeightInCm;
            Standing.Maximum = settings.DeskMaxHeightInCm;
            Seating.Value = settings.SeatingHeightInCm;
            Seating.Minimum = settings.DeskMinHeightInCm;
            Seating.Maximum = settings.DeskMaxHeightInCm;
            DeskName.Text = _nameConverter.EmptyIfDefault(settings.DeviceName);
            DeskAddress.Text = _addressConverter.EmptyIfDefault(settings.DeviceAddress);
            Locked.IsChecked = settings.DeviceLocked;
        }

        private readonly IDoubleToUIntConverter _doubleConverter = new DoubleToUIntConverter();
        private readonly IDeviceNameConverter _nameConverter = new DeviceNameConverter();
        private readonly IDeviceAddressToULongConverter _addressConverter = new DeviceAddressToULongConverter();
        private readonly ILogger _logger;
        private readonly ISettingsManager _manager;
        private Task _storingSettingsTask;
    }
}