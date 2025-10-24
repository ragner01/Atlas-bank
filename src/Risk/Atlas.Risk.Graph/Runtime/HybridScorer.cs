using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using StackExchange.Redis;

namespace Atlas.Risk.Graph.Runtime;

/// <summary>
/// Hybrid risk scorer that combines Redis blacklists, ONNX models, and heuristic rules
/// for comprehensive risk assessment
/// </summary>
public sealed class HybridScorer : IScorer, IDisposable
{
    private readonly InferenceSession? _onnx;
    private readonly IConnectionMultiplexer _redis;

    /// <summary>
    /// Initializes a new instance of the HybridScorer
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file</param>
    /// <param name="redis">Redis connection multiplexer for blacklist checks</param>
    public HybridScorer(string modelPath, IConnectionMultiplexer redis)
    {
        _redis = redis;
        try { _onnx = new InferenceSession(File.ReadAllBytes(modelPath)); } catch { _onnx = null; }
    }

    /// <summary>
    /// Scores risk using hybrid approach: Redis blacklists, ONNX model, or heuristic rules
    /// </summary>
    /// <param name="f">Feature vector for risk assessment</param>
    /// <returns>Tuple containing score (0.0-1.0), decision (BLOCK/REVIEW/ALLOW), and reason</returns>
    public (double score, string decision, string reason) Score(float[] f)
    {
        // Blacklist checks (hard denies)
        var db = _redis.GetDatabase();
        bool isHardHit = false;
        string hardReason = "";
        // (We expect caller to have set these keys if needed)
        // Example sets: risk:blacklist:ip, risk:blacklist:device, risk:blacklist:merchant (SISMEMBER)

        if (_ctx?.Ip is string ip && db.SetContains("risk:blacklist:ip", ip)) { isHardHit = true; hardReason = "ip_blacklist"; }
        if (_ctx?.DeviceId is string dev && db.SetContains("risk:blacklist:device", dev)) { isHardHit = true; hardReason = "device_blacklist"; }
        if (_ctx?.MerchantId is string mer && db.SetContains("risk:blacklist:merchant", mer)) { isHardHit = true; hardReason = "merchant_blacklist"; }
        if (isHardHit) return (0.99, "BLOCK", hardReason);

        // ONNX if available; otherwise rule-of-thumb
        if (_onnx is not null)
        {
            var input = new DenseTensor<float>(f, new[] { 1, f.Length });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("features", input) };
            using var res = _onnx.Run(inputs);
            var score = res.First().AsEnumerable<float>().First();
            var decision = score switch { >= 0.85f => "BLOCK", >= 0.65f => "REVIEW", _ => "ALLOW" };
            var reason = score switch { >= 0.85f => "onnx_high", >= 0.65f => "onnx_medium", _ => "onnx_low" };
            return (score, decision, reason);
        }

        // Fallback rules: amount + graph signals
        double s =
            0.35 * f[0] +          // amount
            0.15 * f[1] +          // degOut
            0.15 * f[2] +          // degIn
            0.10 * f[3] +          // edgeFreq
            0.10 * f[4] +          // device sharing
            0.05 * f[5] +          // ip sharing
            0.05 * f[6] +          // merchant in-degree
            0.05 * (1f - f[7]);    // recency: recent=high risk

        var dec = s switch { >= 0.85 => "BLOCK", >= 0.65 => "REVIEW", _ => "ALLOW" };
        var why = $"heuristic_s={s:0.00}";
        return (s, dec, why);
    }

    // Context for blacklist checks (set by service per-call)
    [ThreadStatic] private static CallContext? _ctx;
    public static void SetContext(CallContext c) => _ctx = c;

    public void Dispose() => _onnx?.Dispose();
    public sealed record CallContext(string? Ip, string? DeviceId, string? MerchantId);
}
