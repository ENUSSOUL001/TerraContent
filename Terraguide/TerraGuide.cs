using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace TerraGuide
{
    [ApiVersion(2, 1)]
    public class TerraGuide : TerrariaPlugin
    {
        public override string Author => "jgranserver";
        public override string Description => "A helpful guide plugin for Terraria servers";
        public override string Name => "TerraGuide";
        public override Version Version => new Version(2, 1);

        private readonly HttpClient _httpClient;
        private const string WikiApiUrl = "https://terraria.wiki.gg/api.php";
        private static bool BroadcastMessages = false;

        public TerraGuide(Main game)
            : base(game)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "TerraGuide/2.1 (TShock Plugin; terraria.wiki.gg/wiki/User:Jgranserver)"
            );
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(WikiCommand, "wiki")
            {
                HelpText = "Searches the Terraria Wiki. Usage: /wiki <search term>",
            });

            Commands.ChatCommands.Add(new Command(RecipeCommand, "recipe")
            {
                HelpText = "Shows crafting information for items. Usage: /recipe <item name>",
            });
            
            Commands.ChatCommands.Add(new Command("terraguide.admin", TerraGuideCommand, "terraguide")
            {
                HelpText = "Manages TerraGuide settings. Usage: /terraguide broadcast",
            });
        }

        private void TerraGuideCommand(CommandArgs args)
        {
            if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "broadcast")
            {
                BroadcastMessages = !BroadcastMessages;
                args.Player.SendSuccessMessage($"TerraGuide message broadcasting is now {(BroadcastMessages ? "ENABLED" : "DISABLED")}.");
            }
            else
            {
                args.Player.SendInfoMessage("Usage: /terraguide broadcast");
            }
        }
        
        private void SendReply(CommandArgs args, string message, Color color)
        {
            if (BroadcastMessages)
            {
                TShock.Utils.Broadcast(message, color);
            }
            else
            {
                foreach (var line in SplitTextIntoChunks(message, 500))
                {
                    args.Player.SendMessage(line, color);
                }
            }
        }

        private async void WikiCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Usage: /wiki <search term>");
                return;
            }

            string searchTerm = string.Join(" ", args.Parameters);
            string searchUrl =
                $"{WikiApiUrl}?action=opensearch&format=json&search={HttpUtility.UrlEncode(searchTerm)}&limit=1&namespace=0&profile=fuzzy";

            try
            {
                SendReply(args, $"Searching wiki for: {searchTerm}...", Color.Aqua);

                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                searchRequest.Headers.Add("Accept", "application/json");

                using (var searchResponse = await _httpClient.SendAsync(searchRequest))
                {
                    searchResponse.EnsureSuccessStatusCode();
                    var searchJson = await searchResponse.Content.ReadAsStringAsync();

                    dynamic searchResult = Newtonsoft.Json.JsonConvert.DeserializeObject(searchJson);

                    if (searchResult[1].Count > 0)
                    {
                        string exactTitle = searchResult[1][0].ToString();
                        string contentUrl =
                            $"{WikiApiUrl}?action=query&format=json&prop=revisions&rvprop=content&rvslots=main&titles={HttpUtility.UrlEncode(exactTitle)}";

                        var contentRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
                        contentRequest.Headers.Add("Accept", "application/json");

                        using (var contentResponse = await _httpClient.SendAsync(contentRequest))
                        {
                            contentResponse.EnsureSuccessStatusCode();
                            var contentJson = await contentResponse.Content.ReadAsStringAsync();

                            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(contentJson);
                            var pages = result.query?.pages;

                            if (pages != null)
                            {
                                var firstPageId = ((Newtonsoft.Json.Linq.JObject)pages).Properties().First().Name;
                                var firstPage = pages[firstPageId];

                                if (firstPage.revisions != null && firstPage.revisions.Count > 0)
                                {
                                    string wikiText = firstPage.revisions[0].slots.main["*"].ToString();
                                    wikiText = CleanWikiText(wikiText);

                                    if (!string.IsNullOrWhiteSpace(wikiText))
                                    {
                                        SendReply(args, wikiText, Color.White);
                                        SendReply(args, $"Read more: https://terraria.wiki.gg/wiki/{HttpUtility.UrlEncode(exactTitle)}", Color.Cyan);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    SendReply(args, $"No information found for '{searchTerm}'. Try using the exact item name (e.g., 'Dirt Block' instead of 'dirt').", Color.OrangeRed);
                }
            }
            catch (Exception ex)
            {
                SendReply(args, $"Error accessing wiki: {ex.Message}", Color.Red);
                TShock.Log.Error($"TerraGuide wiki error for term '{searchTerm}': {ex}");
            }
        }

        private void RecipeCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Usage: /recipe <item name>");
                return;
            }

            string searchTerm = string.Join(" ", args.Parameters);
            var items = TShock.Utils.GetItemByName(searchTerm);

            if (items.Count == 0)
            {
                SendReply(args, $"No items found matching '{searchTerm}'.", Color.OrangeRed);
                return;
            }
            if (items.Count > 1)
            {
                args.Player.SendMultipleMatchError(items.Select(i => i.Name));
                return;
            }

            var bestMatch = items[0];
            var recipes = Main.recipe.Where(r => r != null && r.createItem.type == bestMatch.type).ToList();

            if (recipes.Count == 0)
            {
                SendReply(args, $"No crafting recipe found for {bestMatch.Name}.", Color.OrangeRed);
                return;
            }

            SendReply(args, $"Crafting information for {TextHelper.ColorRecipeName(bestMatch.Name)}:", Color.Gold);

            foreach (var recipe in recipes)
            {
                if (recipe.requiredTile != null && recipe.requiredTile.Length > 0)
                {
                    var stations = recipe.requiredTile.Where(t => t >= 0).Select(t => TextHelper.ColorStation(TileID.Search.GetName(t))).ToList();
                    if(stations.Any())
                        SendReply(args, $"Crafting Station: {string.Join(" or ", stations)}", Color.White);
                }

                SendReply(args, "Required Items:", Color.White);
                for (int i = 0; i < recipe.requiredItem.Length; i++)
                {
                    if (recipe.requiredItem[i].type > 0)
                    {
                        SendReply(args, $"• {recipe.requiredItem[i].stack}x {TextHelper.ColorItem(recipe.requiredItem[i].Name)}", Color.White);
                    }
                }

                var conditions = new List<string>();
                if (recipe.needWater) conditions.Add("Must be near Water");
                if (recipe.needLava) conditions.Add("Must be near Lava");
                if (recipe.needHoney) conditions.Add("Must be near Honey");
                if (recipe.needSnowBiome) conditions.Add("Must be in Snow biome");
                if (recipe.needGraveyardBiome) conditions.Add("Must be in Graveyard biome");

                if (conditions.Count > 0)
                {
                    SendReply(args, "\nSpecial Requirements:", Color.Yellow);
                    foreach (var condition in conditions)
                    {
                        SendReply(args, $"• {condition}", Color.White);
                    }
                }
            }
        }

        private string CleanWikiText(string wikiText)
        {
            wikiText = Regex.Replace(wikiText, @"\{\{item infobox[\s\S]*?\}\}", "");
            wikiText = Regex.Replace(wikiText, @"\{\{[^}]*\}\}", "");
            wikiText = Regex.Replace(wikiText, @"\[\[File:[^\]]*\]\].*?\n", "");
            wikiText = Regex.Replace(wikiText, @"\[\[([^|\]]*?)\]\]", "$1");
            wikiText = Regex.Replace(wikiText, @"\[\[(?:[^|\]]*\|)?([^\]]+)\]\]", "$1");
            var paragraphs = wikiText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith("*") && !p.StartsWith("|") && !p.StartsWith("{{") && !p.StartsWith("}}") && !p.StartsWith("==") && !p.StartsWith(":") && p.Length > 20);
            var description = paragraphs.FirstOrDefault() ?? "No description available.";
            description = Regex.Replace(description, @"'{2,}", "");
            description = Regex.Replace(description, @"\s+", " ");
            description = HttpUtility.HtmlDecode(description).Trim();
            return description;
        }

        private IEnumerable<string> SplitTextIntoChunks(string text, int chunkSize)
        {
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
