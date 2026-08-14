namespace LoanProject.Application.Audit;

/// <summary>
/// One operational fact: who did what to which entity, when. Details is
/// free-form on purpose — each action shapes its own payload, which is the
/// whole reason this lives in a flexible-schema store.
/// </summary>
public sealed record AuditEntry(
    string EntityType,
    string EntityId,
    string Action,
    string Actor,
    DateTime OccurredAtUtc,
    Dictionary<string, object?> Details);
