using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Events;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Trust.Portal")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/trust-portal-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Trust Portal");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Config - NO HARDCODED FALLBACKS
    string TRUST_CORE = Environment.GetEnvironmentVariable("TRUST_CORE") ?? 
                       throw new InvalidOperationException("TRUST_CORE environment variable is required");
    string REG_API_KEY = Environment.GetEnvironmentVariable("REGULATOR_API_KEY") ?? 
                        throw new InvalidOperationException("REGULATOR_API_KEY environment variable is required");
    string OPEN_DATA_CONTAINER = Environment.GetEnvironmentVariable("OPEN_DATA_CONTAINER") ?? 
                                throw new InvalidOperationException("OPEN_DATA_CONTAINER environment variable is required");

    Log.Information("Configuration loaded: Trust Core: {TrustCore}, Container: {Container}", 
        TRUST_CORE, OPEN_DATA_CONTAINER);

    // Services
    b.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("portal", c =>
        {
            c.PermitLimit = 200;
            c.Window = TimeSpan.FromSeconds(1);
            c.QueueLimit = 0;
        });
    });

    // Blob storage - NO HARDCODED FALLBACKS
    var blobConnectionString = Environment.GetEnvironmentVariable("BLOB_CONN") ?? 
                              throw new InvalidOperationException("BLOB_CONN environment variable is required");
    b.Services.AddSingleton(new BlobServiceClient(blobConnectionString));

    b.Services.AddHttpClient("trust", c => c.BaseAddress = new Uri(TRUST_CORE));

    var app = b.Build();
    app.UseRateLimiter();

    // Request/Response logging middleware
    app.Use(async (context, next) =>
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? 
                           Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        using var scope = Log.Logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method,
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("Request started: {Method} {Path}", context.Request.Method, context.Request.Path);

        await next();

        stopwatch.Stop();
        Log.Information("Request completed: {Method} {Path} {StatusCode} {ElapsedMs}ms", 
            context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    });

    // ----- Public Portal (static SPA-lite) -----
    app.MapGet("/", () => Results.Redirect("/portal"));

    app.MapGet("/portal", () => 
    {
        Log.Information("Portal page requested");
        return Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width,initial-scale=1'>
    <title>AtlasBank Trust Portal</title>
    <style>
        body { font-family: system-ui, sans-serif; margin: 0; background: #0b1220; color: #ecf2ff; }
        header { padding: 16px 24px; background: #121a2b; position: sticky; top: 0; box-shadow: 0 2px 10px rgba(0,0,0,.3); }
        main { max-width: 1100px; margin: 24px auto; padding: 0 16px; }
        .card { background: #141f36; border-radius: 16px; padding: 20px; margin-bottom: 16px; box-shadow: 0 8px 24px rgba(0,0,0,.25); }
        .row { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
        input, select, button { background: #0f1728; color: #e5edff; border: 1px solid #23325a; border-radius: 10px; padding: 10px 12px; }
        button { cursor: pointer; }
        .badge { display: inline-flex; align-items: center; gap: 8px; padding: 6px 10px; border-radius: 999px; font-weight: 600; }
        .ok { background: #063; padding: 6px 10px; border: 1px solid #0a5; }
        .mid { background: #554500; border: 1px solid #aa0; }
        .bad { background: #4a0000; border: 1px solid #a00; }
        .muted { color: #9fb3ff; }
        footer { opacity: .7; padding: 24px; text-align: center; }
        a { color: #9bd; }
        .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
    </style>
</head>
<body>
    <header><strong>AtlasBank • Trust Portal</strong></header>
    <main>
        <div class='card'>
            <h2>Check an entity's Trust Score</h2>
            <div class='row'>
                <div>
                    <label>Entity ID (merchant / account / device)</label><br/>
                    <input id='eid' placeholder='e.g. m-123 or acc_abc' />
                </div>
                <div>
                    <label>&nbsp;</label><br/>
                    <button onclick='check()'>Get Score</button>
                </div>
            </div>
            <div id='out' style='margin-top:12px'></div>
        </div>
        <div class='card'>
            <h3>Embeddable Badge</h3>
            <p class='muted'>Show a live trust badge on your site or checkout page.</p>
            <pre class='mono' id='badgeCode' style='overflow:auto; background:#0f1728; padding:12px; border-radius:10px'></pre>
        </div>
        <div class='card'>
            <h3>Open Data</h3>
            <p class='muted'>Download weekly trust snapshots for research and public transparency.</p>
            <ul id='dl'></ul>
        </div>
        <div class='card'>
            <h3>Transparency Digest</h3>
            <p class='muted'>Latest immutable audit tip (hash): <span id='digest' class='mono'>loading…</span></p>
        </div>
    </main>
    <footer>© AtlasBank • Transparency by design</footer>
    <script>
        async function check(){
            const eid = document.getElementById('eid').value.trim();
            if(!eid){ alert('Enter an entity ID'); return; }
            const res = await fetch('" + TRUST_CORE + @"/trust/score?entityId='+encodeURIComponent(eid));
            const data = await res.json();
            const band = data.band;
            const color = band==='EXCELLENT'?'ok':band==='GOOD'?'ok':band==='FAIR'?'mid':'bad';
            const out = document.getElementById('out');
            out.innerHTML = '<div class=\"badge '+color+'\">Score: '+(data.score*100).toFixed(0)+' — '+band+'</div>';
            document.getElementById('badgeCode').textContent = '<img alt=\"Atlas Trust\" src=\"'+location.origin+'/badge/'+encodeURIComponent(eid)+'.svg\" />';
        }
        async function digest(){
            const r = await fetch('" + TRUST_CORE + @"/trust/transparency/digest');
            if(r.ok){ const j = await r.json(); document.getElementById('digest').textContent = j.root + ' (seq '+j.seq+')'; }
        }
        async function listOpenData(){
            try{
                const r = await fetch('/opendata/index.json');
                if(!r.ok) return;
                const arr = await r.json();
                const ul = document.getElementById('dl'); ul.innerHTML='';
                arr.forEach(x=>{
                    const li=document.createElement('li');
                    li.innerHTML = '<a href=\"'+x.url+'\">'+x.name+'</a> • '+x.size;
                    ul.appendChild(li);
                });
            }catch{}
        }
        digest(); listOpenData();
    </script>
</body>
</html>
", "text/html");
    });

    // ----- SVG Trust Badge -----
    app.MapGet("/badge/{entityId}.svg", async (string entityId, IHttpClientFactory f) =>
    {
        Log.Information("Badge requested for entity: {EntityId}", entityId);
        
        try
        {
            var http = f.CreateClient("trust");
            var j = await http.GetFromJsonAsync<JsonElement>($"/trust/score?entityId={Uri.EscapeDataString(entityId)}");
            var score = j.GetProperty("score").GetDouble();
            var band = j.GetProperty("band").GetString() ?? "UNKNOWN";
            
            Log.Information("Badge generated for entity {EntityId}: Score {Score}, Band {Band}", 
                entityId, score, band);
            
            string color = band switch { "EXCELLENT" => "#14b8a6", "GOOD" => "#22c55e", "FAIR" => "#eab308", "RISKY" => "#ef4444", _ => "#94a3b8" };
            string pct = $"{Math.Round(score*100)}%";
            var svg = $@"<svg xmlns='http://www.w3.org/2000/svg' width='200' height='40' role='img' aria-label='Trust {pct}'>
  <rect rx='8' width='200' height='40' fill='#0b1220'/>
  <text x='14' y='25' fill='#9fb3ff' font-family='Segoe UI,Arial' font-size='12'>Atlas Trust</text>
  <rect x='110' y='9' rx='6' width='80' height='22' fill='{color}'/>
  <text x='150' y='25' text-anchor='middle' fill='#071018' font-family='Segoe UI,Arial' font-weight='700'>{pct}</text>
</svg>";
            return Results.Content(svg, "image/svg+xml");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate badge for entity {EntityId}", entityId);
            return Results.Problem("Failed to generate badge");
        }
    }).RequireRateLimiting("portal");

    // ----- Regulator API (API key + signed digest echo) -----
    app.MapGet("/regulator/v1/entities/{entityId}/trust", async (string entityId, HttpRequest req, IHttpClientFactory f) =>
    {
        Log.Information("Regulator API request for entity: {EntityId}", entityId);
        
        if (!req.Headers.TryGetValue("X-API-Key", out var k) || k != REG_API_KEY) 
        {
            Log.Warning("Unauthorized regulator API access attempt for entity {EntityId}", entityId);
            return Results.Unauthorized();
        }
        
        try
        {
            var http = f.CreateClient("trust");
            var j = await http.GetFromJsonAsync<JsonElement>($"/trust/score?entityId={Uri.EscapeDataString(entityId)}");
            // Add transparency digest to the response
            var d = await http.GetFromJsonAsync<JsonElement>("/trust/transparency/digest");
            var body = new { entityId, trust = j, digest = d };
            var bodyJson = JsonSerializer.Serialize(body);
            var sig = Sign(bodyJson, Environment.GetEnvironmentVariable("REGULATOR_API_SIG") ?? 
                           throw new InvalidOperationException("REGULATOR_API_SIG environment variable is required"));
            
            Log.Information("Regulator API response generated for entity {EntityId}", entityId);
            return Results.Json(new { data = body, signature = sig });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process regulator API request for entity {EntityId}", entityId);
            return Results.Problem("Failed to process request");
        }

        static string Sign(string content, string secret)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        }
    });

    // ----- Static Open Data index (populated by Export job) -----
    app.MapGet("/opendata/index.json", async (BlobServiceClient blob, CancellationToken ct) =>
    {
        Log.Information("Open data index requested");
        
        try
        {
            var list = new List<object>();
            var cont = blob.GetBlobContainerClient(OPEN_DATA_CONTAINER);
            await foreach (var b in cont.GetBlobsAsync(cancellationToken: ct))
            {
                list.Add(new { name = b.Name, url = $"/opendata/{b.Name}", size = b.Properties.ContentLength ?? 0 });
            }
            
            Log.Information("Open data index returned {Count} files", list.Count);
            return Results.Json(list);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve open data index");
            return Results.Problem("Failed to retrieve open data index");
        }
    });

    app.MapGet("/opendata/{name}", async (string name, BlobServiceClient blob, CancellationToken ct) =>
    {
        Log.Information("Open data file requested: {FileName}", name);
        
        try
        {
            var cont = blob.GetBlobContainerClient(OPEN_DATA_CONTAINER);
            var blobClient = cont.GetBlobClient(name);
            if (!await blobClient.ExistsAsync(ct)) 
            {
                Log.Warning("Open data file not found: {FileName}", name);
                return Results.NotFound();
            }
            
            var s = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
            Log.Information("Open data file served: {FileName}", name);
            return Results.Stream(s.Value.Content, s.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to serve open data file: {FileName}", name);
            return Results.Problem("Failed to serve file");
        }
    });

    Log.Information("AtlasBank Trust Portal configured and ready to start");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

