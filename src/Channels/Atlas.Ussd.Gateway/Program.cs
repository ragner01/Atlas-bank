#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Atlas.Ussd.Gateway.Security;
using Atlas.Ussd.Gateway;
using Atlas.Ussd.Gateway.Resilience;
using Polly;

// Configure Serilog for structured logging with security considerations
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Ussd.Gateway")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/ussd-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank USSD Gateway Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Configure options
    b.Services.Configure<UssdGatewayOptions>(b.Configuration.GetSection(UssdGatewayOptions.SectionName));
    b.Services.AddSingleton<IValidateOptions<UssdGatewayOptions>, UssdGatewayOptionsValidator>();

    // Add rate limiting
    b.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("UssdPolicy", opt =>
        {
            opt.PermitLimit = 10;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 5;
        });
    });

    // Add Redis with proper configuration
    b.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    {
        var options = provider.GetRequiredService<IOptions<UssdGatewayOptions>>().Value;
        var config = ConfigurationOptions.Parse(options.RedisConnectionString);
        config.AbortOnConnectFail = false;
        config.ConnectRetry = 3;
        config.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(config);
    });

    // Add resilient HTTP client
    b.Services.AddHttpClient<ResilientHttpClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "AtlasBank-USSD-Gateway/1.0");
    });

    // Add global exception handling
    b.Services.AddScoped<GlobalExceptionMiddleware>();

    b.Services.AddOpenApi(); 
    b.Services.AddEndpointsApiExplorer();

    var app = b.Build(); 
    app.MapOpenApi();

    // Add security headers middleware
    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        ctx.Response.Headers.Add("X-Frame-Options", "DENY");
        ctx.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        ctx.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    // Add rate limiting
    app.UseRateLimiter();

    // Add global exception handling
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // Add request/response logging middleware with security considerations
    app.Use(async (ctx, next) =>
    {
        var correlationId = Guid.NewGuid().ToString();
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RemoteIP", ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"))
        {
            Log.Information("Incoming request: {Method} {Path} from {RemoteIP}", 
                ctx.Request.Method, ctx.Request.Path, ctx.Connection.RemoteIpAddress?.ToString());
            
            await next();
            
            Log.Information("Outgoing response: {StatusCode} for {Method} {Path}", 
                ctx.Response.StatusCode, ctx.Request.Method, ctx.Request.Path);
        }
    });

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.Ussd.Gateway", timestamp = DateTime.UtcNow }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

    /*
       Simulated USSD Aggregator callback shape (common pattern):
       POST /ussd with fields: sessionId, msisdn, text, newSession(true/false)
       We persist session state in Redis and render simple menus.

       Menu flows:
       1) 1 Balance
       2) 2 Send Money
       3) 3 Cash-out (Agent)
       4) 4 Cash-in (Agent)
       5) 5 Change PIN
    */
    app.MapPost("/ussd", async (
        [FromServices] IConnectionMultiplexer mux, 
        [FromServices] IOptions<UssdGatewayOptions> options,
        [FromServices] ResilientHttpClient httpClient,
        HttpRequest request) =>
    {
        var gatewayOptions = options.Value;
        
        // Extract and validate form parameters
        var sessionId = InputValidator.SanitizeInput(request.Form["sessionId"].FirstOrDefault() ?? "");
        var msisdn = InputValidator.SanitizeInput(request.Form["msisdn"].FirstOrDefault() ?? "");
        var text = InputValidator.SanitizeInput(request.Form["text"].FirstOrDefault() ?? "");
        var newSession = bool.TryParse(request.Form["newSession"].FirstOrDefault(), out var ns) && ns;

        // Validate required fields
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(msisdn))
        {
            Log.Warning("Invalid USSD request: missing sessionId or msisdn");
            return Results.BadRequest("Invalid request parameters");
        }

        // Validate MSISDN format
        if (!InputValidator.IsValidMsisdn(msisdn))
        {
            Log.Warning("Invalid MSISDN format: {Msisdn}", msisdn);
            return Results.BadRequest("Invalid MSISDN format");
        }

        using (LogContext.PushProperty("SessionId", sessionId))
        using (LogContext.PushProperty("Msisdn", msisdn))
        using (LogContext.PushProperty("Text", text))
        using (LogContext.PushProperty("NewSession", newSession))
        {
            Log.Information("Processing USSD request: SessionId={SessionId}, Msisdn={Msisdn}, Text={Text}, NewSession={NewSession}", 
                sessionId, msisdn, text, newSession);

            try
            {
                var db = mux.GetDatabase();
                var key = $"ussd:{sessionId}";
                
                if (newSession) 
                {
                    await db.KeyDeleteAsync(key);
                    Log.Information("Cleared session state for new session: {SessionId}", sessionId);
                }

                var state = await db.StringGetAsync(key);
                var ctx = state.IsNullOrEmpty ? new UssdState { Step = "root", Msisdn = msisdn } : JsonSerializer.Deserialize<UssdState>(state!)!;

                string reply;
                switch (ctx.Step)
                {
                    case "root":
                        reply = "CON AtlasBank\n1 Balance\n2 Send Money\n3 Cash-out\n4 Cash-in\n5 Change PIN";
                        ctx.Step = "root-choose";
                        Log.Information("Displaying main menu for session: {SessionId}", sessionId);
                        break;

                    case "root-choose":
                        switch (text.Trim())
                        {
                            case "1": 
                                ctx.Step = "bal-pin"; 
                                reply = "CON Enter PIN:"; 
                                Log.Information("User selected Balance option: {SessionId}", sessionId);
                                break;
                            case "2": 
                                ctx.Step = "send-acc"; 
                                reply = "CON Enter destination account ID:"; 
                                Log.Information("User selected Send Money option: {SessionId}", sessionId);
                                break;
                            case "3": 
                                ctx.Step = "cashout-amt"; 
                                reply = "CON Enter amount (minor):"; 
                                Log.Information("User selected Cash-out option: {SessionId}", sessionId);
                                break;
                            case "4": 
                                ctx.Step = "cashin-amt"; 
                                reply = "CON Enter amount (minor):"; 
                                Log.Information("User selected Cash-in option: {SessionId}", sessionId);
                                break;
                            case "5": 
                                ctx.Step = "pin-old"; 
                                reply = "CON Enter current PIN:"; 
                                Log.Information("User selected Change PIN option: {SessionId}", sessionId);
                                break;
                            default: 
                                reply = "CON Invalid. Choose 1-5"; 
                                Log.Warning("Invalid menu selection: {Text} for session: {SessionId}", text, sessionId);
                                break;
                        }
                        break;

                    case "bal-pin":
                        ctx.Temp["pin"] = text.Trim();
                        ctx.Step = "bal-fetch";
                        Log.Information("PIN entered for balance check: {SessionId}", sessionId);
                        
                        // call balance API (Payments/Ledger) – in USSD we only show NGN by default
                        try
                        {
                            var balanceUrl = $"{gatewayOptions.LedgerBaseUrl}/ledger/accounts/{Uri.EscapeDataString($"msisdn::{msisdn}")}/balance/global?currency=NGN";
                            var res = await httpClient.GetAsync(balanceUrl);
                            var body = await res.Content.ReadAsStringAsync();
                            reply = res.IsSuccessStatusCode ? $"END Balance: {body}" : "END Unable to fetch balance";
                            Log.Information("Balance check result for {Msisdn}: {Status}", msisdn, res.IsSuccessStatusCode ? "Success" : "Failed");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error fetching balance for {Msisdn}", msisdn);
                            reply = "END Unable to fetch balance";
                        }
                        break;

                    case "send-acc":
                        ctx.Temp["dest"] = text.Trim();
                        ctx.Step = "send-amt"; 
                        reply = "CON Enter amount (minor):";
                        Log.Information("Destination account entered: {Dest} for session: {SessionId}", text.Trim(), sessionId);
                        break;

                    case "send-amt":
                        ctx.Temp["amt"] = text.Trim();
                        ctx.Step = "send-pin"; 
                        reply = "CON Enter PIN:";
                        Log.Information("Amount entered: {Amount} for session: {SessionId}", text.Trim(), sessionId);
                        break;

                    case "send-pin":
                        ctx.Temp["pin"] = text.Trim();
                        Log.Information("PIN entered for transfer: {SessionId}", sessionId);
                        
                        // fire off payment via Payments fast path (risk+limits already exist in prior phases)
                        try
                        {
                            var src = $"msisdn::{msisdn}";
                            var dst = ctx.Temp["dest"]!;
                            var amt = long.Parse(ctx.Temp["amt"]!);
                            
                            // Validate amount against limits
                            if (amt > gatewayOptions.MaxTransferAmountMinor)
                            {
                                reply = "END Amount exceeds maximum limit";
                                Log.Warning("Transfer amount {Amount} exceeds limit {Limit} for {Msisdn}", 
                                    amt, gatewayOptions.MaxTransferAmountMinor, msisdn);
                                break;
                            }

                            var transferUrl = $"{gatewayOptions.PaymentsBaseUrl}/payments/transfers/with-risk";
                            var transferData = JsonSerializer.Serialize(new { 
                                SourceAccountId = src, 
                                DestinationAccountId = dst, 
                                Minor = amt, 
                                Currency = "NGN", 
                                Narration = "USSD Send" 
                            });
                            
                            var content = new StringContent(transferData, System.Text.Encoding.UTF8, "application/json");
                            var res = await httpClient.PostAsync(transferUrl, content);
                            var ok = res.IsSuccessStatusCode;
                            reply = ok ? "END Transfer submitted" : "END Transfer rejected";
                            Log.Information("Transfer result for {Msisdn}: {Status}", msisdn, ok ? "Success" : "Failed");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error processing transfer for {Msisdn}", msisdn);
                            reply = "END Transfer failed";
                        }
                        break;

                    case "cashout-amt":
                        ctx.Temp["amt"] = text.Trim();
                        ctx.Step = "cashout-agent"; 
                        reply = "CON Enter Agent Code:";
                        Log.Information("Cash-out amount entered: {Amount} for session: {SessionId}", text.Trim(), sessionId);
                        break;

                    case "cashout-agent":
                        ctx.Temp["agent"] = text.Trim();
                        ctx.Step = "cashout-pin"; 
                        reply = "CON Enter PIN:";
                        Log.Information("Agent code entered: {Agent} for session: {SessionId}", text.Trim(), sessionId);
                        break;

                    case "cashout-pin":
                        Log.Information("PIN entered for cash-out: {SessionId}", sessionId);
                        
                        // create withdrawal intent for agent network service
                        try
                        {
                            var amt = long.Parse(ctx.Temp["amt"]!);
                            var agent = ctx.Temp["agent"]!;
                            using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("AGENT_BASE") ?? "http://agentnet:5621") };
                            var res = await http.PostAsync($"/agent/withdraw/intent?msisdn={Uri.EscapeDataString(msisdn)}&agent={Uri.EscapeDataString(agent)}&minor={amt}&currency=NGN",
                                content: null);
                            reply = res.IsSuccessStatusCode ? "END Withdrawal code sent to agent" : "END Could not create withdrawal intent";
                            Log.Information("Cash-out intent result for {Msisdn}: {Status}", msisdn, res.IsSuccessStatusCode ? "Success" : "Failed");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error creating cash-out intent for {Msisdn}", msisdn);
                            reply = "END Could not create withdrawal intent";
                        }
                        break;

                    case "cashin-amt":
                        ctx.Temp["amt"] = text.Trim();
                        ctx.Step = "cashin-agent"; 
                        reply = "CON Enter Agent Code:";
                        Log.Information("Cash-in amount entered: {Amount} for session: {SessionId}", text.Trim(), sessionId);
                        break;

                    case "cashin-agent":
                        ctx.Temp["agent"] = text.Trim();
                        ctx.Step = "cashin-pin"; 
                        reply = "CON Enter PIN:";
                        Log.Information("Agent code entered for cash-in: {Agent} for session: {SessionId}", text.Trim(), sessionId);
                        break;

                    case "cashin-pin":
                        Log.Information("PIN entered for cash-in: {SessionId}", sessionId);
                        
                        try
                        {
                            var amt = long.Parse(ctx.Temp["amt"]!);
                            var agent = ctx.Temp["agent"]!;
                            using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("AGENT_BASE") ?? "http://agentnet:5621") };
                            var res = await http.PostAsync($"/agent/cashin/intent?msisdn={Uri.EscapeDataString(msisdn)}&agent={Uri.EscapeDataString(agent)}&minor={amt}&currency=NGN",
                                content: null);
                            reply = res.IsSuccessStatusCode ? "END Deposit request sent to agent" : "END Could not create deposit intent";
                            Log.Information("Cash-in intent result for {Msisdn}: {Status}", msisdn, res.IsSuccessStatusCode ? "Success" : "Failed");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error creating cash-in intent for {Msisdn}", msisdn);
                            reply = "END Could not create deposit intent";
                        }
                        break;

                    case "pin-old":
                        ctx.Temp["pin_old"] = text.Trim();
                        ctx.Step = "pin-new"; 
                        reply = "CON Enter new PIN:";
                        Log.Information("Current PIN entered for PIN change: {SessionId}", sessionId);
                        break;
                        
                    case "pin-new":
                        ctx.Temp["pin_new"] = text.Trim();
                        ctx.Step = "pin-save";
                        Log.Information("New PIN entered for PIN change: {SessionId}", sessionId);
                        
                        // store hashed PIN (demo; in prod enforce KDF & HSM)
                        try
                        {
                            var hash = BCrypt.Net.BCrypt.HashPassword(ctx.Temp["pin_new"]!);
                            await db.StringSetAsync($"pin:{msisdn}", hash);
                            reply = "END PIN updated";
                            Log.Information("PIN updated successfully for {Msisdn}", msisdn);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error updating PIN for {Msisdn}", msisdn);
                            reply = "END PIN update failed";
                        }
                        break;

                    default:
                        reply = "END Error";
                        Log.Warning("Unknown step: {Step} for session: {SessionId}", ctx.Step, sessionId);
                        break;
                }

                await db.StringSetAsync(key, JsonSerializer.Serialize(ctx), TimeSpan.FromMinutes(3));
                Log.Information("Session state updated for: {SessionId}, Reply: {Reply}", sessionId, reply);
                
                return Results.Content(reply, "text/plain");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing USSD request for session: {SessionId}", sessionId);
                return Results.Content("END System error", "text/plain");
            }
        }
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public sealed class UssdState 
{ 
    public string Step { get; set; } = "root"; 
    public string Msisdn { get; set; } = ""; 
    public Dictionary<string,string> Temp { get; set; } = new(); 
}
