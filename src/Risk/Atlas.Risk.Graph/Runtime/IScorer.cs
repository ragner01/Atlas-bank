namespace Atlas.Risk.Graph.Runtime;

/// <summary>
/// Interface for risk scoring implementations
/// </summary>
public interface IScorer
{
    /// <summary>
    /// Scores a transaction based on feature vector
    /// </summary>
    /// <param name="features">Normalized feature vector</param>
    /// <returns>Risk score (0-1), decision (ALLOW/REVIEW/BLOCK), and reason</returns>
    (double score, string decision, string reason) Score(float[] features);
}
