using Idasen.BluetoothLE.Core;
using Idasen.BluetoothLE.Linak.Interfaces;
using Idasen.SystemTray.Interfaces;
using Idasen.SystemTray.Utils;
using Microsoft.Toolkit.Uwp.Notifications;
using NHotkey;
using NHotkey.Wpf;
using Serilog;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using Constants = Idasen.BluetoothLE.Characteristics.Common.Constants;
using MessageBox = System.Windows.MessageBox;

namespace Idasen.SystemTray
{
    /// <summary>
    ///     Provides bindable properties and commands for the NotifyIcon. In this sample, the
    ///     view model is assigned to the NotifyIcon in XAML. Alternatively, the startup routing
    ///     in App.xaml.cs could have created this view model, and assigned it to the NotifyIcon.
    /// </summary>
    public class NotifyIconViewModel
        : IDisposable
    {
        private static readonly KeyGesture IncrementGesture = new(Key.Up, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);
        private static readonly KeyGesture DecrementGesture = new(Key.Down, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);

        public NotifyIconViewModel()
        {
        }

        public NotifyIconViewModel(
            ILogger logger,
            ISettingsManager manager,
            Func<IDeskProvider> providerFactory,
            IScheduler scheduler,
            IErrorManager errorManager,
            IVersionProvider versionProvider,
            Func<Application, ITaskbarIconProvider> factory)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(manager, nameof(manager));
            Guard.ArgumentNotNull(providerFactory, nameof(providerFactory));
            Guard.ArgumentNotNull(scheduler, nameof(scheduler));
            Guard.ArgumentNotNull(errorManager, nameof(errorManager));
            Guard.ArgumentNotNull(versionProvider, nameof(versionProvider));
            Guard.ArgumentNotNull(factory, nameof(factory));

            _scheduler = scheduler;
            _manager = manager;
            _providerFactory = providerFactory;
            _scheduler = scheduler;
            _errorManager = errorManager;
            _versionProvider = versionProvider;
            _iconProvider = factory(null);
        }

        private void HotkeyManager_HotkeyAlreadyRegistered(object sender, HotkeyAlreadyRegisteredEventArgs e)
        {
            _ = MessageBox.Show($"The hotkey {e.Name} is already registered by another application");
        }

        public void Dispose()
        {
            _logger.Information("Disposing...");

            _tokenSource?.Cancel();

            DisposeDesk();

            _deskProvider?.Dispose();
            _tokenSource?.Dispose();
        }

        /// <summary>
        ///     Shows a window, if none is already open.
        /// </summary>
        public ICommand ShowSettingsCommand => new DelegateCommand
        {
            CanExecuteFunc = () => SettingsWindow == null,
            CommandAction = DoShowSettings
        };

        /// <summary>
        ///     Connects to the Idasen Desk.
        /// </summary>
        public ICommand ConnectCommand => new DelegateCommand
        {
            CommandAction = async () => await DoConnect(),
            CanExecuteFunc = () => _desk == null
        };

        /// <summary>
        ///     Disconnects from the Idasen Desk.
        /// </summary>
        public ICommand DisconnectCommand => new DelegateCommand
        {
            CommandAction = DoDisconnect,
            CanExecuteFunc = () => _desk != null
        };

        /// <summary>
        ///     Moves the desk to the standing height.
        /// </summary>
        public ICommand StandingCommand => new DelegateCommand
        {
            CommandAction = async () => await DoStanding(),
            CanExecuteFunc = () => _desk != null
        };

        /// <summary>
        ///     Moves the desk to the seating height.
        /// </summary>
        public ICommand SeatingCommand => new DelegateCommand
        {
            CommandAction = async () => await DoSeating(),
            CanExecuteFunc = () => _desk != null
        };

        /// <summary>
        ///     Shuts down the application.
        /// </summary>
        public ICommand ExitApplicationCommand =>
            new DelegateCommand
            {
                CommandAction = DoExitApplication
            };

        private ISettingsWindow SettingsWindow
        {
            get => Application.Current.MainWindow as ISettingsWindow;
            set => Application.Current.MainWindow = value as Window;
        }

        public bool IsInitialize => _logger != null && _manager != null; // todo  && _provider != null ;

        private void DoExitApplication()
        {
            _logger.Information("##### Exit...");

            _tokenSource.Cancel();
            Application.Current.Shutdown();
        }

