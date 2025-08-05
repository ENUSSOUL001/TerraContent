using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Text;
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

        public static string ConvertAiColorTags(string text)
        {
            var result = new StringBuilder();
            var colorStack = new Stack<string>();
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] == '<' && i + 8 < text.Length && text[i + 7] == ':')
                {
                    string color = text.Substring(i + 1, 6);
                    if (Regex.IsMatch(color, @"^[0-9A-Fa-f]{6}$"))
                    {
                        colorStack.Push(color);
                        result.Append($"[c/{color}:");
                        i += 8;
                        continue;
                    }
                }

                if (text[i] == '>')
                {
                    if (colorStack.Count > 0)
                    {
                        colorStack.Pop();
                        result.Append("]");
                        if (colorStack.Count > 0)
                        {
                            result.Append($"[c/{colorStack.Peek()}:");
                        }
                        i++;
                        continue;
                    }
                }

                if (text[i] == '\n')
                {
                    foreach (var color in colorStack)
                    {
                        result.Append("]");
                    }
                    result.Append('\n');
                    foreach (var color in new Stack<string>(colorStack))
                    {
                        result.Append($"[c/{color}:");
                    }
                    i++;
                    continue;
                }

                result.Append(text[i]);
                i++;
            }

            while (colorStack.Count > 0)
            {
                result.Append("]");
                colorStack.Pop();
            }

            return result.ToString();
        }
    }
}
