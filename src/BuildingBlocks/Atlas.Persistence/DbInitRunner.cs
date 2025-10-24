using Npgsql;
namespace Atlas.Persistence;
public static class DbInitRunner
{
    public static async Task RunLedgerFastPathMigrationsAsync(string connString, CancellationToken ct=default)
    {
        var sql = await File.ReadAllTextAsync("src/Ledger/Atlas.Ledger.Domain/sql/000_fastpath_schema.sql", ct);
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
