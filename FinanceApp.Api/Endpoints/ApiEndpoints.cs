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

        api.MapGet("/transactions", async (FinanceDbContext db, int? year, int? month) =>
        {
            var query = db.Transactions
                .Include(t => t.Category)
                .Include(t => t.Person)
                .Include(t => t.PaymentMethod)
                .AsQueryable();

            if (year.HasValue && month.HasValue)
            {
                var start = new DateTime(year.Value, month.Value, 1);
                var end = start.AddMonths(1);
                query = query.Where(t => t.ReferenceMonth >= start && t.ReferenceMonth < end);
            }

            return await query.ToListAsync();
        });

        api.MapPost("/transactions", async (FinanceDbContext db, Transaction t) =>
        {
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
                        InstallmentGroupId = groupId
                    };
                    db.Transactions.Add(installment);
                }
            }
            else
            {
                db.Transactions.Add(t);
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        api.MapPut("/transactions/{id}", async (FinanceDbContext db, int id, Transaction input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Description))
                return Results.BadRequest(new { error = "Description is required." });
            if (input.Amount <= 0)
                return Results.BadRequest(new { error = "Amount must be greater than zero." });

            var t = await db.Transactions.FindAsync(id);
            if (t == null) return Results.NotFound();

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

        api.MapGet("/summary/by-category", async (FinanceDbContext db, int year, int month) =>
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var expenses = await db.Transactions
                .Where(t => t.ReferenceMonth >= start && t.ReferenceMonth < end && t.Type == TransactionType.Expense)
                .Include(t => t.Category)
                .ToListAsync();

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

        api.MapGet("/savings/balance", async (FinanceDbContext db) =>
        {
            var deposits = await db.Transactions.Where(t => t.Type == TransactionType.SavingsDeposit).SumAsync(t => (decimal?)t.Amount) ?? 0m;
            var withdrawals = await db.Transactions.Where(t => t.Type == TransactionType.SavingsWithdrawal).SumAsync(t => (decimal?)t.Amount) ?? 0m;
            return Results.Ok(new { Balance = deposits - withdrawals });
        });

        api.MapDelete("/transactions/{id}", async (FinanceDbContext db, int id) =>
        {
            var t = await db.Transactions.FindAsync(id);
            if (t == null) return Results.NotFound();
            
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
