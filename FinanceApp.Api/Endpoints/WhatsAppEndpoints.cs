using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinanceApp.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Api.Endpoints;

public static partial class WhatsAppEndpoints
{
    public static void MapWhatsAppEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/whatsapp");

        api.MapGet("/inbox", async (FinanceDbContext db, HttpContext context, string? status) =>
        {
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();

            var items = db.WhatsAppInboxItems
                .Where(item => item.TenantId == tenantId)
                .OrderByDescending(item => item.ReceivedAt)
                .AsQueryable();

            if (Enum.TryParse<WhatsAppInboxStatus>(status, true, out var parsedStatus))
                items = items.Where(item => item.Status == parsedStatus);

            return Results.Ok(await items.Take(100).ToListAsync());
        });

        api.MapPost("/inbox/{id:int}/confirm", async (FinanceDbContext db, HttpContext context, int id, ConfirmWhatsAppInboxRequest? input) =>
        {
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();

            var item = await db.WhatsAppInboxItems.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.TenantId == tenantId);
            if (item == null) return Results.NotFound();
            if (item.Status != WhatsAppInboxStatus.Pending)
                return Results.BadRequest(new { error = "This message has already been processed." });

            var amount = input?.Amount ?? item.SuggestedAmount;
            var description = input?.Description?.Trim() ?? item.SuggestedDescription;
            if (!amount.HasValue || amount.Value <= 0 || string.IsNullOrWhiteSpace(description))
                return Results.BadRequest(new { error = "Provide a description and an amount greater than zero." });

            var transaction = new Transaction
            {
                Description = description,
                Amount = amount.Value,
                Type = input?.Type ?? item.SuggestedType,
                Date = AsUtc(input?.Date ?? item.ReceivedAt),
                ReferenceMonth = AsUtc(input?.ReferenceMonth ?? new DateTime(item.ReceivedAt.Year, item.ReceivedAt.Month, 1)),
                CategoryId = input?.CategoryId,
                PersonId = input?.PersonId ?? item.PersonId,
                PaymentMethodId = input?.PaymentMethodId ?? item.SuggestedPaymentMethodId,
                IsFixed = input?.IsFixed ?? item.SuggestedIsFixed,
                InstallmentCurrent = input?.InstallmentCurrent ?? item.SuggestedInstallmentCurrent,
                InstallmentTotal = input?.InstallmentTotal ?? item.SuggestedInstallmentTotal ?? 1,
                OwnerId = item.OwnerId,
                TenantId = tenantId
            };

            if (!await ReferencesMatchTenantAsync(db, transaction, tenantId))
                return Results.BadRequest(new { error = "The selected registrations must belong to the active group." });

            var createdTransaction = AddTransactions(db, transaction);
            // Save first so EF generates the transaction identity before it is
            // persisted as the audit link of the incoming WhatsApp message.
            await db.SaveChangesAsync();
            item.Status = WhatsAppInboxStatus.Confirmed;
            item.ProcessedAt = DateTime.UtcNow;
            item.TransactionId = createdTransaction.Id;
            await db.SaveChangesAsync();

            return Results.Ok(new { transactionId = createdTransaction.Id });
        });

        api.MapPost("/inbox/{id:int}/ignore", async (FinanceDbContext db, HttpContext context, int id) =>
        {
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();
            var item = await db.WhatsAppInboxItems.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.TenantId == tenantId);
            if (item == null) return Results.NotFound();
            if (item.Status != WhatsAppInboxStatus.Pending)
                return Results.BadRequest(new { error = "This message has already been processed." });

            item.Status = WhatsAppInboxStatus.Ignored;
            item.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapGet("/senders", async (FinanceDbContext db, HttpContext context) =>
        {
            if (!context.User.IsInRole("Admin")) return Results.Forbid();
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();
            return Results.Ok(await db.WhatsAppSenders.Where(sender => sender.TenantId == tenantId).OrderBy(sender => sender.DisplayName).ToListAsync());
        });

        api.MapPost("/senders", async (FinanceDbContext db, AppIdentityDbContext identityDb, HttpContext context, WhatsAppSender input) =>
        {
            if (!context.User.IsInRole("Admin")) return Results.Forbid();
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();

            var phone = NormalizePhone(input.PhoneNumber);
            if (phone.Length < 10) return Results.BadRequest(new { error = "Enter the complete phone number with country code." });
            if (string.IsNullOrWhiteSpace(input.OwnerId) || !await identityDb.TenantMemberships!.AnyAsync(member => member.UserId == input.OwnerId && member.TenantId == tenantId))
                return Results.BadRequest(new { error = "The selected user must belong to the active group." });
            if (input.PersonId.HasValue && !await db.Persons.AnyAsync(person => person.Id == input.PersonId && person.TenantId == tenantId))
                return Results.BadRequest(new { error = "The selected person must belong to the active group." });
            if (await db.WhatsAppSenders.AnyAsync(sender => sender.PhoneNumber == phone))
                return Results.Conflict(new { error = "This WhatsApp number is already registered." });

            var sender = new WhatsAppSender
            {
                PhoneNumber = phone,
                TenantId = tenantId,
                OwnerId = input.OwnerId,
                PersonId = input.PersonId,
                DisplayName = input.DisplayName?.Trim()
            };
            db.WhatsAppSenders.Add(sender);
            await db.SaveChangesAsync();
            return Results.Created($"/api/whatsapp/senders/{sender.Id}", sender);
        });

        api.MapDelete("/senders/{id:int}", async (FinanceDbContext db, HttpContext context, int id) =>
        {
            if (!context.User.IsInRole("Admin")) return Results.Forbid();
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();
            var sender = await db.WhatsAppSenders.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.TenantId == tenantId);
            if (sender == null) return Results.NotFound();
            db.WhatsAppSenders.Remove(sender);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapGet("/status", (HttpContext context, IConfiguration configuration) =>
        {
            if (!context.User.IsInRole("Admin")) return Results.Forbid();
            return Results.Ok(new { webhookConfigured = !string.IsNullOrWhiteSpace(configuration["WhatsApp:WebhookVerifyToken"]), appSecretConfigured = !string.IsNullOrWhiteSpace(configuration["WhatsApp:AppSecret"]) });
        });

        app.MapMethods("/api/integrations/whatsapp/webhook", [HttpMethods.Get], (HttpContext context, IConfiguration configuration) =>
        {
            var expectedToken = configuration["WhatsApp:WebhookVerifyToken"];
            var mode = context.Request.Query["hub.mode"].FirstOrDefault();
            var suppliedToken = context.Request.Query["hub.verify_token"].FirstOrDefault();
            var challenge = context.Request.Query["hub.challenge"].FirstOrDefault();

            if (string.Equals(mode, "subscribe", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(challenge)
                && FixedTimeEquals(expectedToken, suppliedToken))
                return Results.Text(challenge, "text/plain");

            return Results.Unauthorized();
        }).AllowAnonymous();

        app.MapPost("/api/integrations/whatsapp/webhook", async (FinanceDbContext db, HttpContext context, IConfiguration configuration) =>
        {
            var appSecret = configuration["WhatsApp:AppSecret"];
            if (string.IsNullOrWhiteSpace(appSecret)) return Results.Unauthorized();

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var rawPayload = await reader.ReadToEndAsync();
            if (!HasValidSignature(context.Request.Headers["X-Hub-Signature-256"], rawPayload, appSecret)) return Results.Unauthorized();

            using var payload = JsonDocument.Parse(rawPayload);
            var created = 0;
            foreach (var message in GetTextMessages(payload.RootElement))
            {
                var providerMessageId = message.Id;
                var senderPhone = NormalizePhone(message.From);
                if (string.IsNullOrWhiteSpace(providerMessageId) || string.IsNullOrWhiteSpace(senderPhone)) continue;
                if (await db.WhatsAppInboxItems.AnyAsync(item => item.ProviderMessageId == providerMessageId)) continue;

                var sender = await db.WhatsAppSenders.FirstOrDefaultAsync(candidate => candidate.PhoneNumber == senderPhone);
                if (sender == null) continue;

                var paymentMethods = await db.PaymentMethods.Where(method => method.TenantId == sender.TenantId).ToListAsync();
                var suggestion = ParseSuggestion(message.Body, paymentMethods);
                if (!suggestion.Amount.HasValue) continue;

                db.WhatsAppInboxItems.Add(new WhatsAppInboxItem
                {
                    ProviderMessageId = providerMessageId,
                    SenderPhone = senderPhone,
                    TenantId = sender.TenantId,
                    OwnerId = sender.OwnerId,
                    PersonId = sender.PersonId,
                    ReceivedAt = message.ReceivedAt,
                    Body = message.Body,
                    SuggestedAmount = suggestion.Amount,
                    SuggestedDescription = suggestion.Description,
                    SuggestedType = suggestion.Type,
                    SuggestedPaymentMethodId = suggestion.PaymentMethodId,
                    SuggestedIsFixed = suggestion.IsFixed,
                    SuggestedInstallmentCurrent = suggestion.InstallmentCurrent,
                    SuggestedInstallmentTotal = suggestion.InstallmentTotal
                });
                created++;
            }

            if (created > 0) await db.SaveChangesAsync();
            return Results.Ok();
        }).AllowAnonymous();
    }

    private static Transaction AddTransactions(FinanceDbContext db, Transaction source)
    {
        if (source.IsFixed)
        {
            var groupId = Guid.NewGuid();
            for (var index = 0; index < 12; index++)
            {
                var occurrence = Copy(source);
                occurrence.Date = source.Date.AddMonths(index);
                occurrence.ReferenceMonth = source.ReferenceMonth.AddMonths(index);
                occurrence.InstallmentGroupId = groupId;
                occurrence.InstallmentCurrent = null;
                occurrence.InstallmentTotal = 1;
                db.Transactions.Add(occurrence);
                if (index == 0) source = occurrence;
            }
            return source;
        }

        if (source.InstallmentTotal > 1)
        {
            var groupId = Guid.NewGuid();
            var initial = source.InstallmentCurrent ?? 1;
            for (var index = 0; index < source.InstallmentTotal; index++)
            {
                var installment = Copy(source);
                installment.ReferenceMonth = source.ReferenceMonth.AddMonths(index);
                installment.InstallmentGroupId = groupId;
                installment.InstallmentCurrent = initial + index;
                db.Transactions.Add(installment);
                if (index == 0) source = installment;
            }
            return source;
        }

        db.Transactions.Add(source);
        return source;
    }

    private static Transaction Copy(Transaction source) => new()
    {
        Description = source.Description,
        Amount = source.Amount,
        Type = source.Type,
        Date = source.Date,
        ReferenceMonth = source.ReferenceMonth,
        CategoryId = source.CategoryId,
        PersonId = source.PersonId,
        PaymentMethodId = source.PaymentMethodId,
        IsFixed = source.IsFixed,
        InstallmentCurrent = source.InstallmentCurrent,
        InstallmentTotal = source.InstallmentTotal,
        OwnerId = source.OwnerId,
        TenantId = source.TenantId
    };

    private static async Task<string?> GetActiveTenantIdAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var identityDb = context.RequestServices.GetRequiredService<AppIdentityDbContext>();
        return await identityDb.Users.Where(user => user.Id == userId).Select(user => user.TenantId).FirstOrDefaultAsync();
    }

    private static async Task<bool> ReferencesMatchTenantAsync(FinanceDbContext db, Transaction transaction, string tenantId)
    {
        if (transaction.CategoryId.HasValue && !await db.Categories.AnyAsync(category => category.Id == transaction.CategoryId && category.TenantId == tenantId)) return false;
        if (transaction.PersonId.HasValue && !await db.Persons.AnyAsync(person => person.Id == transaction.PersonId && person.TenantId == tenantId)) return false;
        return !transaction.PaymentMethodId.HasValue || await db.PaymentMethods.AnyAsync(method => method.Id == transaction.PaymentMethodId && method.TenantId == tenantId);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static IEnumerable<IncomingTextMessage> GetTextMessages(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) yield break;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value) || !value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array) continue;
                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("type", out var type) || type.GetString() != "text") continue;
                    if (!message.TryGetProperty("text", out var text) || !text.TryGetProperty("body", out var body)) continue;
                    var id = message.TryGetProperty("id", out var messageId) ? messageId.GetString() : null;
                    var from = message.TryGetProperty("from", out var fromValue) ? fromValue.GetString() : null;
                    var timestamp = message.TryGetProperty("timestamp", out var timeValue) && long.TryParse(timeValue.GetString(), out var seconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(body.GetString()))
                        yield return new IncomingTextMessage(id, from, body.GetString()!, timestamp);
                }
            }
        }
    }

    private static ParsedSuggestion ParseSuggestion(string body, IReadOnlyCollection<PaymentMethod> paymentMethods)
    {
        var amountMatch = CurrencyRegex().Match(body);
        decimal? amount = null;
        if (amountMatch.Success && decimal.TryParse(amountMatch.Groups["amount"].Value.Replace(".", string.Empty).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) amount = parsed;

        var description = amountMatch.Success ? body.Remove(amountMatch.Index, amountMatch.Length).Trim(" -,:;".ToCharArray()) : body.Trim();
        var normalizedDescription = description.ToLowerInvariant();
        var method = paymentMethods
            .OrderByDescending(candidate => candidate.Name.Length)
            .FirstOrDefault(candidate => normalizedDescription.Contains(candidate.Name.ToLowerInvariant(), StringComparison.Ordinal));
        if (method != null) description = Regex.Replace(description, Regex.Escape(method.Name), string.Empty, RegexOptions.IgnoreCase).Trim(" -,:;".ToCharArray());

        var installment = InstallmentRegex().Match(description);
        int? current = null;
        int? total = null;
        if (installment.Success)
        {
            current = int.Parse(installment.Groups["current"].Value, CultureInfo.InvariantCulture);
            total = int.Parse(installment.Groups["total"].Value, CultureInfo.InvariantCulture);
            description = description.Remove(installment.Index, installment.Length).Trim(" -,:;".ToCharArray());
        }

        var fixedExpense = normalizedDescription.Contains("fixa", StringComparison.Ordinal);
        if (fixedExpense) description = Regex.Replace(description, @"\bfixa\b", string.Empty, RegexOptions.IgnoreCase).Trim(" -,:;".ToCharArray());
        var income = normalizedDescription.StartsWith("entrada ", StringComparison.Ordinal) || normalizedDescription.StartsWith("receita ", StringComparison.Ordinal);
        if (income) description = Regex.Replace(description, @"^(entrada|receita)\s+", string.Empty, RegexOptions.IgnoreCase).Trim();

        return new ParsedSuggestion(amount, description, income ? TransactionType.Income : TransactionType.Expense, method?.Id, fixedExpense, current, total);
    }

    private static bool HasValidSignature(string? signature, string payload, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signature) || !signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return FixedTimeEquals(expected, signature[7..]);
    }

    private static bool FixedTimeEquals(string? expected, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }

    private static string NormalizePhone(string? value) => new(value?.Where(char.IsDigit).ToArray() ?? []);

    [GeneratedRegex(@"(?:R\$\s*)?(?<amount>\d{1,3}(?:\.\d{3})*,\d{2}|\d+,\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyRegex();

    [GeneratedRegex(@"\b(?<current>\d{1,3})\s*/\s*(?<total>\d{1,3})\b")]
    private static partial Regex InstallmentRegex();

    public sealed record ConfirmWhatsAppInboxRequest(decimal? Amount, string? Description, TransactionType? Type, int? CategoryId, int? PaymentMethodId, int? PersonId, DateTime? Date, DateTime? ReferenceMonth, bool? IsFixed, int? InstallmentCurrent, int? InstallmentTotal);
    private sealed record IncomingTextMessage(string Id, string From, string Body, DateTime ReceivedAt);
    private sealed record ParsedSuggestion(decimal? Amount, string Description, TransactionType Type, int? PaymentMethodId, bool IsFixed, int? InstallmentCurrent, int? InstallmentTotal);
}
