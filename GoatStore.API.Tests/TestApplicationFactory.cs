using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GoatStore.DataAccess;

namespace GoatStore.API.Tests;

public class TestApplicationFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "SuperSecretTestKeyForJwtThatIsLongEnough123!";
    public const string Issuer = "TestIssuer";
    public const string Audience = "TestAudience";

    private DbConnection? _connection;

    public TestApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "DataSource=:memory:");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", Audience);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "1000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<GoatStoreDbContext>));
            
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<GoatStoreDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GoatStoreDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Close();
        _connection?.Dispose();
    }
}