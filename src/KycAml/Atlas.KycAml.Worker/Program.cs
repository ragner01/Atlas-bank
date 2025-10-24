using Atlas.Risk.Domain.Rules;
using Atlas.KycAml.Worker;
using Atlas.KycAml.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var b = Host.CreateApplicationBuilder(args);
b.Services.AddSingleton<IRiskRuleEngine, RiskRuleEngine>();
b.Services.AddDbContext<CasesDbContext>(o => o.UseNpgsql(b.Configuration.GetConnectionString("Cases")!));
b.Services.AddHostedService<Worker>();
b.Build().Run();
