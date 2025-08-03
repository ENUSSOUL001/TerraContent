using Newtonsoft.Json;
using System.Collections.Generic;

namespace AskAI
{
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

    public class ExaSearchRequest
    {
        [JsonProperty("query")]
        public string Query { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; } = "neural";
        [JsonProperty("numResults")]
        public int NumResults { get; set; } = 10;
        [JsonProperty("includeDomains")]
        public List<string> IncludeDomains { get; set; }
        [JsonProperty("excludeDomains")]
        public List<string> ExcludeDomains { get; set; } = new List<string> { "fandom.com" };
        [JsonProperty("contents")]
        public ExaContentOptions Contents { get; set; } = new ExaContentOptions();
    }

    public class ExaContentOptions 
    { 
        [JsonProperty("text")] 
        public TextOptions Text { get; set; } = new TextOptions();
    }
    
    public class TextOptions
    {
        [JsonProperty("maxCharacters")]
        public int MaxCharacters { get; set; } = 2000;
        [JsonProperty("includeHtmlTags")]
        public bool IncludeHtmlTags { get; set; } = false;
    }
    
    public class ExaSearchResponse
    {
        [JsonProperty("results")]
        public List<ExaResult> Results { get; set; }
    }

    public class ExaResult { [JsonProperty("text")] public string Text { get; set; } }

    public class ExaResearchRequest
    {
        [JsonProperty("instructions")]
        public string Instructions { get; set; }
        [JsonProperty("model")]
        public string Model { get; set; } = "exa-research-pro";
        [JsonProperty("output")]
        public ExaOutputOptions Output { get; set; } = new ExaOutputOptions { InferSchema = true };
    }
    
    public class ExaOutputOptions { [JsonProperty("inferSchema")] public bool InferSchema { get; set; } }

    public class ExaCreateTaskResponse { [JsonProperty("id")] public string Id { get; set; } }

    public class ExaTaskStatusResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("data")]
        public object Data { get; set; }
    }
}
