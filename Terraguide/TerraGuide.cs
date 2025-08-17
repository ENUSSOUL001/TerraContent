using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using Newtonsoft.Json.Linq;

namespace TerraGuide
{
    [ApiVersion(2, 1)]
    public class TerraGuide : TerrariaPlugin
    {
        public override string Author => "jgranserver & RecipesBrowser contributors";
        public override string Description => "A helpful guide plugin for Terraria servers";
        public override string Name => "TerraGuide";
        public override Version Version => new Version(3, 3);

        private readonly HttpClient _httpClient;
        private const string WikiApiUrl = "https://terraria.wiki.gg/api.php";
        private static bool BroadcastMessages = true;

        public TerraGuide(Main game)
            : base(game)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "TerraGuide/3.3 (TShock Plugin; terraria.wiki.gg/wiki/User:Jgranserver)"
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
                HelpText = "Shows crafting info. Usage: /recipe <item name> [-r]",
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
            var lines = SplitTextIntoChunks(message, 500);
            if (BroadcastMessages)
            {
                foreach (var line in lines)
                {
                    TShock.Utils.Broadcast(line, color);
                }
            }
            else
            {
                foreach (var line in lines)
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

                string searchJson = await _httpClient.GetStringAsync(searchUrl);
                var searchResult = JArray.Parse(searchJson);
                var titles = searchResult.Count > 1 ? searchResult[1] as JArray : null;

                if (titles == null || !titles.Any())
                {
                    SendReply(args, $"No information found for '{searchTerm}'. Try using the exact item name (e.g., 'Dirt Block' instead of 'dirt').", Color.OrangeRed);
                    return;
                }

                string exactTitle = titles[0].ToString();
                string contentUrl =
                    $"{WikiApiUrl}?action=query&format=json&prop=revisions&rvprop=content&rvslots=main&titles={HttpUtility.UrlEncode(exactTitle)}";

                string contentJson = await _httpClient.GetStringAsync(contentUrl);
                var result = JObject.Parse(contentJson);
                var pages = result?["query"]?["pages"];
                if (pages == null)
                {
                    SendReply(args, $"Could not parse wiki page content for '{exactTitle}'.", Color.OrangeRed);
                    return;
                }

                var firstPage = pages.Values<JProperty>().FirstOrDefault()?.Value;
                if (firstPage == null)
                {
                    SendReply(args, $"Could not find page data for '{exactTitle}'.", Color.OrangeRed);
                    return;
                }

                var revisions = firstPage["revisions"] as JArray;
                if (revisions != null && revisions.Any())
                {
                    string wikiText = revisions[0]?["slots"]?["main"]?["*"]?.ToString() ?? "";
                    wikiText = CleanWikiText(wikiText);

                    if (!string.IsNullOrWhiteSpace(wikiText))
                    {
                        SendReply(args, wikiText, Color.White);
                        SendReply(args, $"Read more: https://terraria.wiki.gg/wiki/{HttpUtility.UrlEncode(exactTitle)}", Color.Cyan);
                        return;
                    }
                }

                SendReply(args, $"No description found for '{exactTitle}'.", Color.OrangeRed);
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
                args.Player.SendErrorMessage("Usage: /recipe <item name> [-r]");
                return;
            }

            bool reverseLookup = args.Parameters.Contains("-r");
            var searchTerms = args.Parameters.Where(p => p.ToLower() != "-r").ToList();
            string searchTerm = string.Join(" ", searchTerms);

            var items = TShock.Utils.GetItemByName(searchTerm);

            if (items.Count == 0)
            {
                SendReply(args, $"No items found matching '{searchTerm}'.", Color.OrangeRed);
                return;
            }
            if (items.Count > 1)
            {
                args.Player.SendMultipleMatchError(items.Select(i => $"{i.Name}({i.netID})"));
                return;
            }

            var item = items[0];

