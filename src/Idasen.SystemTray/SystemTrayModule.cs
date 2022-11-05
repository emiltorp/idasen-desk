using Autofac;
using Idasen.BluetoothLE.Linak;
using Idasen.SystemTray.Interfaces;
using Idasen.SystemTray.Settings;
using Idasen.SystemTray.Utils;
using System.Diagnostics.CodeAnalysis;

namespace Idasen.SystemTray
{
    [ExcludeFromCodeCoverage]
    public class SystemTrayModule
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            _ = builder.RegisterModule<BluetoothLELinakModule>();

            _ = builder.RegisterType<SettingsManager>()
                   .As<ISettingsManager>();

            _ = builder.RegisterType<VersionProvider>()
                   .As<IVersionProvider>();

            _ = builder.RegisterType<TaskbarIconProvider>()
                   .As<ITaskbarIconProvider>()
                   .SingleInstance();

            _ = builder.RegisterType<TaskbarIconProviderFactory>()
                   .As<ITaskbarIconProviderFactory>()
                   .SingleInstance();

            _ = builder.RegisterType<DynamicIconCreator>()
                   .As<IDynamicIconCreator>();
        }
    }
}