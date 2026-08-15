using System.Text.Json.Serialization;
using LoanProject.Application;
using LoanProject.Application.Audit;
using LoanProject.Application.Customers;
using LoanProject.Application.LoanApplications;
using LoanProject.Application.Loans;
using LoanProject.Application.Payments;
using LoanProject.Application.Rates;
using LoanProject.Application.Reconciliation;
using LoanProject.Application.Reports;
using LoanProject.Application.Secrets;
using LoanProject.Application.Settlement;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Jobs;
using LoanProject.Infrastructure.Messaging;
using LoanProject.Infrastructure.Mongo;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Persistence.Repositories;
using LoanProject.Infrastructure.Rates;
using LoanProject.Infrastructure.ReadModel;
using LoanProject.Infrastructure.Reports;
using LoanProject.Infrastructure.Secrets;
using LoanProject.Infrastructure.Streaming;
using LoanProject.Infrastructure.Stripe;
using LoanProject.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Quartz;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Serialize and accept enums as their names (e.g. "Effective") instead of magic
// numbers — a cleaner API contract, and consistent with the event store which
// already stores enums as strings.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Secrets come from Vault through ISecretProvider (phase 3.5). The
// appsettings values are non-secret local defaults, kept only as the
// Development fallback for when the dev Vault has not been seeded yet.
var connectionString = builder.Configuration.GetConnectionString("LoanDb")
    ?? throw new InvalidOperationException("Connection string 'LoanDb' is not configured.");
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")
    ?? throw new InvalidOperationException("Connection string 'Mongo' is not configured.");
var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Connection string 'RabbitMq' is not configured.");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");
var readConnectionString = builder.Configuration.GetConnectionString("LoanReadDb")
    ?? throw new InvalidOperationException("Connection string 'LoanReadDb' is not configured.");

var vaultAddress = builder.Configuration["Vault:Address"];
if (!string.IsNullOrWhiteSpace(vaultAddress))
{
    var vaultToken = builder.Configuration["Vault:Token"]
        ?? throw new InvalidOperationException("Vault:Token is not configured.");
    ISecretProvider secretProvider = new VaultSecretProvider(vaultAddress, vaultToken);
    builder.Services.AddSingleton(secretProvider);

    try
    {
        connectionString = await secretProvider.GetSecretAsync("LoanDb", CancellationToken.None);
        readConnectionString = await secretProvider.GetSecretAsync("LoanReadDb", CancellationToken.None);
        mongoConnectionString = await secretProvider.GetSecretAsync("Mongo", CancellationToken.None);
        rabbitConnectionString = await secretProvider.GetSecretAsync("RabbitMq", CancellationToken.None);
        redisConnectionString = await secretProvider.GetSecretAsync("Redis", CancellationToken.None);
    }
    catch (Exception exception) when (builder.Environment.IsDevelopment())
    {
        // Local dev may boot before the dev vault is seeded — fall back to
        // the non-secret defaults and say so out loud. Anywhere else this
        // rethrows: an unreadable secret store must stop the boot.
        Console.WriteLine($"WARN: Vault unavailable, using local dev defaults. ({exception.Message})");
    }
}

builder.Services.AddDbContext<LoanDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ILoanRepository>(_ => new LoanEventStoreRepository(connectionString));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
// The unit of work is the DbContext itself — same scoped instance, second door.
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<LoanDbContext>());
builder.Services.AddScoped<IEndOfDaySummaryQuery>(_ => new EndOfDaySummaryQuery(connectionString));
builder.Services.AddScoped<DevDataSeeder>();

// Unlike the scoped DbContext, the Mongo client is a singleton: it is
// thread-safe and owns its connection pool for the whole process.
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<IMongoClient>().GetDatabase("LoanProject"));
builder.Services.AddScoped<IAuditLogWriter, MongoAuditLogWriter>();
builder.Services.AddScoped<ILoanApplicationStore, MongoLoanApplicationStore>();
builder.Services.AddScoped<RecordStripePaymentHandler>();

// --- Phase 5: async pipeline ---

// Event dispatcher: ledger -> Redpanda, single active instance.
var redpandaBootstrap = builder.Configuration["Redpanda:BootstrapServers"]
    ?? throw new InvalidOperationException("Redpanda:BootstrapServers is not configured.");
builder.Services.AddSingleton<IEventStoreReader>(_ => new EventStoreReader(connectionString));
builder.Services.AddSingleton<IDispatcherCursorStore>(_ => new DispatcherCursorStore(connectionString));
builder.Services.AddSingleton<ILoanEventPublisher>(provider => new RedpandaLoanEventPublisher(
    redpandaBootstrap,
    RedpandaLoanEventPublisher.DefaultTopic,
    provider.GetRequiredService<ILogger<RedpandaLoanEventPublisher>>()));
