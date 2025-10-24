using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Atlas.Risk.Domain.Rules;

public sealed record VelocityRule(string Id, string Field, int WindowSeconds, long ThresholdMinor, string Currency, string Action);
public sealed class RuleSet
{
    public List<VelocityRule> Velocity { get; set; } = new();
    public FeatureConfig? Features { get; set; }
}
public sealed class FeatureConfig
{
    public string? FeatureServiceBase { get; set; }
    public string SubjectSelector { get; set; } = "source";
}

public interface IRiskRuleEngine
{
    (bool hit, string action, string reason) EvaluateLocal(RuleSet rules, IEnumerable<long> amountsMinor, string currency);
    Task<(bool hit, string action, string reason)> EvaluateWithFeaturesAsync(RuleSet rules, string tenant, string subject, string currency, IFeatureClient features, CancellationToken ct);
}

public sealed class RiskRuleEngine : IRiskRuleEngine
{
    public (bool, string, string) EvaluateLocal(RuleSet rules, IEnumerable<long> amountsMinor, string currency)
    {
        foreach (var r in rules.Velocity.Where(r => r.Currency == currency))
        {
            var sum = amountsMinor.Sum();
            if (sum >= r.ThresholdMinor)
                return (true, r.Action, $"Local velocity {sum} >= {r.ThresholdMinor}");
        }
        return (false, "ALLOW", "no local hit");
    }

    public async Task<(bool, string, string)> EvaluateWithFeaturesAsync(RuleSet rules, string tenant, string subject, string currency, IFeatureClient features, CancellationToken ct)
    {
        if (rules.Features is null || string.IsNullOrWhiteSpace(rules.Features.FeatureServiceBase))
            return (false, "ALLOW", "feature service not configured");

        foreach (var r in rules.Velocity.Where(r => r.Currency == currency))
        {
            var total = await features.GetVelocityAsync(tenant, subject, r.WindowSeconds, currency, ct);
            if (total >= r.ThresholdMinor)
                return (true, r.Action, $"Feature velocity {total} >= {r.ThresholdMinor} in {r.WindowSeconds}s");
        }
        return (false, "ALLOW", "no feature hit");
    }

    public static RuleSet LoadFromYaml(string yaml)
        => new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build().Deserialize<RuleSet>(yaml);
}