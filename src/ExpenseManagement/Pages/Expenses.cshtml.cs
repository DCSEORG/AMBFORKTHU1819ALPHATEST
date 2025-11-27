using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? SelectedCategory { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? SelectedStatus { get; set; }

    public ExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync(string? search, int? category, int? status)
    {
        SearchTerm = search;
        SelectedCategory = category;
        SelectedStatus = status;
        
        var (categories, _) = await _expenseService.GetCategoriesAsync();
        Categories = categories;
        
        var (statuses, _) = await _expenseService.GetStatusesAsync();
        Statuses = statuses;
        
        if (!string.IsNullOrEmpty(search) || category.HasValue || status.HasValue)
        {
            var searchRequest = new SearchExpensesRequest
            {
                SearchTerm = search,
                CategoryId = category,
                StatusId = status
            };
            var (expenses, error) = await _expenseService.SearchExpensesAsync(searchRequest);
            Expenses = expenses;
            ErrorMessage = error;
        }
        else
        {
            var (expenses, error) = await _expenseService.GetAllExpensesAsync();
            Expenses = expenses;
            ErrorMessage = error;
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        await _expenseService.SubmitExpenseAsync(expenseId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int expenseId)
    {
        await _expenseService.DeleteExpenseAsync(expenseId);
        return RedirectToPage();
    }
}
