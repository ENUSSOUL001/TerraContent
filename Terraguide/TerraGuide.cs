// ADDED: Additional 'using' statements required for new features.
using Microsoft.Xna.Framework;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// --- Original 'using' statements are all preserved below ---
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace TerraGuide
{
    [ApiVersion(2, 1)]
    public class TerraGuide : TerrariaPlugin
    {
        // TWEAKED: Metadata updated to reflect the new version and contributors.
        public override string Author => "jgranserver & RecipesBrowser contributors";
        public override string Description => "A helpful guide plugin for Terraria servers";
        public override string Name => "TerraGuide";
        public override Version Version => new Version(1, 2);

        private readonly HttpClient _httpClient;
        private const string WikiBaseUrl = "https://terraria.wiki.gg/wiki/";
        private const string WikiApiUrl = "https://terraria.wiki.gg/api.php";

        // ADDED: Broadcast toggle feature from the newer code.
        private static bool BroadcastMessages = true;

        public TerraGuide(Main game)
            : base(game)
        {
            _httpClient = new HttpClient();
            // TWEAKED: User-Agent updated to the newer version's standard for better API etiquette.
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "TerraGuide/1.2 (TShock Plugin; terraria.wiki.gg/wiki/User:Jgranserver)"
            );
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(
                new Command("terraguide.use", WikiCommand, "wiki")
                {
                    HelpText = "Searches the Terraria Wiki. Usage: /wiki <search term>",
                }
            );

            Commands.ChatCommands.Add(
                new Command("terraguide.use", RecipeCommand, "recipe")
                {
                    // TWEAKED: Help text updated to inform users of the new -r flag.
                    HelpText = "Shows crafting information for items. Usage: /recipe <item name> [-r to see what this item crafts into]",
                }
            );

            // ADDED: New command for managing plugin settings (broadcast toggle).
            Commands.ChatCommands.Add(
                new Command("terraguide.admin", TerraGuideCommand, "terraguide")
                {
                    HelpText = "Manages TerraGuide settings. Usage: /terraguide broadcast",
                }
            );
        }

        // ADDED: Method to handle the new /terraguide admin command.
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

        // ADDED: Centralized message handler for broadcast/private functionality.
        // This is essential for making the broadcast toggle work everywhere.
        private void SendReply(CommandArgs args, string message, Color color)
        {
            if (BroadcastMessages)
            {
                TShock.Utils.Broadcast(message, color);
            }
            else
            {
                args.Player.SendMessage(message, color);
            }
        }

        // ORIGINAL WikiCommand, TWEAKED to use the new SendReply system for broadcast/color support.
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
                // TWEAKED: Replaced args.Player.SendInfoMessage with SendReply.
                SendReply(args, $"Searching wiki for: {searchTerm}...", Color.Aqua);
                TShock.Log.Info($"Accessing search API URL: {searchUrl}");

                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                searchRequest.Headers.Add("Accept", "application/json");

                using (var searchResponse = await _httpClient.SendAsync(searchRequest))
                {
                    searchResponse.EnsureSuccessStatusCode();
                    var searchJson = await searchResponse.Content.ReadAsStringAsync();
                    TShock.Log.Info($"Search API response: {searchJson}");

                    dynamic searchResult = Newtonsoft.Json.JsonConvert.DeserializeObject(
                        searchJson
                    );

                    if (searchResult[1].Count > 0)
                    {
                        string exactTitle = searchResult[1][0].ToString();
                        string contentUrl =
                            $"{WikiApiUrl}?action=query&format=json&prop=revisions&rvprop=content&rvslots=main&titles={HttpUtility.UrlEncode(exactTitle)}";

                        TShock.Log.Info($"Fetching content from: {contentUrl}");

                        var contentRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
                        contentRequest.Headers.Add("Accept", "application/json");

                        using (var contentResponse = await _httpClient.SendAsync(contentRequest))
                        {
                            contentResponse.EnsureSuccessStatusCode();
                            var contentJson = await contentResponse.Content.ReadAsStringAsync();
                            TShock.Log.Info($"Content API response: {contentJson}");

                            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                contentJson
                            );
                            var pages = result.query?.pages;

                            if (pages != null)
                            {
                                var firstPageId = ((Newtonsoft.Json.Linq.JObject)pages)
                                    .Properties()
                                    .First()
                                    .Name;
                                var firstPage = pages[firstPageId];

                                if (firstPage.revisions != null && firstPage.revisions.Count > 0)
                                {
                                    string wikiText = firstPage
                                        .revisions[0]
                                        .slots.main["*"]
                                        .ToString();
                                    wikiText = CleanWikiText(wikiText);

                                    if (!string.IsNullOrWhiteSpace(wikiText))
                                    {
                                        const int chunkSize = 500;
                                        var chunks = SplitTextIntoChunks(wikiText, chunkSize);

                                        foreach (var chunk in chunks)
                                        {
                                            // TWEAKED: Replaced args.Player.SendInfoMessage with SendReply.
                                            SendReply(args, chunk, Color.White);
                                        }

                                        // TWEAKED: Replaced args.Player.SendInfoMessage with SendReply.
                                        SendReply(args, $"Read more: {WikiBaseUrl}{HttpUtility.UrlEncode(exactTitle)}", Color.Cyan);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    // TWEAKED: Replaced args.Player.SendErrorMessage with SendReply.
                    SendReply(args, $"No information found for '{searchTerm}'. Try using the exact item name (e.g., 'Dirt Block' instead of 'dirt').", Color.OrangeRed);
                }
            }
            catch (Exception ex)
            {
                // TWEAKED: Replaced args.Player.SendErrorMessage with SendReply.
                SendReply(args, $"Error accessing wiki: {ex.Message}", Color.Red);
                TShock.Log.Error($"TerraGuide wiki error for term '{searchTerm}': {ex}");
            }
        }

        // ORIGINAL RecipeCommand, TWEAKED with reverse lookup and enhanced UI.
        private void RecipeCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                // TWEAKED: Replaced SendErrorMessage with SendReply.
                SendReply(args, "Usage: /recipe <item name> [-r]", Color.Red);
                return;
            }
            
            // ADDED: Logic to handle the new reverse lookup feature.
            bool reverseLookup = args.Parameters.Contains("-r");
            var searchTerms = args.Parameters.Where(p => p.ToLower() != "-r").ToList();
            string searchTerm = string.Join(" ", searchTerms);

            // TWEAKED: The original 'searchTerm' variable is now built from the filtered list.
            // The original logic below this point remains identical.
            var matchingItems = new List<(Item item, float score)>();

            // --- UNTOUCHED ORIGINAL CODE BLOCK (Item finding logic) ---
            var searchPattern = string.Join(
                ".*?",
                searchTerm
                    .Split(' ')
                    .Select(term => System.Text.RegularExpressions.Regex.Escape(term))
            );
            var regex = new System.Text.RegularExpressions.Regex(
                searchPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            for (int i = 0; i < ItemID.Count; i++)
            {
                Item item = new Item();
                item.SetDefaults(i);

                if (string.IsNullOrEmpty(item.Name))
                    continue;

                if (item.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    matchingItems.Clear();
                    matchingItems.Add((item, 1.0f));
                    break;
                }

                var match = regex.Match(item.Name);
                if (match.Success)
                {
                    float score =
                        (float)match.Length / Math.Max(item.Name.Length, searchTerm.Length);
                    matchingItems.Add((item, score));
                }
                else if (item.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    matchingItems.Add((item, 0.5f));
                }
            }
            // --- END OF UNTOUCHED ORIGINAL CODE BLOCK ---

            if (matchingItems.Count == 0)
            {
                // TWEAKED: Replaced SendErrorMessage with SendReply.
                SendReply(args, $"No items found matching '{searchTerm}'.", Color.OrangeRed);
                return;
            }

            var bestMatch = matchingItems.OrderByDescending(x => x.score).First();
            
            // ADDED: Execution path for the new reverse lookup feature.
            if (reverseLookup)
            {
                SendReply(args, GetRecipeStringByRequired(bestMatch.item), Color.White);
                return;
            }

            var recipes = Main
                .recipe.Where(r => r != null && r.createItem.type == bestMatch.item.type)
                .ToList();

            if (recipes.Count == 0)
            {
                // TWEAKED: Replaced SendErrorMessage with SendReply.
                SendReply(args, $"No crafting recipe found for {bestMatch.item.Name}.", Color.OrangeRed);
                return;
            }

            // TWEAKED: Upgraded output with colors and better formatting.
            SendReply(args, $"Crafting information for {TextHelper.ColorRecipeName(bestMatch.item.Name)}:", Color.Gold);

            foreach (var recipe in recipes)
            {
                if (recipe.requiredTile != null && recipe.requiredTile.Length > 0)
                {
                    var stations = recipe
                        .requiredTile.Where(t => t >= 0)
                        .Select(t => TextHelper.ColorStation(TileID.Search.GetName(t)))
                        .ToList();
                    // TWEAKED: Upgraded output with colors.
                    SendReply(args, $"Crafting Station: {string.Join(" or ", stations)}", Color.Yellow);
                }

                // TWEAKED: Upgraded output with colors.
                SendReply(args, "Required Items:", Color.Yellow);
                for (int i = 0; i < recipe.requiredItem.Length; i++)
                {
                    if (recipe.requiredItem[i].type > 0)
                    {
                        // TWEAKED: Upgraded output to include item icons [i:ID] and colors.
                        SendReply(args, $"• {TShock.Utils.ItemTag(recipe.requiredItem[i])} {recipe.requiredItem[i].stack}x {TextHelper.ColorItem(recipe.requiredItem[i].Name)}", Color.White);
                    }
                }

                var conditions = new List<string>();
                if (recipe.needWater)
                    conditions.Add("Must be near Water");
                if (recipe.needLava)
                    conditions.Add("Must be near Lava");
                if (recipe.needHoney)
                    conditions.Add("Must be near Honey");
                if (recipe.needSnowBiome)
                    conditions.Add("Must be in Snow biome");
                if (recipe.needGraveyardBiome)
                    conditions.Add("Must be in Graveyard biome");

                if (conditions.Count > 0)
                {
                    // TWEAKED: Upgraded output with colors.
                    SendReply(args, "\nSpecial Requirements:", Color.Yellow);
                    foreach (var condition in conditions)
                    {
                        SendReply(args, $"• {condition}", Color.White);
                    }
                }
            }
        }

        // ADDED: New helper method for the reverse recipe lookup feature.
        private string GetRecipeStringByRequired(Item item)
        {
            var result = new StringBuilder();
            result.AppendLine($"Items that can be crafted with {TextHelper.ColorItem(item.Name)} {TShock.Utils.ItemTag(item)}:");
            var recipes = Main.recipe.Where(r => r != null && r.requiredItem.Any(i => i.type == item.type)).ToList();

            if (recipes.Count == 0)
            {
                return $"No recipes use {item.Name} as a material.";
            }

            var recipeLines = recipes.Select(r => r.createItem)
                                     .DistinctBy(i => i.netID)
                                     .Select(i => $"{TShock.Utils.ItemTag(i)} {i.Name} ({i.stack})");

            result.Append(string.Join(", ", recipeLines));
            return result.ToString();
        }

        // --- ALL ORIGINAL HELPER METHODS BELOW ARE 100% UNTOUCHED ---

        private string CleanWikiText(string wikiText)
        {
            try
            {
                // Debug the input
                TShock.Log.Info(
                    $"Processing wiki text: {wikiText.Substring(0, Math.Min(200, wikiText.Length))}..."
                );

                // Remove item infobox
                wikiText = System.Text.RegularExpressions.Regex.Replace(
                    wikiText,
                    @"\{\{item infobox[\s\S]*?\}\}",
                    ""
                );

                // Remove other templates
                wikiText = System.Text.RegularExpressions.Regex.Replace(
                    wikiText,
                    @"\{\{[^}]*\}\}",
                    ""
                );

                // Remove file references and images with their captions
                wikiText = System.Text.RegularExpressions.Regex.Replace(
                    wikiText,
                    @"\[\[File:[^\]]*\]\].*?\n",
                    ""
                );

                // Convert wiki links to plain text (keep the readable part)
                wikiText = System.Text.RegularExpressions.Regex.Replace(
                    wikiText,
                    @"\[\[([^|\]]*?)\]\]",
                    "$1"
                );
                wikiText = System.Text.RegularExpressions.Regex.Replace(
                    wikiText,
                    @"\[\[(?:[^|\]]*\|)?([^\]]+)\]\]",
                    "$1"
                );

                // Split into paragraphs
                var paragraphs = wikiText
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p =>
                        !string.IsNullOrWhiteSpace(p)
                        && !p.StartsWith("*")
                        && !p.StartsWith("|")
                        && !p.StartsWith("{{")
                        && !p.StartsWith("}}")
                        && !p.StartsWith("==")
                        && !p.StartsWith(":")
                        && p.Length > 20 // Minimum length for meaningful content
                    );

                // Get the first meaningful paragraph
                var description = paragraphs.FirstOrDefault() ?? "No description available.";

                // Clean up remaining markup and whitespace
                description = System.Text.RegularExpressions.Regex.Replace(
                    description,
                    @"'{2,}",
                    ""
                );
                description = System.Text.RegularExpressions.Regex.Replace(
                    description,
                    @"\s+",
                    " "
                );
                description = HttpUtility.HtmlDecode(description).Trim();

                // Debug the output
                TShock.Log.Info($"Cleaned description: {description}");

                return description;
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error cleaning wiki text: {ex}");
                return "Error processing wiki content.";
            }
        }

        private IEnumerable<string> SplitTextIntoChunks(string text, int chunkSize)
        {
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                if (i + chunkSize <= text.Length)
                {
                    yield return text.Substring(i, chunkSize);
                }
                else
                {
                    yield return text.Substring(i);
                }
            }
        }

        private string ExtractCraftingInfo(string wikiText)
        {
            try
            {
                TShock.Log.Info("Starting to extract crafting info...");

                // First find the Recipes section specifically
                var recipesMatch = System.Text.RegularExpressions.Regex.Match(
                    wikiText,
                    @"===\s*Recipes\s*===\s*(.*?)(?===|\z)",
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );

                if (!recipesMatch.Success)
                {
                    TShock.Log.Info("No Recipes section found.");
                    return "No recipe information found.";
                }

                string recipesSection = recipesMatch.Groups[1].Value.Trim();
                TShock.Log.Info($"Found Recipes section: {recipesSection}");
                var craftingInfo = new System.Text.StringBuilder();

                // Look for recipe template with all parameters
                var recipeMatch = System.Text.RegularExpressions.Regex.Match(
                    recipesSection,
                    @"\{\{recipes\|([^}]+)\}\}",
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );

                if (recipeMatch.Success)
                {
                    string recipeParams = recipeMatch.Groups[1].Value;
                    TShock.Log.Info($"Found recipe parameters: {recipeParams}");

                    // Parse recipe parameters
                    var parameters = recipeParams
                        .Split('|')
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();

                    foreach (var param in parameters)
                    {
                        TShock.Log.Info($"Processing parameter: {param}");

                        if (param.StartsWith("station="))
                        {
                            var station = param.Substring("station=".Length).Trim();
                            station = CleanWikiLinks(station);
                            craftingInfo.AppendLine($"Crafting Station: {station}");
                        }
                        else if (param.StartsWith("i") && char.IsDigit(param[1]))
                        {
                            // Find matching amount parameter
                            var itemMatch = System.Text.RegularExpressions.Regex.Match(
                                param,
                                @"i(\d+)\s*=\s*(.+)"
                            );
                            if (itemMatch.Success)
                            {
                                string index = itemMatch.Groups[1].Value;
                                string item = CleanWikiLinks(itemMatch.Groups[2].Value);

                                // Look for corresponding amount
                                var amountParam = parameters.FirstOrDefault(p =>
                                    p.StartsWith($"a{index}=")
                                );
                                if (amountParam != null)
                                {
                                    var amount = amountParam.Substring($"a{index}=".Length).Trim();
                                    if (!craftingInfo.ToString().Contains("Required Items:"))
                                    {
                                        craftingInfo.AppendLine("\nRequired Items:");
                                    }
                                    craftingInfo.AppendLine($"• {amount}x {item}");
                                    TShock.Log.Info($"Added ingredient: {amount}x {item}");
                                }
                            }
                        }
                    }
                }

                // Add crafting tree note if present
                if (wikiText.Contains("=== Crafting tree ==="))
                {
                    craftingInfo.AppendLine(
                        "\nThis item has a complex crafting tree. Check the wiki for the complete recipe tree."
                    );
                }

                string result = craftingInfo.ToString().Trim();
                TShock.Log.Info($"Final crafting info: {result}");
                return string.IsNullOrWhiteSpace(result)
                    ? "No specific crafting information found."
                    : result;
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error extracting crafting info: {ex}");
                return "Error processing crafting information.";
            }
        }

        // Helper method to clean wiki links
        private string CleanWikiLinks(string text)
        {
            // Convert [[Link|Display]] to Display
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\[\[(?:[^|\]]*\|)?([^\]]+)\]\]",
                "$1"
            );
            return text.Trim();
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
