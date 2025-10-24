using Npgsql;
using System.Data;

namespace Atlas.Ledger.App;

public sealed class FastTransferHandler
{
    private readonly NpgsqlDataSource _ds;
    public FastTransferHandler(NpgsqlDataSource ds) => _ds = ds;

    // Single round-trip fast path using stored proc; SERIALIZABLE with retry
    public async Task<(Guid? entryId, bool duplicate)> ExecuteAsync(
        string key, string tenant, string src, string dst, long amountMinor, string currency, string narration,
        CancellationToken ct)
    {
        for (int i = 0; i < 3; i++)
        {
            await using var conn = await _ds.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT sp_idem_transfer_execute(@k,@t,@s,@d,@m,@c,@n)", conn, tx);
                cmd.Parameters.AddWithValue("k", key);
                cmd.Parameters.AddWithValue("t", tenant);
                cmd.Parameters.AddWithValue("s", src);
                cmd.Parameters.AddWithValue("d", dst);
                cmd.Parameters.AddWithValue("m", amountMinor);
                cmd.Parameters.AddWithValue("c", currency);
                cmd.Parameters.AddWithValue("n", narration);
                var result = await cmd.ExecuteScalarAsync(ct);
                await tx.CommitAsync(ct);
                return (result as Guid?, result is null); // null => duplicate
            }
            catch (PostgresException ex) when (ex.SqlState == "40001") // serialization_failure
            {
                await tx.RollbackAsync(ct);
                if (i == 2) throw;
                continue;
            }
        }
        return (null, false);
    }
}
