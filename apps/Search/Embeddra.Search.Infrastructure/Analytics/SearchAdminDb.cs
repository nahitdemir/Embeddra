using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Embeddra.Search.Infrastructure.Analytics;

public sealed class SearchAdminDb
{
    private readonly string _connectionString;

    public SearchAdminDb(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("AdminDb")
            ?? configuration.GetConnectionString("AuditDb")
            ?? "Host=localhost;Port=5433;Database=embeddra;Username=embeddra;Password=embeddra";
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
