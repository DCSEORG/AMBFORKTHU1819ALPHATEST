# Chat UI

This folder contains documentation and resources for the Chat UI feature of the Expense Management System.

## Overview

The Chat UI is integrated directly into the main application as a floating chat widget. It provides natural language interaction with the expense management system.

## Features

- **Natural Language Queries**: Ask questions about expenses, approvals, and statistics
- **Function Calling**: When GenAI is enabled, the chat can execute real operations
- **Formatted Responses**: Lists and data are displayed in a readable format
- **Error Handling**: Graceful fallback when GenAI services are not available

## Deployment Options

### Without GenAI (deploy.sh)
- Chat UI is available but uses predefined responses
- Shows sample data and helpful suggestions
- Displays note about enabling GenAI for full functionality

### With GenAI (deploy-with-chat.sh)
- Full AI-powered natural language understanding
- Can query real expense data
- Can create, submit, and approve expenses via chat
- Uses Azure OpenAI GPT-4o model

## Available Chat Functions

When GenAI is enabled, the following functions are available:

| Function | Description |
|----------|-------------|
| `get_all_expenses` | Retrieves all expenses |
| `get_expenses_by_status` | Filter expenses by status |
| `get_dashboard_stats` | Get summary statistics |
| `get_categories` | List expense categories |
| `create_expense` | Create a new expense |
| `submit_expense` | Submit expense for approval |
| `approve_expense` | Approve a submitted expense |
| `search_expenses` | Search with filters |

## Example Queries

- "Show all expenses"
- "What expenses are pending approval?"
- "Show dashboard statistics"
- "Create a new travel expense for £50"
- "Submit expense #3"
- "Approve expense #1"

## Security

- Uses Managed Identity for Azure OpenAI authentication
- No API keys stored in code or configuration
- Role-based access through Azure RBAC
