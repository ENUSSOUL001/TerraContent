using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AskAI
{
    public static class ApiClients
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static int _keyIndex = 0;

        public static async Task<VertexAIResponse> CallVertexAPI(string prompt, AskAIConfig config, string modelId, string systemPrompt, List<Tool> tools = null)
        {
            var apiKeys = new[] { config.VertexApiKey1, config.VertexApiKey2 };
            string apiKey = apiKeys[_keyIndex];
            _keyIndex = (_keyIndex + 1) % apiKeys.Length;

            var apiUrl = $"{config.ApiUrl}{modelId}:generateContent?key={apiKey}";

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
                throw new HttpRequestException($"Vertex API request failed with status code {response.StatusCode}: {responseJson}");
            }

            return JsonConvert.DeserializeObject<VertexAIResponse>(responseJson);
        }

        public static async Task<ExaSearchResponse> CallExaSearchAPI(ExaSearchRequest request, AskAIConfig config)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.exa.ai/search");
            httpRequest.Headers.Add("x-api-key", config.ExaApiKey);
            httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(httpRequest);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Exa Search API request failed with status code {response.StatusCode}: {responseJson}");
            }

            return JsonConvert.DeserializeObject<ExaSearchResponse>(responseJson);
        }

        public static async Task<ExaCreateTaskResponse> CallExaResearchAPI_Create(ExaResearchRequest request, AskAIConfig config)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.exa.ai/research/v0/tasks");
            httpRequest.Headers.Add("x-api-key", config.ExaApiKey);
            httpRequest.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            
            var response = await HttpClient.SendAsync(httpRequest);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Exa Research Create API request failed with status code {response.StatusCode}: {responseJson}");
            }

            return JsonConvert.DeserializeObject<ExaCreateTaskResponse>(responseJson);
        }

        public static async Task<ExaTaskStatusResponse> CallExaResearchAPI_Poll(string taskId, AskAIConfig config)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.exa.ai/research/v0/tasks/{taskId}");
            httpRequest.Headers.Add("x-api-key", config.ExaApiKey);
            
            var response = await HttpClient.SendAsync(httpRequest);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Exa Research Poll API request failed with status code {response.StatusCode}: {responseJson}");
            }

            return JsonConvert.DeserializeObject<ExaTaskStatusResponse>(responseJson);
        }
    }
}
