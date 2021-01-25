﻿using System ;
using System.Collections.Generic ;
using System.Reactive.Concurrency ;
using System.Reactive.Linq ;
using System.Reactive.Subjects ;
using Autofac.Extras.DynamicProxy ;
using Idasen.Aop.Aspects ;
using Idasen.BluetoothLE.Core.Interfaces.DevicesDiscovery ;
using JetBrains.Annotations ;
using Serilog ;

// [assembly: InternalsVisibleTo("Idasen.BluetoothLE.Tests")]

namespace Idasen.BluetoothLE.Core.DevicesDiscovery
{
    /// <inheritdoc cref="IDeviceMonitor" />
    [ Intercept ( typeof ( LogAspect ) ) ]
    public class DeviceMonitor
        : IDeviceMonitor
    {
        public DeviceMonitor (
            [ NotNull ] ILogger                       logger ,
            [ NotNull ] IScheduler                    scheduler ,
            [ NotNull ] Func < ISubject < IDevice > > factory ,
            [ NotNull ] IDevices                      devices ,
            [ NotNull ] IWatcher                      watcher )
        {
            Guard.ArgumentNotNull ( logger ,
                                    nameof ( logger ) ) ;
            Guard.ArgumentNotNull ( factory ,
                                    nameof ( factory ) ) ;
            Guard.ArgumentNotNull ( devices ,
                                    nameof ( devices ) ) ;
            Guard.ArgumentNotNull ( watcher ,
                                    nameof ( watcher ) ) ;
            Guard.ArgumentNotNull ( scheduler ,
                                    nameof ( scheduler ) ) ;

            _logger  = logger ;
            _devices = devices ;
            _watcher = watcher ;

            _deviceUpdated     = factory.Invoke ( ) ;
            _deviceDiscovered  = factory.Invoke ( ) ;
            _deviceNameUpdated = factory.Invoke ( ) ;

            _disposableStarted = _watcher.Started
                                         .SubscribeOn ( scheduler )
                                         .Subscribe ( OnStarted ) ;
            _disposableStopped = _watcher.Stopped
                                         .SubscribeOn ( scheduler )
                                         .Subscribe ( OnStopped ) ;
            _disposableUpdated = _watcher.Received
                                         .SubscribeOn ( scheduler )
                                         .Subscribe ( OnDeviceUpdated ) ;
        }

        /// <inheritdoc />
        public IReadOnlyCollection < IDevice > DiscoveredDevices => _devices.DiscoveredDevices ;

        /// <inheritdoc />
        public bool IsListening => _watcher.IsListening ;

        /// <inheritdoc />
        public IObservable < IDevice > DeviceUpdated => _deviceUpdated ;

        /// <inheritdoc />
        public IObservable < IDevice > DeviceDiscovered => _deviceDiscovered ;

        /// <inheritdoc />
        public IObservable < IDevice > DeviceNameUpdated => _deviceNameUpdated ;

        /// <inheritdoc />
        public void Start ( )
        {
            _watcher.Start ( ) ;
        }

        /// <inheritdoc />
        public void Stop ( )
        {
            _watcher.Stop ( ) ;
        }

        /// <inheritdoc />
        public void RemoveDevice ( IDevice device )
        {
            _devices.RemoveDevice ( device ) ;
        }

        /// <inheritdoc />
        public void Dispose ( )
        {
            _disposableStarted.Dispose ( ) ;
            _disposableStopped.Dispose ( ) ;
            _disposableUpdated.Dispose ( ) ;
        }

        private void OnDeviceUpdated ( IDevice device )
        {
            if ( ! _devices.TryGetDevice ( device.Address ,
                                           out var storedDevice ) )
            {
                _logger.Information ( $"[{device.Address}] Discovered Device" ) ;

                _devices.AddOrUpdateDevice ( device ) ;

                _deviceDiscovered.OnNext ( device ) ;
            }
            else
            {
                _logger.Information ( $"[{device.Address}] Updated Device" ) ;

                var hasNameChanged = HasDeviceNameChanged ( device ,
                                                            storedDevice ) ;

                _devices.AddOrUpdateDevice ( device ) ;

                _deviceUpdated.OnNext ( device ) ;

                if ( ! hasNameChanged )
                    return ;

                _logger.Information ( $"[{device.Address}] Device Name Changed" ) ;

                _deviceNameUpdated.OnNext ( device ) ;
            }
        }

        private void OnStopped ( DateTime dateTime )
        {
            _logger.Information ( "Watcher Stopped listening" ) ;
        }

        private void OnStarted ( DateTime dateTime )
        {
            _logger.Information ( "Watcher Started listening" ) ;
        }

        private static bool HasDeviceNameChanged ( IDevice device ,
                                                   IDevice storedDevice )
        {
            if ( string.IsNullOrWhiteSpace ( storedDevice.Name ) &&
                 ! string.IsNullOrWhiteSpace ( device.Name ) )
                return storedDevice.Name != device.Name ;

            return false ;
        }

        private readonly ISubject < IDevice > _deviceDiscovered ;
        private readonly ISubject < IDevice > _deviceNameUpdated ;
        private readonly IDevices             _devices ;
        private readonly ISubject < IDevice > _deviceUpdated ;

        private readonly IDisposable _disposableStarted ;
        private readonly IDisposable _disposableStopped ;
        private readonly IDisposable _disposableUpdated ;
        private readonly ILogger     _logger ;

        private readonly IWatcher _watcher ;
    }
}