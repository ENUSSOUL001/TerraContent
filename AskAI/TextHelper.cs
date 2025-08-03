using Microsoft.Xna.Framework;
using System.Linq;
using System.Text.RegularExpressions;

namespace AskAI
{
    public static class TextHelper
    {
        public static readonly Color AINameColor = new Color(173, 216, 230);
        public static readonly Color InfoColor = Color.Yellow;
        public static readonly Color ErrorColor = new Color(255, 100, 100);
        public static readonly Color UsageColor = new Color(230, 230, 230);
        public static readonly Color UsageParamColor = new Color(180, 180, 180);

        private static readonly Regex ColorTagRegex = new Regex(@"\[c\/(?<color>[0-9A-Fa-f]{6}):(?<text>.*?)\]", RegexOptions.Singleline);

        public static string Colorize(string text, Color color)
        {
            return $"[c/{color.R:X2}{color.G:X2}{color.B:X2}:{text}]";
        }

        public static string FixMalformedColorTags(string text)
        {
            return ColorTagRegex.Replace(text, match =>
            {
                var color = match.Groups["color"].Value;
                var content = match.Groups["text"].Value;

                if (!content.Contains('\n'))
                {
                    return match.Value;
                }

                var parts = content.Split('\n');
                return string.Join("\n", parts.Select(p => $"[c/{color}:{p}]"));
            });
        }
    }
}
