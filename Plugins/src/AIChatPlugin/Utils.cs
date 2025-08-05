using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using TShockAPI;
using static AIChatPlugin.Configuration;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace AIChatPlugin;
internal class Utils
{
    #region 问题审核
    public static readonly Dictionary<int, List<string>> playerContexts = new();
    public static DateTime lastCmdTime = DateTime.MinValue;
    public static bool isProcessing = false;
    public static void ChatWithAI(TSPlayer player, string question)
    {
        var playerIndex = player.Index;
        if (isProcessing)
        {
            player.SendErrorMessage(AIChatPlugin.Instance.GetString("[i:1344]有其他玩家在询问问题，请排队[i:1344]"));
            return;
        }
        if (string.IsNullOrWhiteSpace(question))
        {
            player.SendErrorMessage(AIChatPlugin.Instance.GetString("[i:1344]您的问题不能为空，请输入您想询问的内容！[i:1344]"));
            return;
        }
        lastCmdTime = DateTime.Now;
        player.SendSuccessMessage(AIChatPlugin.Instance.GetString("[i:1344]正在处理您的请求，请稍候...[i:1344]"));
        isProcessing = true;
        Task.Run(async () =>
        {
            try
            {
                await ProcessAIChat(player, question);
            }
            catch (Exception ex)
            {
                string format = AIChatPlugin.Instance.GetString("[AIChatPlugin] 处理`{0}`的请求时发生错误！详细信息：{1}");
                string message = string.Format(format, player.Name, ex.Message);
                TShock.Log.ConsoleError(message);

                if (player.RealPlayer)
                {
                    player.SendErrorMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] 处理请求时发生错误！详细信息请查看日志"));
                }
            }
            finally
            {
                isProcessing = false;
            }
        });
    }
    #endregion
    #region 请求处理
    public static async Task ProcessAIChat(TSPlayer player, string question)
    {
        try
        {
            var context = GetContext(player.Index);
            var formattedContext = context.Count > 0 ? string.Join("\n", context) + "\n" : "";
            using var client = new System.Net.Http.HttpClient() { Timeout = TimeSpan.FromSeconds(Config.AITimeoutPeriod) };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer 742701d3fea4bed898578856989cb03c.5mKVzv5shSIqkkS7");
            var tools = new List<object>()
            {
            new
            {
                type = "web_search",
                web_search = new
                {
                    enable = true,
                    search_result = true,
                    search_query = question
                }
            }
            };
            var requestBody = new
            {
                model = "glm-4-flash",
                messages = new[]
                {
                new
                {
                    role = "system",
                    content = Config.AISettings + "\n" + string.Format(AIChatPlugin.Instance.GetString("当前时间是 {0:yyyy-MM-dd HH:mm}"), DateTime.Now)
                }
                }
                .Concat(GetContext(player.Index)
                        .Select(msg =>
                        {
                            var parts = msg.Split(new[] { ':' }, 2);
                            return new
                            {
                                role = parts.Length > 1 ? parts[0].Trim().ToLower() : "user",
                                content = parts.Length > 1 ? parts[1].Trim() : msg
                            };
                        }))
                .Concat(new[]
                {
                    new
                    {
                        role = "user",
                        content = question
                    }
                })
                .ToArray(),
                tools
            };
            var response = await client.PostAsync("https://open.bigmodel.cn/api/paas/v4/chat/completions",
                new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<AIResponse>(jsonResponse);
                var taskId = result?.Id ?? "Not provided";
                TShock.Log.Debug($"[AIChatPlugin] Dialogue ID：{taskId}");
                if (result != null && result.Choices != null && result.Choices.Length > 0)
                {
                    var firstChoice = result.Choices[0];
                    var responseMessage = firstChoice.Message.Content;
                    if (responseMessage.Length > Config.AIAnswerWordsLimit)
                    {
                        responseMessage = TruncateMessage(responseMessage);
                    }
                    var formattedQuestion = FormatMessage(question);
                    var formattedResponse = FormatMessage(responseMessage);
                    StringBuilder broadcastMessageBuilder = new();
                    broadcastMessageBuilder.AppendFormat(AIChatPlugin.Instance.GetString("[i:267][c/FFD700:{0}]\n"), player.Name);
                    broadcastMessageBuilder.AppendFormat(AIChatPlugin.Instance.GetString("[i:149][c/00FF00:提问: {0}]\n"), formattedQuestion);
                    broadcastMessageBuilder.AppendLine(AIChatPlugin.Instance.GetString("[c/A9A9A9:============================]"));
                    broadcastMessageBuilder.AppendFormat(AIChatPlugin.Instance.GetString("[i:4805][c/FF00FF:{0}]\n"), Config.AIName);
                    broadcastMessageBuilder.AppendFormat(AIChatPlugin.Instance.GetString("[i:149][c/FF4500:回答:] {0}\n"), formattedResponse);
                    broadcastMessageBuilder.AppendLine(AIChatPlugin.Instance.GetString("[c/A9A9A9:============================]"));
                    var broadcastMessage = broadcastMessageBuilder.ToString();
                    TSPlayer.All.SendInfoMessage(broadcastMessage); TShock.Log.ConsoleInfo(broadcastMessage);
                    AddToContext(player.Index, question, true); AddToContext(player.Index, responseMessage, false);
                }
                else
                {
                    player.SendErrorMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] 很抱歉，这次未获得有效的AI响应"));
                }
            }
            else
            {
                string format = AIChatPlugin.Instance.GetString("[AIChatPlugin] AI未能及时响应，状态码：{0}");
                string message = string.Format(format, response.StatusCode);
                TShock.Log.ConsoleError(message);
                if (player.RealPlayer)
                {
                    player.SendErrorMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] AI未能及时响应！详细信息请查看日志"));
                }
            }
        }
        catch (TaskCanceledException)
        {
            player.SendErrorMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] 请求超时！"));
        }
        catch (Exception ex)
        {
            string format = AIChatPlugin.Instance.GetString("[AIChatPlugin] 出现错误！详细信息：{0}");
            string message = string.Format(format, ex.Message);
            TShock.Log.ConsoleError(message);
            if (player.RealPlayer)
            {
                player.SendErrorMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] 出现错误！详细信息请查看日志"));
            }
        }
    }
    public class AIResponse
    {
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
        public string? Id { get; set; }
    }
    public class Choice
    {
        public Message Message { get; set; } = new Message();
    }
    public class Message
    {
        public string Content { get; set; } = string.Empty;
    }
    #endregion
    #region 历史限制
    public static void AddToContext(int playerId, string message, bool isUserMessage)
    {
        if (!playerContexts.ContainsKey(playerId))
        {
            playerContexts[playerId] = new List<string>();
        }
        var taggedMessage = $"{(isUserMessage ? "user" : "assistant")}:{message}";
        if (playerContexts[playerId].Count >= Config.AIContextuallimitations)
        {
            playerContexts[playerId].RemoveAt(0);
        }
        playerContexts[playerId].Add(taggedMessage);
    }
    public static List<string> GetContext(int playerId)
    {
        return playerContexts.ContainsKey(playerId) ? playerContexts[playerId] : new List<string>();
    }
    public static void AIclear(CommandArgs args)
    {
        if (playerContexts.Count == 0)
        {
            args.Player.SendInfoMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] 当前没有任何人的上下文记录"));
        }
        else
        {
            playerContexts.Clear();
            args.Player.SendSuccessMessage(AIChatPlugin.Instance.GetString("[AIChatPlugin] 所有人的上下文已清除"));
        }
    }
    #endregion
    #region 回答优限
    public static string TruncateMessage(string message)
    {
        if (message.Length <= Config.AIAnswerWordsLimit)
        {
            return message;
        }
        var enumerator = StringInfo.GetTextElementEnumerator(message);
        StringBuilder truncated = new();
        var count = 0;
        while (enumerator.MoveNext())
        {
            var textElement = enumerator.GetTextElement();
            if (truncated.Length + textElement.Length > Config.AIAnswerWordsLimit)
            {
                break;
            }
            truncated.Append(textElement);
            count++;
        }
        if (count == 0 || truncated.Length >= Config.AIAnswerWordsLimit)
        {
            string format = AIChatPlugin.Instance.GetString("\n\n[i:1344]超出字数限制 {0} 已截断！[i:1344]");
            truncated.Append(string.Format(format, Config.AIAnswerWordsLimit));
        }
        return truncated.ToString();
    }
    public static string FormatMessage(string message)
    {
        StringBuilder formattedMessage = new();
        var enumerator = StringInfo.GetTextElementEnumerator(message);
        var currentLength = 0;
        while (enumerator.MoveNext())
        {
            var textElement = enumerator.GetTextElement();
            if (currentLength + textElement.Length > Config.AIAnswerWithLinebreaks)
            {
                if (formattedMessage.Length > 0)
                {
                    formattedMessage.AppendLine();
                }
                currentLength = 0;
            }
            formattedMessage.Append(textElement);
            currentLength += textElement.Length;
        }
        return formattedMessage.ToString();
    }
    #endregion
    }
