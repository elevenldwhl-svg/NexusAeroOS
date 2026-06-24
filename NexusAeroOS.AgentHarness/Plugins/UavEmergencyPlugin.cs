using System.ComponentModel;
using Microsoft.SemanticKernel;
using NexusAeroOS.Domain.Entities; // 引入纯洁的 Domain 契约实体

namespace NexusAeroOS.AgentHarness.Plugins;

public class UavEmergencyPlugin
{
    // 💥 划重点：大白话 Description 是写给 DeepSeek 看的“核按钮说明书”
    [KernelFunction, Description("当低空飞行器发生极度超温(>65℃)或动力行将报废时，强行调用此物理干涉接口，向地面备用仓申请锁死一块新电池，并生成紧急派工单。")]
    public async Task<EmergencyDispatchOrder> AllocateBackupBatteryAsync(
        [Description("涉事飞行器的机载识别码，例如 '南山4号接驳机'")] string droneId,
        [Description("大模型作为军师，结合飞行章程给出的、必须执行本次物理干涉的判决理由说明书")] string actionReason)
    {
        // 模拟服务器机房里，物理继电器发出“咔哒”一声强行吸合的巨响：
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n⚡️ [C#脑干中枢 接收到机载视网膜干涉请求] -> 正在向南山1号仓WMS系统下达物理锁仓指令...");
        Console.WriteLine($"   涉事载体: {droneId}");
        Console.WriteLine($"   脑干审核理由: \"{actionReason}\"");

        await Task.Delay(1200); // 模拟WMS系统寻址耗时

        Console.WriteLine($"   🔒 物理继电器闭合成功！南山1号仓 备用电池 SKU-BAT-999 已强行锁死，接驳航线已就绪！\n");
        Console.ResetColor();

        // 💥 划重点：C# 本地执行完脏活后，把强类型的 Domain 实体直接回传给 AI！
        return new EmergencyDispatchOrder(
            SourceDroneId: droneId,
            TargetWarehouseId: "深圳-南山1号特种接驳仓",
            AllocatedBackupSku: "SKU-BAT-999 (低空高倍率石墨烯固态电池)",
            ActionReason: actionReason,
            DispatchedAt: DateTime.Now
        );
    }
}