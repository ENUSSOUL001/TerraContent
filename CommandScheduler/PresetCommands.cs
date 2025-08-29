using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CommandScheduler
{
    [ApiVersion(2, 1)]
    public class CommandScheduler : TerrariaPlugin
    {
        public override string Author => "enussoul";
        public override string Description => "Simple stuff";
        public override string Name => "CommandScheduler";
        public override Version Version => new Version(1, 0);

        private Config _config;
        private readonly string _configPath = Path.Combine(TShock.SavePath, "commands.json");

        public CommandScheduler(Main game) : base(game)
        {
            _config = new Config();
        }

        public override void Initialize()
        {
            LoadConfig();
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                SaveConfig();
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
                else
                {
                    TShock.Log.ConsoleError("[CommandScheduler] Failed to deserialize commands.json. Using a new empty configuration.");
                    _config = new Config();
                    SaveConfig();
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

        private void OnServerJoin(JoinEventArgs args)
        {
            TSPlayer player = TShock.Players[args.Who];
            if (player == null || !player.Active || !player.IsLoggedIn)
            {
                return;
            }

            foreach (var trigger in _config.Settings.JoinTriggers)
            {
                bool appliesToAll = !trigger.PlayerNames.Any() && !trigger.GroupNames.Any();
                bool nameMatch = trigger.PlayerNames.Any(n => n.Equals(player.Name, StringComparison.OrdinalIgnoreCase));
                bool groupMatch = trigger.GroupNames.Any(g => g.Equals(player.Group.Name, StringComparison.OrdinalIgnoreCase));

                if (appliesToAll || nameMatch || groupMatch)
                {
                    if (trigger.OneTimeExecution)
                    {
                        if (trigger.ExecutedPlayersUUIDs.Contains(player.UUID))
                        {
                            continue;
                        }
                        trigger.ExecutedPlayersUUIDs.Add(player.UUID);
                        SaveConfig();
                    }
                    ExecuteCommands(trigger.Commands, player);
                }
            }
        }

        private void ExecuteCommands(List<string> commands, TSPlayer targetPlayer)
        {
            foreach (string cmd in commands)
            {
                string processedCommand = cmd.Replace("{PLAYER_NAME}", $"\"{targetPlayer.Name}\"");

                if (!processedCommand.StartsWith(TShock.Config.Settings.CommandSpecifier))
                {
                    processedCommand = TShock.Config.Settings.CommandSpecifier + processedCommand;
                }
                
                TShockAPI.Commands.HandleCommand(TSPlayer.Server, processedCommand);
            }
        }
    }
}
