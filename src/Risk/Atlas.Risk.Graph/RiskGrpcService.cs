using Atlas.Contracts.Risk.V1;
using Grpc.Core;
using Atlas.Risk.Graph.Runtime;

namespace Atlas.Risk.Graph;

public sealed class RiskGrpcService : RiskService.RiskServiceBase
{
    private readonly GraphFeatureExtractor _fx;
    private readonly IScorer _scorer;
    public RiskGrpcService(Neo4j.Driver.IDriver neo, IScorer scorer) { _fx = new GraphFeatureExtractor(neo); _scorer = scorer; }

    public override async Task<ScoreResponse> Score(ScoreRequest request, ServerCallContext context)
    {
        // enrich call-context for blacklists
        if (_scorer is HybridScorer hs)
            HybridScorer.SetContext(new HybridScorer.CallContext(request.Ip, request.DeviceId, request.MerchantId));

        var f = await _fx.ExtractAsync(request.TenantId, request.SourceAccountId, request.DestinationAccountId,
                                       request.Minor, request.Currency, request.DeviceId, request.Ip, request.MerchantId,
                                       context.CancellationToken);
        var (score, decision, reason) = _scorer.Score(f);
        return new ScoreResponse { RiskScore = score, Decision = decision, Reason = reason };
    }
}
