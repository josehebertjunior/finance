namespace FinanceApp.Api.Models;

public enum WhatsAppInboxStatus
{
    Pending = 0,
    Confirmed = 1,
    Ignored = 2
}

/// <summary>
/// A message received from WhatsApp. It is kept separate from a transaction so
/// the user can review the suggestion before any financial data is recorded.
/// </summary>
public class WhatsAppInboxItem
{
    public int Id { get; set; }
    public string ProviderMessageId { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int? PersonId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Body { get; set; } = string.Empty;
    public decimal? SuggestedAmount { get; set; }
    public string SuggestedDescription { get; set; } = string.Empty;
    public TransactionType SuggestedType { get; set; } = TransactionType.Expense;
    public int? SuggestedPaymentMethodId { get; set; }
    public bool SuggestedIsFixed { get; set; }
    public int? SuggestedInstallmentCurrent { get; set; }
    public int? SuggestedInstallmentTotal { get; set; }
    public WhatsAppInboxStatus Status { get; set; } = WhatsAppInboxStatus.Pending;
    public DateTime? ProcessedAt { get; set; }
    public int? TransactionId { get; set; }
}

/// <summary>
/// Connects an approved WhatsApp number to a Finance user and group. Numbers
/// not registered here are deliberately ignored by the webhook.
/// </summary>
public class WhatsAppSender
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int? PersonId { get; set; }
    public string? DisplayName { get; set; }
}
