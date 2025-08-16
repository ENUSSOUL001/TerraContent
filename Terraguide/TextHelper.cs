using Microsoft.Xna.Framework;
using TShockAPI;

namespace TerraGuide
{
    public static class TextHelper
    {
        public static string ColorHeader(string text) => $"[c/FFD700:{text}]";

        public static string ColorRecipeName(string text) => $"[c/87CEEB:{text}]";

        public static string ColorStation(string text) => $"[c/98FB98:{text}]";

        public static string ColorItem(string text) => $"[c/DDA0DD:{text}]";

        public static string ColorRequirement(string text) => $"[c/FF6B6B:{text}]";

        public static string MakeListItem(string text) => $"  • {text}";
    }
}
