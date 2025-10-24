using Yarp.ReverseProxy;
var b = WebApplication.CreateBuilder(args);
b.Services.AddReverseProxy().LoadFromConfig(b.Configuration.GetSection("ReverseProxy"));
var app = b.Build();
app.MapGet("/health", () => Results.Ok());
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());
app.MapReverseProxy();
app.Run();
