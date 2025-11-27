using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IExpenseService _expenseService;

    public DashboardStats Stats { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public IndexModel(ILogger<IndexModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        var (stats, statsError) = await _expenseService.GetDashboardStatsAsync();
        Stats = stats;
        
        var (expenses, expensesError) = await _expenseService.GetAllExpensesAsync();
        RecentExpenses = expenses;
        
        ErrorMessage = statsError ?? expensesError;
    }
}
