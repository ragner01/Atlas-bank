using Atlas.Contracts.Ledger.V1;
using Atlas.Ledger.App;
using Atlas.Ledger.Domain;
using Grpc.Core;

namespace Atlas.Ledger.Api.Grpc;

/// <summary>
/// gRPC service implementation for Ledger operations
/// </summary>
public sealed class LedgerGrpcService : LedgerService.LedgerServiceBase
{
    private readonly PostJournalEntryHandler _handler;
    private readonly ILedgerRepository _repo;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Initializes a new instance of the LedgerGrpcService
    /// </summary>
    /// <param name="h">The journal entry handler</param>
    /// <param name="r">The ledger repository</param>
    /// <param name="tenantContext">The tenant context</param>
    public LedgerGrpcService(PostJournalEntryHandler h, ILedgerRepository r, ITenantContext tenantContext)
    {
        _handler = h;
        _repo = r;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Posts a journal entry to the ledger
    /// </summary>
    /// <param name="request">The journal entry request</param>
    /// <param name="context">The gRPC call context</param>
    /// <returns>The response containing the journal entry ID</returns>
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

    /// <summary>
    /// Gets the balance for a specific account
    /// </summary>
    /// <param name="request">The balance request</param>
    /// <param name="context">The gRPC call context</param>
    /// <returns>The response containing the account balance</returns>
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