        private void DoShowSettings()
        {
            _logger.Debug($"{nameof(ShowSettingsCommand)}");

            SettingsWindow = new SettingsWindow(_logger, _manager, _versionProvider);

            if (SettingsWindow == null)
            {
                return;
            }

            SettingsWindow.Show();
            SettingsWindow.AdvancedSettingsChanged += OnAdvancedSettingsChanged;
            SettingsWindow.LockSettingsChanged += OnLockSettingsChanged;
        }

        private void DoDisconnect()
        {
            try
            {
                _logger.Debug($"{nameof(DisconnectCommand)}");

                Disconnect();
            }
            catch (Exception e)
            {
                _logger.Error(e,
                                $"Failed to call {nameof(DisconnectCommand)}");

                _errorManager.PublishForMessage($"Failed to call {nameof(DisconnectCommand)}");
            }
        }

        private async Task DoConnect()
        {
            try
            {
                _logger.Debug($"{nameof(DoConnect)}");
                await Connect().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to call {nameof(DoConnect)}");
                _errorManager.PublishForMessage($"Failed to call {nameof(DoConnect)}");
            }
        }

        private async Task DoStanding()
        {
            try
            {
                _logger.Debug($"{nameof(StandingCommand)}");
                await Standing().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to call {nameof(StandingCommand)}");
                _errorManager.PublishForMessage($"Failed to call {nameof(StandingCommand)}");
            }
        }

        private async Task DoSeating()
        {
            try
            {
                _logger.Debug($"{nameof(SeatingCommand)}");
                await _manager.Load().ConfigureAwait(false);
                _desk?.MoveTo(_manager.CurrentSettings.SeatingHeightInCm * 100);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to call {nameof(SeatingCommand)}");
                _errorManager.PublishForMessage($"Failed to call {nameof(SeatingCommand)}");
            }
        }

        private void OnErrorChanged(IErrorDetails details)
        {
            _logger.Error($"[{_desk?.DeviceName}] {details.Message}");
            ShowNotification("Error", details.Message);
        }

        private async Task Standing()
        {
            _logger.Debug("Executing Standing...");
            await _manager.Load();
            _desk?.MoveTo(_manager.CurrentSettings.StandingHeightInCm * 100);
        }

        public NotifyIconViewModel Initialize(
            ILogger logger,
            ISettingsManager manager,
            Func<IDeskProvider> providerFactory,
            IErrorManager errorManager,
            IVersionProvider versionProvider,
            ITaskbarIconProvider iconProvider)
        {
            Guard.ArgumentNotNull(iconProvider, nameof(iconProvider));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(manager, nameof(manager));
            Guard.ArgumentNotNull(providerFactory, nameof(providerFactory));
            Guard.ArgumentNotNull(errorManager, nameof(errorManager));
            Guard.ArgumentNotNull(versionProvider, nameof(versionProvider));

            _logger = logger;
            _manager = manager;
            _providerFactory = providerFactory;
            _versionProvider = versionProvider;
            _iconProvider = iconProvider;

            _logger.Debug("Initializing...");

            _tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            _token = _tokenSource.Token;

            _onErrorChanged = errorManager.ErrorChanged
                                          .ObserveOn(_scheduler)
                                          .Subscribe(OnErrorChanged);


            HotkeyManager.HotkeyAlreadyRegistered += HotkeyManager_HotkeyAlreadyRegistered;

            HotkeyManager.Current.AddOrReplace("Increment", IncrementGesture, OnGlobalHotKeyStanding);
            HotkeyManager.Current.AddOrReplace("Decrement", DecrementGesture, OnGlobalHotKeySeating);

            return this;
        }

        public async Task AutoConnect()
        {
            _logger.Debug("Auto connecting...");

            try
            {
                CheckIfInitialized();

                _logger.Debug("Trying to load settings...");
                await _manager.Load();

                _logger.Debug("Trying to auto connect to Idasen Desk...");
                await Task.Delay(TimeSpan.FromSeconds(3), _token);

                ShowNotification("Auto Connect", "Trying to auto connect to Idasen Desk...");

                await Connect();
            }
            catch (TaskCanceledException)
            {
                _logger.Information("Auto connect was canceled");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to auto connect to desk");
                ConnectFailed();
            }
        }

        private void CheckIfInitialized()
        {
            if (!IsInitialize)
            {
                throw new Exception("Initialize needs to be called first!");
            }
        }

