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

    /// <summary>Who the loan belongs to — carried by LoanOriginated. Needed by
    /// cross-aggregate rules (e.g. the KYC check the approve handler runs).</summary>
    public Guid CustomerId { get; private set; }

    public LoanStatus Status { get; private set; }
    public decimal Principal { get; private set; }
    public decimal AnnualRate { get; private set; }
    public RateType RateType { get; private set; }
    public int TermMonths { get; private set; }

    // Principal remaining. Zero until disbursement (the debt exists only once
    // money has actually gone out); afterwards it tracks the RemainingBalance
    // of the last paid schedule row.
    public decimal OutstandingBalance { get; private set; }

    /// <summary>Number of events applied — the optimistic-concurrency handle for Phase 2.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents;

    /// <summary>
    /// Null until money is disbursed. Derived state: never stored in events —
    /// rebuilt inside Apply(LoanDisbursed) from the loan terms, which is
    /// replay-safe because the calculators are pure functions.
    /// </summary>
    public IReadOnlyList<Installment>? Schedule { get; private set; }

    /// <summary>1-based number of the next installment due.</summary>
    public int NextInstallmentNo { get; private set; } = 1;

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
        if (installmentNo != NextInstallmentNo)
            throw new ArgumentOutOfRangeException(nameof(installmentNo), installmentNo,
                $"Installments are paid in order; next due is installment {NextInstallmentNo}.");

        // Exact-amount policy: no partial payments, no prepayments (an ADR
        // decision — both are deferred business features, not accidents).
        // The due figure already includes the final installment's
        // rounding-drift adjustment.
        var due = Schedule![installmentNo - 1];
        if (amount != due.Payment)
            throw new ArgumentException(
                $"Payment must match the due installment exactly: expected {due.Payment}.", nameof(amount));

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

    /// <summary>Captures every non-derived field for the snapshot table.</summary>
    public LoanSnapshotState ToSnapshot() => new(
        Id, CustomerId, Status, Principal, AnnualRate, RateType, TermMonths,
        OutstandingBalance, NextInstallmentNo, Version);

    /// <summary>
    /// Third way a Loan comes to life (besides Originate and LoadFromHistory):
    /// restore from a snapshot, then replay only the events that happened
    /// after it. Taking the tail here keeps replay a one-entry-point affair.
    /// </summary>
    public static Loan FromSnapshot(LoanSnapshotState snapshot, IEnumerable<IDomainEvent> subsequentEvents)
    {
        var loan = new Loan
        {
            Id = snapshot.Id,
            CustomerId = snapshot.CustomerId,
            Status = snapshot.Status,
            Principal = snapshot.Principal,
            AnnualRate = snapshot.AnnualRate,
            RateType = snapshot.RateType,
            TermMonths = snapshot.TermMonths,
            OutstandingBalance = snapshot.OutstandingBalance,
            NextInstallmentNo = snapshot.NextInstallmentNo,
            Version = snapshot.Version,
        };

        // The schedule exists from disbursement onward. It is never part of
        // the snapshot — rebuilt here so the tail's PaymentReceived events
        // can look their installments up during replay.
        if (loan.Status is LoanStatus.Active or LoanStatus.Settled or LoanStatus.Defaulted)
            loan.Schedule = loan.BuildScheduleFromTerms();

        foreach (var domainEvent in subsequentEvents)
            loan.Apply(domainEvent);

        return loan;
    }

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private IReadOnlyList<Installment> BuildScheduleFromTerms() =>
        RateType == RateType.Flat
            ? AmortizationCalculator.BuildFlatSchedule(Principal, AnnualRate, TermMonths)
            : AmortizationCalculator.BuildSchedule(Principal, AnnualRate, TermMonths);

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
                CustomerId = e.CustomerId;
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
                // Derived, not stored in the event: the calculators are pure
                // functions, so replay always rebuilds the identical schedule
                // from the loan terms.
                Schedule = BuildScheduleFromTerms();
                Status = LoanStatus.Active;
                break;
            case PaymentReceived e:
                OutstandingBalance = Schedule![e.InstallmentNo - 1].RemainingBalance;
                NextInstallmentNo = e.InstallmentNo + 1;
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
