using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Atlas.Risk.Domain.Rules;

public sealed record VelocityRule(string Id, string Field, int WindowSeconds, long ThresholdMinor, string Currency, string Action);

public sealed class RuleSet 
{ 
    public List<VelocityRule> Velocity { get; set; } = new(); 
}

public interface IRiskRuleEngine 
{
    (bool hit, string action, string reason) Evaluate(RuleSet rules, IEnumerable<long> amountsMinor, string currency);
}

public sealed class RiskRuleEngine : IRiskRuleEngine 
{
    public (bool, string, string) Evaluate(RuleSet rules, IEnumerable<long> amountsMinor, string currency) 
    {
        foreach (var r in rules.Velocity.Where(r => r.Currency == currency)) 
        {
            var sum = amountsMinor.Sum();
            if (sum >= r.ThresholdMinor)
                return (true, r.Action, $"Velocity {sum} >= {r.ThresholdMinor}");
        }
        return (false, "ALLOW", "no hit");
    }
    
    public static RuleSet LoadFromYaml(string yaml)
        => new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<RuleSet>(yaml);
}
