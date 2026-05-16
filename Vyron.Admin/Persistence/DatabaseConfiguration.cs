using Microsoft.EntityFrameworkCore;
using Vyron.Admin.Data;
using Vyron.Shared.Enums;

namespace Vyron.Admin.Persistence;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddVyronAdminDatabase(this IServiceCollection services, IConfiguration config)
    {
        var providerRaw = config["Database:Provider"] ?? "SqlServer";
        var provider = Enum.TryParse<DatabaseProvider>(providerRaw, true, out var p) ? p : DatabaseProvider.SqlServer;

        var conn = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection missing.");

        services.AddDbContext<AdminDbContext>(opt =>
        {
            switch (provider)
            {
                case DatabaseProvider.PostgreSQL:
                    opt.UseNpgsql(conn);
                    break;
                case DatabaseProvider.SqlServer:
                default:
                    opt.UseSqlServer(conn);
                    break;
            }
        });

        return services;
    }
}
