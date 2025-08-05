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

namespace AskAI
{
    [ApiVersion(2, 1)]
    public class AskAI : TerrariaPlugin
    {
        public override string Author => "You";
        public override string Description => "Lets players ask questions to a powerful AI in-game.";
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
            
            _askAiCommand = new Command("", AskAICommand, "askai")
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
            var parameters = args.Parameters;
            if (parameters.Count == 0)
            {
                string usage = TextHelper.Colorize("/askai", TextHelper.UsageColor);
                string param = TextHelper.Colorize("<your question>", TextHelper.UsageParamColor);
                args.Player.SendErrorMessage($"Usage: {usage} {param}");
                return;
            }

            string modelToUse = _config.ModelId;
            if (parameters.Contains("-flash"))
            {
                modelToUse = "gemini-2.5-flash";
                parameters.Remove("-flash");
            }
            else if (parameters.Contains("-lite"))
            {
                modelToUse = "gemini-2.5-flash-lite";
                parameters.Remove("-lite");
            }

            if (parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Please provide a prompt to ask the AI.");
                return;
            }

            string userPrompt = string.Join(" ", parameters);
            string finalPrompt = $"Asked from {args.Player.Name}: {userPrompt}";

            TShock.Utils.Broadcast("Waiting for response...", TextHelper.InfoColor);

            string aiResponse = null;
            bool success = false;
            string rawRequestBody = "";
            string rawResponseBody = "";
            
            for (int i = 0; i < _config.ApiKeys.Count; i++)
            {
                try
                {
                    string apiKey = _config.ApiKeys[_currentApiKeyIndex];
                    (aiResponse, rawRequestBody, rawResponseBody) = await VertexAI.AskAsync(finalPrompt, _config, apiKey, modelToUse);
                    success = true;
                    LogRequest(args.Player.Name, _currentApiKeyIndex, modelToUse, userPrompt, rawRequestBody, rawResponseBody, aiResponse);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    if (ex.Message.Contains("\"code\": 429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("API key expired"))
                    {
                        LogToFile($"[KEY_FAILURE] User: {args.Player.Name} | KeyIndex: {_currentApiKeyIndex} | Prompt: {userPrompt}\n[ERROR] {ex.Message}\n");
                        _currentApiKeyIndex = (_currentApiKeyIndex + 1) % _config.ApiKeys.Count;
                        
                        if (i < _config.ApiKeys.Count - 1) 
                        {
                             TShock.Utils.Broadcast("Something happened... Hold on....", TextHelper.InfoColor);
                        }
                        continue;
                    }
                    else
                    {
                        BroadcastError(ex);
                        LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    BroadcastError(ex);
                    LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
                    return;
                }
            }

            if (success)
            {
                aiResponse = TextHelper.ConvertAiColorTags(aiResponse);
                
                string promptDisplay = TextHelper.Colorize($"\"{userPrompt}\"", Color.LightGray);
                string header = $" - Asked by {args.Player.Name}";
                TShock.Utils.Broadcast($"{promptDisplay}{header}", TextHelper.AINameColor);
                
                string responseHeader = TextHelper.Colorize("AskAI:", TextHelper.AINameColor);
                
                const int terrariaChatLimit = 500;
                var responseLines = aiResponse.Split('\n');

                foreach (var line in responseLines)
                {
                    for (int i = 0; i < line.Length; i += terrariaChatLimit)
                    {
                        string chunk = line.Substring(i, Math.Min(terrariaChatLimit, line.Length - i));
                        TShock.Utils.Broadcast($"{responseHeader} {chunk}", Color.White);
                    }
                }
            }
            else
            {
                args.Player.SendErrorMessage("AI request failed after trying all available API keys. Please check the server console for details.", TextHelper.ErrorColor);
            }
        }
        
        private static void BroadcastError(Exception ex)
        {
            TShock.Utils.Broadcast("[AskAI] API Request Failed. Full error details:", TextHelper.ErrorColor);
            string fullError = ex.ToString();
            const int terrariaChatLimit = 500;
            for (int i = 0; i < fullError.Length; i += terrariaChatLimit)
            {
                string chunk = fullError.Substring(i, Math.Min(terrariaChatLimit, fullError.Length - i));
                TShock.Utils.Broadcast(chunk, TextHelper.ErrorColor);
            }
        }
        
        private void LogRequest(string userName, int keyIndex, string model, string prompt, string requestBody, string rawResponse, string parsedResponse)
        {
            var logMessage = new StringBuilder();
            logMessage.AppendLine($"[SUCCESS] User: {userName} | KeyIndex: {keyIndex} | Model: {model} | Prompt: {prompt}");

            if (_config.LogSettings.LogApiRequests)
            {
                logMessage.AppendLine("--- REQUEST BODY ---");
                logMessage.AppendLine(requestBody);
            }
            if (_config.LogSettings.LogApiRawResponses)
            {
                logMessage.AppendLine("--- RAW RESPONSE ---");
                logMessage.AppendLine(rawResponse);
            }
            if (_config.LogSettings.LogParsedResponses)
            {
                logMessage.AppendLine("--- PARSED RESPONSE ---");
                logMessage.AppendLine(parsedResponse);
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
