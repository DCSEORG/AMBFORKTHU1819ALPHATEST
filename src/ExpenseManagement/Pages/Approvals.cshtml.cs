using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> PendingExpenses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public ApprovalsModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _expenseService.GetExpensesByStatusAsync("Submitted");
        
        if (!string.IsNullOrEmpty(Filter))
        {
            expenses = expenses.Where(e => 
                (e.UserName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }
        
        PendingExpenses = expenses;
        ErrorMessage = error;
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewerId)
    {
        await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewerId)
    {
        await _expenseService.RejectExpenseAsync(expenseId, reviewerId);
        return RedirectToPage();
    }
}
