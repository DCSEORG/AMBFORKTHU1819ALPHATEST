# Azure Services Architecture

This diagram shows the Azure services deployed by the infrastructure scripts and how they connect to each other.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Azure Resource Group                                │
│                              (rg-expensemgmt-demo)                              │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  ┌─────────────────────┐         ┌─────────────────────┐                        │
│  │   User Assigned     │         │    Azure App        │                        │
│  │  Managed Identity   │◄───────►│     Service         │                        │
│  │  (mid-expensemgmt)  │         │  (app-expensemgmt)  │                        │
│  └─────────┬───────────┘         └──────────┬──────────┘                        │
│            │                                 │                                   │
│            │ Authentication                  │ HTTPS                            │
│            ▼                                 ▼                                   │
│  ┌─────────────────────┐         ┌─────────────────────┐                        │
│  │     Azure SQL       │◄────────│    Web Browser      │                        │
│  │      Server         │         │     (Client)        │                        │
│  │  (sql-expensemgmt)  │         └─────────────────────┘                        │
│  │                     │                                                         │
│  │  Database:          │                                                         │
│  │  - Northwind        │                                                         │
│  └─────────────────────┘                                                         │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │                    GenAI Resources (Optional)                                ││
│  │                    (Deployed with deploy-with-chat.sh)                       ││
│  │  ┌─────────────────────┐         ┌─────────────────────┐                    ││
│  │  │   Azure OpenAI      │         │    Azure AI         │                    ││
│  │  │   (swedencentral)   │         │     Search          │                    ││
│  │  │                     │         │   (uksouth)         │                    ││
│  │  │  Model: GPT-4o      │         └─────────────────────┘                    ││
│  │  │  Capacity: 8        │                                                     ││
│  │  └─────────────────────┘                                                     ││
│  └─────────────────────────────────────────────────────────────────────────────┘│
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Data Flow

1. **User Request Flow:**
   - User accesses the App Service via HTTPS
   - App Service authenticates to Azure SQL using Managed Identity
   - Data is retrieved via stored procedures and returned to the user

2. **Chat UI Flow (when GenAI is enabled):**
   - User sends chat message
   - App Service calls Azure OpenAI with function definitions
   - Azure OpenAI may invoke functions to query/modify expense data
   - Response is returned to the user

## Authentication

- **App Service → Azure SQL:** User Assigned Managed Identity with AD authentication
- **App Service → Azure OpenAI:** User Assigned Managed Identity with Cognitive Services OpenAI User role
- **App Service → Azure AI Search:** User Assigned Managed Identity with Search Index Data Reader role

## Resources Summary

| Resource | SKU | Location | Purpose |
|----------|-----|----------|---------|
| App Service Plan | S1 | UK South | Host web application |
| App Service | - | UK South | ASP.NET Core Razor Pages app |
| Managed Identity | - | UK South | Secure authentication |
| Azure SQL Server | - | UK South | Database server |
| Azure SQL Database | Basic | UK South | Expense data storage |
| Azure OpenAI | S0 | Sweden Central | GPT-4o for chat |
| Azure AI Search | Free | UK South | RAG pattern support |

## Deployment Scripts

- `deploy.sh` - Deploys App Service + SQL Database (no GenAI)
- `deploy-with-chat.sh` - Deploys all resources including GenAI services
