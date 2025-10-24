using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;

var b = WebApplication.CreateBuilder(args);
var app = b.Build();

string chConn = Environment.GetEnvironmentVariable("CLICKHOUSE_CONN")
    ?? "Host=clickhouse;Port=8123;Database=default;User=default;Password=";

// GET /features/velocity?tenant=tnt_demo&subject=acc_123&seconds=120&currency=NGN
app.MapGet("/features/velocity", async (string tenant, string subject, int seconds, string currency) =>
{
    // Expect a materialized table `ledger_events` with columns: ts DateTime64, tenant String, source String, dest String, minor Int64, currency String
    await using var conn = new ClickHouseConnection(chConn);
    await conn.OpenAsync();

    var sql = @"
        SELECT sum(minor) AS totalMinor
        FROM ledger_events
        WHERE tenant = {tenant:String}
          AND currency = {currency:String}
          AND (source = {subject:String} OR dest = {subject:String})
          AND ts >= now() - toIntervalSecond({seconds:Int32})";
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    cmd.AddParameter("tenant", tenant);
    cmd.AddParameter("currency", currency);
    cmd.AddParameter("subject", subject);
    cmd.AddParameter("seconds", seconds);

    var total = (long?)await cmd.ExecuteScalarAsync() ?? 0L;
    return Results.Ok(new { tenant, subject, seconds, currency, totalMinor = total });
});

app.Run();
