using System.Text.Json.Serialization;
using LoanProject.Application;
using LoanProject.Application.Audit;
using LoanProject.Application.Auth;
using LoanProject.Application.Customers;
using LoanProject.Application.LoanApplications;
using LoanProject.Application.Loans;
using LoanProject.Application.Payments;
using LoanProject.Application.Rates;
using LoanProject.Application.Reconciliation;
using LoanProject.Application.Reports;
using LoanProject.Application.Secrets;
using LoanProject.Application.Security;
using LoanProject.Application.Settlement;
using LoanProject.Domain.Customers;
using LoanProject.Infrastructure.Auth;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Health;
using LoanProject.Infrastructure.Jobs;
using LoanProject.Infrastructure.Messaging;
using LoanProject.Infrastructure.Mongo;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Persistence.Repositories;
using LoanProject.Infrastructure.Rates;
using LoanProject.Infrastructure.ReadModel;
using LoanProject.Infrastructure.Reports;
using LoanProject.Infrastructure.Secrets;
using LoanProject.Infrastructure.Security;
using LoanProject.Infrastructure.Streaming;
using LoanProject.Infrastructure.Stripe;
using LoanProject.Api.Endpoints;
using LoanProject.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Quartz;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Phase 9 (HA): one image runs in one of three roles, selected by App:Role.
//   "all"    (default) — serves HTTP *and* runs every background singleton plus
//                        the dev DB migration/seed. This is a plain `dotnet run`.
//   "api"    — serves HTTP only; sits behind the load balancer, scaled out.
//   "worker" — the single instance that owns all background work: the event
//              dispatcher (whose cursor must never be raced), the read-model
//              projector, the payment-notification consumer, and the Quartz jobs.
// Exactly one process runs background work, so nothing double-fires (a duplicated
// dispatcher would corrupt the cursor; duplicated Quartz jobs would settle twice).
var appRole = builder.Configuration["App:Role"] ?? "all";
var runsBackgroundWork =
    appRole.Equals("all", StringComparison.OrdinalIgnoreCase) ||
    appRole.Equals("worker", StringComparison.OrdinalIgnoreCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Serialize and accept enums as their names (e.g. "Effective") instead of magic
// numbers — a cleaner API contract, and consistent with the event store which
// already stores enums as strings.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Structured logging to Console + Seq (Phase 8). The destructuring policy masks
// a Customer's PII whenever one is logged with {@Customer}, so a national id or
// bank account never reaches the log sink even by accident.
var seqServerUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Destructure.ByTransforming<Customer>(customer => new
    {
        customer.Id,
        customer.FullName,
        customer.KycStatus,
        NationalId = SensitiveDataMasker.MaskTail(customer.NationalId),
        BankAccountNumber = SensitiveDataMasker.MaskTail(customer.BankAccountNumber),
        customer.CreatedAtUtc,
    })
    .WriteTo.Console()
    .WriteTo.Seq(seqServerUrl));

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

// Auth + data-protection secrets (Phase 8): the JWT signing key, the PII
// field-encryption key, and the dev seed credentials. Real environments read
// them from Vault; dev falls back to fixed non-secret local values so the app
// boots before the dev Vault is seeded — the same policy as the DB passwords.
var jwtSigningKey = "loan-dev-jwt-signing-key-change-me-0123456789abcdef";
var fieldEncryptionKey = "loan-dev-field-encryption-key-change-me";
var devSeedUserPassword = "Dev!Passw0rd";
var devOAuthClientSecret = "dev-oauth-client-secret-change-me";
const string DevOAuthClientId = "loan-report-bot";

