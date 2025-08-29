using System.Collections.Generic;
using Newtonsoft.Json;

namespace CommandScheduler
{
    public class Config
    {
        public ConfigSettings Settings { get; set; } = new ConfigSettings();
    }

    public class ConfigSettings
    {
        public List<JoinTrigger> JoinTriggers { get; set; } = new List<JoinTrigger>();
    }

    public class JoinTrigger
    {
        public string TriggerName { get; set; }
        public string Description { get; set; }
        public List<string> PlayerNames { get; set; } = new List<string>();
        public List<string> GroupNames { get; set; } = new List<string>();
        public bool OneTimeExecution { get; set; }
        public List<string> Commands { get; set; } = new List<string>();
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> ExecutedPlayersUUIDs { get; set; } = new HashSet<string>();
    }
}
