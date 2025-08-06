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
        private enum TokenType { PlainText, OpenTag, CloseTag, NewLine }

        private struct Token
        {
            public readonly TokenType Type;
            public readonly string Content;
            public readonly TagType TagStyle;

            public Token(TokenType type, string content = "", TagType tagStyle = TagType.Colon)
            {
                Type = type;
                Content = content;
                TagStyle = tagStyle;
            }
        }

        public static string Colorize(string text, Color color)
        {
            return $"[c/{color.R:X2}{color.G:X2}{color.B:X2}:{text}]";
        }

        private static List<Token> Tokenize(ReadOnlySpan<char> input)
        {
            var tokens = new List<Token>();
            var textBuffer = new StringBuilder();
            int i = 0;

            while (i < input.Length)
            {
                if (input[i] == '\n')
                {
                    if (textBuffer.Length > 0)
                    {
                        tokens.Add(new Token(TokenType.PlainText, textBuffer.ToString()));
                        textBuffer.Clear();
                    }
                    tokens.Add(new Token(TokenType.NewLine));
                    i++;
                    continue;
                }

                if (input[i] == '<')
                {
                    if (i + 8 < input.Length && input[i + 7] == ':' && IsHexString(input.Slice(i + 1, 6)))
                    {
                        if (textBuffer.Length > 0)
                        {
                            tokens.Add(new Token(TokenType.PlainText, textBuffer.ToString()));
                            textBuffer.Clear();
                        }
                        tokens.Add(new Token(TokenType.OpenTag, input.Slice(i + 1, 6).ToString(), TagType.Colon));
                        i += 8;
                        continue;
                    }
                    
                    if (i + 8 < input.Length && input[i + 1] == '#' && input[i + 8] == '>' && IsHexString(input.Slice(i + 2, 6)))
                    {
                        if (textBuffer.Length > 0)
                        {
                            tokens.Add(new Token(TokenType.PlainText, textBuffer.ToString()));
                            textBuffer.Clear();
                        }
                        tokens.Add(new Token(TokenType.OpenTag, input.Slice(i + 2, 6).ToString(), TagType.Hash));
                        i += 9;
                        continue;
                    }

                    if (i + 3 < input.Length && input.Slice(i, 4).SequenceEqual("</#>".AsSpan()))
                    {
                        if (textBuffer.Length > 0)
                        {
                            tokens.Add(new Token(TokenType.PlainText, textBuffer.ToString()));
                            textBuffer.Clear();
                        }
                        tokens.Add(new Token(TokenType.CloseTag, tagStyle: TagType.Hash));
                        i += 4;
                        continue;
                    }
                }

                if (input[i] == '>')
                {
                    if (textBuffer.Length > 0)
                    {
                        tokens.Add(new Token(TokenType.PlainText, textBuffer.ToString()));
                        textBuffer.Clear();
                    }
                    tokens.Add(new Token(TokenType.CloseTag, tagStyle: TagType.Colon));
                    i++;
                    continue;
                }

                textBuffer.Append(input[i]);
                i++;
            }

            if (textBuffer.Length > 0)
            {
                tokens.Add(new Token(TokenType.PlainText, textBuffer.ToString()));
            }

            return tokens;
        }

        public static string ConvertAiColorTags(string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                return string.Empty;
            }

            var tokens = Tokenize(inputText.AsSpan());
            var result = new StringBuilder(inputText.Length + 100);
            var tagStack = new Stack<TagType>();

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.PlainText:
                        result.Append(token.Content);
                        break;

                    case TokenType.OpenTag:
                        result.Append($"[c/{token.Content}:");
                        tagStack.Push(token.TagStyle);
                        break;

                    case TokenType.CloseTag:
                        if (tagStack.Count > 0 && tagStack.Peek() == token.TagStyle)
                        {
                            tagStack.Pop();
                            result.Append(']');
                        }
                        break;

                    case TokenType.NewLine:
                        var reversedStack = new Stack<TagType>();
                        while (tagStack.Count > 0)
                        {
                            result.Append(']');
                            reversedStack.Push(tagStack.Pop());
                        }
                        result.Append('\n');
                        while (reversedStack.Count > 0)
                        {
                            var tagType = reversedStack.Pop();
                            var color = result.ToString().Substring(result.ToString().LastIndexOf($"[c/") + 3, 6);
                            result.Append($"[c/{color}:");
                            tagStack.Push(tagType);
                        }
                        break;
                }
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
