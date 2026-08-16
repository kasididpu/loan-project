using LoanProject.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LoanProject.Api.Tests;

/// <summary>
/// Boots the real API in-memory for HTTP-level tests. Runs in the Development
/// environment so migrations and the dev seed apply (needs SQL Server up), but
/// strips the background hosted services (dispatcher/projector/RabbitMQ/Quartz)
/// so the auth tests do not depend on Redpanda/RabbitMQ, and swaps the OTP store
/// for a capturing double so the MFA flow is assertable.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public CapturingOtpStore Otp { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IOtpStore>();
            services.AddSingleton<IOtpStore>(Otp);
        });
    }
}
