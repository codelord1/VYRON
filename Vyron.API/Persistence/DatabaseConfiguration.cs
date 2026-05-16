using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Vyron.API.Data;
using Vyron.Shared.Enums;

namespace Vyron.API.Persistence;

/// <summary>
/// Configures the EF Core DbContext based on the <c>Database:Provider</c>
/// setting. Supports SQL Server and PostgreSQL transparently.
/// </summary>
public static class DatabaseConfiguration
{
    public static DatabaseProvider GetProvider(IConfiguration config)
    {
        var raw = config["Database:Provider"] ?? "SqlServer";
        return Enum.TryParse<DatabaseProvider>(raw, ignoreCase: true, out var p)
            ? p
            : DatabaseProvider.SqlServer;
    }

    public static IServiceCollection AddVyronDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = GetProvider(config);
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        services.AddDbContext<VyronDbContext>(opt =>
        {
            switch (provider)
            {
                case DatabaseProvider.PostgreSQL:
                    opt.UseNpgsql(connectionString, npg =>
                    {
                        npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                        npg.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    });
                    break;

                case DatabaseProvider.SqlServer:
                default:
                    opt.UseSqlServer(connectionString, sql =>
                    {
                        sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                    });
                    break;
            }
        });

        return services;
    }

    public static void ConfigureHangfire(IGlobalConfiguration global, IConfiguration config)
    {
        var provider = GetProvider(config);
        var hangfireConn = config.GetConnectionString("HangfireConnection")
            ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Hangfire connection missing.");

        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                global.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(hangfireConn));
                break;
            case DatabaseProvider.SqlServer:
            default:
                global.UseSqlServerStorage(hangfireConn);
                break;
        }
    }
}
