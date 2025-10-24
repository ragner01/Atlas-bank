using Azure.Storage.Blobs;
using Npgsql;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Atlas.Settlement;

public sealed class SettlementWorker : BackgroundService
{
    private readonly ILogger<SettlementWorker> _log;
    private readonly BlobServiceClient _blob;
    private readonly IConfiguration _configuration;
    private readonly SettlementOptions _options;

    public SettlementWorker(
        ILogger<SettlementWorker> log, 
        BlobServiceClient blob,
        IConfiguration configuration,
        IOptions<SettlementOptions> options)
    { 
        _log = log; 
        _blob = blob; 
        _configuration = configuration;
        _options = options.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));
    private async Task Loop(CancellationToken ct)
    {
        var runEvery = TimeSpan.FromMinutes(_options.IntervalMinutes);
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnce(ct); }
            catch (Exception ex) { _log.LogError(ex, "settlement failed"); }
            await Task.Delay(runEvery, ct);
        }
    }

    private async Task RunOnce(CancellationToken ct)
    {
        // Use configuration instead of hardcoded credentials
        var connectionString = _configuration.GetConnectionString("Ledger") 
            ?? throw new InvalidOperationException("Ledger connection string not configured");
        
        await using var conn = new NpgsqlConnection(connectionString); 
        await conn.OpenAsync(ct);

        // Select unsettled merchant credits in last window
        var from = DateTimeOffset.UtcNow.Date.AddDays(-1);
        var to = DateTimeOffset.UtcNow.Date;
        var sql = @"
          with m as (
            select p.entry_id, a.account_id, p.amount_minor, je.booking_date, a.tenant_id
            from postings p
            join accounts a on a.account_id = p.account_id
            join journal_entries je on je.entry_id = p.entry_id
            where p.side='C' and a.account_id like 'merchant::%' and je.booking_date >= @from and je.booking_date < @to
              and p.entry_id not in (select entry_id from settlements)
          )
          select tenant_id, account_id as merchant, sum(amount_minor) as gross, count(*) as tx_count
          from m group by tenant_id, account_id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("from", from.UtcDateTime);
        cmd.Parameters.AddWithValue("to", to.UtcDateTime);

        var rows = new List<(string tenant,string merchant,long gross,int count)>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
                rows.Add((r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetInt32(3)));
        }
        if (rows.Count == 0) { return; }

        // Basic fee calc: use configuration instead of environment variables
        decimal mdrBp = _options.MdrBasisPoints;
        long fixedMinor = _options.FixedFeeMinor;

        var csv = new StringBuilder();
        csv.AppendLine("tenant,merchant,gross_minor,fees_minor,net_minor,tx_count,from,to");
        foreach (var row in rows)
        {
            decimal amount = row.gross / 100m;
            long fee = (long)Math.Round(amount * (mdrBp/10000m) * 100m, MidpointRounding.AwayFromZero) + fixedMinor;
            long net = row.gross - fee;
            csv.AppendLine($"{row.tenant},{row.merchant},{row.gross},{fee},{net},{row.count},{from:O},{to:O}");

            // Mark postings as settled by inserting into settlements table (idempotent via unique entry_id)
            var ins = new NpgsqlCommand(@"
               insert into settlements(entry_id, merchant, settled_at) 
               select p.entry_id, @m, now()
               from postings p join accounts a on a.account_id = p.account_id
               join journal_entries je on je.entry_id = p.entry_id
               where p.side='C' and a.account_id = @m and je.booking_date >= @from and je.booking_date < @to
               on conflict do nothing;", conn);
            ins.Parameters.AddWithValue("m", row.merchant);
            ins.Parameters.AddWithValue("from", from.UtcDateTime);
            ins.Parameters.AddWithValue("to", to.UtcDateTime);
            await ins.ExecuteNonQueryAsync(ct);
        }

        // Upload bank payout file
        var cont = _blob.GetBlobContainerClient(_options.BlobContainerName);
        await cont.CreateIfNotExistsAsync(cancellationToken: ct);
        var name = $"payout_{to:yyyyMMdd}.csv";
        await cont.UploadBlobAsync(name, new BinaryData(csv.ToString()), ct);
        
        _log.LogInformation("Settlement completed: {FileName} with {RowCount} merchants", name, rows.Count);
    }
}
