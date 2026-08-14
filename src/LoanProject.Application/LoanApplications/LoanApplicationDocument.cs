namespace LoanProject.Application.LoanApplications;

/// <summary>
/// A loan application form with product-specific fields: a car loan carries
/// different questions than a home loan, yet both live in one collection —
/// the flexible-schema case relational tables handle poorly.
/// </summary>
public sealed record LoanApplicationDocument(
    Guid Id,
    Guid CustomerId,
    DateTime SubmittedAtUtc,
    Dictionary<string, object?> Fields);
