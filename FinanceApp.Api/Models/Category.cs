namespace FinanceApp.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FFFFFF"; // For dark mode optional customization
}
