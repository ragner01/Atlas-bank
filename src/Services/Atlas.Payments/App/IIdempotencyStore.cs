namespace Atlas.Payments.App;

public interface IIdempotencyStore
{
    Task<bool> SeenAsync(string key, CancellationToken cancellationToken = default);
    Task MarkAsync(string key, CancellationToken cancellationToken = default);
}
