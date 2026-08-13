using LoanProject.Application.Customers;
using LoanProject.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly LoanDbContext _dbContext;

    public CustomerRepository(LoanDbContext dbContext) => _dbContext = dbContext;

    // Registers with the change tracker only — SQL happens at SaveChanges.
    public void Add(Customer customer) => _dbContext.Customers.Add(customer);

    // Tracked read on purpose: phase 7 updates KYC status on this entity,
    // and updating requires the tracker to know the original values.
    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}
