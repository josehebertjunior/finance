using FinanceApp.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;

namespace FinanceApp.Api.Endpoints;

public static class ApiEndpoints
{
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/categories", async (FinanceDbContext db, HttpContext ctx) =>
        {
            var tenantId = await GetActiveTenantIdAsync(ctx);
            return Results.Ok(await db.Categories.Where(category => category.TenantId == tenantId).ToListAsync());
        });

        api.MapPost("/categories", async (FinanceDbContext db, HttpContext ctx, Category category) =>
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var tenantId = await GetActiveTenantIdAsync(ctx);
            if (tenantId == null) return Results.BadRequest(new { error = "Select an active group first." });

            category.TenantId = tenantId;
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{category.Id}", category);
        });

        api.MapPut("/categories/{id}", async (FinanceDbContext db, HttpContext ctx, int id, Category input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var category = await db.Categories.FindAsync(id);
            if (category == null) return Results.NotFound();
            if (category.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();

            category.Name = input.Name;
            category.ColorHex = input.ColorHex;
            await db.SaveChangesAsync();
            return Results.Ok(category);
        });

        api.MapGet("/paymentmethods", async (FinanceDbContext db, HttpContext ctx) =>
        {
            var tenantId = await GetActiveTenantIdAsync(ctx);
            return Results.Ok(await db.PaymentMethods.Where(method => method.TenantId == tenantId).ToListAsync());
        });

        api.MapPost("/paymentmethods", async (FinanceDbContext db, HttpContext ctx, PaymentMethod pm) =>
        {
            if (string.IsNullOrWhiteSpace(pm.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var tenantId = await GetActiveTenantIdAsync(ctx);
            if (tenantId == null) return Results.BadRequest(new { error = "Select an active group first." });

            pm.TenantId = tenantId;
            db.PaymentMethods.Add(pm);
            await db.SaveChangesAsync();
            return Results.Created($"/api/paymentmethods/{pm.Id}", pm);
        });

        api.MapPut("/paymentmethods/{id}", async (FinanceDbContext db, HttpContext ctx, int id, PaymentMethod input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var pm = await db.PaymentMethods.FindAsync(id);
            if (pm == null) return Results.NotFound();
            if (pm.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();

            pm.Name = input.Name;
            pm.IsCreditCard = input.IsCreditCard;
            await db.SaveChangesAsync();
            return Results.Ok(pm);
        });

        api.MapGet("/persons", async (FinanceDbContext db, HttpContext ctx) =>
        {
            var tenantId = await GetActiveTenantIdAsync(ctx);
            return Results.Ok(await db.Persons.Where(person => person.TenantId == tenantId).ToListAsync());
        });

        api.MapPost("/persons", async (FinanceDbContext db, HttpContext ctx, Person person) =>
        {
            if (string.IsNullOrWhiteSpace(person.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var tenantId = await GetActiveTenantIdAsync(ctx);
            if (tenantId == null) return Results.BadRequest(new { error = "Select an active group first." });

            person.TenantId = tenantId;
            db.Persons.Add(person);
            await db.SaveChangesAsync();
            return Results.Created($"/api/persons/{person.Id}", person);
        });

        api.MapPut("/persons/{id}", async (FinanceDbContext db, HttpContext ctx, int id, Person input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var person = await db.Persons.FindAsync(id);
            if (person == null) return Results.NotFound();
            if (person.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();

            person.Name = input.Name;
            await db.SaveChangesAsync();
            return Results.Ok(person);
        });

        api.MapGet("/transactions", async (HttpContext ctx) =>
        {
            var db = ctx.RequestServices.GetRequiredService<FinanceDbContext>();
            var user = ctx.User;
            if (!user?.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            var query = db.Transactions
                .Include(t => t.Category)
                .Include(t => t.Person)
                .Include(t => t.PaymentMethod)
                .AsQueryable();

            var q = ctx.Request.Query;
            if (q.TryGetValue("year", out var ys) && q.TryGetValue("month", out var ms) && int.TryParse(ys.FirstOrDefault(), out var y) && int.TryParse(ms.FirstOrDefault(), out var m))
            {
                var start = AsUtc(new DateTime(y, m, 1));
                var end = start.AddMonths(1);
                query = query.Where(t => t.ReferenceMonth >= start && t.ReferenceMonth < end);
            }

            var ownerIds = await GetSharedOwnerIdsAsync(ctx, sub);
            var activeTenantId = await GetActiveTenantIdAsync(ctx);
            query = query.Where(t => ownerIds.Contains(t.OwnerId) && t.TenantId == activeTenantId);

            return Results.Ok(await query.ToListAsync());
        });

        api.MapGet("/transactions/{id}", async (HttpContext ctx, int id) =>
        {
            var db = ctx.RequestServices.GetRequiredService<FinanceDbContext>();
            var user = ctx.User;
            if (!user?.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            var transaction = await db.Transactions
                .Include(t => t.Category)
                .Include(t => t.Person)
                .Include(t => t.PaymentMethod)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null) return Results.NotFound();
            var ownerIds = await GetSharedOwnerIdsAsync(ctx, sub);
            if (!ownerIds.Contains(transaction.OwnerId) || transaction.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();

            return Results.Ok(transaction);
        });

        api.MapPost("/transactions", async (HttpContext ctx) =>
        {
            var db = ctx.RequestServices.GetRequiredService<FinanceDbContext>();
            var user = ctx.User;
            if (!user?.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            var t = await ctx.Request.ReadFromJsonAsync<Transaction>();
            if (t == null) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(t.Description))
                return Results.BadRequest(new { error = "Description is required." });
            if (t.Amount <= 0)
                return Results.BadRequest(new { error = "Amount must be greater than zero." });

            var activeTenantId = await GetActiveTenantIdAsync(ctx);
            if (activeTenantId == null || !await ReferencesMatchTenantAsync(db, t, activeTenantId))
                return Results.BadRequest(new { error = "The selected registrations must belong to the active group." });

            t.Date = AsUtc(t.Date);
            t.ReferenceMonth = AsUtc(t.ReferenceMonth);

            if (t.IsFixed)
            {
                // A conta fixa nasce no mês escolhido e fica disponível nos próximos 11 meses.
                // Cada ocorrência é um lançamento independente para permitir ajustes pontuais.
                var groupId = Guid.NewGuid();
                for (var i = 0; i < 12; i++)
                {
                    db.Transactions.Add(new Transaction
                    {
                        Description = t.Description,
                        Amount = t.Amount,
                        Type = t.Type,
                        Date = t.Date.AddMonths(i),
                        ReferenceMonth = t.ReferenceMonth.AddMonths(i),
                        CategoryId = t.CategoryId,
                        PersonId = t.PersonId,
                        PaymentMethodId = t.PaymentMethodId,
                        IsFixed = true,
                        InstallmentCurrent = null,
                        InstallmentTotal = 1,
                        InstallmentGroupId = groupId,
                        OwnerId = sub ?? string.Empty,
                        TenantId = activeTenantId
                    });
                }
            }
            else if (t.InstallmentTotal > 1)
            {
                var groupId = Guid.NewGuid();
                for (int i = 0; i < t.InstallmentTotal; i++)
                {
                    var installment = new Transaction
                    {
                        Description = t.Description,
                        Amount = t.Amount,
                        Type = t.Type,
                        Date = t.Date,
                        ReferenceMonth = t.ReferenceMonth.AddMonths(i),
                        CategoryId = t.CategoryId,
                        PersonId = t.PersonId,
                        PaymentMethodId = t.PaymentMethodId,
                        IsFixed = false,
                        InstallmentCurrent = (t.InstallmentCurrent ?? 1) + i,
                        InstallmentTotal = t.InstallmentTotal,
                        InstallmentGroupId = groupId,
                        OwnerId = sub ?? string.Empty,
                        TenantId = activeTenantId
                    };
                    db.Transactions.Add(installment);
                }
            }
            else
            {
                t.OwnerId = sub ?? string.Empty;
                t.TenantId = activeTenantId;
                db.Transactions.Add(t);
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapPut("/transactions/{id}", async (HttpContext ctx, int id) =>
        {
            var db = ctx.RequestServices.GetRequiredService<FinanceDbContext>();
            var user = ctx.User;
            if (!user?.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            var input = await ctx.Request.ReadFromJsonAsync<Transaction>();
            if (input == null) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(input.Description)) return Results.BadRequest(new { error = "Description is required." });
            if (input.Amount <= 0) return Results.BadRequest(new { error = "Amount must be greater than zero." });

            var activeTenantId = await GetActiveTenantIdAsync(ctx);
            if (activeTenantId == null || !await ReferencesMatchTenantAsync(db, input, activeTenantId))
                return Results.BadRequest(new { error = "The selected registrations must belong to the active group." });

            input.Date = AsUtc(input.Date);
            input.ReferenceMonth = AsUtc(input.ReferenceMonth);

            var t = await db.Transactions.FindAsync(id);
            if (t == null) return Results.NotFound();
            var ownerIds = await GetSharedOwnerIdsAsync(ctx, sub);
            if (!ownerIds.Contains(t.OwnerId) || t.TenantId != activeTenantId) return Results.Forbid();

            var updateSeries = string.Equals(ctx.Request.Query["scope"].FirstOrDefault(), "series", StringComparison.OrdinalIgnoreCase)
                && t.InstallmentGroupId.HasValue;
            var targets = updateSeries
                ? await db.Transactions.Where(item => item.InstallmentGroupId == t.InstallmentGroupId && item.OwnerId == t.OwnerId && item.TenantId == activeTenantId).ToListAsync()
                : new List<Transaction> { t };

            foreach (var target in targets)
            {
                target.Description = input.Description;
                target.Amount = input.Amount;
                target.Type = input.Type;
                target.CategoryId = input.CategoryId;
                target.PersonId = input.PersonId;
                target.PaymentMethodId = input.PaymentMethodId;
                target.IsFixed = input.IsFixed;
            }

            // A série mantém os meses já programados. Ao editar apenas um item, sua data e mês podem mudar.
            if (!updateSeries)
            {
                t.Date = input.Date;
                t.ReferenceMonth = input.ReferenceMonth;
            }

            await db.SaveChangesAsync();
            return Results.Ok(t);
        });

        api.MapGet("/summary/by-category", async (FinanceDbContext db, HttpContext ctx, int year, int month) =>
        {
            var start = AsUtc(new DateTime(year, month, 1));
            var end = start.AddMonths(1);

            var user = ctx.User;
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            var baseQuery = db.Transactions.Where(t => t.ReferenceMonth >= start && t.ReferenceMonth < end && t.Type == TransactionType.Expense).Include(t => t.Category).AsQueryable();
            var ownerIds = await GetSharedOwnerIdsAsync(ctx, sub);
            var activeTenantId = await GetActiveTenantIdAsync(ctx);
            baseQuery = baseQuery.Where(t => ownerIds.Contains(t.OwnerId) && t.TenantId == activeTenantId);

            var expenses = await baseQuery.ToListAsync();

            var summary = expenses
                .GroupBy(t => t.Category?.Name ?? "Sem categoria")
                .Select(g => new
                {
                    CategoryName = g.Key,
                    Total = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            return Results.Ok(summary);
        });

        api.MapGet("/savings/balance", async (FinanceDbContext db, HttpContext ctx) =>
        {
            var user = ctx.User;
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            var depositsQ = db.Transactions.Where(t => t.Type == TransactionType.SavingsDeposit).AsQueryable();
            var withdrawalsQ = db.Transactions.Where(t => t.Type == TransactionType.SavingsWithdrawal).AsQueryable();
            var ownerIds = await GetSharedOwnerIdsAsync(ctx, sub);
            var activeTenantId = await GetActiveTenantIdAsync(ctx);
            depositsQ = depositsQ.Where(t => ownerIds.Contains(t.OwnerId) && t.TenantId == activeTenantId);
            withdrawalsQ = withdrawalsQ.Where(t => ownerIds.Contains(t.OwnerId) && t.TenantId == activeTenantId);
            var deposits = await depositsQ.Select(t => (double?)t.Amount).SumAsync() ?? 0.0;
            var withdrawals = await withdrawalsQ.Select(t => (double?)t.Amount).SumAsync() ?? 0.0;
            return Results.Ok(new { Balance = (decimal)deposits - (decimal)withdrawals });
        });

        api.MapDelete("/transactions/{id}", async (FinanceDbContext db, HttpContext ctx, int id) =>
        {
            var t = await db.Transactions.FindAsync(id);
            if (t == null) return Results.NotFound();
            var user = ctx.User;
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            var ownerIds = await GetSharedOwnerIdsAsync(ctx, sub);
            var activeTenantId = await GetActiveTenantIdAsync(ctx);
            if (!ownerIds.Contains(t.OwnerId) || t.TenantId != activeTenantId) return Results.Forbid();
            var deleteSeries = string.Equals(ctx.Request.Query["scope"].FirstOrDefault(), "series", StringComparison.OrdinalIgnoreCase)
                && t.InstallmentGroupId.HasValue;
            if (deleteSeries)
            {
                var series = await db.Transactions
                    .Where(item => item.InstallmentGroupId == t.InstallmentGroupId && item.OwnerId == t.OwnerId && item.TenantId == activeTenantId)
                    .ToListAsync();
                db.Transactions.RemoveRange(series);
            }
            else
            {
                db.Transactions.Remove(t);
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapDelete("/categories/{id}", async (FinanceDbContext db, HttpContext ctx, int id) =>
        {
            var c = await db.Categories.FindAsync(id);
            if (c != null && c.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();
            if (c != null) { db.Categories.Remove(c); await db.SaveChangesAsync(); }
            return Results.Ok();
        });

        api.MapDelete("/persons/{id}", async (FinanceDbContext db, HttpContext ctx, int id) =>
        {
            var p = await db.Persons.FindAsync(id);
            if (p != null && p.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();
            if (p != null) { db.Persons.Remove(p); await db.SaveChangesAsync(); }
            return Results.Ok();
        });

        api.MapDelete("/paymentmethods/{id}", async (FinanceDbContext db, HttpContext ctx, int id) =>
        {
            var p = await db.PaymentMethods.FindAsync(id);
            if (p != null && p.TenantId != await GetActiveTenantIdAsync(ctx)) return Results.Forbid();
            if (p != null) { db.PaymentMethods.Remove(p); await db.SaveChangesAsync(); }
            return Results.Ok();
        });
    }

    private static async Task<string[]> GetSharedOwnerIdsAsync(HttpContext context, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];

        var identityDb = context.RequestServices.GetRequiredService<AppIdentityDbContext>();
        var tenantId = await identityDb.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.TenantId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(tenantId)) return [userId];

        var memberIds = await identityDb.TenantMemberships!
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId)
            .Select(membership => membership.UserId)
            .ToListAsync();

        return memberIds
            .Append(userId)
            .OfType<string>()
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<string?> GetActiveTenantIdAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var identityDb = context.RequestServices.GetRequiredService<AppIdentityDbContext>();
        return await identityDb.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.TenantId)
            .FirstOrDefaultAsync();
    }

    private static async Task<bool> ReferencesMatchTenantAsync(FinanceDbContext db, Transaction transaction, string tenantId)
    {
        if (transaction.CategoryId.HasValue && !await db.Categories.AnyAsync(category => category.Id == transaction.CategoryId && category.TenantId == tenantId))
            return false;
        if (transaction.PersonId.HasValue && !await db.Persons.AnyAsync(person => person.Id == transaction.PersonId && person.TenantId == tenantId))
            return false;
        return !transaction.PaymentMethodId.HasValue || await db.PaymentMethods.AnyAsync(method => method.Id == transaction.PaymentMethodId && method.TenantId == tenantId);
    }
}
