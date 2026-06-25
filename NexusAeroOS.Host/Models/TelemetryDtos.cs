namespace NexusAeroOS.Host.Models;

public record TelemetryReportRequest(string PilotVoice);

public record TelemetryReportResponse(bool IsIntervened, string Message, object? ParsedData);