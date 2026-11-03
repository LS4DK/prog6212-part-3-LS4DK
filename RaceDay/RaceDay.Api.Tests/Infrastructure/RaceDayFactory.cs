using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>
/// Starts the real API in memory for testing, but swaps SQL Server for an in-memory database
/// and uses fast password hashing. Each instance gets its own empty database.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the SQL Server setup that Program.cs registered...
            var sqlServerSetup = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                             && d.ServiceType.GetGenericArguments().Contains(typeof(RaceDayDbContext))))
                .ToList();
            foreach (var registration in sqlServerSetup)
                services.Remove(registration);

            // ...and use an in-memory database instead
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Fast hashing for tests (the stored hash records its own iteration count)
            services.RemoveAll<PasswordService>();
            services.AddSingleton(new PasswordService(1_000));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Creates the tables and the two seeded roles (Organiser, Participant)
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();

        return host;
    }
}