using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace AskAI
{
    [ApiVersion(2, 1)]
    public class AskAI : TerrariaPlugin
    {
        public override string Author => "You";
        public override string Description => "Lets players ask questions to a powerful AI in-game.";
        public override string Name => "AskAI";
        public override Version Version => new Version(3, 4, 1, 0);

        private static AskAIConfig _config;
        private Command _askAiCommand;
        private static string _logFilePath;
        private const string AI_OPERATOR_USER = "AskAI_Operator";
        private const string AI_OPERATOR_GROUP = "ai_operator";
        private static readonly HashSet<string> _playersWaiting = new HashSet<string>();
        private static User _aiUserAccount;

        public AskAI(Main game) : base(game) { }

        public override void Initialize()
        {
            string configDirectory = Path.Combine(TShock.SavePath, "AskAI");
            Directory.CreateDirectory(configDirectory);
            _logFilePath = Path.Combine(configDirectory, "live.log");
            string configPath = Path.Combine(configDirectory, "config.json");
            _config = AskAIConfig.Read(configPath);
            _askAiCommand = new Command("", AskAICommand, "askai")
            {
                HelpText = "Asks a question to the AI. Usage: /askai <prompt> [-flash|-deep|-op]",
                AllowServer = false
            };
            TShockAPI.Commands.ChatCommands.Add(_askAiCommand);
            if (_config.SetupAIOperatorAccount)
            {
                ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            }
        }

        private void OnPostInitialize(EventArgs args)
        {
            SetupAIOperator();
        }

        private void SetupAIOperator()
        {
            var group = TShock.Groups.GetGroupByName(AI_OPERATOR_GROUP);
            if (group == null)
            {
                TShock.Groups.AddGroup(AI_OPERATOR_GROUP, null, "*", "173,216,230");
                TShock.Log.Info("[AskAI] Created dedicated AI operator group with full permissions.");
            }
            _aiUserAccount = TShock.Users.GetUserByName(AI_OPERATOR_USER);
            if (_aiUserAccount == null)
            {
                var password = Guid.NewGuid().ToString();
                TShock.Users.AddUser(new User(AI_OPERATOR_USER, password, Guid.NewGuid().ToString(), AI_OPERATOR_GROUP));
                _aiUserAccount = TShock.Users.GetUserByName(AI_OPERATOR_USER);
                TShock.Log.Info("[AskAI] Created dedicated AI operator user account.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TShockAPI.Commands.ChatCommands.Remove(_askAiCommand);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
            }
            base.Dispose(disposing);
        }

        private async void AskAICommand(CommandArgs args)
        {
            string playerName = args.Player.Name;
            if (_playersWaiting.Contains(playerName))
            {
                args.Player.SendErrorMessage("Please wait for your current request to finish before sending a new one.");
                return;
            }
            _playersWaiting.Add(playerName);
            string userPrompt = string.Join(" ", args.Parameters);
            string mode = "standard";
            try
            {
                var promptParameters = new List<string>(args.Parameters);
                string modelToUse = _config.DefaultModelId;
                var flags = promptParameters.Where(p => p.StartsWith("-")).ToList();
                promptParameters.RemoveAll(p => p.StartsWith("-"));
                if (flags.Contains("-flash")) mode = "flash";
                else if (flags.Contains("-deep")) mode = "deep";
                else if (flags.Contains("-op")) mode = "op";
                else if (flags.Contains("-pro")) mode = "standard";
                if (promptParameters.Count == 0)
                {
                    args.Player.SendErrorMessage("Please provide a prompt.");
                    return;
                }
                userPrompt = string.Join(" ", promptParameters);
                string waitingMessage = "Waiting for response...";
                if (mode == "deep") waitingMessage = "Waiting for detailed response... (takes time)";
                TShock.Utils.Broadcast(waitingMessage, TextHelper.InfoColor);
                List<Part> responseParts;
                switch (mode)
                {
                    case "flash":
                        modelToUse = "gemini-2.5-flash";
                        responseParts = await HandleStandardRequest(userPrompt, playerName, modelToUse);
                        break;
                    case "deep":
                        bool extendTimeout = flags.Contains("-time");
                        responseParts = await HandleDeepRequest(userPrompt, playerName, extendTimeout);
                        break;
                    case "op":
                        responseParts = await HandleOperatorRequest(userPrompt, args.Player);
                        break;
                    default:
                        responseParts = await HandleStandardRequest(userPrompt, playerName, modelToUse);
                        break;
                }
                string header = $"\"{userPrompt}\" Asked by {playerName}:";
                TShock.Utils.Broadcast(header, TextHelper.AINameColor);
                string fullResponseText = await ProcessAIResponseParts(responseParts, args.Player, userPrompt);
                BroadcastResponse(fullResponseText);
                LogToFile($"[SUCCESS] User: {playerName} | Mode: {mode} | Model: {modelToUse} | Prompt: {userPrompt}\n[RESPONSE] {fullResponseText}\n");
            }
            catch (Exception ex)
            {
                string fullError = ex.ToString();
                TShock.Utils.Broadcast("[AskAI] API Request Failed. Please see live.log for details.", TextHelper.ErrorColor);
                LogToFile($"[FAILURE] User: {playerName} | Prompt: {userPrompt}\n[ERROR] {fullError}\n");
            }
            finally
            {
                _playersWaiting.Remove(playerName);
            }
        }

        private async Task<List<Part>> HandleStandardRequest(string userPrompt, string playerName, string modelId)
        {
            var plannerResponse = await ApiClients.CallVertexAPI($"From {playerName}: {userPrompt}", _config, "gemini-2.5-flash", "You are a research planner. Generate a JSON array of 3 diverse search queries to research the user's prompt. If the user is just saying hello, return a friendly greeting instead.", null);
            var firstPartText = plannerResponse.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "[]";
            if (!firstPartText.Trim().StartsWith("["))
            {
                return plannerResponse.Candidates.First().Content.Parts;
            }
            var queryList = JsonConvert.DeserializeObject<List<string>>(firstPartText);
            var researchContent = new StringBuilder();
            foreach (var query in queryList)
            {
                var searchRequest = new ExaSearchRequest { Query = query, NumResults = 3 };
                var searchResponse = await ApiClients.CallExaSearchAPI(searchRequest, _config);
                foreach (var result in searchResponse.Results)
                {
                    researchContent.AppendLine(result.Text);
                }
            }
            string finalPrompt = $"Based on the following research, answer the user's original question.\n\nResearch:\n{researchContent}\n\nOriginal Question: {userPrompt}";
            var finalResponse = await ApiClients.CallVertexAPI(finalPrompt, _config, modelId, _config.SystemPromptHybrid, null);
            return finalResponse.Candidates?.FirstOrDefault()?.Content?.Parts;
        }

        private async Task<List<Part>> HandleDeepRequest(string userPrompt, string playerName, bool extendTimeout)
        {
            var plannerResponse = await ApiClients.CallVertexAPI($"From {playerName}: {userPrompt}", _config, "gemini-2.5-flash", _config.SystemPromptDeepPlanner, null);
            string researchInstructions = plannerResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? userPrompt;
            LogToFile($"[INFO] Deep research instructions generated: {researchInstructions}");
            var taskRequest = new ExaResearchRequest { Instructions = researchInstructions };
            var taskResponse = await ApiClients.CallExaResearchAPI_Create(taskRequest, _config);
            var timeout = extendTimeout ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(_config.ExaResearchTimeoutMinutes);
            var cancellationToken = new System.Threading.CancellationTokenSource(timeout).Token;
            ExaTaskStatusResponse pollResponse;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pollResponse = await ApiClients.CallExaResearchAPI_Poll(taskResponse.Id, _config);
                if (pollResponse.Status == "completed") break;
                if (pollResponse.Status == "failed") throw new Exception("Exa research task failed.");
                await Task.Delay(5000, cancellationToken);
            }
            string researchData = JsonConvert.SerializeObject(pollResponse.Data, Formatting.Indented);
            string synthesizerPrompt = $"Please answer the user's original question based on the following structured JSON data. Write a detailed, human-readable summary.\n\nJSON Data:\n{researchData}\n\nOriginal Question: {userPrompt}";
            var synthesizerResponse = await ApiClients.CallVertexAPI(synthesizerPrompt, _config, "gemini-2.5-pro", _config.SystemPromptDeepSynthesizer, null);
            return synthesizerResponse.Candidates?.FirstOrDefault()?.Content?.Parts;
        }

        private async Task<List<Part>> HandleOperatorRequest(string userPrompt, TSPlayer player)
        {
            var opTools = new List<Tool>
            {
                new Tool { FunctionDeclarations = new List<FunctionDeclaration>
                    {
                        new FunctionDeclaration
                        {
                            Name = "exa_web_research", Description = "Performs web research using the Exa API. Provide a list of queries and a search type ('TERRARIA' or 'GENERAL').",
                            Parameters = new ParametersInfo { Properties = new Dictionary<string, PropertyInfo>
                            {
                                { "queries", new PropertyInfo { Type = "ARRAY", Description = "A JSON array of 1-4 detailed search queries." } },
                                { "search_type", new PropertyInfo { Type = "STRING", Description = "Either 'TERRARIA' or 'GENERAL'." } }
                            }}
                        },
                        new FunctionDeclaration
                        {
                            Name = "tshock_command", Description = "Executes a TShock server command.",
                            Parameters = new ParametersInfo { Properties = new Dictionary<string, PropertyInfo> { { "command_string", new PropertyInfo { Type = "STRING", Description = "The full command string to execute, starting with a '/'." } } } }
                        }
                    }
                }
            };
            var response = await ApiClients.CallVertexAPI($"From {player.Name}: {userPrompt}", _config, "gemini-2.5-pro", _config.SystemPromptOperator, opTools);
            return response.Candidates?.FirstOrDefault()?.Content?.Parts;
        }

        private async Task<string> ProcessAIResponseParts(List<Part> parts, TSPlayer player, string userPrompt)
        {
            var responseBuilder = new StringBuilder();
            if (parts == null) return "The AI returned an empty response.";
            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    responseBuilder.AppendLine(part.Text);
                }
                if (part.FunctionCall != null)
                {
                    LogToFile($"[INFO] AI requested tool: {part.FunctionCall.Name} with args: {JsonConvert.SerializeObject(part.FunctionCall.Args)}");
                    if (part.FunctionCall.Name == "tshock_command")
                    {
                        string command = part.FunctionCall.Args["command_string"].ToString();
                        var group = TShock.Groups.GetGroupByName(AI_OPERATOR_GROUP);
                        var aiOperator = new TSRestPlayer(AI_OPERATOR_USER);
                        bool success = TShockAPI.Commands.HandleCommand(aiOperator, command);
                        if (success)
                        {
                            responseBuilder.AppendLine($"Executed: {command}");
                        }
                        else
                        {
                            responseBuilder.AppendLine($"Tried Executed: {command} but Failed");
                        }
                    }
                    else if (part.FunctionCall.Name == "exa_web_research")
                    {
                        var queries = JsonConvert.DeserializeObject<List<string>>(part.FunctionCall.Args["queries"].ToString());
                        var searchType = part.FunctionCall.Args["search_type"].ToString();
                        var researchContent = new StringBuilder();
                        foreach (var query in queries)
                        {
                            var searchRequest = new ExaSearchRequest { Query = query };
                            if (searchType == "TERRARIA")
                            {
                                searchRequest.IncludeDomains = new List<string> { "terraria.wiki.gg", "reddit.com" };
                            }
                            var searchResponse = await ApiClients.CallExaSearchAPI(searchRequest, _config);
                            foreach (var result in searchResponse.Results)
                            {
                                researchContent.AppendLine(result.Text);
                            }
                        }
                        var opTools = new List<Tool> { new Tool { FunctionDeclarations = new List<FunctionDeclaration> { new FunctionDeclaration { Name = "tshock_command", Description = "Executes a TShock server command.", Parameters = new ParametersInfo { Properties = new Dictionary<string, PropertyInfo> { { "command_string", new PropertyInfo { Type = "STRING", Description = "The full command string to execute, starting with a '/'." } } } } } } } };
                        var followupPrompt = $"Based on this research, what is the final command to execute for the user's request?\n\nResearch:\n{researchContent}\n\nOriginal Request: {player.Name} wants to: {userPrompt}";
                        var followupResponse = await ApiClients.CallVertexAPI(followupPrompt, _config, "gemini-2.5-pro", _config.SystemPromptOperator, opTools);
                        responseBuilder.Append(await ProcessAIResponseParts(followupResponse.Candidates?.FirstOrDefault()?.Content?.Parts, player, userPrompt));
                    }
                }
            }
            return responseBuilder.ToString();
        }

        private void BroadcastResponse(string responseText)
        {
            var sanitizedText = responseText.Replace("\r", "");
            string[] lines = sanitizedText.Split('\n');
            var aiTPlayer = new TSPlayer(255) { User = _aiUserAccount };
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("Executed:"))
                {
                    TShock.Utils.Broadcast(line, TextHelper.SuccessColor);
                }
                else if (line.StartsWith("Tried Executed:"))
                {
                    TShock.Utils.Broadcast(line, TextHelper.ErrorColor);
                }
                else
                {
                    TSPlayer.All.SendMessage(line, Color.White, aiTPlayer.Index);
                }
            }
        }
        
        private static void LogToFile(string message)
        {
            try
            {
                File.AppendAllText(_logFilePath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[AskAI] Failed to write to live.log: {ex.Message}");
            }
        }
    }
}
