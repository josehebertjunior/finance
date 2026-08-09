using FinanceApp.Api.Models;
using FinanceApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/imports");

        api.MapPost("/pdf/preview", async (FinanceDbContext db, HttpContext context, IFormFile statement, int paymentMethodId, DateTime referenceMonth, PdfStatementExtractor extractor) =>
        {
            var tenantId = await GetActiveTenantIdAsync(context);
            if (tenantId == null) return Results.Unauthorized();
            if (statement.Length == 0 || statement.Length > 10 * 1024 * 1024) return Results.BadRequest(new { error = "Envie um PDF de até 10 MB." });
            if (!statement.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "Envie um arquivo PDF." });
            if (!await db.PaymentMethods.AnyAsync(method => method.Id == paymentMethodId && method.TenantId == tenantId)) return Results.BadRequest(new { error = "Selecione um método de pagamento do grupo ativo." });

            await using var stream = statement.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return Results.BadRequest(new { error = "O arquivo enviado não é um PDF válido." });

            try
            {
                var month = DateTime.SpecifyKind(new DateTime(referenceMonth.Year, referenceMonth.Month, 1), DateTimeKind.Utc);
                var items = extractor.Extract(bytes, month);
                return Results.Ok(new { fileName = Path.GetFileName(statement.FileName), items, detected = items.Count });
            }
            catch (Exception)
            {
                return Results.BadRequest(new { error = "Não foi possível ler este PDF. Arquivos protegidos por senha ou escaneados precisam de uma versão com texto selecionável." });
            }
        });

        api.MapPost("/pdf/confirm", async (FinanceDbContext db, HttpContext context, ConfirmPdfImportRequest request) =>
        {
            var tenantId = await GetActiveTenantIdAsync(context);
            var ownerId = GetUserId(context);
            if (tenantId == null || ownerId == null) return Results.Unauthorized();
            if (request.Items is not { Count: > 0 } || request.Items.Count > 300) return Results.BadRequest(new { error = "Selecione entre 1 e 300 lançamentos." });

            var methodIds = request.Items.Select(item => item.PaymentMethodId).Distinct().ToArray();
            if (await db.PaymentMethods.CountAsync(method => methodIds.Contains(method.Id) && method.TenantId == tenantId) != methodIds.Length) return Results.BadRequest(new { error = "Há um método de pagamento inválido para este grupo." });

            foreach (var item in request.Items)
            {
                if (item.Amount <= 0 || string.IsNullOrWhiteSpace(item.Description)) return Results.BadRequest(new { error = "Todo lançamento deve ter descrição e valor maior que zero." });
                db.Transactions.Add(new Transaction
                {
                    Description = item.Description.Trim(), Amount = item.Amount, Type = item.Type == 0 ? TransactionType.Income : TransactionType.Expense,
                    Date = AsUtc(item.Date), ReferenceMonth = AsUtc(item.ReferenceMonth), PaymentMethodId = item.PaymentMethodId,
                    OwnerId = ownerId, TenantId = tenantId
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { created = request.Items.Count });
        });
    }

    private static string? GetUserId(HttpContext context) => context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
    private static async Task<string?> GetActiveTenantIdAsync(HttpContext context)
    {
        var userId = GetUserId(context);
        if (userId == null) return null;
        var identityDb = context.RequestServices.GetRequiredService<AppIdentityDbContext>();
        return await identityDb.Users.Where(user => user.Id == userId).Select(user => user.TenantId).FirstOrDefaultAsync();
    }
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed record ConfirmPdfImportRequest(List<ConfirmPdfImportItem> Items);
public sealed record ConfirmPdfImportItem(DateTime Date, DateTime ReferenceMonth, string Description, decimal Amount, int Type, int PaymentMethodId);
