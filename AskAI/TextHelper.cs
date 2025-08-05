using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using TShockAPI;

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

        public static string ConvertAiColorTags(string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                return string.Empty;
            }

            var result = new StringBuilder(inputText.Length + 50);
            var colorStack = new Stack<string>();
            var span = inputText.AsSpan();

            for (int i = 0; i < span.Length; i++)
            {
                char currentChar = span[i];
                
                if (currentChar == '<' && i + 8 < span.Length && span[i + 7] == ':')
                {
                    var colorSlice = span.Slice(i + 1, 6);
                    if (IsHexString(colorSlice))
                    {
                        string color = colorSlice.ToString();
                        colorStack.Push(color);
                        result.Append($"[c/{color}:");
                        i += 7;
                        continue;
                    }
                }
                
                if (currentChar == '>')
                {
                    if (colorStack.Count > 0)
                    {
                        colorStack.Pop();
                        result.Append(']');
                        continue;
                    }
                }
                
                if (currentChar == '\n')
                {
                    var reversedStack = new Stack<string>(colorStack.Count);
                    while (colorStack.Count > 0)
                    {
                        result.Append(']');
                        reversedStack.Push(colorStack.Pop());
                    }

                    result.Append('\n');
                    
                    while (reversedStack.Count > 0)
                    {
                        string color = reversedStack.Pop();
                        result.Append($"[c/{color}:");
                        colorStack.Push(color);
                    }
                    continue;
                }
                
                result.Append(currentChar);
            }
            
            while (colorStack.Count > 0)
            {
                result.Append(']');
                colorStack.Pop();
            }

            return result.ToString();
        }
        
        private static bool IsHexString(ReadOnlySpan<char> span)
        {
            if (span.Length != 6)
                return false;

            foreach (char c in span)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
