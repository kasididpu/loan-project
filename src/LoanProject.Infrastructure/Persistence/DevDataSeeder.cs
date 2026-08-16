using LoanProject.Application.Loans;
using LoanProject.Domain.Customers;
using LoanProject.Domain.Loans;
using LoanProject.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Persistence;

/// <summary>
/// Development-only sample data so anyone who clones the repo has both
/// worlds to explore immediately: CRUD rows (customers, a payment) and an
/// event-sourced loan with real history in the ledger. Idempotent — seeding
/// an already-seeded database changes nothing.
/// </summary>
public sealed class DevDataSeeder
{
    /// <summary>
    /// Well-known ids (hex "5EED" prefix) so tests, manual queries and the
    /// idempotence check can always find the seeded rows.
    /// </summary>
    public static readonly Guid SeedLoanId = new("5eed0000-0000-0000-0000-000000000001");
    public static readonly Guid SeedCustomerWithLoanId = new("5eed0000-0000-0000-0000-000000000002");
    public static readonly Guid SeedCustomerNewId = new("5eed0000-0000-0000-0000-000000000003");
    public static readonly Guid SeedPaymentId = new("5eed0000-0000-0000-0000-000000000004");

    /// <summary>Stand-in officer id for seeded lifecycle events (no real AppUser behind the seed).</summary>
    public static readonly Guid SeedOfficerId = new("5eed0000-0000-0000-0002-000000000001");

    // Fixed historic date: reruns and tests see identical data everywhere.
    private static readonly DateTime SeedDate = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    private readonly LoanDbContext _dbContext;
    private readonly ILoanRepository _loanRepository;

    public DevDataSeeder(LoanDbContext dbContext, ILoanRepository loanRepository)
    {
        _dbContext = dbContext;
        _loanRepository = loanRepository;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Two independently-guarded blocks: whichever half is missing gets
        // seeded, so even a crash between them heals on the next run.
        if (await _loanRepository.LoadAsync(SeedLoanId, cancellationToken) is null)
            await SeedLoanStreamAsync(cancellationToken);

        if (!await _dbContext.Customers.AnyAsync(c => c.Id == SeedCustomerWithLoanId, cancellationToken))
            await SeedCrudRowsAsync(cancellationToken);
    }

    private async Task SeedLoanStreamAsync(CancellationToken cancellationToken)
    {
        // A loan with real history: originated, approved, disbursed, first
        // installment collected — five events in the ledger.
        var loan = Loan.Originate(
            SeedLoanId, SeedCustomerWithLoanId, 100_000m, 0.12m, RateType.Effective, 12, SeedDate);
        loan.Approve(SeedOfficerId, "seed-officer", SeedDate);
        loan.Disburse(100_000m, SeedOfficerId, "seed-officer", SeedDate);
        loan.ReceivePayment(SeedPaymentId, loan.Schedule![0].Payment, 1, "evt_seed_0001", SeedDate.AddMonths(1));

        await _loanRepository.SaveAsync(loan, cancellationToken);
    }

    private async Task SeedCrudRowsAsync(CancellationToken cancellationToken)
    {
        // Somsri already has an active loan → KYC verified. Somchai is new and
        // stays Pending, so the Phase 7 KYC gate can be demonstrated on him.
        // Both carry identity documents (fake) so the Phase 8 field-level
        // encryption is visible: these values are stored as ciphertext at rest.
        var somsri = new Customer(SeedCustomerWithLoanId, "Seed: Somsri Borrower", SeedDate);
        somsri.SetKycStatus(KycStatus.Verified);
        somsri.SetIdentityDocuments("1234567890123", "111-2-34567-8");
        _dbContext.Customers.Add(somsri);

        var somchai = new Customer(SeedCustomerNewId, "Seed: Somchai Newcomer", SeedDate);
        somchai.SetIdentityDocuments("9876543210987", "222-6-54321-0");
        _dbContext.Customers.Add(somchai);

        // The CRUD record of the payment the ledger already knows — same
        // SeedPaymentId and Stripe event id link the two worlds together.
        var firstInstallment = AmortizationCalculator.BuildSchedule(100_000m, 0.12m, 12)[0];
        _dbContext.Payments.Add(new Payment(
            SeedPaymentId, SeedLoanId, firstInstallment.Payment, "evt_seed_0001", SeedDate.AddMonths(1)));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
