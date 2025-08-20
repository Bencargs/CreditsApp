namespace CreditsApp.Accounting.Messaging;

public record CreateTransferCommand(
    string ToHandle,
    decimal Amount,
    string? Message,
    string IdempotencyKey,
    Guid FromUserId);