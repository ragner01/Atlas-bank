using StackExchange.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;

namespace Atlas.Ledger.AutoHeal;

/// <summary>
/// Background service that automatically reconciles drift between regions
/// by applying compensating entries when drift is within acceptable limits
/// </summary>
public sealed class AutoHealer : BackgroundService
{
    private readonly ILogger<AutoHealer> _log;
    private readonly IConnectionMultiplexer _redis;
    private readonly HttpClient _http = new();
    
    /// <summary>
    /// Initializes a new instance of the AutoHealer
    /// </summary>
    /// <param name="log">Logger instance</param>
    /// <param name="redis">Redis connection multiplexer</param>
    public AutoHealer(ILogger<AutoHealer> log, IConnectionMultiplexer redis) { _log = log; _redis = redis; }

    /// <summary>
    /// Executes the background service to monitor and heal drift
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service</param>
    /// <returns>A task representing the background service execution</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));

    /// <summary>
    /// Main processing loop for monitoring drift and applying heals
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    private void Loop(CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var interval = TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("HEAL_RATE_SECONDS"), out var s) ? s : 10);
        var maxDelta = long.TryParse(Environment.GetEnvironmentVariable("HEAL_MAX_ABS_MINOR"), out var m) ? m : 2_000_00L; // max 2,000.00
        var suspense = Environment.GetEnvironmentVariable("HEAL_SUSPENSE_ACCOUNT") ?? "suspense";
        var tenant = Environment.GetEnvironmentVariable("HEAL_TENANT") ?? "tnt_demo";
        var regionTarget = Environment.GetEnvironmentVariable("HEAL_REGION_TARGET") ?? "regionA"; // apply heals to this region's ledger API
        var regionUrl = Environment.GetEnvironmentVariable("HEAL_REGION_URL") ?? "http://ledgerapi_regionA:6181";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // scan drift keys: drift:{tenant}:{account}:{ccy}
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                foreach (var key in server.Keys(pattern: $"drift:{tenant}:*"))
                {
                    var he = db.HashGetAll(key);
                    var acct = key.ToString().Split(':')[2];
                    var ccy  = key.ToString().Split(':')[3];
                    long posA = Get(he, "pos:regionA"), negA = Get(he, "neg:regionA");
                    long posB = Get(he, "pos:regionB"), negB = Get(he, "neg:regionB");
                    long balA = posA - negA, balB = posB - negB;
                    long diff = balA - balB; // if positive, A> B

                    if (diff == 0) continue;
                    var abs = Math.Abs(diff);
                    if (abs > maxDelta) { _log.LogWarning("Drift too large to auto-heal now: {Key} {Diff}", key, diff); continue; }

                    // Wait until watermarks indicate both regions are caught up (global watermark recent)
                    var g = (long?)db.StringGet($"wm:{tenant}:global") ?? 0L;
                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (nowMs - g > 5_000) { _log.LogInformation("Skipping heal; global watermark stale"); continue; }

                    // Build compensating entry on the smaller region by moving funds to/from suspense
                    // If balA > balB (diff>0), we need to CREDIT account in B (increase) or DEBIT in A (decrease). We choose to fix the smaller side.
                    var fixRegion = diff > 0 ? "regionB" : "regionA";
                    var fixUrl = fixRegion == "regionA" ? regionUrl : (Environment.GetEnvironmentVariable("HEAL_REGION_URL_B") ?? "http://ledgerapi_regionB:7181");

                    var amountMinor = abs;
                    var keyIdem = $"heal::{fixRegion}::{tenant}::{acct}::{ccy}::{g}";
                    var src = diff > 0 ? suspense : acct; // A>B => add to B (credit B) => src=suspense,dst=acct
                    var dst = diff > 0 ? acct : suspense;

                    var uri = $"{fixUrl}/ledger/fast-transfer?sourceAccountId={Uri.EscapeDataString(src)}&destinationAccountId={Uri.EscapeDataString(dst)}&minor={amountMinor}&currency={ccy}&narration={Uri.EscapeDataString($"auto-heal({regionTarget})")}&tenantId={tenant}";
                    var req = new HttpRequestMessage(HttpMethod.Post, uri);
                    req.Headers.Add("Idempotency-Key", keyIdem);

                    var resp = _http.Send(req, ct);
                    if ((int)resp.StatusCode == 202 || (int)resp.StatusCode == 200)
                    {
                        _log.LogInformation("Auto-heal applied for {Key} diff={Diff} via {FixRegion}", key, diff, fixRegion);
                        // Mark balanced by incrementing the opposite direction in drift hash to zero out
                        if (diff > 0) db.HashIncrement(key, $"pos:{fixRegion}", amountMinor);
                        else db.HashIncrement(key, $"neg:{fixRegion}", amountMinor);
                    }
                    else
                    {
                        _log.LogWarning("Auto-heal request failed {Status}: {Msg}", (int)resp.StatusCode, resp.ReasonPhrase);
                    }
                }
            }
            catch (Exception ex) { _log.LogWarning(ex, "auto-heal loop error"); }
            Thread.Sleep(interval);
        }

        static long Get(HashEntry[] arr, string n) => arr.FirstOrDefault(e => e.Name == n).Value.TryParse(out long v) ? v : 0L;
    }
}
