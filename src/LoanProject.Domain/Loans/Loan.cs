using LoanProject.Domain.Loans.Events;

namespace LoanProject.Domain.Loans;

/// <summary>
/// Event-sourced aggregate — the only one in this system, by design.
/// All state changes flow through Raise -> Apply so that replaying the
/// stored stream always rebuilds the exact same state. Apply never
/// validates: replayed events already happened and cannot be refused.
/// </summary>
public sealed class Loan
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();

    public Guid Id { get; private set; }
    public LoanStatus Status { get; private set; }
    public decimal Principal { get; private set; }
    public decimal AnnualRate { get; private set; }
    public RateType RateType { get; private set; }
    public int TermMonths { get; private set; }

    // Slice 1 simplification: principal-only balance, and it starts at zero —
    // the debt exists only once money has actually been disbursed. Interest
    // joins in the amortization slices; the state-machine guards stay as-is.
    public decimal OutstandingBalance { get; private set; }

    /// <summary>Number of events applied — the optimistic-concurrency handle for Phase 2.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents;

    private Loan() { }

    public static Loan Originate(
        Guid loanId,
        Guid customerId,
        decimal principal,
        decimal annualRate,
        RateType rateType,
        int termMonths,
        DateTime utcNow)
    {
        if (principal <= 0)
            throw new ArgumentOutOfRangeException(nameof(principal), principal, "Principal must be positive.");
        if (annualRate < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRate), annualRate, "Annual rate cannot be negative.");
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), termMonths, "Term must be at least one month.");

        var loan = new Loan();
        loan.Raise(new LoanOriginated(loanId, customerId, principal, annualRate, rateType, termMonths, utcNow));
        return loan;
    }

    public void Approve(string approvedBy, DateTime utcNow)
    {
        EnsureStatus(LoanStatus.Originated, "approve");
        Raise(new LoanApproved(approvedBy, utcNow));
    }

    public void Reject(string rejectedBy, string reason, DateTime utcNow)
    {
        EnsureStatus(LoanStatus.Originated, "reject");
        Raise(new LoanRejected(rejectedBy, reason, utcNow));
    }

    public void Disburse(decimal amount, DateTime utcNow)
    {
        EnsureStatus(LoanStatus.Approved, "disburse");
        // Slice 1 assumption: full single disbursement of exactly the principal.
        if (amount != Principal)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Disbursed amount must equal the approved principal.");

        Raise(new LoanDisbursed(amount, utcNow));
    }

    public void ReceivePayment(Guid paymentId, decimal amount, int installmentNo, string stripeEventId, DateTime utcNow)
    {
        EnsureStatus(LoanStatus.Active, "receive a payment for");
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Payment amount must be positive.");
        // Slice 1 policy: refuse overpayment outright. The real over/under
        // payment rules arrive with the amortization schedule.
        if (amount > OutstandingBalance)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Payment exceeds the outstanding balance.");

        Raise(new PaymentReceived(paymentId, amount, installmentNo, stripeEventId, utcNow));
    }

    public void Settle(Guid finalPaymentId, DateTime utcNow)
    {
        EnsureStatus(LoanStatus.Active, "settle");
        if (OutstandingBalance != 0)
            throw new InvalidLoanTransitionException(Status, "settle a loan whose balance is not zero");

        Raise(new LoanSettled(finalPaymentId, utcNow));
    }

    public void MarkDefaulted(int daysOverdue, DateTime utcNow)
    {
        EnsureStatus(LoanStatus.Active, "default");
        Raise(new LoanDefaulted(daysOverdue, OutstandingBalance, utcNow));
    }

    /// <summary>Rebuilds state by replaying history. Bypasses Raise: replayed events are facts, not new intentions.</summary>
    public static Loan LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        var loan = new Loan();
        foreach (var domainEvent in history)
            loan.Apply(domainEvent);
        return loan;
    }

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void EnsureStatus(LoanStatus required, string action)
    {
        if (Status != required)
            throw new InvalidLoanTransitionException(Status, action);
    }

    private void Raise(IDomainEvent domainEvent)
    {
        Apply(domainEvent);
        _uncommittedEvents.Add(domainEvent);
    }

    private void Apply(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case LoanOriginated e:
                Id = e.LoanId;
                Principal = e.Principal;
                AnnualRate = e.AnnualRate;
                RateType = e.RateType;
                TermMonths = e.TermMonths;
                Status = LoanStatus.Originated;
                break;
            case LoanApproved:
                Status = LoanStatus.Approved;
                break;
            case LoanRejected:
                Status = LoanStatus.Rejected;
                break;
            case LoanDisbursed e:
                OutstandingBalance = e.DisbursedAmount;
                Status = LoanStatus.Active;
                break;
            case PaymentReceived e:
                OutstandingBalance -= e.Amount;
                break;
            case LoanSettled:
                Status = LoanStatus.Settled;
                break;
            case LoanDefaulted:
                Status = LoanStatus.Defaulted;
                break;
            default:
                // Fail loudly: a new event type without an Apply arm would
                // otherwise corrupt every replay silently.
                throw new ArgumentException($"Unknown event type '{domainEvent.GetType().Name}'.", nameof(domainEvent));
        }

        Version++;
    }
}