builder.Services.AddHostedService<EventDispatcher>();

// Payment notifications over RabbitMQ: best-effort publisher + deduping consumer.
builder.Services.AddSingleton<IPaymentNotifier>(provider => new RabbitMqPaymentNotifier(
    rabbitConnectionString,
    RabbitMqPaymentNotifier.DefaultQueueName,
    provider.GetRequiredService<ILogger<RabbitMqPaymentNotifier>>()));
builder.Services.AddSingleton<PaymentNotificationDeduplicator>();
builder.Services.AddHostedService(provider => new PaymentNotificationConsumer(
    rabbitConnectionString,
    RabbitMqPaymentNotifier.DefaultQueueName,
    provider.GetRequiredService<PaymentNotificationDeduplicator>(),
    provider.GetRequiredService<ILogger<PaymentNotificationConsumer>>()));

// Rate lookup behind a Redis cache-aside decorator. AbortOnConnectFail off:
// the app must boot (and serve rates from the source) even with Redis down.
var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton<IInterestRateLookup>(provider => new RedisRateCache(
    provider.GetRequiredService<IConnectionMultiplexer>(),
    new StaticRateSheet(),
    provider.GetRequiredService<ILogger<RedisRateCache>>()));

// Scheduled jobs run in the app (Quartz), not the database: Azure SQL
// Database has no SQL Agent, so this keeps the optional cloud path viable.
builder.Services.AddScoped<IStripeEventSource, StripeEventSource>();
builder.Services.AddScoped<ReconcileStripePaymentsHandler>();
builder.Services.AddScoped<SettleEndOfDayHandler>();
var reconciliationCron = builder.Configuration["Jobs:ReconciliationCron"] ?? "0 0/30 * * * ?";
var settlementCron = builder.Configuration["Jobs:SettlementCron"] ?? "0 59 23 * * ?";
builder.Services.AddQuartz(quartz =>
{
    var reconciliationKey = new JobKey(nameof(ReconciliationJob));
    quartz.AddJob<ReconciliationJob>(job => job.WithIdentity(reconciliationKey));
    quartz.AddTrigger(trigger => trigger
        .ForJob(reconciliationKey)
        .WithIdentity($"{nameof(ReconciliationJob)}Trigger")
        .WithCronSchedule(reconciliationCron));

    var settlementKey = new JobKey(nameof(SettlementJob));
    quartz.AddJob<SettlementJob>(job => job.WithIdentity(settlementKey));
    quartz.AddTrigger(trigger => trigger
        .ForJob(settlementKey)
        .WithIdentity($"{nameof(SettlementJob)}Trigger")
        .WithCronSchedule(settlementCron));
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

// --- Phase 6: CQRS read side ---

// Read database: a physically separate database, synced only by projecting
// loan-events (never a cross-database query — Azure SQL Database has none).
builder.Services.AddDbContext<ReadDbContext>(options => options.UseSqlServer(readConnectionString));

// Command handlers append to the event store (write side); the dispatcher
// publishes, so these never touch Redpanda or the read side directly.
builder.Services.AddScoped<OriginateLoanHandler>();
builder.Services.AddScoped<ApproveLoanHandler>();
builder.Services.AddScoped<DisburseLoanHandler>();
builder.Services.AddScoped<RejectLoanHandler>();

// Query side reads only the Read DB.
builder.Services.AddScoped<ILoanStatusQuery, LoanStatusQuery>();
builder.Services.AddScoped<IPortfolioSummaryQuery, PortfolioSummaryQuery>();
builder.Services.AddScoped<IDailyCollectionsQuery, DailyCollectionsQuery>();

// Projector: single consumer draining loan-events into the Read DB. A fresh
// scoped projection (and ReadDbContext) per message, same lifetime as a request.
builder.Services.AddScoped<LoanReadModelProjection>();
builder.Services.AddHostedService(provider => new LoanReadModelProjector(
    redpandaBootstrap,
    RedpandaLoanEventPublisher.DefaultTopic,
    provider.GetRequiredService<IServiceScopeFactory>(),
    provider.GetRequiredService<ILogger<LoanReadModelProjector>>()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Sample data for local exploration only — real environments are never seeded.
    using var scope = app.Services.CreateScope();

    // Create/migrate both databases on boot in dev so the app is ready without a
    // manual `dotnet ef database update` — same treatment for Write and Read.
    // Prod stays deliberate: this whole block is dev-only. Write DB first, since
    // the seeder and event store need its tables.
    await scope.ServiceProvider.GetRequiredService<LoanDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ReadDbContext>().Database.MigrateAsync();

    await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync(CancellationToken.None);
}

app.UseHttpsRedirection();

// Endpoint groups live in Api/Endpoints — Program.cs only composes.
app.MapStripeWebhook();
app.MapRates();
app.MapLoans();
app.MapReports();

app.Run();
