using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
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
        public override Version Version => new Version(2, 0);

        private readonly HttpClient _httpClient;
        private const string WikiApiUrl = "https://terraria.wiki.gg/api.php";
        private static bool BroadcastMessages = false;

        public TerraGuide(Main game)
            : base(game)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "TerraGuide/2.0 (TShock Plugin; terraria.wiki.gg/wiki/User:Jgranserver)"
            );
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(WikiCommand, "wiki")
            {
                HelpText = "Searches the Terraria Wiki. Usage: /wiki <search term> [-d|-f] [page]",
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

        private async void WikiCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Usage: /wiki <search term> [-d|-f] [page]");
                return;
            }

            ParseWikiParameters(args.Parameters, out string searchTerm, out string mode, out int pageNumber);

            string searchUrl = $"{WikiApiUrl}?action=opensearch&format=json&search={HttpUtility.UrlEncode(searchTerm)}&limit=1&namespace=0&profile=fuzzy";

            try
            {
                SendReply(args, $"Searching wiki for: {searchTerm}...", Color.Aqua);

                var searchResponse = await _httpClient.GetStringAsync(searchUrl);
                var searchResult = JArray.Parse(searchResponse);

                if (searchResult.Count < 2 || !searchResult[1].Any())
                {
                    SendReply(args, $"No wiki page found for '{searchTerm}'. Try a more specific name.", Color.OrangeRed);
                    return;
                }

                string pageTitle = searchResult[1][0].ToString();
                await ProcessWikiRequest(args, pageTitle, mode, pageNumber);
            }
            catch (Exception ex)
            {
                SendReply(args, $"An error occurred while accessing the wiki: {ex.Message}", Color.Red);
                TShock.Log.Error($"TerraGuide wiki error for '{searchTerm}': {ex}");
            }
        }

        private async Task ProcessWikiRequest(CommandArgs args, string pageTitle, string mode, int pageNumber)
        {
            string apiUrl;
            switch (mode)
            {
                case "d":
                    apiUrl = $"{WikiApiUrl}?action=parse&prop=wikitext|sections&page={HttpUtility.UrlEncode(pageTitle)}&format=json";
                    break;
                case "f":
                    apiUrl = $"{WikiApiUrl}?action=parse&prop=text&page={HttpUtility.UrlEncode(pageTitle)}&format=json";
                    break;
                default:
                    apiUrl = $"{WikiApiUrl}?action=query&prop=extracts&exintro=true&explaintext=true&titles={HttpUtility.UrlEncode(pageTitle)}&format=json";
                    break;
            }

            var responseJson = await _httpClient.GetStringAsync(apiUrl);
            var result = JObject.Parse(responseJson);

            string content = "";
            switch (mode)
            {
                case "d":
                    content = CleanWikiTextForDetails(result);
                    break;
                case "f":
                    content = CleanWikiTextForFull(result);
                    break;
                default:
                    content = CleanWikiTextForSummary(result);
                    break;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                SendReply(args, $"No readable content found for '{pageTitle}'.", Color.OrangeRed);
                return;
            }

            if (mode == "f")
            {
                var lines = SplitTextIntoLines(content, 70);
                PaginationTools.SendPage(args.Player, pageNumber, lines, new PaginationTools.Settings
                {
                    HeaderFormat = $"Full Wiki for {pageTitle} (Page {{0}}/{{1}}):",
                    FooterFormat = $"Type /wiki {args.Parameters.FirstOrDefault()} -f {{0}} for more.",
                    NothingToDisplayString = "No content to display on this page."
                });
            }
            else
            {
                SendReply(args, content, Color.White);
            }

            if (mode != "f")
            {
                SendReply(args, $"Read more: https://terraria.wiki.gg/wiki/{HttpUtility.UrlEncode(pageTitle)}", Color.Cyan);
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
                var stations = recipe.requiredTile.Where(t => t != -1).Select(t => TileID.Search.GetName(t)).ToList();
                if (stations.Any())
                {
                    SendReply(args, $"Station: {string.Join(" or ", stations.Select(TextHelper.ColorStation))}", Color.White);
                }

                SendReply(args, "Ingredients:", Color.White);
                foreach (var item in recipe.requiredItem)
                {
                    if (item.type > 0)
                    {
                        SendReply(args, $"  • {item.stack}x {TextHelper.ColorItem(item.Name)}", Color.White);
                    }
                }

                var conditions = new List<string>();
                if (recipe.needWater) conditions.Add("Water");
                if (recipe.needLava) conditions.Add("Lava");
                if (recipe.needHoney) conditions.Add("Honey");
                if (recipe.needSnowBiome) conditions.Add("Snow Biome");
                if (recipe.needGraveyardBiome) conditions.Add("Graveyard");

                if (conditions.Any())
                {
                    SendReply(args, $"Conditions: {string.Join(", ", conditions.Select(TextHelper.ColorRequirement))}", Color.White);
                }
            }
        }

        private void ParseWikiParameters(List<string> parameters, out string searchTerm, out string mode, out int page)
        {
            searchTerm = "";
            mode = "s";
            page = 1;

            var searchTerms = new List<string>();
            for (int i = 0; i < parameters.Count; i++)
            {
                string param = parameters[i].ToLower();
                if (param == "-d")
                {
                    mode = "d";
                }
                else if (param == "-f")
                {
                    mode = "f";
                }
                else if (int.TryParse(param, out int pageNum) && (parameters.Contains("-f") || parameters.Contains("-F")))
                {
                    page = Math.Max(1, pageNum);
                }
                else
                {
                    searchTerms.Add(parameters[i]);
                }
            }
            searchTerm = string.Join(" ", searchTerms);
        }

        private string CleanWikiTextForSummary(JObject result)
        {
            var pages = result?["query"]?["pages"];
            if (pages == null) return "";
            var firstPage = pages.Values<JProperty>().FirstOrDefault()?.Value;
            return firstPage?["extract"]?.ToString().Trim() ?? "";
        }

        private string CleanWikiTextForDetails(JObject result)
        {
            var parse = result?["parse"];
            if (parse == null) return "";

            string wikitext = parse["wikitext"]?["*"]?.ToString() ?? "";
            var sections = parse["sections"]?.ToObject<List<dynamic>>() ?? new List<dynamic>();

            var content = new System.Text.StringBuilder();

            string intro = Regex.Split(wikitext, @"(==[^=]+==)")[0];
            content.AppendLine(CleanWikitext(intro));

            var relevantSections = sections.Where(s =>
                s.line.ToString().Equals("Notes", StringComparison.OrdinalIgnoreCase) ||
                s.line.ToString().Equals("Tips", StringComparison.OrdinalIgnoreCase)
            ).Take(2);

            foreach (var section in relevantSections)
            {
                string sectionTitle = section.line;
                int sectionIndex = section.number;
                var matches = Regex.Matches(wikitext, @"==\s*" + Regex.Escape(sectionTitle) + @"\s*==([\s\S]*?)(?===|$)");

                if (matches.Any())
                {
                    string sectionContent = matches[0].Groups[1].Value;
                    content.AppendLine($"\n--- {sectionTitle} ---");
                    content.AppendLine(CleanWikitext(sectionContent));
                }
            }

            return content.ToString().Trim();
        }

        private string CleanWikiTextForFull(JObject result)
        {
            string html = result?["parse"]?["text"]?["*"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(html)) return "";

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var acceptableTags = new String[] { "p", "h1", "h2", "h3", "h4", "li", "span" };
            var nodes = doc.DocumentNode.SelectNodes(".//p | .//h1 | .//h2 | .//h3 | .//h4 | .//li | .//span");

            var sb = new System.Text.StringBuilder();
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    string text = HttpUtility.HtmlDecode(node.InnerText).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private string CleanWikitext(string text)
        {
            text = Regex.Replace(text, @"\{\{[^\{\}]*?\}\}", "");
            text = Regex.Replace(text, @"\[\[File:[^\]]*?\]\]", "");
            text = Regex.Replace(text, @"\[\[(?:[^|\]]*\|)?([^\]]+)\]\]", "$1");
            text = Regex.Replace(text, @"<[^>]*>", "");
            text = Regex.Replace(text, @"'{2,5}", "");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        private List<string> SplitTextIntoLines(string text, int maxLineLength)
        {
            var lines = new List<string>();
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var currentLine = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 > maxLineLength)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }
                currentLine.Append(word + " ");
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString().Trim());
            }

            return lines;
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
