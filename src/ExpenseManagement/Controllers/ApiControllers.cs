using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetAll()
    {
        var (expenses, error) = await _expenseService.GetAllExpensesAsync();
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
        {
            return NotFound();
        }
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(expense);
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{statusName}")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetByStatus(string statusName)
    {
        var (expenses, error) = await _expenseService.GetExpensesByStatusAsync(statusName);
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Get expenses by user
    /// </summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetByUser(int userId)
    {
        var (expenses, error) = await _expenseService.GetExpensesByUserAsync(userId);
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Search expenses
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> Search([FromBody] SearchExpensesRequest request)
    {
        var (expenses, error) = await _expenseService.SearchExpensesAsync(request);
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] CreateExpenseRequest request)
    {
        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        if (error != null)
        {
            return BadRequest(new { error });
        }
        return CreatedAtAction(nameof(GetById), new { id = expenseId }, new { expenseId });
    }

    /// <summary>
    /// Update an expense
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        var (success, error) = await _expenseService.UpdateExpenseAsync(id, request);
        if (error != null)
        {
            return BadRequest(new { error });
        }
        return Ok(new { success });
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Submit(int id)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id);
        if (error != null)
        {
            return BadRequest(new { error });
        }
        return Ok(new { success });
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Approve(int id, [FromQuery] int reviewerId)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        if (error != null)
        {
            return BadRequest(new { error });
        }
        return Ok(new { success });
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Reject(int id, [FromQuery] int reviewerId)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, reviewerId);
        if (error != null)
        {
            return BadRequest(new { error });
        }
        return Ok(new { success });
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(int id)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
        if (error != null)
        {
            return BadRequest(new { error });
        }
        return Ok(new { success });
    }

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardStats>> GetDashboardStats()
    {
        var (stats, error) = await _expenseService.GetDashboardStatsAsync();
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(stats);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public CategoriesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ExpenseCategory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpenseCategory>>> GetAll()
    {
        var (categories, error) = await _expenseService.GetCategoriesAsync();
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(categories);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public StatusesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all statuses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ExpenseStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpenseStatus>>> GetAll()
    {
        var (statuses, error) = await _expenseService.GetStatusesAsync();
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(statuses);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public UsersController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<User>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        var (users, error) = await _expenseService.GetUsersAsync();
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        return Ok(users);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Send a chat message
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        var response = await _chatService.GetChatResponseAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Check if GenAI is enabled
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new { isGenAIEnabled = _chatService.IsGenAIEnabled });
    }
}
