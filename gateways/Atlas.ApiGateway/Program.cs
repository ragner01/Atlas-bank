using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yarp.ReverseProxy;

var b = WebApplication.CreateBuilder(args);

b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = b.Configuration["Authentication:Authority"];
        o.Audience  = b.Configuration["Authentication:Audience"];
        o.RequireHttpsMetadata = bool.Parse(b.Configuration["Authentication:RequireHttpsMetadata"] ?? "true");
        o.TokenValidationParameters.ValidTypes = new[] { "at+jwt", "JWT" };
    });

b.Services.AddAuthorization(options =>
{
    options.AddPolicy("ScopeAccountsRead", p => p.RequireClaim("scope", "accounts.read"));
    options.AddPolicy("ScopePaymentsWrite", p => p.RequireClaim("scope", "payments.write"));
    options.AddPolicy("ScopeAmlRead", p => p.RequireClaim("scope", "aml.read"));
});

b.Services.AddReverseProxy().LoadFromConfig(b.Configuration.GetSection("ReverseProxy"));

var app = b.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapReverseProxy();

app.Run();
