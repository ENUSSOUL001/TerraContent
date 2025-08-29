using System.Collections.Generic;
using Newtonsoft.Json;

namespace PresetCommands
{
    public class Config
    {
        public ConfigSettings Settings { get; set; } = new ConfigSettings();
    }

    public class ConfigSettings
    {
        public List<CommandPreset> CommandPresets { get; set; } = new List<CommandPreset>
        {
            new CommandPreset
            {
                Name = "superbuff",
                Permissions = new List<string> { "owner" },
                Commands = new List<string>
                {
                    "/gpermabuff 1 {player}", "/gpermabuff 2 {player}", "/gpermabuff 3 {player}",
                    "/gpermabuff 4 {player}", "/gpermabuff 5 {player}", "/gpermabuff 6 {player}",
                    "/gpermabuff 7 {player}", "/gpermabuff 8 {player}", "/gpermabuff 9 {player}",
                    "/gpermabuff 11 {player}", "/gpermabuff 12 {player}", "/gpermabuff 14 {player}",
                    "/gpermabuff 15 {player}", "/gpermabuff 16 {player}", "/gpermabuff 71 {player}",
                    "/gpermabuff 73 {player}", "/gpermabuff 74 {player}", "/gpermabuff 75 {player}",
                    "/gpermabuff 76 {player}", "/gpermabuff 77 {player}", "/gpermabuff 78 {player}",
                    "/gpermabuff 79 {player}", "/gpermabuff 104 {player}", "/gpermabuff 105 {player}",
                    "/gpermabuff 106 {player}", "/gpermabuff 107 {player}", "/gpermabuff 108 {player}",
                    "/gpermabuff 109 {player}", "/gpermabuff 110 {player}", "/gpermabuff 111 {player}",
                    "/gpermabuff 112 {player}", "/gpermabuff 113 {player}", "/gpermabuff 114 {player}",
                    "/gpermabuff 115 {player}", "/gpermabuff 117 {player}", "/gpermabuff 121 {player}",
                    "/gpermabuff 122 {player}", "/gpermabuff 123 {player}", "/gpermabuff 124 {player}",
                    "/gpermabuff 207 {player}", "/gpermabuff 257 {player}", "/gpermabuff 343 {player}",
                    "/gpermabuff 58 {player}", "/gpermabuff 59 {player}", "/gpermabuff 60 {player}",
                    "/gpermabuff 62 {player}", "/gpermabuff 63 {player}", "/gpermabuff 97 {player}",
                    "/gpermabuff 100 {player}", "/gpermabuff 172 {player}", "/gpermabuff 175 {player}",
                    "/gpermabuff 178 {player}", "/gpermabuff 181 {player}", "/gpermabuff 198 {player}",
                    "/gpermabuff 205 {player}", "/gpermabuff 306 {player}", "/gpermabuff 308 {player}",
                    "/gpermabuff 311 {player}", "/gpermabuff 312 {player}", "/gpermabuff 314 {player}",
                    "/gpermabuff 29 {player}", "/gpermabuff 93 {player}", "/gpermabuff 150 {player}",
                    "/gpermabuff 159 {player}", "/gpermabuff 348 {player}", "/gpermabuff 48 {player}",
                    "/gpermabuff 87 {player}", "/gpermabuff 89 {player}", "/gpermabuff 146 {player}",
                    "/gpermabuff 147 {player}", "/gpermabuff 157 {player}", "/gpermabuff 158 {player}",
                    "/gpermabuff 165 {player}", "/gpermabuff 215 {player}"
                }
            }
        };
    }

    public class CommandPreset
    {
        public string? Name { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
        public List<string> Commands { get; set; } = new List<string>();
    }
}
