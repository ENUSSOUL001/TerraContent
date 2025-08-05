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

        public static async Task<(string, string, string)> AskAsync(string prompt, AskAIConfig config, string apiKey, string modelId)
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
                },
                GenerationConfig = new GenerationConfig
                {
                    Temperature = config.GenerationSettings.Temperature,
                    TopP = config.GenerationSettings.TopP,
                    MaxOutputTokens = 8192 
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(apiUrl, httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}: {responseJson}");
            }

            var vertexResponse = JsonConvert.DeserializeObject<VertexAIResponse>(responseJson);
            var combinedText = new StringBuilder();
            if (vertexResponse?.Candidates != null)
            {
                foreach (var candidate in vertexResponse.Candidates)
                {
                    if (candidate.Content?.Parts != null)
                    {
                        foreach (var part in candidate.Content.Parts)
                        {
                            combinedText.Append(part.Text);
                        }
                    }
                }
            }
            return (combinedText.Length > 0 ? combinedText.ToString() : "AI response was empty or malformed.", jsonContent, responseJson);
        }

        public static async Task<string> CleanUpResponseAsync(string rawResponse, AskAIConfig config, string apiKey)
        {
            var modelId = "gemini-2.5-flash";
            var apiUrl = $"{config.ApiUrl}{modelId}:generateContent?key={apiKey}";

            var requestBody = new VertexAIRequest
            {
                SystemInstruction = new Instruction
                {
                    Parts = new List<Part> { new Part { Text = config.SmarterConvertSystemPrompt } }
                },
                Contents = new List<Content>
                {
                    new Content
                    {
                        Role = "user",
                        Parts = new List<Part> { new Part { Text = rawResponse } }
                    }
                },
                GenerationConfig = new GenerationConfig
                {
                    Temperature = 0.1f,
                    TopP = 1.0f,
                    MaxOutputTokens = 8192
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(apiUrl, httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Cleanup API request failed with status code {response.StatusCode}: {responseJson}");
            }

            var vertexResponse = JsonConvert.DeserializeObject<VertexAIResponse>(responseJson);
            var combinedText = new StringBuilder();
            if (vertexResponse?.Candidates != null)
            {
                foreach (var candidate in vertexResponse.Candidates)
                {
                    if (candidate.Content?.Parts != null)
                    {
                        foreach (var part in candidate.Content.Parts)
                        {
                            combinedText.Append(part.Text);
                        }
                    }
                }
            }
            return combinedText.Length > 0 ? combinedText.ToString() : rawResponse;
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
        [JsonProperty("generationConfig")]
        public GenerationConfig GenerationConfig { get; set; }
    }
    
    public class GenerationConfig
    {
        [JsonProperty("temperature")]
        public float Temperature { get; set; }
        [JsonProperty("topP")]
        public float TopP { get; set; }
        [JsonProperty("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
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
