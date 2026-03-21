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

### Color Palette

> **The values below are the single source of truth for all styling decisions. When adding new UI elements, always reach for a color already in this palette. Do not introduce new hex values without updating this guide first.**

Dark mode is manual (`[data-theme="dark"]` on `<html>`) — Bootstrap 5.1 has no native dark mode. All overrides live in `wwwroot/app.css`. The nav rail is always dark regardless of theme.

#### Brand / Semantic Colors (both modes)

| Role | Hex | Usage |
|------|-----|-------|
| Ideas / amber | `#ffc107` | Pipeline stage border, badge bg, nav active icon, partial-tracked state |
| Goals / blue | `#0d6efd` | Pipeline stage border, nav active icon, Goal-related accents |
| Budgeted / teal | `#0dcaf0` | Pipeline stage border, Plans accents |
| Tracked / grey | `#6c757d` | Pipeline stage border (inactive) |
| Complete / green | `#198754` | Pipeline tracked-complete border, success states |

#### Light Mode

| Role | Hex | Notes |
|------|-----|-------|
| Body text | `#000` | Jet black — maximum contrast |
| Link | `#006bb7` | |
| `.text-muted` | `#5a6472` | ~5.5:1 WCAG AA on white |
| `.text-warning` | `#92400e` | 9.3:1 — replaces Bootstrap's low-contrast yellow |
| `.text-info` | `#0c4a6e` | 9.5:1 — replaces Bootstrap's low-contrast cyan |
| `.bg-warning` | `#b45309` + white text | 5.4:1 — replaces Bootstrap yellow |
| `.bg-info` | `#0e7490` + white text | 4.6:1 |
| Validation error | `#e50000` | `.invalid` outline, `.validation-message` |

#### Dark Mode — Backgrounds (lightest → darkest layer)

| Role | Hex |
|------|-----|
| Page / body background | `#1a1d24` |
| Card / modal / list-group item | `#21272f` |
| Elevated surface (`.bg-light`, inputs, table-light) | `#252c3d` |
| Input disabled / `.bg-dark` interior | `#1e253a` |
| Hover (list items, pagination, buttons) | `#2a3140` |
| Active item / pagination active | `#3d5acc` |
| Badge `.bg-light` / scrollbar thumb | `#353d52` |

#### Dark Mode — Borders

| Role | Hex |
|------|-----|
| Standard border | `#3a4258` |
| Input default | `#404859` |
| Checkbox / form-check | `#4a5568` |
| Btn-outline-dark | `#5a6680` |
| Input focused | `#5b6cad` |
| `.border-warning` | `#c98a1a` |

#### Dark Mode — Text

| Role | Value | Notes |
|------|-------|-------|
| Primary text | `#fff` | All body copy, labels, table cells |
| Secondary / muted | `rgba(255,255,255,0.6)` | `.text-muted`, `.text-secondary`, `--bs-secondary-color` |
| Link / `.btn-link` | `rgba(255,255,255,0.85)` | Hover → `#fff` |
| Placeholder / disabled text | `#7a8da0` | Inputs placeholder and disabled state |
| `.text-warning` | `#fcd34d` | 9.7:1 on page background |
| `.text-info` | `#7dd3fc` | 9.5:1 on page background |

#### Dark Mode — Semantic Backgrounds

| Class | Background | Text |
|-------|-----------|------|
| `.bg-warning` | `#ffc107` | `#111` |
| `.bg-info` | `#0e7490` | `#fff` |
| `.alert-danger` | `#3d1f23` / border `#6b2a32` | `#fff` |
| `.alert-success` | `#1a3028` / border `#1e5038` | `#fff` |
| `.alert-warning` | `#3d2f10` / border `#6b4f18` | `#fff` |
| `.alert-info` | `#152a3d` / border `#1e4d7b` | `#fff` |
| `.alert-primary` | `#0d2a52` / border `#1a4080` | `#fff` |

#### Nav Rail (Always Dark — Theme-Independent)

| Role | Value |
|------|-------|
| Rail background | `#111` |
| Icon default | `rgba(255,255,255,0.70)` |
| Icon hover / active | `rgba(255,255,255,0.95)` / `#fff` |
| Logo icon | `rgba(255,255,255,0.55)`, hover `rgba(255,255,255,0.90)` |
| Section label | `rgba(255,255,255,0.38)` |
| Section divider | `rgba(255,255,255,0.10)` |

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
