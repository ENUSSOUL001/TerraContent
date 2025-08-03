using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using System.Net.Http;
using Terraria.Localization;
using Terraria.Chat;

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
            
            player.SendMessage(TextHelper.Colorize("[AskAI]", TextHelper.AINameColor), Color.White);
            string usage = TextHelper.Colorize("/askai <prompt>", TextHelper.UsageColor);
            string flags = TextHelper.Colorize("[optional: -flash|-lite]", TextHelper.UsageParamColor);
            player.SendMessage($"{usage} {flags}", Color.White);
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

            string modelId = _config.ModelId;
            if(parameters.Contains("-flash"))
            {
                modelId = "gemini-2.5-flash";
                parameters.Remove("-flash");
            }
            else if (parameters.Contains("-lite"))
            {
                modelId = "gemini-2.5-flash-lite";
                parameters.Remove("-lite");
            }
            
            string userPrompt = string.Join(" ", parameters);
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                args.Player.SendErrorMessage("Please provide a prompt after the flag.");
                return;
            }
            
            string finalPrompt = $"Asked from {args.Player.Name}: {userPrompt}";

            TShock.Utils.Broadcast("Waiting for response...", TextHelper.InfoColor);

            string aiResponse = null;
            Exception lastException = null;

            for (int i = 0; i < _config.ApiKeys.Count; i++)
            {
                try
                {
                    string apiKey = _config.ApiKeys[_currentApiKeyIndex];
                    aiResponse = await VertexAI.AskAsync(finalPrompt, _config, apiKey, modelId);
                    
                    LogToFile($"[SUCCESS] User: {args.Player.Name} | KeyIndex: {_currentApiKeyIndex} | Model: {modelId} | Prompt: {userPrompt}\n[RESPONSE] {aiResponse}\n");
                    lastException = null; 
                    break;
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    if (ex.Message.Contains("\"code\": 429") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                    {
                        int previousKeyIndex = _currentApiKeyIndex;
                        _currentApiKeyIndex = (_currentApiKeyIndex + 1) % _config.ApiKeys.Count;
                        TShock.Log.Info($"[AskAI] API key at index {previousKeyIndex} was rate limited. Retrying with key index: {_currentApiKeyIndex}");
                        continue;
                    }
                    else
                    {
                        LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
                        break; 
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogToFile($"[FAILURE] User: {args.Player.Name} | Prompt: {userPrompt}\n[ERROR] {ex}\n");
                    break;
                }
            }

            if (aiResponse != null)
            {
                aiResponse = TextHelper.FixMalformedColorTags(aiResponse);
                
                string header = $"Asked by {args.Player.Name}:";
                TShock.Utils.Broadcast(header, TextHelper.AINameColor);
                
                BroadcastAsFakePlayer("AI", aiResponse, TextHelper.AINameColor);
            }
            else if (lastException != null)
            {
                BroadcastError(lastException);
            }
        }
        
        private void BroadcastAsFakePlayer(string name, string text, Color color)
        {
            int fakePlayerIndex = -1;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (TShock.Players[i] == null || !TShock.Players[i].Active)
                {
                    fakePlayerIndex = i;
                    break;
                }
            }

            if (fakePlayerIndex == -1)
            {
                TShock.Utils.Broadcast(text, color);
                return;
            }

            var player = Main.player[fakePlayerIndex];
            var originalName = player.name;
            player.name = name;

            const int terrariaChatLimit = 500;
            for (int i = 0; i < text.Length; i += terrariaChatLimit)
            {
                string chunk = text.Substring(i, Math.Min(terrariaChatLimit, text.Length - i));
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(chunk), color, fakePlayerIndex);
            }
            
            player.name = originalName;
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
