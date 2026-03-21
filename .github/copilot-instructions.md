# PlanToSave — Copilot Instructions

## Project Overview

PlanToSave is a **life-first financial planner** built with Blazor Web App (.NET 8) + PostgreSQL hosted on Render. The core idea: money is a means, not an end. The app leads with *what you want to do with your life* and works backwards into the budget.

The full loop: **Dream it → Budget for it → Plan it → Do it → Review it.**

## Tech Stack

- **Frontend/Backend**: Blazor Server (.NET 8), ASP.NET Core 8 (same process)
- **Database**: PostgreSQL via Entity Framework Core 8 + Npgsql
- **Auth**: ASP.NET Core Identity + Google OAuth 2.0 (OIDC)
- **Hosting**: Render (Dockerized)

## Solution Structure

```
src/
  PlanToSave.Web/           # Blazor Web App — entry point
    Components/Pages/       # Razor pages (Accounts, Goals, Plans, Flows, Ideas, Activities, Calendar)
    Components/Layout/      # NavMenu, MainLayout
    Components/Shared/      # Reusable UI components
    Services/               # Web-layer services (e.g. CsvImportService)
    Data/                   # ApplicationDbContext, ApplicationUser
    Migrations/             # EF Core migrations
  PlanToSave.Application/   # Service interfaces + DTOs (no EF Core here)
    Accounts/               # IAccountService, AccountDto, CreateAccountDto, etc.
    Flows/                  # IActualFlowService, ActualFlowDto, CsvImportDto, etc.
    Goals/                  # IGoalService, GoalDto, CreateGoalDto, etc.
    Ideas/                  # IIdeaService, IdeaDto, IdeaFilterDto, etc.
    Plans/                  # IMonthlyPlanService, MonthlyPlanDto, etc.
    Activities/             # IActivityPlanService, ActivityPlanDto, etc.
  PlanToSave.Domain/        # Entities + Enums (pure C# — no dependencies)
    Entities/               # Account, Goal, Idea, ActivityPlan, ActualFlow, etc.
    Enums/                  # AccountType, IdeaStatus, ActivityPlanStatus, etc.
  PlanToSave.Infrastructure/ # EF Core implementations of Application service interfaces
```

## Architecture Rules

### Service Layer
- All service interfaces live in `PlanToSave.Application`
- All EF Core implementations live in `PlanToSave.Infrastructure/Services/`
- Blazor pages inject **interfaces**, never concrete classes (exception: `CsvImportService` which has no interface)
- Every service method takes `string userId` as its first parameter and scopes all queries to that user
- **Never expose raw EF entities to Blazor pages** — always project to DTOs

### Blazor-Specific Rules
- **DbContext is scoped per Blazor circuit** — never call multiple EF Core service methods with `Task.WhenAll`. Always use sequential `await` calls to avoid `"A second operation was started on this context instance"` error
- Pages use `@inject AuthenticationStateProvider AuthState` + `auth.User.FindFirst(ClaimTypes.NameIdentifier)?.Value` to get the current user ID
- All pages have `@attribute [Authorize]`

### Database / EF Core
- Migrations are in `PlanToSave.Web/Migrations/`
- `ApplicationDbContext` is in `PlanToSave.Web/Data/`
- Run migrations at startup via `context.Database.MigrateAsync()` in `Program.cs`
- Balance for stock accounts = computed on the fly: `SUM(ActualFlows where ToAccountId = account) − SUM(ActualFlows where FromAccountId = account)`

### Account Types
- `Checking`, `Savings`, `Credit`, `Investment` → **Stock accounts** (have a running balance; `IsStockAccount = true`)
- `Income`, `Expense` → **Flow accounts** (no balance, only period totals; `IsStockAccount = false`)
- Transaction rules: Income can only be `FromAccount`; Expense can only be `ToAccount`; Stock accounts can be either side

## UI Conventions

- **Bootstrap 5** throughout — cards, tables, progress bars, badges, modals
- Nav is an icon-only vertical rail with CSS tooltips (`class="rail-tooltip" data-tip="..."`)
- Modals are implemented as Blazor conditionals (no JS — `@if (showModal) { <div class="modal fade show d-block"> ... }`) + a `.modal-backdrop.fade.show`
- Inline forms use Bootstrap cards (`<div class="card mb-4"><div class="card-body">...`)
- Empty states use centered card with muted text
- Color conventions: amber = Ideas, blue = Goals, teal = Budgeted/Plans, green = complete/tracked

## Key Files to Know

| File | Purpose |
|------|---------|
| `Program.cs` | DI registration, EF setup, auth pipeline |
| `Data/ApplicationDbContext.cs` | DbSets, entity configurations |
| `Components/Layout/NavMenu.razor` | Icon rail nav |
| `Components/Pages/Home.razor` | Dashboard |
| `Components/Pages/Pipeline.razor` | "Your Savings Journey" 4-stage view |
| `Components/Pages/Flows/Import.razor` | CSV import (4-step UX) |
| `Web/Services/CsvImportService.cs` | Robust CSV parser (auto-delimiter, 60+ column aliases) |

## Development Commands

```bash
# Build
dotnet build

# Run (dev)
dotnet run --project src/PlanToSave.Web

# Add migration
dotnet ef migrations add <Name> --project src/PlanToSave.Web

# Update database (local dev)
dotnet ef database update --project src/PlanToSave.Web
```

## What Has Already Been Built (Phases 1–15)

All core features are complete: Accounts, Dashboard, Transactions, Monthly Plans, Recurring Templates, Goals, Ideas Backlog, Activity Planning, Review Loop, Calendar & Export, Budget↔Life Loop integration, Surprise Me!, CSV Import, and a dedicated Pipeline/Journey page. The nav is an icon-only rail. Authentication is Google OAuth + Identity.

## When Making Changes

1. DTO changes in `PlanToSave.Application` — update the interface too
2. New entity fields → add EF migration
3. New pages → add a `@attribute [Authorize]` and get user from `AuthState`
4. New services → register in `Program.cs` (usually `AddScoped`)
5. Always use sequential awaits, never `Task.WhenAll`, for multiple EF Core calls in the same component
