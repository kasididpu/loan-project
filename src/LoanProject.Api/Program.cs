using LoanProject.Application.Loans;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("LoanDb")
    ?? throw new InvalidOperationException("Connection string 'LoanDb' is not configured.");
builder.Services.AddDbContext<LoanDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ILoanRepository>(_ => new LoanEventStoreRepository(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
