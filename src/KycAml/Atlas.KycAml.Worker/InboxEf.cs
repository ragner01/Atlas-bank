using Atlas.KycAml.Domain;
using Atlas.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Atlas.KycAml.Worker;

public sealed class EfInbox : IInbox
{
    private readonly CasesDbContext _db;
    public EfInbox(CasesDbContext db) => _db = db;

    public async Task<bool> HasProcessedAsync(string consumer, string messageId, CancellationToken ct)
        => await _db.Inbox.FindAsync(new object[] { consumer, messageId }, ct) is not null;

    public async Task MarkProcessedAsync(string consumer, string messageId, CancellationToken ct)
    {
        _db.Inbox.Add(new InboxMessage { Consumer = consumer, MessageId = messageId });
        await _db.SaveChangesAsync(ct);
    }
}
