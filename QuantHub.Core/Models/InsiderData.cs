namespace QuantHub.Core.Models;

public sealed class InsiderTransaction
{
    public required string Insider { get; init; }
    public string? Position { get; init; }
    public required string TransactionType { get; init; }
    public long? Shares { get; init; }
    public double? Value { get; init; }
    public string? Text { get; init; }
    public string? Date { get; init; }
    public string? Ownership { get; init; }
}

public sealed class InsiderPurchases6m
{
    public long? PurchaseShares { get; init; }
    public long? PurchaseTrans { get; init; }
    public long? SaleShares { get; init; }
    public long? SaleTrans { get; init; }
}

public sealed class InstitutionalHolder
{
    public required string Organization { get; init; }
    public double? PctHeld { get; init; }
    public long? Position { get; init; }
    public double? Value { get; init; }
    public double? PctChange { get; init; }
}

public sealed class InsiderData
{
    public required string Ticker { get; init; }
    public required string Name { get; init; }
    public double? InsiderOwnership { get; init; }
    public double? InstitutionalOwnership { get; init; }
    public required string NetSentiment { get; init; }
    public int BuyCount { get; init; }
    public int SellCount { get; init; }
    public InsiderPurchases6m? Purchases6m { get; init; }
    public required IReadOnlyList<InsiderTransaction> Transactions { get; init; }
    public IReadOnlyList<InstitutionalHolder> TopInstitutionalHolders { get; init; } = [];
}