        private async Task Connect()
        {
            try
            {
                _logger.Debug("Trying to initialize provider...");

                _deskProvider?.Dispose();
                _deskProvider = _providerFactory();
                _ = _deskProvider.Initialize(_manager.CurrentSettings.DeviceName,
                                       _manager.CurrentSettings.DeviceAddress,
                                       _manager.CurrentSettings.DeviceMonitoringTimeout);

                _logger.Debug($"[{_desk?.DeviceName}] Trying to connect to Idasen Desk...");

                _tokenSource?.Cancel(false);

                _tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                _token = _tokenSource.Token;

                (bool isSuccess, IDesk desk) = await _deskProvider.TryGetDesk(_token);

                if (isSuccess)
                {
                    ConnectSuccessful(desk);
                }
                else
                {
                    ConnectFailed();
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, $"[{_desk?.DeviceName}] Failed to connect");
                ConnectFailed();
            }
        }

        private void Disconnect()
        {
            try
            {
                _logger.Debug($"[{_desk?.DeviceName}] Trying to disconnect from Idasen Desk...");

                DisposeDesk();

                _tokenSource?.Cancel(false);

                _logger.Debug($"[{_desk?.DeviceName}] ...disconnected from Idasen Desk");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to disconnect");
                ConnectFailed();
            }
        }

        private void ConnectFailed()
        {
            _logger.Debug("Connection failed...");

            Disconnect();

            ShowNotification("Failed to Connect", Constants.CheckAndEnableBluetooth);
        }

        private void DisposeDesk()
        {
            _logger.Debug($"[{_desk?.Name}] Disposing desk");

            _finished?.Dispose();
            _desk?.Dispose();
            _deskProvider?.Dispose();

            _finished = null;
            _desk = null;
            _deskProvider = null;
        }

        private void ConnectSuccessful(IDesk desk)
        {
            _logger.Information($"[{desk.DeviceName}] Connected to {desk.DeviceName} " +
                                  $"with address {desk.BluetoothAddress} " +
                                  $"(MacAddress {desk.BluetoothAddress.ToMacAddress()})");

            _desk = desk;

            _finished = _desk.FinishedChanged.ObserveOn(_scheduler).Subscribe(OnFinishedChanged);

            ShowNotification("Success", $"Connected to desk: {Environment.NewLine}{desk.Name}");

            _iconProvider.Initialize(_desk);

            _logger.Debug($"[{_desk?.DeviceName}] Connected successful");

            if (!_manager.CurrentSettings.DeviceLocked)
            {
                return;
            }

            _logger.Information("Locking desk movement");

            _desk?.MoveLock();
        }

        private void OnFinishedChanged(uint height)
        {
            _logger.Debug($"Height = {height}");
            double heightInCm = Math.Round(height / 100.0);
            ShowNotification("Finished", $"Desk height is {heightInCm:F0} cm");
        }

        private void ShowNotification(string title, string text)
        {
            new ToastContentBuilder().AddText(title).AddText(text).Show();
        }

        private async void OnAdvancedSettingsChanged(object sender, EventArgs args)
        {
            try
            {
                _tokenSource?.Cancel(false);

                await Task.Delay(3000).ConfigureAwait(false);

                Disconnect();

                await Connect().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed  to reconnect after advanced settings change.");
            }
        }

        private void OnLockSettingsChanged(object sender, LockSettingsChangedEventArgs args)
        {
            try
            {
                if (args.IsLocked)
                {
                    _desk?.MoveLock();
                }
                else
                {
                    _desk?.MoveUnlock();
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed  to lock/unlock after locked settings change.");
            }
        }

        private void OnGlobalHotKeyStanding(object sender, HotkeyEventArgs e)
        {
            try
            {
                _logger.Information("Received global hot key for 'Standing' command...");

                if (!StandingCommand.CanExecute(this))
                {
                    return;
                }

                System.Runtime.CompilerServices.ConfiguredTaskAwaitable task = Standing().ConfigureAwait(false);

                StandingCommand.Execute(this);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to handle global hot key command for 'Standing'.");
            }
        }

        private void OnGlobalHotKeySeating(object sender, HotkeyEventArgs e)
        {
            try
            {
                _logger.Information("Received global hot key for 'Seating' command...");

                if (!SeatingCommand.CanExecute(this))
                {
                    return;
                }

                SeatingCommand.Execute(this);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to handle global hot key command for 'Seating'.");
            }
        }


        private readonly IErrorManager _errorManager;
        private readonly IScheduler _scheduler = Scheduler.CurrentThread;

        private IDesk _desk;
        private IDisposable _finished;
        private ILogger _logger;
        private ISettingsManager _manager;
        private IDisposable _onErrorChanged;
        private IVersionProvider _versionProvider;
        private ITaskbarIconProvider _iconProvider;
        private IDeskProvider _deskProvider;
        private Func<IDeskProvider> _providerFactory;
        private CancellationToken _token;
        private CancellationTokenSource _tokenSource;
    }
}