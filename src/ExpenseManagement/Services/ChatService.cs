using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> GetChatResponseAsync(ChatRequest request);
    bool IsGenAIEnabled { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private OpenAIClient? _openAIClient;
    private readonly string? _deploymentName;
    private readonly bool _isGenAIEnabled;

    public bool IsGenAIEnabled => _isGenAIEnabled;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        
        var endpoint = _configuration["OpenAI:Endpoint"];
        _deploymentName = _configuration["OpenAI:DeploymentName"];
        _isGenAIEnabled = !string.IsNullOrEmpty(endpoint) && 
                          !string.IsNullOrEmpty(_deploymentName) &&
                          _configuration.GetValue<bool>("GenAI:Enabled", false);
        
        if (_isGenAIEnabled)
        {
            try
            {
                var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
                TokenCredential credential;
                
                if (!string.IsNullOrEmpty(managedIdentityClientId))
                {
                    _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                    credential = new ManagedIdentityCredential(managedIdentityClientId);
                }
                else
                {
                    _logger.LogInformation("Using DefaultAzureCredential");
                    credential = new DefaultAzureCredential();
                }
                
                _openAIClient = new OpenAIClient(new Uri(endpoint!), credential);
                _logger.LogInformation("OpenAI client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenAI client");
                _isGenAIEnabled = false;
            }
        }
    }

    public async Task<ChatResponse> GetChatResponseAsync(ChatRequest request)
    {
        if (!_isGenAIEnabled || _openAIClient == null)
        {
            return new ChatResponse
            {
                Message = GetDummyResponse(request.Message),
                IsGenAIEnabled = false
            };
        }

        try
        {
            var chatMessages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(GetSystemPrompt())
            };

            // Add conversation history
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    if (msg.Role == "user")
                        chatMessages.Add(new ChatRequestUserMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        chatMessages.Add(new ChatRequestAssistantMessage(msg.Content));
                }
            }

            chatMessages.Add(new ChatRequestUserMessage(request.Message));

            var options = new ChatCompletionsOptions(_deploymentName, chatMessages)
            {
                Temperature = 0.7f,
                MaxTokens = 1000
            };

            // Add function definitions using the correct API
            AddFunctionTools(options);

            var response = await _openAIClient.GetChatCompletionsAsync(options);
            var choice = response.Value.Choices[0];

            // Handle function calls
            while (choice.FinishReason == CompletionsFinishReason.ToolCalls)
            {
                var assistantMessage = new ChatRequestAssistantMessage(choice.Message);
                chatMessages.Add(assistantMessage);

                foreach (var toolCall in choice.Message.ToolCalls.OfType<ChatCompletionsFunctionToolCall>())
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.Name, toolCall.Arguments);
                    chatMessages.Add(new ChatRequestToolMessage(functionResult, toolCall.Id));
                }

                options = new ChatCompletionsOptions(_deploymentName, chatMessages);
                response = await _openAIClient.GetChatCompletionsAsync(options);
                choice = response.Value.Choices[0];
            }

            return new ChatResponse
            {
                Message = choice.Message.Content ?? "I couldn't generate a response.",
                IsGenAIEnabled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            return new ChatResponse
            {
                Message = $"I encountered an error: {ex.Message}. Please try again.",
                IsGenAIEnabled = true
            };
        }
    }

    private void AddFunctionTools(ChatCompletionsOptions options)
    {
        // get_all_expenses
        var getAllExpenses = new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_all_expenses",
            Description = "Retrieves all expenses from the database with their details including amount, category, status, and user information"
        };
        options.Tools.Add(getAllExpenses);

        // get_expenses_by_status
        var getByStatus = new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_expenses_by_status",
            Description = "Retrieves expenses filtered by status (Draft, Submitted, Approved, Rejected)",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    status = new { type = "string", description = "The status to filter by: Draft, Submitted, Approved, or Rejected" }
                },
                required = new[] { "status" }
            })
        };
        options.Tools.Add(getByStatus);

        // get_dashboard_stats
        var getDashboardStats = new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_dashboard_stats",
            Description = "Retrieves dashboard statistics including total expenses, pending approvals, approved amount, and approved count"
        };
        options.Tools.Add(getDashboardStats);

        // get_categories
        var getCategories = new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_categories",
            Description = "Retrieves all expense categories"
        };
        options.Tools.Add(getCategories);

        // create_expense
        var createExpense = new ChatCompletionsFunctionToolDefinition
        {
            Name = "create_expense",
            Description = "Creates a new expense in draft status",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    userId = new { type = "integer", description = "The ID of the user creating the expense" },
                    categoryId = new { type = "integer", description = "The category ID for the expense" },
                    amount = new { type = "number", description = "The expense amount in GBP" },
                    expenseDate = new { type = "string", description = "The date of the expense (YYYY-MM-DD format)" },
                    description = new { type = "string", description = "Description of the expense" }
                },
                required = new[] { "userId", "categoryId", "amount", "expenseDate" }
            })
        };
        options.Tools.Add(createExpense);

        // submit_expense
        var submitExpense = new ChatCompletionsFunctionToolDefinition
        {
            Name = "submit_expense",
            Description = "Submits a draft expense for approval",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    expenseId = new { type = "integer", description = "The ID of the expense to submit" }
                },
                required = new[] { "expenseId" }
            })
        };
        options.Tools.Add(submitExpense);

        // approve_expense
        var approveExpense = new ChatCompletionsFunctionToolDefinition
        {
            Name = "approve_expense",
            Description = "Approves a submitted expense (manager action)",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    expenseId = new { type = "integer", description = "The ID of the expense to approve" },
                    reviewerId = new { type = "integer", description = "The ID of the manager approving the expense" }
                },
                required = new[] { "expenseId", "reviewerId" }
            })
        };
        options.Tools.Add(approveExpense);

        // search_expenses
        var searchExpenses = new ChatCompletionsFunctionToolDefinition
        {
            Name = "search_expenses",
            Description = "Searches expenses with various filters",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    searchTerm = new { type = "string", description = "Text to search in description or user name" },
                    categoryId = new { type = "integer", description = "Filter by category ID" },
                    statusId = new { type = "integer", description = "Filter by status ID" },
                    userId = new { type = "integer", description = "Filter by user ID" }
                }
            })
        };
        options.Tools.Add(searchExpenses);
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments);

            switch (functionName)
            {
                case "get_all_expenses":
                    var (expenses, _) = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.AmountGBP:N2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.StatusName,
                        e.Description
                    }));

                case "get_expenses_by_status":
                    var status = args.RootElement.GetProperty("status").GetString() ?? "Draft";
                    var (statusExpenses, _) = await _expenseService.GetExpensesByStatusAsync(status);
                    return JsonSerializer.Serialize(statusExpenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.AmountGBP:N2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "get_dashboard_stats":
                    var (stats, _) = await _expenseService.GetDashboardStatsAsync();
                    return JsonSerializer.Serialize(new
                    {
                        stats.TotalExpenses,
                        stats.PendingApprovals,
                        ApprovedAmount = $"£{stats.ApprovedAmountGBP:N2}",
                        stats.ApprovedCount
                    });

                case "get_categories":
                    var (categories, _) = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "create_expense":
                    var createRequest = new CreateExpenseRequest
                    {
                        UserId = args.RootElement.GetProperty("userId").GetInt32(),
                        CategoryId = args.RootElement.GetProperty("categoryId").GetInt32(),
                        Amount = args.RootElement.GetProperty("amount").GetDecimal(),
                        ExpenseDate = DateTime.Parse(args.RootElement.GetProperty("expenseDate").GetString()!),
                        Description = args.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() : null
                    };
                    var (expenseId, createError) = await _expenseService.CreateExpenseAsync(createRequest);
                    return createError != null 
                        ? JsonSerializer.Serialize(new { error = createError })
                        : JsonSerializer.Serialize(new { success = true, expenseId });

                case "submit_expense":
                    var submitExpenseId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var (submitSuccess, submitError) = await _expenseService.SubmitExpenseAsync(submitExpenseId);
                    return JsonSerializer.Serialize(new { success = submitSuccess, error = submitError });

                case "approve_expense":
                    var approveExpenseId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var reviewerId = args.RootElement.GetProperty("reviewerId").GetInt32();
                    var (approveSuccess, approveError) = await _expenseService.ApproveExpenseAsync(approveExpenseId, reviewerId);
                    return JsonSerializer.Serialize(new { success = approveSuccess, error = approveError });

                case "search_expenses":
                    var searchRequest = new SearchExpensesRequest();
                    if (args.RootElement.TryGetProperty("searchTerm", out var searchTerm))
                        searchRequest.SearchTerm = searchTerm.GetString();
                    if (args.RootElement.TryGetProperty("categoryId", out var catId))
                        searchRequest.CategoryId = catId.GetInt32();
                    if (args.RootElement.TryGetProperty("statusId", out var statId))
                        searchRequest.StatusId = statId.GetInt32();
                    if (args.RootElement.TryGetProperty("userId", out var usrId))
                        searchRequest.UserId = usrId.GetInt32();
                    
                    var (searchResults, _) = await _expenseService.SearchExpensesAsync(searchRequest);
                    return JsonSerializer.Serialize(searchResults.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.AmountGBP:N2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.StatusName,
                        e.Description
                    }));

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an intelligent assistant for the Expense Management System. You help users manage their expenses, view reports, and navigate the application.

