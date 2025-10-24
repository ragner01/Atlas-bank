using Atlas.Risk.Domain.Rules;
using Atlas.KycAml.Worker;
using Atlas.KycAml.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;

var b = Host.CreateApplicationBuilder(args);
b.Services.AddSingleton<IRiskRuleEngine, RiskRuleEngine>();
b.Services.AddHttpClient<IFeatureClient, HttpFeatureClient>(c =>
{
    var baseUrl = Environment.GetEnvironmentVariable("FEATURES_API") ?? "http://riskfeatures:5301";
    c.BaseAddress = new Uri(baseUrl);
});
b.Services.AddDbContext<CasesDbContext>(o => o.UseNpgsql(b.Configuration.GetConnectionString("Cases")!));
b.Services.AddHostedService<Worker>();
b.Build().Run();
