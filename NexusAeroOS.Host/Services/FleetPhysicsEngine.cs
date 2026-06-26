using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NexusAeroOS.AgentHarness.Infrastructure;

namespace NexusAeroOS.Host.Services; // 💥 找回遗失的灵魂户口！

public class FleetPhysicsEngine : BackgroundService
{
    private readonly FleetRegistryService _registry;
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly AirspaceService _airspace;
    private readonly MissionRepository _missionRepo;
    private readonly DroneRepository _droneRepo;

    public FleetPhysicsEngine(FleetRegistryService registry, IAgentEventBroadcaster broadcaster, AirspaceService airspace, MissionRepository missionRepo, DroneRepository droneRepo)
    {
        _registry = registry; _broadcaster = broadcaster; _airspace = airspace; _missionRepo = missionRepo;
        _droneRepo = droneRepo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool moved = await _registry.TickAllDronesMotionAsync(_airspace, _missionRepo, _droneRepo);
            if (moved) await _broadcaster.BroadcastFleetStatusAsync(_registry.GetFleetSnapshot());
            await Task.Delay(1000, stoppingToken);
        }
    }
}