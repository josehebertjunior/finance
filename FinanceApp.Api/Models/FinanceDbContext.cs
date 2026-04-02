using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Api.Models;

public class FinanceDbContext : DbContext
{
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options) { }

    public DbSet<Person> Persons { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
}
