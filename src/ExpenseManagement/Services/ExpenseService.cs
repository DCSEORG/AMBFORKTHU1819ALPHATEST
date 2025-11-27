using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<(List<Expense> Expenses, string? Error)> GetAllExpensesAsync();
    Task<(Expense? Expense, string? Error)> GetExpenseByIdAsync(int expenseId);
    Task<(List<Expense> Expenses, string? Error)> GetExpensesByStatusAsync(string statusName);
    Task<(List<Expense> Expenses, string? Error)> GetExpensesByUserAsync(int userId);
    Task<(int ExpenseId, string? Error)> CreateExpenseAsync(CreateExpenseRequest request);
    Task<(bool Success, string? Error)> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request);
    Task<(bool Success, string? Error)> SubmitExpenseAsync(int expenseId);
    Task<(bool Success, string? Error)> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, string? Error)> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, string? Error)> DeleteExpenseAsync(int expenseId);
    Task<(DashboardStats Stats, string? Error)> GetDashboardStatsAsync();
    Task<(List<ExpenseCategory> Categories, string? Error)> GetCategoriesAsync();
    Task<(List<ExpenseStatus> Statuses, string? Error)> GetStatusesAsync();
    Task<(List<User> Users, string? Error)> GetUsersAsync();
    Task<(List<Expense> Expenses, string? Error)> SearchExpensesAsync(SearchExpensesRequest request);
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;
    private string? _lastError;

    public string? LastError => _lastError;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string GetConnectionString()
    {
        return _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetAllExpensesAsync()
    {
        try
        {
            var expenses = new List<Expense>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetAllExpenses", connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            _lastError = null;
            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all expenses");
            _lastError = FormatError(ex);
            return (DummyData.GetDummyExpenses(), _lastError);
        }
    }

    public async Task<(Expense? Expense, string? Error)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetExpenseById @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return (MapExpense(reader), null);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", expenseId);
            _lastError = FormatError(ex);
            return (DummyData.GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId), _lastError);
        }
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetExpensesByStatusAsync(string statusName)
    {
        try
        {
            var expenses = new List<Expense>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetExpensesByStatus @StatusName", connection);
            command.Parameters.AddWithValue("@StatusName", statusName);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by status {StatusName}", statusName);
            _lastError = FormatError(ex);
            return (DummyData.GetDummyExpenses().Where(e => e.StatusName == statusName).ToList(), _lastError);
        }
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetExpensesByUserAsync(int userId)
    {
        try
        {
            var expenses = new List<Expense>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetExpensesByUser @UserId", connection);
            command.Parameters.AddWithValue("@UserId", userId);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by user {UserId}", userId);
            _lastError = FormatError(ex);
            return (DummyData.GetDummyExpenses().Where(e => e.UserId == userId).ToList(), _lastError);
        }
    }

    public async Task<(int ExpenseId, string? Error)> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand(
                "EXEC sp_CreateExpense @UserId, @CategoryId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile", 
                connection);
            
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", "GBP");
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            _lastError = FormatError(ex);
            return (0, _lastError);
        }
    }

    public async Task<(bool Success, string? Error)> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand(
                "EXEC sp_UpdateExpense @ExpenseId, @CategoryId, @AmountMinor, @ExpenseDate, @Description, @ReceiptFile", 
                connection);
            
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result) > 0, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_SubmitExpense @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result) > 0, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_ApproveExpense @ExpenseId, @ReviewerId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result) > 0, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_RejectExpense @ExpenseId, @ReviewerId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result) > 0, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_DeleteExpense @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result) > 0, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(DashboardStats Stats, string? Error)> GetDashboardStatsAsync()
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetDashboardStats", connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return (new DashboardStats
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    ApprovedAmountMinor = reader.GetInt32(reader.GetOrdinal("ApprovedAmountMinor")),
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                }, null);
            }

            return (new DashboardStats(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            _lastError = FormatError(ex);
            return (DummyData.GetDummyDashboardStats(), _lastError);
        }
    }

    public async Task<(List<ExpenseCategory> Categories, string? Error)> GetCategoriesAsync()
    {
        try
        {
            var categories = new List<ExpenseCategory>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetAllCategories", connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return (categories, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            _lastError = FormatError(ex);
            return (DummyData.GetDummyCategories(), _lastError);
        }
    }

    public async Task<(List<ExpenseStatus> Statuses, string? Error)> GetStatusesAsync()
    {
        try
        {
            var statuses = new List<ExpenseStatus>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetAllStatuses", connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }

            return (statuses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            _lastError = FormatError(ex);
            return (DummyData.GetDummyStatuses(), _lastError);
        }
    }

    public async Task<(List<User> Users, string? Error)> GetUsersAsync()
    {
        try
        {
            var users = new List<User>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("EXEC sp_GetAllUsers", connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }

            return (users, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            _lastError = FormatError(ex);
            return (DummyData.GetDummyUsers(), _lastError);
        }
    }

    public async Task<(List<Expense> Expenses, string? Error)> SearchExpensesAsync(SearchExpensesRequest request)
    {
        try
        {
            var expenses = new List<Expense>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand(
                "EXEC sp_SearchExpenses @SearchTerm, @CategoryId, @StatusId, @UserId, @StartDate, @EndDate", 
                connection);
            
            command.Parameters.AddWithValue("@SearchTerm", (object?)request.SearchTerm ?? DBNull.Value);
            command.Parameters.AddWithValue("@CategoryId", (object?)request.CategoryId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", (object?)request.StatusId ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserId", (object?)request.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StartDate", (object?)request.StartDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@EndDate", (object?)request.EndDate ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching expenses");
            _lastError = FormatError(ex);
            return (DummyData.GetDummyExpenses(), _lastError);
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            AmountGBP = reader.GetDecimal(reader.GetOrdinal("AmountGBP")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private string FormatError(Exception ex)
    {
        var errorMessage = $"Database Error: {ex.Message}";
        
        // Add managed identity specific guidance
        if (ex.Message.Contains("Login failed") || ex.Message.Contains("authentication"))
        {
            errorMessage += "\n\nManaged Identity Issue: The application could not authenticate to the database using the managed identity. " +
                           "Please ensure:\n" +
                           "1. The managed identity is properly assigned to the App Service\n" +
                           "2. The managed identity has been granted access in Azure SQL (CREATE USER [identity-name] FROM EXTERNAL PROVIDER)\n" +
                           "3. The AZURE_CLIENT_ID environment variable is set to the managed identity's client ID\n" +
                           "4. The connection string uses 'Authentication=Active Directory Managed Identity'";
        }
        
        // Add source file reference
        errorMessage += $"\n\nSource: Services/ExpenseService.cs";
        
        return errorMessage;
    }
}
