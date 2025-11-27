using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    [BindProperty]
    public decimal Amount { get; set; }
    
    [BindProperty]
    public DateTime? ExpenseDate { get; set; }
    
    [BindProperty]
    public int CategoryId { get; set; }
    
    [BindProperty]
    public int UserId { get; set; }
    
    [BindProperty]
    public string? Description { get; set; }

    public NewExpenseModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        ExpenseDate = DateTime.Today;
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadDataAsync();
        
        if (Amount <= 0)
        {
            ErrorMessage = "Amount must be greater than 0";
            return Page();
        }
        
        if (!ExpenseDate.HasValue)
        {
            ErrorMessage = "Please select a date";
            return Page();
        }
        
        if (CategoryId == 0)
        {
            ErrorMessage = "Please select a category";
            return Page();
        }
        
        if (UserId == 0)
        {
            ErrorMessage = "Please select a user";
            return Page();
        }
        
        var request = new CreateExpenseRequest
        {
            UserId = UserId,
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate.Value,
            Description = Description
        };
        
        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        
        if (error != null)
        {
            ErrorMessage = error;
            return Page();
        }
        
        SuccessMessage = $"Expense created successfully with ID: {expenseId}";
        
        // Reset form
        Amount = 0;
        ExpenseDate = DateTime.Today;
        CategoryId = 0;
        Description = null;
        
        return Page();
    }

    private async Task LoadDataAsync()
    {
        var (categories, _) = await _expenseService.GetCategoriesAsync();
        Categories = categories;
        
        var (users, _) = await _expenseService.GetUsersAsync();
        Users = users;
    }
}
