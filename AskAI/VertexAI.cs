using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using System.Net.Http;

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
        private static string _logFilePath;

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
                HelpText = "Asks a question to the AI. Usage: /askai <your question>",
                AllowServer = false
            };
            Commands.ChatCommands.Add(_askAiCommand);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Commands.ChatCommands.Remove(_askAiCommand);
            }
            base.Dispose(disposing);
        }

        private async void AskAICommand(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                string usage = TextHelper.Colorize("/askai", TextHelper.UsageColor);
                string param = TextHelper.Colorize("<your question>", TextHelper.UsageParamColor);
                args.Player.SendErrorMessage($"Usage: {usage} {param}");
                return;
            }

            string userPrompt = string.Join(" ", args.Parameters);
            string finalPrompt = $"Asked from {args.Player.Name}: {userPrompt}";

            TShock.Utils.Broadcast("Waiting for response...", TextHelper.InfoColor);

            try
            {
                string apiKey = _config.ApiKeys[_currentApiKeyIndex];
                string aiResponse = await VertexAI.AskAsync(finalPrompt, _config, apiKey);
                
                aiResponse = TextHelper.FixMalformedColorTags(aiResponse);
                
                string header = $"Asked by {args.Player.Name}:";
                TShock.Utils.Broadcast(header, TextHelper.AINameColor);

                const int terrariaChatLimit = 500;
                for (int i = 0; i < aiResponse.Length; i += terrariaChatLimit)
                {
                    string chunk = aiResponse.Substring(i, Math.Min(terrariaChatLimit, aiResponse.Length - i));
                    TShock.Utils.Broadcast(chunk, Color.White);
                }

                LogToFile($"[SUCCESS] User: {args.Player.Name} | KeyIndex: {_currentApiKeyIndex} | Prompt: {userPrompt}\n[RESPONSE] {aiResponse}\n");
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("\"code\": 429") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                {
                    _currentApiKeyIndex = (_currentApiKeyIndex + 1) % _config.ApiKeys.Count;
                    TShock.Log.Info($"[AskAI] API key rate limited. Switched to key index: {_currentApiKeyIndex}");
                    args.Player.SendErrorMessage("AI is busy, please try again in a moment.", TextHelper.ErrorColor);
                    LogToFile($"[RATE_LIMIT] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
                }
                else
                {
                    BroadcastError(ex);
                    LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
                }
            }
            catch (Exception ex)
            {
                BroadcastError(ex);
                LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
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
