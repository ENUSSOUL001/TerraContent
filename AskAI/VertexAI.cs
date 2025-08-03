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

        public static async Task<string> AskAsync(string prompt, AskAIConfig config, string apiKey, string modelId)
        {
            var apiUrl = $"{config.ApiUrl}{modelId}:generateContent?key={apiKey}";

            var requestBody = new VertexAIRequest
            {
                SystemInstruction = new Instruction
                {
                    Parts = new List<Part> { new Part { Text = config.SystemPrompt } }
                },
                Contents = new List<Content>
                {
                    new Content
                    {
                        Role = "user",
                        Parts = new List<Part> { new Part { Text = prompt } }
                    }
                },
                Tools = new List<Tool>
                {
                    new Tool { GoogleSearch = new object() }
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(apiUrl, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var vertexResponse = JsonConvert.DeserializeObject<VertexAIResponse>(responseJson);

            var firstCandidate = vertexResponse?.Candidates?.FirstOrDefault();
            var firstPart = firstCandidate?.Content?.Parts?.FirstOrDefault();

            return firstPart?.Text ?? "AI response was empty or malformed.";
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

    public class Instruction
    {
        [JsonProperty("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Content
    {
        [JsonProperty("role")]
        public string Role { get; set; }
        [JsonProperty("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Part
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class Tool
    {
        [JsonProperty("google_search")]
        public object GoogleSearch { get; set; }
    }

    public class Candidate
    {
        [JsonProperty("content")]
        public Content Content { get; set; }
    }
}
