namespace LoanProject.Domain.Loans.Events;

/// <summary>
/// Contract for events in the Loan stream. A uniform timestamp lets the
/// event store persist one OccurredAtUtc column for every event type.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
