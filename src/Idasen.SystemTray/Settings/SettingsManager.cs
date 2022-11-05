﻿using Idasen.BluetoothLE.Core;
using Idasen.SystemTray.Interfaces;
using Idasen.SystemTray.Utils;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Idasen.SystemTray.Settings
{
    public class SettingsManager
        : ISettingsManager
    {
        public SettingsManager(ILogger logger)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));

            _logger = logger;

            _settingsFolderName = CreateFullPathSettingsFolderName();
            _settingsFileName = CreateFullPathSettingsFileName();
        }

        public ISettings CurrentSettings => _current;

        public async Task Save()
        {
            _logger.Debug($"Saving current setting [{_current}] to '{_settingsFileName}'");

            try
            {
                if (!Directory.Exists(_settingsFolderName))
                {
                    _ = Directory.CreateDirectory(_settingsFolderName);
                }

                await using FileStream stream = File.Create(_settingsFileName);

                await JsonSerializer.SerializeAsync(stream, _current);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to save settings in file '{_settingsFileName}'");
            }
        }

        public async Task Load()
        {
            _logger.Debug($"Loading setting from '{_settingsFileName}'");

            try
            {
                if (!File.Exists(_settingsFileName))
                {
                    return;
                }

                await using FileStream openStream = File.OpenRead(_settingsFileName);

                _current = await JsonSerializer.DeserializeAsync<Settings>(openStream);

                _logger.Debug($"Settings loaded: {_current}");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to load settings");
            }
        }

        private readonly ILogger _logger;

        private readonly string _settingsFileName;

        private readonly string _settingsFolderName;

        private Settings _current = new();

        public string CreateFullPathSettingsFileName()
        {
            string fileName = Path.Combine(CreateFullPathSettingsFolderName(), Constants.SettingsFileName);
            return fileName;
        }

        private string CreateFullPathSettingsFolderName()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string folderName = Path.Combine(appData, Constants.ApplicationName);

            return folderName;
        }
    }
}