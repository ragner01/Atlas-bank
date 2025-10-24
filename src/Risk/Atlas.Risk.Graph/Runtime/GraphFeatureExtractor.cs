using Neo4j.Driver;

namespace Atlas.Risk.Graph.Runtime;

public sealed class GraphFeatureExtractor
{
    private readonly IDriver _neo;
    public GraphFeatureExtractor(IDriver neo) => _neo = neo;

    // Feature vector (normalized):
    // 0 amount, 1 degOut(src), 2 degIn(dst), 3 edgeFreq(src->dst), 4 sharedDeviceCount(src),
    // 5 sharedIpCount(src), 6 merchantInDeg(dstMerchant), 7 recencyMin(src->dst)
    public async Task<float[]> ExtractAsync(string tenant, string src, string dst, long minor, string currency, string? deviceId, string? ip, string? merchantId, CancellationToken ct)
    {
        await using var session = _neo.AsyncSession(o => o.WithDatabase("neo4j"));
        var cy = @"
MATCH (t:Tenant {id:$tenant}),
      (a:Account {id:$src})-[:BELONGS_TO]->(t),
      (b:Account {id:$dst})-[:BELONGS_TO]->(t)
OPTIONAL MATCH (a)-[x:TRANSFER_TO]->(:Account) WITH a,b,count(x) AS degOut
OPTIONAL MATCH (:Account)-[y:TRANSFER_TO]->(b) WITH a,b,degOut,count(y) AS degIn
OPTIONAL MATCH (a)-[:TRANSFER_TO]->(b) WITH a,b,degOut,degIn,count(*) AS edgeFreq
OPTIONAL MATCH (a)-[:USES_DEVICE]->(d:Device) WITH a,b,degOut,degIn,edgeFreq,count(d) AS devCnt
OPTIONAL MATCH (a)-[:USES_IP]->(i:Ip) WITH a,b,degOut,degIn,edgeFreq,devCnt,count(i) AS ipCnt
OPTIONAL MATCH (a)-[r:TRANSFER_TO]->(b) WITH a,b,degOut,degIn,edgeFreq,devCnt,ipCnt, max(r.ts) AS lastTs
OPTIONAL MATCH (a)-[:PAYS_TO]->(m:Merchant) WITH a,b,degOut,degIn,edgeFreq,devCnt,ipCnt,lastTs,m
OPTIONAL MATCH (:Account)-[:PAYS_TO]->(m) WITH degOut,degIn,edgeFreq,devCnt,ipCnt,lastTs,count(*) AS merchIn
RETURN degOut AS o, degIn AS i, edgeFreq AS e, devCnt AS d, ipCnt AS p, merchIn AS mi, lastTs AS ts
";
        var rec = await (await session.RunAsync(cy, new { tenant, src, dst })).SingleAsync();
        long o = rec["o"].As<long>(0), i = rec["i"].As<long>(0), e = rec["e"].As<long>(0);
        long d = rec["d"].As<long>(0), p = rec["p"].As<long>(0), mi = rec["mi"].As<long>(0);
        long ts = rec["ts"] == null ? 0 : rec["ts"].As<long>();

        float fAmount = MathF.Min(1f, minor / 2_000_000_00f); // normalize by 2M
        float fDegOut = MathF.Min(1f, (float)o / 1000f);
        float fDegIn  = MathF.Min(1f, (float)i / 1000f);
        float fEdge   = MathF.Min(1f, (float)e / 100f);
        float fDev    = MathF.Min(1f, (float)d / 50f);
        float fIp     = MathF.Min(1f, (float)p / 200f);
        float fMerch  = MathF.Min(1f, (float)mi / 2000f);
        float fRecMin = ts == 0 ? 1f : MathF.Min(1f, (float)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ts) / (30f * 60_000f)); // 0..1 over 30m

        return new[] { fAmount, fDegOut, fDegIn, fEdge, fDev, fIp, fMerch, fRecMin };
    }
}
