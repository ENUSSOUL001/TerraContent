using System.Collections.Generic;
using System.Linq;
using Terraria;
using TShockAPI;

namespace AskAI
{
    public static class PluginTools
    {
        public static string GetRecipe(string itemName)
        {
            var matchingItems = TShock.Utils.GetItemByName(itemName);
            if (matchingItems.Count == 0)
            {
                return $"No items found matching '{itemName}'.";
            }
            if (matchingItems.Count > 1)
            {
                return $"Multiple items found for '{itemName}'. Please be more specific.";
            }

            var item = matchingItems.First();
            var recipes = Main.recipe.Where(r => r != null && r.createItem.type == item.type).ToList();

            if (recipes.Count == 0)
            {
                return $"No crafting recipe found for {item.Name}.";
            }

            var result = new List<string>();
            result.Add($"Crafting recipe for {item.Name}:");

            foreach (var recipe in recipes)
            {
                if (recipe.requiredTile.Length > 0 && recipe.requiredTile[0] != -1)
                {
                    var stations = recipe.requiredTile.Where(t => t >= 0).Select(Terraria.ID.TileID.Search.GetName);
                    result.Add($"Station: {string.Join(" or ", stations)}");
                }

                result.Add("Ingredients:");
                foreach (var ingredient in recipe.requiredItem)
                {
                    if (ingredient.type > 0)
                    {
                        result.Add($"- {ingredient.stack}x {ingredient.Name}");
                    }
                }
            }
            return string.Join("\n", result);
        }
    }
}
