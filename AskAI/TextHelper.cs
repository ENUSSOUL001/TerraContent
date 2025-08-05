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

        private enum TagType { Colon, Hash }
        private struct TagInfo
        {
            public readonly string Color;
            public readonly TagType Type;
            public TagInfo(string color, TagType type)
            {
                Color = color;
                Type = type;
            }
        }

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

            var result = new StringBuilder(inputText.Length + 100);
            var tagStack = new Stack<TagInfo>();
            var span = inputText.AsSpan();

            for (int i = 0; i < span.Length; i++)
            {
                char currentChar = span[i];

                if (currentChar == '<')
                {
                    if (i + 1 < span.Length && span[i + 1] == '#')
                    {
                        if (i + 3 < span.Length && span[i + 2] == '/' && span[i + 3] == '>')
                        {
                            if (tagStack.Count > 0 && tagStack.Peek().Type == TagType.Hash)
                            {
                                tagStack.Pop();
                                result.Append(']');
                            }
                            i += 3;
                            continue;
                        }
                        
                        if (i + 8 < span.Length && span[i + 8] == '>')
                        {
                            var colorSlice = span.Slice(i + 2, 6);
                            if (IsHexString(colorSlice))
                            {
                                string color = colorSlice.ToString();
                                tagStack.Push(new TagInfo(color, TagType.Hash));
                                result.Append($"[c/{color}:");
                                i += 8;
                                continue;
                            }
                        }
                    }
                    
                    if (i + 8 < span.Length && span[i + 7] == ':')
                    {
                        var colorSlice = span.Slice(i + 1, 6);
                        if (IsHexString(colorSlice))
                        {
                            string color = colorSlice.ToString();
                            tagStack.Push(new TagInfo(color, TagType.Colon));
                            result.Append($"[c/{color}:");
                            i += 7;
                            continue;
                        }
                    }
                }

                if (currentChar == '>')
                {
                    if (tagStack.Count > 0 && tagStack.Peek().Type == TagType.Colon)
                    {
                        tagStack.Pop();
                        result.Append(']');
                        continue;
                    }
                }
                
                if (currentChar == '\n')
                {
                    var reversedStack = new Stack<TagInfo>(tagStack.Count);
                    while (tagStack.Count > 0)
                    {
                        result.Append(']');
                        reversedStack.Push(tagStack.Pop());
                    }

                    result.Append('\n');
                    
                    while (reversedStack.Count > 0)
                    {
                        var tagInfo = reversedStack.Pop();
                        result.Append($"[c/{tagInfo.Color}:");
                        tagStack.Push(tagInfo);
                    }
                    continue;
                }
                
                result.Append(currentChar);
            }
            
            while (tagStack.Count > 0)
            {
                result.Append(']');
                tagStack.Pop();
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
