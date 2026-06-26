using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NexusAeroOS.AgentHarness.Agents;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.AgentHarness.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusAeroOS.Host.Services;

public class OrderPumpEngine : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FleetRegistryService _registry;
    private readonly IAgentEventBroadcaster _broadcaster;

    // 💥 深圳全域 12 大核心空间坐标矩阵（排列组合支撑上百种任务路网）
    private readonly List<(string Name, string Coords)> _hubs = new()
    {
        ("南山·腾讯滨海大厦", "113.9355, 22.5300"), ("南山·深圳大学总医院", "113.9210, 22.5520"),
        ("福田·市民中心枢纽", "114.0550, 22.5430"), ("福田·平安金融中心停机坪", "114.0540, 22.5330"),
        ("宝安·壹方城前海基地", "113.8880, 22.5530"), ("宝安·国际机场航空保税区", "113.8150, 22.6320"),
        ("龙华·深圳北站集散场", "114.0290, 22.6100"), ("罗湖·地王大厦高空驿站", "114.1180, 22.5400"),
        ("南山·世界之窗科创站", "113.9720, 22.5360"), ("深圳湾·人才公园海韵枢纽", "113.9480, 22.5130"),
        ("宝安·西乡大铲湾码头", "113.8620, 22.5780"), ("龙华·富士康智能制造舱", "114.0480, 22.6520")
    };

    private readonly string[] _payloadTypes = { "加急冷链血浆", "芯片晶圆测试件", "跨区加急航空件", "特种移植载荷", "极速高端生鲜", "军工保密固件" };

    public OrderPumpEngine(IServiceProvider serviceProvider, FleetRegistryService registry, IAgentEventBroadcaster broadcaster)
    {
        _serviceProvider = serviceProvider; _registry = registry; _broadcaster = broadcaster;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("⚙️ [混沌任务中台] 100级随机订单泵已挂载，进入极速并发模式 (20s/Order)...");
        await Task.Delay(4000, stoppingToken);
        int orderCounter = 2001;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var random = new Random();
                var fromHub = _hubs[random.Next(_hubs.Count)];
                var toHub = _hubs.Where(h => h.Name != fromHub.Name).ToList()[random.Next(_hubs.Count - 1)];
                string orderId = $"ORD-{orderCounter++}";
                string payloadType = _payloadTypes[random.Next(_payloadTypes.Length)];
                double weight = Math.Round(random.NextDouble() * 22 + 1.5, 1);

                await _broadcaster.BroadcastThoughtAsync("System", "Tower", "OrderPumped",
                    $"📢 [极速任务流] 空降转运单 [{orderId}]: 【{payloadType} / {weight}kg】，路线：{fromHub.Name} ➔ {toHub.Name}。大脑介入算路...");

                using var scope = _serviceProvider.CreateScope();
                var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
                var missionRepository = scope.ServiceProvider.GetRequiredService<MissionRepository>();
                var airspaceService = scope.ServiceProvider.GetRequiredService<AirspaceService>();

                kernel.Plugins.AddFromObject(new FleetDispatchPlugin(_registry, _broadcaster, scope.ServiceProvider.GetRequiredService<HitlApprovalService>(), scope.ServiceProvider.GetRequiredService<DroneOnboardAgent>(), kernel, airspaceService, missionRepository), "FleetOps");
                kernel.Plugins.AddFromObject(new AirspacePlugin(airspaceService), "AirspaceOps");

                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                var history = new ChatHistory();

                // ====================================================================
                // 💥 修正：System Message 只充当“纯粹的冷酷法律和工具说明书”
                // ====================================================================
                history.AddSystemMessage("""
                你是星河飞控中心的【中央调度指挥长】。
                你必须全权接管用户在 UserMessage 中递交的最新自动化运单，并利用工具链执行无人干预的派单。
                
                【全自主群智派单决策链】：
                1. 盘点资产：立刻调用 `FleetOps.CheckFleetStatus` 盘点全网谁处于 Idle 状态且电量 > 60%。
                2. 路径获取：根据运单提供的[起点坐标]和[一阶段取货点坐标]，调用 `AirspaceOps.GetAutomatedSafeRoute` 拿到底层计算好的避障虚线天路字符串。
                3. 静默直飞（核心铁律）：立刻调用 `FleetOps.SilentAutoDispatch` 串联点火。
                   你必须严格从用户消息里提取参数进行映射，严禁向人类询问或索要参数：
                   - targetDroneId: 选中的可运行飞机ID
                   - waypointsRoute: 刚刚算好的一阶段取货折线串
                   - deliveryDest: 运单中提供的【最终送货终点坐标】（绝对不准漏掉！）
                   - orderId: 运单中提供的业务订单号
                   - payloadType: 运单中提供的载荷类型
                   - weightKg: 运单中提供的货物重量（纯数字）
                   - aiReasoningSummary: 一句话简述你为什么挑它（如"电池99%且距离近"）
                """);

                // ====================================================================
                // 💥 核心自愈：把所有血肉数据，用铁钉直接焊死在 User Message 里面！
                // ====================================================================
                history.AddUserMessage($"""
                【紧急航空运单挂载总线汇报】
                长官，混沌宇宙刚刚生产了以下全参数实体运单，请立刻提取参数并调用工具链进行静默派发，不准回传任何疑问！
                
                - 业务订单号: {orderId}
                - 载荷货物类型: {payloadType}
                - 货物真实重量: {weight}
                - 一阶段发货取货点坐标(起点): {fromHub.Coords} ({fromHub.Name})
                - 二阶段最终送货终点坐标(终点): {toHub.Coords} ({toHub.Name})
                
                指令：请立刻调动资产，起飞！
                """);

                var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
                kernel.FunctionInvocationFilters.Clear();
                kernel.FunctionInvocationFilters.Add(new AgentObservabilityFilter(_broadcaster, "System"));

                var res = await chatService.GetChatMessageContentAsync(history, settings, kernel, stoppingToken);

                string markdownSummary = res.Content ?? "全自动指派成功";

                // 将 DeepSeek 呕心沥血生成的华丽摘要，直接封印进对应的任务流水单记录中！
                await missionRepository.UpdateFinalDecisionReportAsync(orderId, markdownSummary);

                await _broadcaster.BroadcastThoughtAsync("System", "System", "FinalDecision", $"### ⚡ {orderId} 自主处置完结\n{res.Content}");
            }
            catch (Exception ex)
            {
                // 💥 强行用血红色打印完整致死堆栈，绝不许它静默死亡！
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ [核弹级报错] 订单泵推演暴毙: {ex.ToString()}");
                Console.ResetColor();
            }

            await Task.Delay(20000, stoppingToken); // 💥 20秒极速喷发！
        }
    }
}