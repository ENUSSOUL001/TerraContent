using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;

namespace AskAI
{
    public static class TextHelper
    {
        public static readonly Color AINameColor = new Color(173, 216, 230);
        public static readonly Color InfoColor = Color.Yellow;
        public static readonly Color ErrorColor = new Color(255, 100, 100);
        public static readonly Color UsageColor = new Color(0, 255, 150);
        public static readonly Color UsageParamColor = new Color(150, 255, 200);
        public static readonly Color GreetColor = new Color(0, 255, 150);
        public static readonly Color GreetHeaderColor = new Color(150, 255, 200);

        public static string Colorize(string text, Color color)
        {
            return $"[c/{color.R:X2}{color.G:X2}{color.B:X2}:{text}]";
        }

        private static readonly Regex AICodeRegex = new Regex(@"<(?<color>[0-9A-Fa-f]{6}):(?<text>.*?)>", RegexOptions.Singleline);
        public static string ConvertAiColorTags(string text)
        {
            return AICodeRegex.Replace(text, match =>
            {
                var color = match.Groups["color"].Value;
                var content = match.Groups["text"].Value;
                var nestedContent = ConvertAiColorTags(content);
                return $"[c/{color}:{nestedContent}]";
            });
        }
    }
}
