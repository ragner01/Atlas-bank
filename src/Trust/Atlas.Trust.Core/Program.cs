using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Neo4j.Driver;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

var b = WebApplication.CreateBuilder(args);
b.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
b.Services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://neo4j:7687",
    AuthTokens.Basic("neo4j", Environment.GetEnvironmentVariable("NEO4J_PASS") ?? "neo4jpass")));
b.Services.AddSingleton(new NpgsqlDataSourceBuilder(Environment.GetEnvironmentVariable("LEDGER_CONN") ?? "Host=postgres;Database=atlas;Username=postgres;Password=postgres").Build());

var app = b.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// 1️⃣ Realtime trust score (actors, merchants, users, devices)
app.MapGet("/trust/score", async (
    string entityId,
    [FromServices] IConnectionMultiplexer redis,
    [FromServices] IDriver neo,
    [FromServices] NpgsqlDataSource ds) =>
{
    // Aggregate trust from 3 layers:
    // - Behavior (transactions, disputes)
    // - Network (Neo4j relationships)
    // - Community feedback (Redis weighted score)
    double txHealth = await TxHealthAsync(ds, entityId);
    double graphHealth = await GraphHealthAsync(neo, entityId);
    double feedback = await FeedbackAsync(redis, entityId);

    double score = 0.5 * txHealth + 0.3 * graphHealth + 0.2 * feedback;
    string band = score switch { >= 0.8 => "EXCELLENT", >= 0.6 => "GOOD", >= 0.4 => "FAIR", _ => "RISKY" };
    return Results.Ok(new { entityId, score, band });

    static async Task<double> TxHealthAsync(NpgsqlDataSource ds, string eid)
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("select count(*) filter (where status='FAILED') as f, count(*) as t from transactions where actor_id=@a", conn);
        cmd.Parameters.AddWithValue("a", eid);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return 0.8;
        long f = r.GetInt64(0), t = r.GetInt64(1);
        if (t == 0) return 0.8;
        double failRate = (double)f / t;
        return Math.Max(0, 1 - failRate * 3);
    }

    static async Task<double> GraphHealthAsync(IDriver neo, string eid)
    {
        await using var s = neo.AsyncSession();
        var cy = @"MATCH (n {id:$eid})-[r]->(m) RETURN count(r) as rels";
        var res = await (await s.RunAsync(cy, new { eid })).SingleAsync();
        long rels = res["rels"].As<long>();
        return rels switch { > 50 => 1.0, > 20 => 0.9, > 5 => 0.7, _ => 0.5 };
    }

    static async Task<double> FeedbackAsync(IConnectionMultiplexer r, string eid)
    {
        var db = r.GetDatabase();
        var v = await db.SortedSetScoreAsync("trust:feedback", eid);
        if (v is null) return 0.6;
        return Math.Clamp(v.Value, 0, 1);
    }
});

// 2️⃣ Feedback API — community ratings (customers → merchants/users)
app.MapPost("/trust/feedback", async (IConnectionMultiplexer redis, string from, string to, int rating) =>
{
    rating = Math.Clamp(rating, 1, 5);
    var db = redis.GetDatabase();
    double normalized = rating / 5.0;
    // rolling average
    var prev = await db.SortedSetScoreAsync("trust:feedback", to);
    var newScore = prev.HasValue ? (prev.Value * 0.9 + normalized * 0.1) : normalized;
    await db.SortedSetAddAsync("trust:feedback", to, newScore);
    return Results.Ok(new { to, newScore });
});

// 3️⃣ Transparency ledger digest API — cryptographic anchor
app.MapGet("/trust/transparency/digest", async ([FromServices] NpgsqlDataSource ds) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("select max(seq), encode(hash,'hex') from gl_audit", conn);
    await using var r = await cmd.ExecuteReaderAsync();
    return await r.ReadAsync() ? Results.Ok(new { seq = r.GetInt64(0), root = r.GetString(1) }) : Results.NoContent();
});

// 4️⃣ Proof API — verify record hash against chain tip
app.MapPost("/trust/proof", async ([FromServices] NpgsqlDataSource ds, [FromBody] ProofReq req) =>
{
    await using var conn = await ds.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("select encode(hash,'hex') from gl_audit where seq=@s", conn);
    cmd.Parameters.AddWithValue("s", req.Seq);
    var got = (string?)await cmd.ExecuteScalarAsync();
    if (got is null) return Results.NotFound();
    bool match = string.Equals(got, req.HashHex, StringComparison.OrdinalIgnoreCase);
    return Results.Ok(new { req.Seq, req.HashHex, match });
});

app.Run();

record ProofReq(long Seq, string HashHex);
