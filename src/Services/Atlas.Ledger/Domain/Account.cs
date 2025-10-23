using Atlas.Common.ValueObjects;
using Atlas.Common.Results;
using Atlas.Messaging.Events;

namespace Atlas.Ledger.Domain;

/// <summary>
/// Represents a ledger account
/// </summary>
public class Account
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public EntityId Id { get; init; }
    public TenantId TenantId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AccountType Type { get; init; }
    public Currency Currency { get; init; }
    public Money Balance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Timestamp CreatedAt { get; init; }
    public Timestamp UpdatedAt { get; private set; }

    // Domain events
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Account() { } // EF Core

    public Account(EntityId id, TenantId tenantId, string accountNumber, string name, AccountType type, Currency currency)
    {
        Id = id;
        TenantId = tenantId;
        AccountNumber = accountNumber;
        Name = name;
        Type = type;
        Currency = currency;
        Balance = Money.Zero(currency);
        CreatedAt = Timestamp.Now;
        UpdatedAt = Timestamp.Now;
    }

    private void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Restore balance from persistence layer (internal use only)
    /// </summary>
    internal void RestoreBalance(Money balance)
    {
        Balance = balance;
    }

    public Result<Money> Debit(Money amount)
    {
        if (!IsActive)
            return Result<Money>.Failure("Account is not active");

        if (amount.Currency != Currency)
            return Result<Money>.Failure("Currency mismatch");

        if (amount.IsNegative || amount.IsZero)
            return Result<Money>.Failure("Debit amount must be positive");

        // Check for insufficient balance (prevent negative balances for Asset accounts)
        if (Type == AccountType.Asset && Balance < amount)
            return Result<Money>.Failure("Insufficient balance");

        var previousBalance = Balance;
        Balance = Balance - amount;
        UpdatedAt = Timestamp.Now;
        
        // Raise domain event
        AddDomainEvent(new AccountBalanceChangedEvent(TenantId, Id, previousBalance, Balance, amount));
        
        return Result<Money>.Success(amount);
    }

    public Result<Money> Credit(Money amount)
    {
        if (!IsActive)
            return Result<Money>.Failure("Account is not active");

        if (amount.Currency != Currency)
            return Result<Money>.Failure("Currency mismatch");

        if (amount.IsNegative || amount.IsZero)
            return Result<Money>.Failure("Credit amount must be positive");

        var previousBalance = Balance;
        Balance = Balance + amount;
        UpdatedAt = Timestamp.Now;
        
        // Raise domain event
        AddDomainEvent(new AccountBalanceChangedEvent(TenantId, Id, previousBalance, Balance, amount));
        
        return Result<Money>.Success(amount);
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = Timestamp.Now;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = Timestamp.Now;
    }
}

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

/// <summary>
/// Domain events for the ledger
/// </summary>
public record AccountCreatedEvent(TenantId TenantId, EntityId AccountId, string AccountNumber, string Name, AccountType Type, Currency Currency) : DomainEvent(TenantId);

public record AccountBalanceChangedEvent(TenantId TenantId, EntityId AccountId, Money PreviousBalance, Money NewBalance, Money ChangeAmount) : DomainEvent(TenantId);
