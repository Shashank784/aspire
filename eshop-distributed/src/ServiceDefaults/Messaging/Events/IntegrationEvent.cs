namespace ServiceDefaults.Messaging.Events;
public record IntegrationEvent
{
    // Set once when the event is created (these were computed on every read before,
    // so the id and timestamp changed each time they were accessed).
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().AssemblyQualifiedName!;
}