You have access to the following functions to interact with the expense database:
- get_all_expenses: Get all expenses with their details
- get_expenses_by_status: Get expenses filtered by status (Draft, Submitted, Approved, Rejected)
- get_dashboard_stats: Get dashboard statistics
- get_categories: Get all expense categories
- create_expense: Create a new expense
- submit_expense: Submit an expense for approval
- approve_expense: Approve an expense (manager only)
- search_expenses: Search expenses with filters

When users ask about expenses, use these functions to get real data. Always format currency as GBP (£).
When listing items, use a clear formatted list.
Be helpful, concise, and professional.

Available expense categories are: Travel, Meals, Supplies, Accommodation, Other.
Expense statuses are: Draft, Submitted, Approved, Rejected.";
    }

    private string GetDummyResponse(string message)
    {
        var lowerMessage = message.ToLower();
        
        if (lowerMessage.Contains("expense") && (lowerMessage.Contains("list") || lowerMessage.Contains("show") || lowerMessage.Contains("all")))
        {
            return @"Here are the expenses from our sample data:

1. **Alice Example** - Travel - £120.00 - Submitted
   Taxi from airport to client site

2. **Alice Example** - Meals - £69.00 - Approved
   Client lunch meeting

3. **Alice Example** - Supplies - £99.50 - Draft
   Office supplies

4. **Alice Example** - Accommodation - £192.00 - Approved
   Hotel for client visit

**Note:** GenAI services are not deployed. To enable AI-powered responses, run `deploy-with-chat.sh` instead of `deploy.sh`.";
        }
        
        if (lowerMessage.Contains("pending") || lowerMessage.Contains("approval"))
        {
            return @"There is **1 expense pending approval**:

- **Alice Example** - Travel - £120.00
  Taxi from airport to client site

**Note:** GenAI services are not deployed. To enable AI-powered responses, run `deploy-with-chat.sh` instead of `deploy.sh`.";
        }
        
        if (lowerMessage.Contains("dashboard") || lowerMessage.Contains("stats") || lowerMessage.Contains("summary"))
        {
            return @"**Dashboard Summary (Sample Data)**:

- Total Expenses: 4
- Pending Approvals: 1
- Total Approved Amount: £261.00
- Approved Count: 2

**Note:** GenAI services are not deployed. To enable AI-powered responses, run `deploy-with-chat.sh` instead of `deploy.sh`.";
        }
        
        if (lowerMessage.Contains("categor"))
        {
            return @"**Available Expense Categories**:

1. Travel
2. Meals
3. Supplies
4. Accommodation
5. Other

**Note:** GenAI services are not deployed. To enable AI-powered responses, run `deploy-with-chat.sh` instead of `deploy.sh`.";
        }
        
        if (lowerMessage.Contains("help"))
        {
            return @"**How can I help you?**

I can assist you with:
- Viewing expenses (try: ""show all expenses"")
- Checking pending approvals (try: ""show pending expenses"")
- Viewing dashboard statistics (try: ""show dashboard"")
- Understanding expense categories (try: ""what categories are available?"")

**Note:** GenAI services are not deployed. For full AI capabilities including creating and managing expenses through chat, run `deploy-with-chat.sh` instead of `deploy.sh`.";
        }
        
        return @"I'm the Expense Management Assistant. I can help you with viewing and managing expenses.

**Try asking me:**
- ""Show all expenses""
- ""What expenses are pending approval?""
- ""Show dashboard statistics""
- ""What expense categories are available?""

**Note:** GenAI services are not deployed. For full AI-powered responses and the ability to create/manage expenses through chat, run `deploy-with-chat.sh` instead of `deploy.sh`.";
    }
}
