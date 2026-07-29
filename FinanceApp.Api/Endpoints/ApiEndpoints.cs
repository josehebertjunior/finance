using FinanceApp.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;

namespace FinanceApp.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/categories", async (FinanceDbContext db) =>
            await db.Categories.ToListAsync());

        api.MapPost("/categories", async (FinanceDbContext db, Category category) =>
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return Results.BadRequest(new { error = "Name is required." });

            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{category.Id}", category);
        });

        api.MapPut("/categories/{id}", async (FinanceDbContext db, int id, Category input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var category = await db.Categories.FindAsync(id);
            if (category == null) return Results.NotFound();

            category.Name = input.Name;
            category.ColorHex = input.ColorHex;
            await db.SaveChangesAsync();
            return Results.Ok(category);
        });

        api.MapGet("/paymentmethods", async (FinanceDbContext db) =>
            await db.PaymentMethods.ToListAsync());

        api.MapPost("/paymentmethods", async (FinanceDbContext db, PaymentMethod pm) =>
        {
            if (string.IsNullOrWhiteSpace(pm.Name))
                return Results.BadRequest(new { error = "Name is required." });

            db.PaymentMethods.Add(pm);
            await db.SaveChangesAsync();
            return Results.Created($"/api/paymentmethods/{pm.Id}", pm);
        });

        api.MapPut("/paymentmethods/{id}", async (FinanceDbContext db, int id, PaymentMethod input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var pm = await db.PaymentMethods.FindAsync(id);
            if (pm == null) return Results.NotFound();

            pm.Name = input.Name;
            pm.IsCreditCard = input.IsCreditCard;
            await db.SaveChangesAsync();
            return Results.Ok(pm);
        });

        api.MapGet("/persons", async (FinanceDbContext db) =>
            await db.Persons.ToListAsync());

        api.MapPost("/persons", async (FinanceDbContext db, Person person) =>
        {
            if (string.IsNullOrWhiteSpace(person.Name))
                return Results.BadRequest(new { error = "Name is required." });

            db.Persons.Add(person);
            await db.SaveChangesAsync();
            return Results.Created($"/api/persons/{person.Id}", person);
        });

        api.MapPut("/persons/{id}", async (FinanceDbContext db, int id, Person input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var person = await db.Persons.FindAsync(id);
            if (person == null) return Results.NotFound();

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
                var start = new DateTime(y, m, 1);
                var end = start.AddMonths(1);
                query = query.Where(t => t.ReferenceMonth >= start && t.ReferenceMonth < end);
            }

            if (!user.IsInRole("Admin") && !string.IsNullOrEmpty(sub))
            {
                query = query.Where(t => t.OwnerId == sub);
            }

            return Results.Ok(await query.ToListAsync());
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

            if (t.InstallmentTotal > 1)
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
                        OwnerId = sub ?? string.Empty
                    };
                    db.Transactions.Add(installment);
                }
            }
            else
            {
                t.OwnerId = sub ?? string.Empty;
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

            var t = await db.Transactions.FindAsync(id);
            if (t == null) return Results.NotFound();
            if (!ctx.User.IsInRole("Admin") && t.OwnerId != (sub ?? string.Empty)) return Results.Forbid();

            t.Description = input.Description;
            t.Amount = input.Amount;
            t.Type = input.Type;
            t.Date = input.Date;
            t.ReferenceMonth = input.ReferenceMonth;
            t.CategoryId = input.CategoryId;
            t.PersonId = input.PersonId;
            t.PaymentMethodId = input.PaymentMethodId;
            t.IsFixed = input.IsFixed;

            await db.SaveChangesAsync();
            return Results.Ok(t);
        });

        api.MapGet("/summary/by-category", async (FinanceDbContext db, HttpContext ctx, int year, int month) =>
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var user = ctx.User;
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            var baseQuery = db.Transactions.Where(t => t.ReferenceMonth >= start && t.ReferenceMonth < end && t.Type == TransactionType.Expense).Include(t => t.Category).AsQueryable();
            if (!user.IsInRole("Admin") && !string.IsNullOrEmpty(sub)) baseQuery = baseQuery.Where(t => t.OwnerId == sub);

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
            if (!user.IsInRole("Admin") && !string.IsNullOrEmpty(sub)) { depositsQ = depositsQ.Where(t => t.OwnerId == sub); withdrawalsQ = withdrawalsQ.Where(t => t.OwnerId == sub); }
            var deposits = await depositsQ.SumAsync(t => (decimal?)t.Amount) ?? 0m;
            var withdrawals = await withdrawalsQ.SumAsync(t => (decimal?)t.Amount) ?? 0m;
            return Results.Ok(new { Balance = deposits - withdrawals });
        });

        api.MapDelete("/transactions/{id}", async (FinanceDbContext db, HttpContext ctx, int id) =>
        {
            var t = await db.Transactions.FindAsync(id);
            if (t == null) return Results.NotFound();
            var user = ctx.User;
            var sub = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (!user.IsInRole("Admin") && t.OwnerId != (sub ?? string.Empty)) return Results.Forbid();
            db.Transactions.Remove(t);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapDelete("/categories/{id}", async (FinanceDbContext db, int id) =>
        {
            var c = await db.Categories.FindAsync(id);
            if (c != null) { db.Categories.Remove(c); await db.SaveChangesAsync(); }
            return Results.Ok();
        });

        api.MapDelete("/persons/{id}", async (FinanceDbContext db, int id) =>
        {
            var p = await db.Persons.FindAsync(id);
            if (p != null) { db.Persons.Remove(p); await db.SaveChangesAsync(); }
            return Results.Ok();
        });

        api.MapDelete("/paymentmethods/{id}", async (FinanceDbContext db, int id) =>
        {
            var p = await db.PaymentMethods.FindAsync(id);
            if (p != null) { db.PaymentMethods.Remove(p); await db.SaveChangesAsync(); }
            return Results.Ok();
        });
    }
}
