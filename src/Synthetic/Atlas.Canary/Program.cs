using System.Net.Http.Json;
using Serilog;
using Serilog.Events;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Canary")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/canary-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Canary Service");

    // Simple console application - no need for Host

    var baseUrl = Environment.GetEnvironmentVariable("PAYMENTS_BASE") ?? "http://paymentsapi:5191";
    var tenant = Environment.GetEnvironmentVariable("TENANT") ?? "tnt_demo";
    var sleepMs = int.TryParse(Environment.GetEnvironmentVariable("CANARY_MS"), out var ms) ? ms : 15000;
    var client = new HttpClient();

    Log.Information("Canary configuration: BaseUrl={BaseUrl}, Tenant={Tenant}, Interval={Interval}ms", 
        baseUrl, tenant, sleepMs);

    var testCases = new[]
    {
        new { 
            Name = "Small Charge", 
            Amount = 1000, 
            Currency = "NGN", 
            CardToken = "tok_demo", 
            MerchantId = "m-123", 
            Mcc = "5411" 
        },
        new { 
            Name = "Medium Charge", 
            Amount = 50000, 
            Currency = "NGN", 
            CardToken = "tok_demo", 
            MerchantId = "m-123", 
            Mcc = "5411" 
        },
        new { 
            Name = "Large Charge", 
            Amount = 500000, 
            Currency = "NGN", 
            CardToken = "tok_demo", 
            MerchantId = "m-123", 
            Mcc = "5411" 
        },
        new { 
            Name = "Blocked MCC", 
            Amount = 10000, 
            Currency = "NGN", 
            CardToken = "tok_demo", 
            MerchantId = "m-123", 
            Mcc = "4829" 
        }
    };

    var testIndex = 0;
    var consecutiveFailures = 0;
    var maxConsecutiveFailures = 5;

    while (true)
    {
        try
        {
            var testCase = testCases[testIndex % testCases.Length];
            testIndex++;

            Log.Information("Running canary test: {TestName}", testCase.Name);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{baseUrl}/payments/cnp/charge/enforced?amountMinor={testCase.Amount}&currency={testCase.Currency}&cardToken={testCase.CardToken}&merchantId={testCase.MerchantId}&mcc={testCase.Mcc}");
            
            request.Headers.Add("X-Tenant-Id", tenant);
            request.Headers.Add("X-Device-Id", "canary-device-001");
            request.Headers.Add("X-Ip", "192.168.1.100");
            request.Headers.Add("X-Local-Time", DateTime.UtcNow.ToString("O"));

            var response = await client.SendAsync(request);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync();
            var limitReview = response.Headers.GetValues("X-Limit-Review").FirstOrDefault();
            var limitReason = response.Headers.GetValues("X-Limit-Reason").FirstOrDefault();

            Log.Information("Canary test completed: {TestName}, Status={StatusCode}, Duration={Duration}ms, LimitReview={LimitReview}, LimitReason={LimitReason}", 
                testCase.Name, response.StatusCode, stopwatch.ElapsedMilliseconds, limitReview, limitReason);

            // Check for expected behaviors
            if (testCase.Name == "Blocked MCC" && response.StatusCode != System.Net.HttpStatusCode.Forbidden)
            {
                Log.Warning("Expected blocked MCC to return 403, got {StatusCode}", response.StatusCode);
                consecutiveFailures++;
            }
            else if (testCase.Name != "Blocked MCC" && response.StatusCode != System.Net.HttpStatusCode.OK && response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                Log.Warning("Unexpected status code {StatusCode} for test {TestName}", response.StatusCode, testCase.Name);
                consecutiveFailures++;
            }
            else
            {
                consecutiveFailures = 0; // Reset on success
            }

            // Alert if too many consecutive failures
            if (consecutiveFailures >= maxConsecutiveFailures)
            {
                Log.Error("Canary detected {ConsecutiveFailures} consecutive failures - possible service degradation", consecutiveFailures);
                // In a real implementation, you would send alerts here
            }

            // Check response time
            if (stopwatch.ElapsedMilliseconds > 5000)
            {
                Log.Warning("Canary test took {Duration}ms - performance degradation detected", stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            consecutiveFailures++;
            Log.Error(ex, "Canary test failed: {Message}", ex.Message);
            
            if (consecutiveFailures >= maxConsecutiveFailures)
            {
                Log.Error("Canary detected {ConsecutiveFailures} consecutive failures - service may be down", consecutiveFailures);
            }
        }

        await Task.Delay(sleepMs);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Canary service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
