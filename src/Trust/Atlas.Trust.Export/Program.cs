using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using CsvHelper;
using Npgsql;

var b = Host.CreateApplicationBuilder(args);

// Config - NO HARDCODED FALLBACKS
var blobConnectionString = Environment.GetEnvironmentVariable("BLOB_CONN") ?? 
                          throw new InvalidOperationException("BLOB_CONN environment variable is required");
var blob = new BlobServiceClient(blobConnectionString);

string containerName = Environment.GetEnvironmentVariable("OPEN_DATA_CONTAINER") ?? 
                      throw new InvalidOperationException("OPEN_DATA_CONTAINER environment variable is required");
string ledgerConn = Environment.GetEnvironmentVariable("LEDGER_CONN") ?? 
                   throw new InvalidOperationException("LEDGER_CONN environment variable is required");
int everyMinutes = int.TryParse(Environment.GetEnvironmentVariable("EXPORT_EVERY_MIN"), out var x) ? x : 60*24*7; // default weekly

await new Runner(blob, containerName, ledgerConn, TimeSpan.FromMinutes(everyMinutes)).RunAsync();

public sealed class Runner
{
    private readonly BlobServiceClient _blob;
    private readonly string _container;
    private readonly string _conn;
    private readonly TimeSpan _interval;

    public Runner(BlobServiceClient blob, string container, string conn, TimeSpan interval)
    { _blob = blob; _container = container; _conn = conn; _interval = interval; }

    public async Task RunAsync()
    {
        var cont = _blob.GetBlobContainerClient(_container);
        await cont.CreateIfNotExistsAsync();

        while (true)
        {
            try
            {
                var (csvBytes, jsonBytes, nameBase) = await BuildFilesAsync();
                await cont.UploadBlobAsync($"{nameBase}.csv", new BinaryData(csvBytes));
                await cont.UploadBlobAsync($"{nameBase}.json", new BinaryData(jsonBytes));

                // update index manifest
                var indexBlob = cont.GetBlobClient("index.json");
                var list = new List<object>();
                await foreach (var b in cont.GetBlobsAsync())
                    list.Add(new { name = b.Name, url = $"/opendata/{b.Name}", size = b.Properties.ContentLength ?? 0 });
                await indexBlob.UploadAsync(new BinaryData(JsonSerializer.Serialize(list)), overwrite: true);
            }
            catch { /* swallow; this is a best-effort exporter */ }

            await Task.Delay(_interval);
        }
    }

    private async Task<(byte[] csv, byte[] json, string nameBase)> BuildFilesAsync()
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        // Minimal synthetic dataset: merchant daily aggregates & trust band
        var from = DateTime.UtcNow.Date.AddDays(-7);
        var to = DateTime.UtcNow.Date;
        var sql = @"
            with tx as (
              select a.account_id as merchant, sum(case when p.side='C' then p.amount_minor else 0 end) as credited_minor, count(*) filter (where p.side='C') as tx_count
              from postings p join accounts a on a.account_id=p.account_id
              join journal_entries je on je.entry_id = p.entry_id
              where a.account_id like 'merchant::%' and je.booking_date>=@from and je.booking_date<@to
              group by a.account_id)
            select merchant, credited_minor, tx_count
            from tx
            order by credited_minor desc";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        var rows = new List<(string merchant,long minor,int count)>();
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
                rows.Add((r.GetString(0), r.GetInt64(1), r.GetInt32(2)));
        }

        // CSV
        var csvSb = new StringBuilder();
        using (var writer = new StringWriter(csvSb))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteField("merchant"); csv.WriteField("credited_minor"); csv.WriteField("tx_count"); csv.WriteField("band");
            csv.NextRecord();
            foreach (var row in rows)
            {
                var band = row.minor switch { > 10_000_000_00 => "EXCELLENT", > 2_000_000_00 => "GOOD", > 200_000_00 => "FAIR", _ => "RISKY" };
                csv.WriteField(row.merchant); csv.WriteField(row.minor); csv.WriteField(row.count); csv.WriteField(band);
                csv.NextRecord();
            }
        }
        var csvBytes = Encoding.UTF8.GetBytes(csvSb.ToString());

        // JSON
        var json = rows.Select(row => new {
            merchant = row.merchant, credited_minor = row.minor, tx_count = row.count,
            band = row.minor switch { > 10_000_000_00 => "EXCELLENT", > 2_000_000_00 => "GOOD", > 200_000_00 => "FAIR", _ => "RISKY" }
        }).ToArray();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(json));
        var nameBase = $"trust_week_{DateTime.UtcNow:yyyyMMdd}";
        return (csvBytes, jsonBytes, nameBase);
    }
}

