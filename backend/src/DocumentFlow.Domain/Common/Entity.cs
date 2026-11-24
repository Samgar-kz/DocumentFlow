namespace DocumentFlow.Domain.Common;

/// <summary>
/// Base type for entities. Identifiers are time-ordered UUIDv7 values. Aggregates record
/// domain events while they change; the persistence layer dispatches them in the same
/// database transaction (to write the audit trail and notifications).
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _events = [];

    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    /// <summary>Events raised since the last save, without clearing them.</summary>
    public IReadOnlyList<IDomainEvent> PeekEvents() => _events.ToList();

    /// <summary>Returns and clears the events raised since the last save.</summary>
    public IReadOnlyList<IDomainEvent> DequeueEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);
}

/// <summary>Something meaningful that happened in the domain, performed by an actor.</summary>
public interface IDomainEvent
{
    Guid ActorId { get; }

    DateTimeOffset OccurredAt { get; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Raised when an operation would violate a business rule (e.g. an invalid state transition).</summary>
public sealed class DomainException(string message) : Exception(message);

/// <summary>Raised when the actor is not allowed to perform an operation on a resource they can see.</summary>
public sealed class NotPermittedException(string message) : Exception(message);
