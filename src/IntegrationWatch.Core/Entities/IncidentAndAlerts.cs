using IntegrationWatch.Core.Enums;

namespace IntegrationWatch.Core.Entities;

public class Incident
{
    public Guid Id { get; private set; }
    public Guid EndpointId { get; private set; }
    public string EndpointName { get; private set; } = string.Empty;
    public IncidentSeverity Severity { get; private set; } = IncidentSeverity.High;
    public IncidentStatus Status { get; private set; } = IncidentStatus.Triggered;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime TriggeredAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? ResolutionNotes { get; private set; }

    private Incident() { }

    public static Incident Trigger(Guid endpointId, string endpointName, string reason, IncidentSeverity severity = IncidentSeverity.High)
    {
        return new Incident
        {
            Id = Guid.NewGuid(),
            EndpointId = endpointId,
            EndpointName = endpointName,
            Severity = severity,
            Status = IncidentStatus.Triggered,
            Title = $"Downtime Detected: {endpointName}",
            Description = reason,
            TriggeredAtUtc = DateTime.UtcNow
        };
    }

    public void Acknowledge()
    {
        if (Status == IncidentStatus.Triggered)
        {
            Status = IncidentStatus.Investigating;
            AcknowledgedAtUtc = DateTime.UtcNow;
        }
    }

    public void Resolve(string resolutionNotes)
    {
        Status = IncidentStatus.Resolved;
        ResolvedAtUtc = DateTime.UtcNow;
        ResolutionNotes = resolutionNotes;
    }
}

public class AlertSubscription
{
    public Guid Id { get; private set; }
    public Guid? EndpointId { get; private set; } // Null for global alerts
    public string ChannelType { get; private set; } = "Webhook"; // Webhook, Slack, Email
    public string DestinationUrl { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private AlertSubscription() { }

    public static AlertSubscription Create(string channelType, string destinationUrl, Guid? endpointId = null)
    {
        return new AlertSubscription
        {
            Id = Guid.NewGuid(),
            EndpointId = endpointId,
            ChannelType = channelType,
            DestinationUrl = destinationUrl,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
