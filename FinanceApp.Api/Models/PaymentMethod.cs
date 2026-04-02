namespace FinanceApp.Api.Models;

public class PaymentMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., NU, C6, ITAU, Débito
    public bool IsCreditCard { get; set; }
}
