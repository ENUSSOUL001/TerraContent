using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AskAI
{
    public static class VertexAI
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        static VertexAI()
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "TShock-AskAI-Plugin/1.0");
        }

        public static async Task<VertexAIResponse> AskAsync(string prompt, AskAIConfig config, string modelId, string systemPrompt, List<Tool> tools = null)
        {
            var apiUrl = $"{config.ApiUrl}{modelId}:generateContent?key={config.ApiKey}";

            var requestBody = new VertexAIRequest
            {
                SystemInstruction = new Instruction { Parts = new List<Part> { new Part { Text = systemPrompt } } },
                Contents = new List<Content> { new Content { Role = "user", Parts = new List<Part> { new Part { Text = prompt } } } },
                Tools = tools
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(apiUrl, httpContent);

            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}: {responseJson}");
            }

            return JsonConvert.DeserializeObject<VertexAIResponse>(responseJson);
        }
    }

    public class VertexAIRequest
    {
        [JsonProperty("systemInstruction")]
        public Instruction SystemInstruction { get; set; }
        [JsonProperty("contents")]
        public List<Content> Contents { get; set; }
        [JsonProperty("tools")]
        public List<Tool> Tools { get; set; }
    }

    public class VertexAIResponse
    {
        [JsonProperty("candidates")]
        public List<Candidate> Candidates { get; set; }
    }

    public class Instruction { [JsonProperty("parts")] public List<Part> Parts { get; set; } }
    public class Content { [JsonProperty("role")] public string Role { get; set; } [JsonProperty("parts")] public List<Part> Parts { get; set; } }
    public class Part
    {
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
        [JsonProperty("functionCall", NullValueHandling = NullValueHandling.Ignore)]
        public FunctionCall FunctionCall { get; set; }
        [JsonProperty("functionResponse", NullValueHandling = NullValueHandling.Ignore)]
        public FunctionResponse FunctionResponse { get; set; }
    }

    public class Tool
    {
        [JsonProperty("functionDeclarations", NullValueHandling = NullValueHandling.Ignore)]
        public List<FunctionDeclaration> FunctionDeclarations { get; set; }
        [JsonProperty("google_search", NullValueHandling = NullValueHandling.Ignore)]
        public object GoogleSearch { get; set; }
    }

    public class FunctionDeclaration
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("parameters")]
        public ParametersInfo Parameters { get; set; }
    }

    public class ParametersInfo
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "OBJECT";
        [JsonProperty("properties")]
        public Dictionary<string, PropertyInfo> Properties { get; set; }
    }

    public class PropertyInfo
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class Candidate
    {
        [JsonProperty("content")]
        public Content Content { get; set; }
    }

    public class FunctionCall
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("args")]
        public Dictionary<string, object> Args { get; set; }
    }

    public class FunctionResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("response")]
        public Dictionary<string, object> Response { get; set; }
    }
}
