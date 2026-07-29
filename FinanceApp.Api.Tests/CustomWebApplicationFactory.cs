using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Api.Models;

namespace FinanceApp.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FinanceDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(_connection));

            var idDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>));
            if (idDescriptor != null) services.Remove(idDescriptor);
            services.AddDbContext<AppIdentityDbContext>(options => options.UseSqlite(_connection));
        });

        var host = base.CreateHost(builder);

        // apply migrations
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        db.Database.EnsureCreated();
        var idDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        idDb.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_connection != null) { _connection.Close(); _connection.Dispose(); }
    }
}
