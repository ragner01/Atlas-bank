namespace Atlas.Payments.App;

public interface IIdempotencyStore
{
    Task<bool> SeenAsync(string key, CancellationToken cancellationToken = default);
    Task MarkAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atomically check if key exists and mark it if it doesn't.
    /// Returns true if the key was already processed, false if it's new and now marked.
    /// </summary>
    Task<bool> CheckAndMarkAsync(string key, CancellationToken cancellationToken = default);
}
