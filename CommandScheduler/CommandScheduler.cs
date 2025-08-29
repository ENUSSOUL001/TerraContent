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
                _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_configPath));
            }
            else
            {
                _config = new Config();
                _config.Settings.JoinTriggers.Add(new JoinTrigger
                {
                    TriggerName = "WelcomeMessage",
                    Description = "This is a default trigger explaining how the plugin works. It runs for every player who joins and is not a one-time execution.",
                    PlayerNames = new List<string>(),
                    GroupNames = new List<string>(),
                    OneTimeExecution = false,
                    Commands = new List<string> { "/say Welcome to the server, {PLAYER_NAME}!" }
                });
                _config.Settings.JoinTriggers.Add(new JoinTrigger
                {
                    TriggerName = "EnussoulBuffsOneTime",
                    Description = "This trigger is specifically for the player 'enussoul' and will only run once.",
                    PlayerNames = new List<string> { "enussoul" },
                    GroupNames = new List<string>(),
                    OneTimeExecution = true,
                    Commands = new List<string>
                    {
                        "gpermabuff 1 enussoul", "gpermabuff 2 enussoul", "gpermabuff 3 enussoul",
                        "gpermabuff 4 enussoul", "gpermabuff 5 enussoul", "gpermabuff 6 enussoul",
                        "gpermabuff 7 enussoul", "gpermabuff 8 enussoul", "gpermabuff 9 enussoul",
                        "gpermabuff 11 enussoul", "gpermabuff 12 enussoul", "gpermabuff 14 enussoul",
                        "gpermabuff 15 enussoul", "gpermabuff 16 enussoul", "gpermabuff 71 enussoul",
                        "gpermabuff 73 enussoul", "gpermabuff 74 enussoul", "gpermabuff 75 enussoul",
                        "gpermabuff 76 enussoul", "gpermabuff 77 enussoul", "gpermabuff 78 enussoul",
                        "gpermabuff 79 enussoul", "gpermabuff 104 enussoul", "gpermabuff 105 enussoul",
                        "gpermabuff 106 enussoul", "gpermabuff 107 enussoul", "gpermabuff 108 enussoul",
                        "gpermabuff 109 enussoul", "gpermabuff 110 enussoul", "gpermabuff 111 enussoul",
                        "gpermabuff 112 enussoul", "gpermabuff 113 enussoul", "gpermabuff 114 enussoul",
                        "gpermabuff 115 enussoul", "gpermabuff 117 enussoul", "gpermabuff 121 enussoul",
                        "gpermabuff 122 enussoul", "gpermabuff 123 enussoul", "gpermabuff 124 enussoul",
                        "gpermabuff 207 enussoul", "gpermabuff 257 enussoul", "gpermabuff 343 enussoul",
                        "gpermabuff 58 enussoul", "gpermabuff 59 enussoul", "gpermabuff 60 enussoul",
                        "gpermabuff 62 enussoul", "gpermabuff 63 enussoul", "gpermabuff 97 enussoul",
                        "gpermabuff 100 enussoul", "gpermabuff 172 enussoul", "gpermabuff 175 enussoul",
                        "gpermabuff 178 enussoul", "gpermabuff 181 enussoul", "gpermabuff 198 enussoul",
                        "gpermabuff 205 enussoul", "gpermabuff 306 enussoul", "gpermabuff 308 enussoul",
                        "gpermabuff 311 enussoul", "gpermabuff 312 enussoul", "gpermabuff 314 enussoul",
                        "gpermabuff 29 enussoul", "gpermabuff 93 enussoul", "gpermabuff 150 enussoul",
                        "gpermabuff 159 enussoul", "gpermabuff 348 enussoul", "gpermabuff 48 enussoul",
                        "gpermabuff 87 enussoul", "gpermabuff 89 enussoul", "gpermabuff 146 enussoul",
                        "gpermabuff 147 enussoul", "gpermabuff 157 enussoul", "gpermabuff 158 enussoul",
                        "gpermabuff 165 enussoul", "gpermabuff 215 enussoul"
                    }
                });
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
            if (player == null || !player.Active)
            {
                return;
            }

            if (!player.IsLoggedIn)
            {
                TShock.Log.ConsoleDebug($"CommandScheduler: Player {player.Name} is not logged in. Skipping triggers.");
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
