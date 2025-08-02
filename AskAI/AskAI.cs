using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Microsoft.Xna.Framework;

namespace AskAI
{
    [ApiVersion(2, 1)]
    public class AskAI : TerrariaPlugin
    {
        public override string Author => "You";
        public override string Description => "Lets players ask questions to a powerful AI in-game.";
        public override string Name => "AskAI";
        public override Version Version => new Version(2, 4, 0, 0);

        private static AskAIConfig _config;
        private Command _askAiCommand;
        private static string _logFilePath;
        private const string AI_OPERATOR_USER = "AskAI_Operator";
        private const string AI_OPERATOR_GROUP = "ai_operator";
        
        private static bool _isAIBusy = false;

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
                HelpText = "Asks a question to the AI. Usage: /askai <prompt> [-flash|-detailed|-op]",
                AllowServer = false
            };
            TShockAPI.Commands.ChatCommands.Add(_askAiCommand);

            if (_config.SetupAIOperatorAccount)
            {
                ServerApi.Hooks.GamePostInitialize.Register(this, (args) => SetupAIOperator());
            }
        }

        private void SetupAIOperator()
        {
            var group = TShock.Groups.GetGroupByName(AI_OPERATOR_GROUP);
            if (group == null)
            {
                TShock.Groups.AddGroup(AI_OPERATOR_GROUP, null, "", "173,216,230");
                group = TShock.Groups.GetGroupByName(AI_OPERATOR_GROUP);
                TShock.Log.Info("[AskAI] Created dedicated AI operator group.");
            }
            
            var aiUser = TShock.UserAccounts.GetUserAccountByName(AI_OPERATOR_USER);
            if (aiUser == null)
            {
                var password = Guid.NewGuid().ToString();
                aiUser = new UserAccount { Name = AI_OPERATOR_USER, Group = AI_OPERATOR_GROUP };
                TShock.UserAccounts.AddUserAccount(aiUser);
                TShock.UserAccounts.SetUserAccountPassword(aiUser, password);
                TShock.Log.Info("[AskAI] Created dedicated AI operator user account.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TShockAPI.Commands.ChatCommands.Remove(_askAiCommand);
            }
            base.Dispose(disposing);
        }

        private async void AskAICommand(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                string usage = TextHelper.Colorize("/askai", TextHelper.UsageColor);
                string param = TextHelper.Colorize("<prompt> [-flash|-detailed|-op]", TextHelper.UsageParamColor);
                args.Player.SendErrorMessage($"Usage: {usage} {param}");
                return;
            }

            if (_isAIBusy)
            {
                args.Player.SendErrorMessage("The AI is currently processing another request. Please wait a moment.");
                return;
            }
            
            _isAIBusy = true;
            try
            {
                var promptParameters = new List<string>(args.Parameters);
                string mode = "standard";
                string modelToUse = _config.DefaultModelId;
                string systemPrompt = _config.SystemPrompt;

                if (promptParameters.Count > 0)
                {
                    string lastParam = promptParameters.Last().ToLower();
                    if (lastParam == "-flash" || lastParam == "-detailed" || lastParam == "-op" || lastParam == "-pro")
                    {
                        mode = lastParam.Substring(1);
                        promptParameters.RemoveAt(promptParameters.Count - 1);
                    }
                }

                if (promptParameters.Count == 0)
                {
                    args.Player.SendErrorMessage("Please provide a prompt before the flag.");
                    return;
                }
                
                string userPrompt = string.Join(" ", promptParameters);

                string waitingMessage = "Waiting for response...";
                if (mode == "detailed") waitingMessage = "Waiting for detailed response... (takes time)";

                TShock.Utils.Broadcast(waitingMessage, TextHelper.InfoColor);

                string aiResponseText = "";
                switch (mode)
                {
                    case "flash":
                        modelToUse = "gemini-2.5-flash";
                        aiResponseText = await HandleStandardRequest(userPrompt, args.Player.Name, modelToUse, systemPrompt);
                        break;
                    case "detailed":
                        aiResponseText = await HandleDetailedRequest(userPrompt, args.Player.Name);
                        break;
                    case "op":
                        systemPrompt = _config.SystemPromptOperator;
                        aiResponseText = await HandleOperatorRequest(userPrompt, args.Player);
                        break;
                    default: 
                        aiResponseText = await HandleStandardRequest(userPrompt, args.Player.Name, modelToUse, systemPrompt);
                        break;
                }
                
                string header = $"\"{userPrompt}\" Asked by {args.Player.Name}:";
                TShock.Utils.Broadcast(header, TextHelper.AINameColor);

                BroadcastResponse(aiResponseText);

                LogToFile($"[SUCCESS] User: {args.Player.Name} | Mode: {mode} | Model: {modelToUse} | Prompt: {userPrompt}\n[RESPONSE] {aiResponseText}\n");
            }
            catch (Exception ex)
            {
                string fullError = ex.ToString();
                TShock.Utils.Broadcast("[AskAI] API Request Failed. Please see live.log for details.", TextHelper.ErrorColor);
                LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {string.Join(" ", args.Parameters)}\n[ERROR] {fullError}\n");
            }
            finally
            {
                _isAIBusy = false;
            }
        }
        
        private async Task<string> HandleStandardRequest(string userPrompt, string playerName, string modelId, string systemPrompt)
        {
            string finalPrompt = $"Asked from {playerName}: {userPrompt}";
            var tools = new List<Tool> { new Tool { GoogleSearch = new object() } };
            var response = await VertexAI.AskAsync(finalPrompt, _config, modelId, systemPrompt, tools);
            return response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "The AI returned an empty response.";
        }
        
        private async Task<string> HandleDetailedRequest(string userPrompt, string playerName)
        {
            string dispatcherModel = "gemini-2.5-flash";
            string synthesizerModel = "gemini-2.5-pro";

            var dispatcherTools = new List<Tool> { new Tool { FunctionDeclarations = new List<FunctionDeclaration>
            {
                new FunctionDeclaration
                {
                    Name = "get_recipe", Description = "Finds the crafting recipe for a given Terraria item name.",
                    Parameters = new ParametersInfo { Properties = new Dictionary<string, PropertyInfo> { { "itemName", new PropertyInfo { Type = "STRING", Description = "The name of the Terraria item." } } } }
                }
            }, GoogleSearch = new object() } };

            var dispatcherResponse = await VertexAI.AskAsync($"From {playerName}: {userPrompt}", _config, dispatcherModel, "You are a dispatcher. Your only job is to determine the best tool to answer the user's query.", dispatcherTools);
            var functionCall = dispatcherResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.FunctionCall;

            string context = "";
            if (functionCall?.Name == "get_recipe")
            {
                context = PluginTools.GetRecipe(functionCall.Args["itemName"].ToString());
            }
            else
            {
                var searchResponse = await HandleStandardRequest(userPrompt, playerName, dispatcherModel, _config.SystemPrompt);
                context = "Search Result: " + searchResponse;
            }

            string synthesizerPrompt = $"Please answer the user's original question based on the following information.\n\nContext:\n{context}\n\nOriginal Question: {userPrompt}";
            var synthesizerResponse = await VertexAI.AskAsync(synthesizerPrompt, _config, synthesizerModel, _config.SystemPromptDetailed, null);
            return synthesizerResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "The AI failed to synthesize a detailed response.";
        }
        
        private async Task<string> HandleOperatorRequest(string userPrompt, TSPlayer player)
        {
            var opTools = new List<Tool>
            {
                new Tool { FunctionDeclarations = new List<FunctionDeclaration>
                {
                    new FunctionDeclaration
                    {
                        Name = "tshock_command", Description = "Executes a TShock server command.",
                        Parameters = new ParametersInfo { Properties = new Dictionary<string, PropertyInfo> { { "command_string", new PropertyInfo { Type = "STRING", Description = "The full command string to execute, starting with a '/'." } } } }
                    }
                }, GoogleSearch = new object() }
            };

            var response = await VertexAI.AskAsync($"From {player.Name}: {userPrompt}", _config, "gemini-2.5-pro", _config.SystemPromptOperator, opTools);
            var functionCall = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.FunctionCall;

            if (functionCall?.Name == "tshock_command")
            {
                string command = functionCall.Args["command_string"].ToString();
                var aiOperator = new TSRestPlayer(AI_OPERATOR_USER, TShock.Groups.GetGroupByName(AI_OPERATOR_GROUP));
                TShockAPI.Commands.HandleCommand(aiOperator, command);
                string commandOutput = string.Join("\n", aiOperator.GetCommandOutput());
                return $"Command executed: `{command}`\nOutput: {commandOutput}";
            }
            
            return response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "The AI chose not to execute a command.";
        }

        private void BroadcastResponse(string responseText)
        {
            var processedText = ProcessEmojis(responseText);
            var sanitizedText = processedText.Replace("\r", "");
            string[] lines = sanitizedText.Split('\n');
            
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string fixedLine = FixBrokenColorTags(line);
                TShock.Utils.Broadcast(fixedLine, Color.White);
            }
        }
        
        private string ProcessEmojis(string text)
        {
            return Regex.Replace(text, @"::([\w\s]+)::", match =>
            {
                return $":{match.Groups[1].Value.Replace(" ", "").ToLowerInvariant()}:";
            });
        }

        private string FixBrokenColorTags(string line)
        {
            if (line.Contains("[c/") && !line.EndsWith("]"))
            {
                return line + "]";
            }
            return line;
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
