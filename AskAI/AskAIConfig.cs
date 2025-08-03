using Newtonsoft.Json;
using System.IO;

namespace AskAI
{
    public class AskAIConfig
    {
        [JsonProperty("VertexApiKey1", Order = 1)]
        public string VertexApiKey1 { get; set; } = "AQ.Ab8RN6IN2NqAzK7J6QX2sEBsZnT6kMHq_QMbZOdzJh5Bmx5ZMQ";

        [JsonProperty("VertexApiKey2", Order = 2)]
        public string VertexApiKey2 { get; set; } = "AQ.Ab8RN6JliB2j1VqtJrg4g0zo3OeAx-QghqnsGtRxNedYL-594g";

        [JsonProperty("ExaApiKey", Order = 3)]
        public string ExaApiKey { get; set; } = "df21471d-84e2-4fa8-b58a-8aa6b68d6640";

        [JsonProperty("DefaultModelId", Order = 4)]
        public string DefaultModelId { get; set; } = "gemini-2.5-pro";
        
        [JsonProperty("ApiUrl", Order = 5)]
        public string ApiUrl { get; set; } = "https://aiplatform.googleapis.com/v1/publishers/google/models/";

        [JsonProperty("SetupAIOperatorAccount", Order = 6)]
        public bool SetupAIOperatorAccount { get; set; } = true;
        
        [JsonProperty("ExaResearchTimeoutMinutes", Order = 7)]
        public int ExaResearchTimeoutMinutes { get; set; } = 60;

        [JsonProperty("SystemPromptHybrid", Order = 8)]
        public string SystemPromptHybrid { get; set; } = "PUT-HERE";

        [JsonProperty("SystemPromptDeepPlanner", Order = 9)]
        public string SystemPromptDeepPlanner { get; set; } = "PUT-HERE";
        
        [JsonProperty("SystemPromptDeepSynthesizer", Order = 10)]
        public string SystemPromptDeepSynthesizer { get; set; } = "PUT-HERE";

        [JsonProperty("SystemPromptOperator", Order = 11)]
        public string SystemPromptOperator { get; set; } = "PUT-HERE";

        public static AskAIConfig Read(string path)
        {
            if (!File.Exists(path))
            {
                var newConfig = new AskAIConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(newConfig, Formatting.Indented));
                return newConfig;
            }
            return JsonConvert.DeserializeObject<AskAIConfig>(File.ReadAllText(path));
        }
    }
}