            if (reverseLookup)
            {
                SendReply(args, GetRecipeStringByRequired(item), Color.White);
            }
            else
            {
                var recipes = Main.recipe.Where(r => r != null && r.createItem.type == item.type).ToList();
                if (recipes.Count == 0)
                {
                    SendReply(args, $"No crafting recipe found for {item.Name}.", Color.OrangeRed);
                    return;
                }

                var result = new StringBuilder();
                result.AppendLine($"Item: {TShock.Utils.ItemTag(item)}");

                for (var i = 0; i < recipes.Count; i++)
                {
                    result.AppendLine($"Recipe {i + 1}:");
                    result.AppendLine(GetRecipeStringByResult(recipes[i]));
                }
                SendReply(args, result.ToString().TrimEnd(), Color.White);
            }
        }

        private string GetRecipeStringByResult(Recipe recipe)
        {
            var result = new StringBuilder();
            result.Append("Materials: ");
            foreach (var item in recipe.requiredItem.Where(r => r.stack > 0))
            {
                result.Append($"{TShock.Utils.ItemTag(item)} ({item.stack}) ");
            }
            result.AppendLine();

            var stations = recipe.requiredTile.Where(i => i >= 0).Select(GetStationName).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (stations.Any())
            {
                result.Append("Station: ");
                result.Append(string.Join(", ", stations));
            }
            else
            {
                result.Append("Station: By Hand [i:3258]");
            }

            var conditions = new List<string>();
            if (recipe.needWater) conditions.Add("Water [i:126]");
            if (recipe.needLava) conditions.Add("Lava [i:4825]");
            if (recipe.needHoney) conditions.Add("Honey [i:1134]");
            if (recipe.needSnowBiome) conditions.Add("Snow Biome [i:593]");
            if (recipe.needGraveyardBiome) conditions.Add("Graveyard [i:321]");

            if (conditions.Any())
            {
                result.AppendLine();
                result.Append($"Conditions: {string.Join(", ", conditions)}");
            }

            return result.ToString();
        }

        private string GetRecipeStringByRequired(Item item)
        {
            var result = new StringBuilder();
            result.AppendLine($"Items that can be crafted with {TShock.Utils.ItemTag(item)}:");
            var recipes = Main.recipe.Where(r => r != null && r.requiredItem.Any(i => i.type == item.type)).ToList();

            if (recipes.Count == 0)
            {
                return $"No recipes use {item.Name} as a material.";
            }

            var recipeLines = recipes.Select(r => r.createItem)
                                     .DistinctBy(i => i.netID)
                                     .Select(i => $"{TShock.Utils.ItemTag(i)} ({i.stack})");

            result.Append(string.Join(", ", recipeLines));
            return result.ToString();
        }

        private string? GetStationName(int tileId)
        {
            if (tileId < 0 || tileId >= Terraria.Map.MapHelper.tileLookup.Length)
            {
                return null;
            }

            int legendIndex = Terraria.Map.MapHelper.tileLookup[tileId];
            if (legendIndex < 0 || legendIndex >= Lang._mapLegendCache.Length)
            {
                return null;
            }

            var langEntry = Lang._mapLegendCache[legendIndex];
            return langEntry?.Value;
        }

        private string CleanWikiText(string wikiText)
        {
            wikiText = Regex.Replace(wikiText, @"\{\{item infobox[\s\S]*?\}\}", "");
            wikiText = Regex.Replace(wikiText, @"\{\{[^}]*\}\}", "");
            wikiText = Regex.Replace(wikiText, @"\[\[File:[^\]]*?\]\]", "");
            wikiText = Regex.Replace(wikiText, @"\[\[([^|\]]*?)\]\]", "$1");
            wikiText = Regex.Replace(wikiText, @"\[\[(?:[^|\]]*\|)?([^\]]+)\]\]", "$1");
            var paragraphs = wikiText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith("*") && !p.StartsWith("|") && !p.StartsWith("{{") && !p.StartsWith("}}") && !p.StartsWith("==") && !p.StartsWith(":") && p.Length > 20);
            var description = paragraphs.FirstOrDefault() ?? "No description available.";
            description = Regex.Replace(description, @"'{2,5}", "");
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