var vaultAddress = builder.Configuration["Vault:Address"];
if (!string.IsNullOrWhiteSpace(vaultAddress))
{
    var vaultToken = builder.Configuration["Vault:Token"]
        ?? throw new InvalidOperationException("Vault:Token is not configured.");
    // Per-environment secret document: host dev reads "loan-api" (localhost
    // connection strings); the containerized HA stack reads "loan-docker"
    // (service-name connection strings). Genuine secrets — keys, passwords,
    // Stripe — are identical in both documents.
    var vaultBasePath = builder.Configuration["Vault:BasePath"] ?? "loan-api";
    ISecretProvider secretProvider = new VaultSecretProvider(vaultAddress, vaultToken, basePath: vaultBasePath);
    builder.Services.AddSingleton(secretProvider);

    try
    {
        connectionString = await secretProvider.GetSecretAsync("LoanDb", CancellationToken.None);
        readConnectionString = await secretProvider.GetSecretAsync("LoanReadDb", CancellationToken.None);
        mongoConnectionString = await secretProvider.GetSecretAsync("Mongo", CancellationToken.None);
        rabbitConnectionString = await secretProvider.GetSecretAsync("RabbitMq", CancellationToken.None);
        redisConnectionString = await secretProvider.GetSecretAsync("Redis", CancellationToken.None);
        jwtSigningKey = await secretProvider.GetSecretAsync("JwtSigningKey", CancellationToken.None);
        fieldEncryptionKey = await secretProvider.GetSecretAsync("FieldEncryptionKey", CancellationToken.None);
        devSeedUserPassword = await secretProvider.GetSecretAsync("DevSeedUserPassword", CancellationToken.None);
        devOAuthClientSecret = await secretProvider.GetSecretAsync("DevOAuthClientSecret", CancellationToken.None);
    }
    catch (Exception exception) when (builder.Environment.IsDevelopment())
    {
        // Local dev may boot before the dev vault is seeded — fall back to
        // the non-secret defaults and say so out loud. Anywhere else this
        // rethrows: an unreadable secret store must stop the boot.
        Console.WriteLine($"WARN: Vault unavailable, using local dev defaults. ({exception.Message})");
    }
}

// PII field encryption (Phase 8). Registered before the DbContext because its
// value converters resolve IFieldEncryptor from DI. Singleton: it derives a key
// once and is thread-safe.
builder.Services.AddSingleton<IFieldEncryptor>(new AesGcmFieldEncryptor(fieldEncryptionKey));

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
// The dispatcher runs on the worker only — a second one would race the cursor.
if (runsBackgroundWork)
    builder.Services.AddHostedService<EventDispatcher>();

// Payment notifications over RabbitMQ: best-effort publisher + deduping consumer.
// The publisher stays on every role (the webhook path publishes); the consumer
// drains on the worker only.
builder.Services.AddSingleton<IPaymentNotifier>(provider => new RabbitMqPaymentNotifier(
    rabbitConnectionString,
    RabbitMqPaymentNotifier.DefaultQueueName,
    provider.GetRequiredService<ILogger<RabbitMqPaymentNotifier>>()));
builder.Services.AddSingleton<PaymentNotificationDeduplicator>();
if (runsBackgroundWork)
{
    builder.Services.AddHostedService(provider => new PaymentNotificationConsumer(
        rabbitConnectionString,
        RabbitMqPaymentNotifier.DefaultQueueName,
        provider.GetRequiredService<PaymentNotificationDeduplicator>(),
        provider.GetRequiredService<ILogger<PaymentNotificationConsumer>>()));
}

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
// The scheduler runs on the worker only: replicated Quartz would reconcile and
// settle N times per cron tick. The handlers above stay registered everywhere.
if (runsBackgroundWork)
{
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
}

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

// Phase 7: KYC compliance action.
builder.Services.AddScoped<SetKycStatusHandler>();

// Query side reads only the Read DB.
builder.Services.AddScoped<ILoanStatusQuery, LoanStatusQuery>();
builder.Services.AddScoped<IPortfolioSummaryQuery, PortfolioSummaryQuery>();
builder.Services.AddScoped<IDailyCollectionsQuery, DailyCollectionsQuery>();

// Projector: single consumer draining loan-events into the Read DB. A fresh
// scoped projection (and ReadDbContext) per message, same lifetime as a request.
builder.Services.AddScoped<LoanReadModelProjection>();
// One projector drains loan-events into the Read DB — worker only, so the read
// side is never written by competing consumers.
if (runsBackgroundWork)
{
    builder.Services.AddHostedService(provider => new LoanReadModelProjector(
        redpandaBootstrap,
        RedpandaLoanEventPublisher.DefaultTopic,
        provider.GetRequiredService<IServiceScopeFactory>(),
        provider.GetRequiredService<ILogger<LoanReadModelProjector>>()));
}

// --- Phase 8: authentication, authorization & data protection ---

// ASP.NET Core Identity provides the user/role store and password hashing.
// AddIdentityCore (not AddIdentity) keeps it store-only — no cookie handler is
// wired up, because this API authenticates with JWTs it issues itself.
builder.Services
    .AddIdentityCore<AppUser>(options => options.User.RequireUniqueEmail = false)
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<LoanDbContext>();

