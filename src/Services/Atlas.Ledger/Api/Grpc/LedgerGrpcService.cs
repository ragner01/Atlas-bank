using Atlas.Contracts.Ledger.V1;
using Atlas.Ledger.App;
using Atlas.Ledger.Domain;
using Grpc.Core;

namespace Atlas.Ledger.Api.Grpc;

public sealed class LedgerGrpcService : LedgerService.LedgerServiceBase
{
    private readonly PostJournalEntryHandler _handler;
    private readonly ILedgerRepository _repo;
    private readonly ITenantContext _tenantContext;

    public LedgerGrpcService(PostJournalEntryHandler h, ILedgerRepository r, ITenantContext tenantContext)
    {
        _handler = h;
        _repo = r;
        _tenantContext = tenantContext;
    }

    public override async Task<PostEntryResponse> PostEntry(PostEntryRequest request, ServerCallContext context)
    {
        // Convert minor units to decimal value (minor units / 10^scale)
        var decimalValue = request.Amount.Minor / (decimal)Math.Pow(10, request.Amount.Scale);
        var money = new Atlas.Common.ValueObjects.Money(
            decimalValue, 
            Atlas.Common.ValueObjects.Currency.FromCode(request.Amount.Currency), 
            request.Amount.Scale);
        
        var debit = (new AccountId(request.SourceAccountId), money);
        var credit = (new AccountId(request.DestinationAccountId), money);
        
        var entry = await _handler.HandleAsync(
            new(request.Narration, new[] { debit }, new[] { credit }), 
            context.CancellationToken);
        
        return new PostEntryResponse 
        { 
            EntryId = entry.Id.Value.ToString(), 
            Status = "Pending" 
        };
    }

    public override async Task<GetBalanceResponse> GetBalance(GetBalanceRequest request, ServerCallContext context)
    {
        var acc = await _repo.GetAsync(new AccountId(request.AccountId), context.CancellationToken);
        
        return new GetBalanceResponse 
        { 
            AccountId = request.AccountId, 
            LedgerMinor = acc.Balance.LedgerCents, 
            Currency = acc.Balance.Currency.Code, 
            Scale = acc.Balance.Scale 
        };
    }
}
