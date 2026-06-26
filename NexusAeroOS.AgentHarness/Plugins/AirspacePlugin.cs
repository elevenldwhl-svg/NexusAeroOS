using System.ComponentModel;
using Microsoft.SemanticKernel;
using NexusAeroOS.AgentHarness.Infrastructure;

namespace NexusAeroOS.AgentHarness.Plugins;

public class AirspacePlugin
{
    private readonly AirspaceService _airspace;
    public AirspacePlugin(AirspaceService airspace) { _airspace = airspace; }

    [KernelFunction("VerifyRouteSafety")]
    [Description("【几何核验工具】输入拟飞行的起点和预期折线航点，计算该路线是否会撞上现实中的禁飞区。返回 SAFE 或 违规报告。")]
    public string VerifyRouteSafety(
        [Description("当前无人机所在坐标，如 '113.9300, 22.5480'")] string currentCoords,
        [Description("拟制定的分号隔离航点，如 '113.9350,22.5500; 113.9400,22.5500'")] string proposedWaypoints)
    {
        var (isSafe, report) = _airspace.TestRouteSafety(currentCoords, proposedWaypoints);
        return isSafe ? "【核验结果：SAFE】该路线未相交任何管制空域，准予下发。" : report!;
    }

    [KernelFunction("GetAutomatedSafeRoute")]
    [Description("【几何核算总枢】输入起点坐标和终点坐标，系统底层GIS引擎将自动避开深圳所有禁飞区，直接回传一串100%绝对安全的航点字符串！")]
    public string GetAutomatedSafeRoute(string startCoords, string destCoords)
    {
        // 直接将起点终点，转交给 AirspaceService 的【五向穷举自愈求生算法】！
        return _airspace.GetGuaranteedSafeCorridor(startCoords, destCoords);
    }
}