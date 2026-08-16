using LoanProject.Application.Auth;
using LoanProject.Application.Customers;
using LoanProject.Domain.Customers;

namespace LoanProject.Application.Loans;

/// <summary>
/// Approves a loan. The aggregate is loaded straight from the event store — the
/// real-time source of truth — never the read model: an approval decision must
/// see the loan's current state, not an eventually-consistent projection that
/// may lag. This is the deliberate "bypass the Read DB" point Phase 6 calls out.
/// The KYC gate is a cross-aggregate rule, so it lives in the handler (the Loan
/// aggregate validates only its own state) — Phase 7. The approving officer is
/// the authenticated caller (Phase 8), not a value from the request body.
/// </summary>
public sealed class ApproveLoanHandler
{
    private readonly ILoanRepository _loans;
    private readonly ICustomerRepository _customers;
    private readonly ICurrentUser _currentUser;

    public ApproveLoanHandler(ILoanRepository loans, ICustomerRepository customers, ICurrentUser currentUser)
    {
        _loans = loans;
        _customers = customers;
        _currentUser = currentUser;
    }

    public async Task HandleAsync(Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await _loans.LoadAsync(loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        // Cross-aggregate gate: a loan may only be approved for a KYC-verified
        // customer. The customer is read real-time from the write side (same
        // reason the aggregate itself is loaded real-time, not from the Read DB).
        var customer = await _customers.FindAsync(loan.CustomerId, cancellationToken);
        if (customer is null || customer.KycStatus != KycStatus.Verified)
            throw new KycNotVerifiedException(loan.CustomerId, customer?.KycStatus);

        // Accountability comes from the verified identity on the token — the API
        // no longer accepts an "approvedBy" the caller could forge. Behind
        // RequireAuthorization the subject is always present; its absence is a
        // misconfiguration, so fail loudly rather than record an empty actor.
        var officerId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated caller has no subject id.");
        loan.Approve(officerId, _currentUser.Name, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
    }
}
