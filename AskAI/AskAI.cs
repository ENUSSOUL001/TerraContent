using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AskAI
{
    [ApiVersion(2, 1)]
    public class AskAI : TerrariaPlugin
    {
        public override string Author => "enussoul";
        public override string Description => "Ask AI within the Game!";
        public override string Name => "AskAI";
        public override Version Version => new Version(1, 0, 0, 0);

        private static AskAIConfig _config;
        private Command _askAiCommand;
        private static int _currentApiKeyIndex = 0;
        private static string _logDirectory;

        public AskAI(Main game) : base(game) { }

        public override void Initialize()
        {
            _logDirectory = Path.Combine(TShock.SavePath, "AskAI", "Logs");
            Directory.CreateDirectory(_logDirectory);

            string configPath = Path.Combine(TShock.SavePath, "AskAI", "config.json");
            _config = AskAIConfig.Read(configPath);
            
            _askAiCommand = new Command("askai.use", AskAICommand, "askai")
            {
                HelpText = "Asks a question to the AI. Usage: /askai <your question>",
                AllowServer = false
            };
            Commands.ChatCommands.Add(_askAiCommand);
            
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Commands.ChatCommands.Remove(_askAiCommand);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
            }
            base.Dispose(disposing);
        }
        
        private void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            var player = TShock.Players[args.Who];
            if (player == null) return;

            string header = TextHelper.Colorize("[AskAI]", TextHelper.GreetHeaderColor);
            string command = TextHelper.Colorize("/askai <prompt>", TextHelper.UsageColor);
            string flags = TextHelper.Colorize("[optional: -flash|-lite]", TextHelper.UsageParamColor);

            player.SendMessage(header, Color.White);
            player.SendMessage($"  {command} {flags}", Color.White);
        }

        private async void AskAICommand(CommandArgs args)
        {
            var (modelToUse, userPrompt) = ParseArguments(args.Parameters);

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                string usage = TextHelper.Colorize("/askai", TextHelper.UsageColor);
                string param = TextHelper.Colorize("<your question>", TextHelper.UsageParamColor);
                args.Player.SendErrorMessage($"Usage: {usage} {param}");
                return;
            }

            TShock.Utils.Broadcast("Waiting for AI response...", TextHelper.InfoColor);

            var (success, initialAiResponse, rawRequestBody, rawResponseBody) = await HandleApiRequest(args.Player, modelToUse, userPrompt);
            
            if (success)
            {
                string processedResponse = TextHelper.ConvertAiColorTags(initialAiResponse);
                LogRequest(args.Player.Name, _currentApiKeyIndex, modelToUse, userPrompt, rawRequestBody, rawResponseBody, initialAiResponse, processedResponse, true);
                BroadcastResponse(args.Player, userPrompt, processedResponse);
            }
            else
            {
                args.Player.SendErrorMessage("AI request failed after trying all available API keys. Please check the server logs for details.");
            }
        }

        private (string model, string prompt) ParseArguments(List<string> parameters)
        {
            string modelToUse = _config.ModelId;
            var promptParts = new List<string>(parameters);

            if (promptParts.Contains("-flash"))
            {
                modelToUse = "gemini-2.5-flash";
                promptParts.Remove("-flash");
            }
            else if (promptParts.Contains("-lite"))
            {
                modelToUse = "gemini-2.5-flash-lite";
                promptParts.Remove("-lite");
            }

            return (modelToUse, string.Join(" ", promptParts));
        }

        private async Task<(bool success, string response, string reqBody, string resBody)> HandleApiRequest(TSPlayer player, string modelToUse, string userPrompt)
        {
            string finalPrompt = $"Asked from {player.Name}: {userPrompt}";
            
            for (int i = 0; i < _config.ApiKeys.Count; i++)
            {
                try
                {
                    string apiKey = _config.ApiKeys[_currentApiKeyIndex];
                    var (aiResponse, rawRequestBody, rawResponseBody) = await VertexAI.AskAsync(finalPrompt, _config, apiKey, modelToUse);
                    return (true, aiResponse, rawRequestBody, rawResponseBody);
                }
                catch (HttpRequestException ex)
                {
                    bool isQuotaError = ex.Message.Contains("\"code\": 429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("API key expired");
                    if (isQuotaError)
                    {
                        LogRequest(player.Name, _currentApiKeyIndex, modelToUse, userPrompt, "", ex.Message, null, null, false, true);
                        _currentApiKeyIndex = (_currentApiKeyIndex + 1) % _config.ApiKeys.Count;
                        
                        if (i < _config.ApiKeys.Count - 1)
                        {
                            TShock.Utils.Broadcast("Something happened... Hold on...", TextHelper.InfoColor);
                        }
                        continue;
                    }
                    else
                    {
                        player.SendErrorMessage("An API request error occurred. Check server logs for details.");
                        TShock.Log.Error($"[AskAI] API Request Failed: {ex}");
                        LogRequest(player.Name, _currentApiKeyIndex, modelToUse, userPrompt, "", ex.ToString(), null, null, false);
                        return (false, null, null, null);
                    }
                }
                catch (Exception ex)
                {
                    player.SendErrorMessage("A critical error occurred. Check server logs for details.");
                    TShock.Log.Error($"[AskAI] Critical Failure: {ex}");
                    LogRequest(player.Name, _currentApiKeyIndex, modelToUse, userPrompt, "", ex.ToString(), null, null, false);
                    return (false, null, null, null);
                }
            }
            return (false, null, null, null);
        }

        private void BroadcastResponse(TSPlayer asker, string userPrompt, string processedResponse)
        {
            string promptDisplay = TextHelper.Colorize($"\"{userPrompt}\"", Color.LightGray);
            string header = $" - Asked by {asker.Name}";
            TShock.Utils.Broadcast($"{promptDisplay}{header}", TextHelper.AINameColor);
            
            string responseHeader = TextHelper.Colorize("<AI>", TextHelper.AINameColor);
            
            const int terrariaChatLimit = 500;
            var responseLines = processedResponse.Split('\n');
            bool headerSent = false;

            foreach (var line in responseLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    TShock.Utils.Broadcast(" ", Color.White);
                    continue;
                }

                for (int i = 0; i < line.Length; i += terrariaChatLimit)
                {
                    string chunk = line.Substring(i, Math.Min(terrariaChatLimit, line.Length - i));
                    if (!headerSent)
                    {
                        TShock.Utils.Broadcast($"{responseHeader} {chunk}", Color.White);
                        headerSent = true;
                    }
                    else
                    {
                        TShock.Utils.Broadcast(chunk, Color.White);
                    }
                }
            }
        }
        
        private void LogRequest(string userName, int keyIndex, string model, string prompt, string requestBody, string rawResponse, string initialAiOutput, string processedAiOutput, bool success, bool isQuotaError = false)
        {
            var logMessage = new StringBuilder();
            string status = isQuotaError ? "KEY_FAILURE" : (success ? "SUCCESS" : "FAILURE");
            logMessage.AppendLine($"[{status}] User: {userName} | KeyIndex: {keyIndex} | Model: {model} | Prompt: {prompt}");

            if (_config.LogSettings.LogApiRequests && !string.IsNullOrEmpty(requestBody))
            {
                logMessage.AppendLine("--- REQUEST BODY ---");
                logMessage.AppendLine(requestBody);
            }
            if (_config.LogSettings.LogApiRawResponses && !string.IsNullOrEmpty(rawResponse))
            {
                logMessage.AppendLine("--- RAW RESPONSE ---");
                logMessage.AppendLine(rawResponse);
            }
            if (_config.LogSettings.LogInitialAiOutput && !string.IsNullOrEmpty(initialAiOutput))
            {
                logMessage.AppendLine("--- INITIAL AI OUTPUT ---");
                logMessage.AppendLine(initialAiOutput);
            }
            if (_config.LogSettings.LogProcessedAiOutput && !string.IsNullOrEmpty(processedAiOutput))
            {
                logMessage.AppendLine("--- PROCESSED FINAL OUTPUT ---");
                logMessage.AppendLine(processedAiOutput);
            }
            
            LogToFile(logMessage.ToString());
        }

        private static void LogToFile(string message)
        {
            try
            {
                string logFilePath = Path.Combine(_logDirectory, $"live_{DateTime.UtcNow:yyyy-MM-dd}.txt");
                File.AppendAllText(logFilePath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]\n{message}\n");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[AskAI] Failed to write to log: {ex.Message}");
            }
        }
    }
}
