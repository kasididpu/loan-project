using LoanProject.Application;
using LoanProject.Application.Customers;
using LoanProject.Application.Loans;
using LoanProject.Application.Payments;
using LoanProject.Application.Reports;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Persistence.Repositories;
using LoanProject.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("LoanDb")
    ?? throw new InvalidOperationException("Connection string 'LoanDb' is not configured.");
builder.Services.AddDbContext<LoanDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ILoanRepository>(_ => new LoanEventStoreRepository(connectionString));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
// The unit of work is the DbContext itself — same scoped instance, second door.
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<LoanDbContext>());
builder.Services.AddScoped<IEndOfDaySummaryQuery>(_ => new EndOfDaySummaryQuery(connectionString));
builder.Services.AddScoped<DevDataSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Sample data for local exploration only — real environments are never seeded.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync(CancellationToken.None);
}

app.UseHttpsRedirection();

app.Run();
