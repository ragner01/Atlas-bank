namespace Atlas.Payments.App;

public record TransferRequest(string SourceAccountId, string DestinationAccountId, long Minor, string Currency, string Narration);
