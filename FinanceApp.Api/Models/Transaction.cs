using System;

namespace FinanceApp.Api.Models;

public class Transaction
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    
    public DateTime Date { get; set; } 
    public DateTime ReferenceMonth { get; set; } 

    // Relationships
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    // Recurring/Installment properties
    public bool IsFixed { get; set; }
    public int? InstallmentCurrent { get; set; }
    public int? InstallmentTotal { get; set; }
    public Guid? InstallmentGroupId { get; set; }

    // Ownership
    public string OwnerId { get; set; } = string.Empty;
}

public enum TransactionType
{
    Income = 0,
    Expense = 1,
    SavingsDeposit = 2,
    SavingsWithdrawal = 3
}