// JWT: signing key from Vault; issuer/audience/lifetimes from configuration.
var jwtOptions = new JwtOptions(
    Issuer: builder.Configuration["Jwt:Issuer"] ?? "loan-api",
    Audience: builder.Configuration["Jwt:Audience"] ?? "loan-api-clients",
    SigningKey: jwtSigningKey,
    AccessTokenLifetime: TimeSpan.FromMinutes(builder.Configuration.GetValue("Jwt:AccessTokenMinutes", 60)),
    MfaTokenLifetime: TimeSpan.FromMinutes(builder.Configuration.GetValue("Jwt:MfaTokenMinutes", 5)));
var jwtTokenService = new JwtTokenService(jwtOptions);
builder.Services.AddSingleton<IJwtTokenService>(jwtTokenService);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read claims by their raw names (sub, role, customer_id) — no remapping
        // to long ClaimTypes URIs, so authorization and ICurrentUser see exactly
        // what the token service issued.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtTokenService.ValidationParameters();
    });

// One policy per endpoint group — the role-to-endpoint mapping lives only here.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.LoanOfficer, policy => policy.RequireRole(Roles.Admin, Roles.LoanOfficer))
    .AddPolicy(AuthPolicies.Compliance, policy => policy.RequireRole(Roles.Admin, Roles.ComplianceOfficer))
    // Staff roles + the System client (a reporting bot via client credentials).
    .AddPolicy(AuthPolicies.BackOffice,
        policy => policy.RequireRole(Roles.Admin, Roles.LoanOfficer, Roles.ComplianceOfficer, Roles.System));

// Turns the request's validated claims into the ICurrentUser handlers depend on.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// OTP store (Redis), customer onboarding handler, and the dev-only auth seeder.
builder.Services.AddSingleton<IOtpStore>(provider =>
    new RedisOtpStore(provider.GetRequiredService<IConnectionMultiplexer>()));
builder.Services.AddScoped<CreateCustomerHandler>();
builder.Services.AddScoped<AuthDataSeeder>();

// --- Phase 9: high availability ---

// Liveness ("/health/live") is just "the process is up". Readiness
// ("/health/ready") verifies the backing services this instance needs, so the
// load balancer only routes to replicas that can actually serve. The probes live
// in Infrastructure because they reach out to external tech.
builder.Services.AddHealthChecks()
    .AddCheck("write-db", new SqlServerHealthCheck(connectionString), tags: ["ready"])
    .AddCheck("read-db", new SqlServerHealthCheck(readConnectionString), tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
    .AddCheck<MongoHealthCheck>("mongo", tags: ["ready"])
    .AddCheck("rabbitmq", new RabbitMqHealthCheck(rabbitConnectionString), tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Create/migrate both databases and seed sample data on boot in dev — but
    // only on the single background-work instance, so API replicas never race
    // each other running EF migrations. Prod stays deliberate: dev-only block.
    if (runsBackgroundWork)
    {
        // Sample data for local exploration only — real environments are never seeded.
        using var scope = app.Services.CreateScope();

        // Write DB first, since the seeder and event store need its tables.
        await scope.ServiceProvider.GetRequiredService<LoanDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ReadDbContext>().Database.MigrateAsync();

        await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync(CancellationToken.None);

        // Dev-only auth seed: roles, one demo user per role, and the OAuth client.
        // Credentials come from Vault (dev fallback) — nothing is hard-coded here.
        await scope.ServiceProvider.GetRequiredService<AuthDataSeeder>()
            .SeedAsync(devSeedUserPassword, DevOAuthClientId, devOAuthClientSecret, CancellationToken.None);
    }
}

// Structured request logging (method, path, status, elapsed) via Serilog.
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

// Authentication must run before authorization, and both before the endpoints
// whose policies they enforce.
app.UseAuthentication();
app.UseAuthorization();

// Health endpoints are anonymous — the load balancer and container runtime probe
// them without a token. Liveness runs no checks; readiness runs the "ready" set.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Endpoint groups live in Api/Endpoints — Program.cs only composes.
app.MapAuth();
app.MapStripeWebhook();
app.MapRates();
app.MapLoans();
app.MapReports();
app.MapCustomers();

app.Run();

// Exposed as public so the integration test project can drive the app through
// WebApplicationFactory<Program>; top-level statements otherwise emit it as internal.
public partial class Program { }
