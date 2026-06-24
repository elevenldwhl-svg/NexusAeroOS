using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexusAeroOS.AgentHarness.Infrastructure;

/// <summary>
/// 工业级大模型输出净化器（Harness 系统的第一道防弹衣）
/// </summary>
public static class RobustJsonParser
{
    // 💥 铁律配置1：让C#的反序列化器变成“极度宽容的慈母”
    private static readonly JsonSerializerOptions StandardOptions = new()
    {
        PropertyNameCaseInsensitive = true, // 极其重要：容忍大模型把 DroneId 写成 droneId 或 drone_id
        AllowTrailingCommas = true,         // 极其重要：容忍大模型在 JSON 最后一个字段末尾手贱多写一个逗号
        ReadCommentHandling = JsonCommentHandling.Skip // 极其重要：容忍大模型在 JSON 内部写 //注释
    };

    // 💥 铁律配置2：编译期预热的高性能正则，专门扒掉 ```json { ... } ``` 外壳
    private static readonly Regex JsonCodeBlockRegex = new(
        @"(?:```(?:json)?\s*)([\s\S]*?)(?:
```)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static T? IncinerateAndParse<T>(string rawAiOutput) where T : class
    {
        if (string.IsNullOrWhiteSpace(rawAiOutput)) return null;

        string cleanedJson = rawAiOutput.Trim();

        // 步骤 1：尝试用正则强行剥离 Markdown 外壳
        var match = JsonCodeBlockRegex.Match(rawAiOutput);
        if (match.Success)
        {
            cleanedJson = match.Groups[1].Value.Trim();
        }
        else
        {
            // 步骤 2：如果大模型没带 ```，但前后夹杂了人类废话，我们暴力锁定第一个 { 和最后一个 }
            int firstBrace = rawAiOutput.IndexOf('{');
            int lastBrace = rawAiOutput.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleanedJson = rawAiOutput.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
        }

        try
        {
            // 步骤 3：送入慈母反序列化器
            return JsonSerializer.Deserialize<T>(cleanedJson, StandardOptions);
        }
        catch (JsonException ex)
        {
            // 生产环境的 Harness 在这里会触发【AI自纠错闭环】（明天讲），今晚我们先优雅拦截
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Harness装甲拦截事故]: 大模型产生了严重的JSON结构幻觉！\n报错点: {ex.Message}\n原文截取: {rawAiOutput.Substring(0, Math.Min(rawAiOutput.Length, 80))}...");
            Console.ResetColor();
            return null;
        }
    }
}