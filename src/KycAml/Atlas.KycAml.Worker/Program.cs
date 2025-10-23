using Atlas.Risk.Domain.Rules;
using Atlas.KycAml.Worker;

var b = Host.CreateApplicationBuilder(args);
b.Services.AddSingleton<IRiskRuleEngine, RiskRuleEngine>();
b.Services.AddHostedService<Worker>();
b.Build().Run();
