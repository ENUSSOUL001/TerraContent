using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace PresetCommands
{
    [ApiVersion(2, 1)]
    public class PresetCommands : TerrariaPlugin
    {
        public override string Author => "enussoul";
        public override string Description => "Create preset commands that run multiple commands.";
        public override string Name => "PresetCommands";
        public override Version Version => new Version("1.0");

        private Config _config;
        private readonly string _configPath = Path.Combine(TShock.SavePath, "PresetCommands.json");
        private readonly List<Command> _registeredCommands = new List<Command>();

        public PresetCommands(Main game) : base(game)
        {
            _config = new Config();
        }

        public override void Initialize()
        {
            LoadConfig();
            RegisterPresetCommands();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterPresetCommands();
            }
            base.Dispose(disposing);
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var loadedConfig = JsonConvert.DeserializeObject<Config>(json);
                if (loadedConfig != null)
                {
                    _config = loadedConfig;
                }
            }
            else
            {
                _config = new Config();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }

        private void RegisterPresetCommands()
        {
            UnregisterPresetCommands();
            foreach (var preset in _config.Settings.CommandPresets)
            {
                if (string.IsNullOrWhiteSpace(preset.Name))
                {
                    continue;
                }

                var capturedPreset = preset;
                var command = new Command(capturedPreset.Permissions, args => HandlePresetCommand(args, capturedPreset), capturedPreset.Name)
                {
                    HelpText = $"Executes the '{capturedPreset.Name}' command preset."
                };

                TShockAPI.Commands.ChatCommands.Add(command);
                _registeredCommands.Add(command);
            }
        }

        private void UnregisterPresetCommands()
        {
            foreach (var cmd in _registeredCommands)
            {
                TShockAPI.Commands.ChatCommands.Remove(cmd);
            }
            _registeredCommands.Clear();
        }

        private void HandlePresetCommand(CommandArgs args, CommandPreset preset)
        {
            foreach (var cmd in preset.Commands)
            {
                string processedCommand = cmd.Replace("{player}", $"\"{args.Player.Name}\"");

                if (!processedCommand.StartsWith(TShock.Config.Settings.CommandSpecifier))
                {
                    processedCommand = TShock.Config.Settings.CommandSpecifier + processedCommand;
                }
                
                TShockAPI.Commands.HandleCommand(TSPlayer.Server, processedCommand);
            }
        }
    }
}
