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

## UI Style Guide

> **This style guide is the design law for this codebase. Every new page, component, and feature must follow these conventions. Do not introduce new patterns, color values, inline styles, or structural deviations without updating this guide first.**

### Foundations

- **Bootstrap 5.1** throughout — cards, tables, progress bars, badges, modals
- Nav is an icon-only vertical rail with CSS tooltips (`class="rail-tooltip" data-tip="..."`)
- All dark mode overrides live in `wwwroot/app.css` under `[data-theme="dark"]`
- Never use `style="..."` inline attributes — use CSS classes instead (see Anti-patterns)

---

### Page Layout

Every page follows the same two-zone structure:

```razor
@* ── Page header ──────────────────────────────────────── *@
<div class="d-flex justify-content-between align-items-center mb-4">
    <h1 class="mb-0">Page Title</h1>
    <div class="d-flex gap-2">
        @* action buttons go here *@
    </div>
</div>

@* ── Page body ─────────────────────────────────────────── *@
@* content cards, tables, etc. *@
```

Rules:
- Page title is always `<h1 class="mb-0">` — never `h2` or `.h3` at the top level
- Header flex wrapper always uses `mb-4`
- Breadcrumbs (`<nav aria-label="breadcrumb">`) go **above** the `<h1>`, not inside the flex row — use only on detail/edit pages, not list pages
- All pages must have `@attribute [Authorize]`

---

### Action Buttons

Buttons in the page header follow a priority order: secondary actions left, primary action rightmost.

```razor
<div class="d-flex gap-2">
    <button class="btn btn-outline-secondary" @onclick="...">Secondary Action</button>
    <a href="/page/new" class="btn btn-primary">+ Primary Action</a>
</div>
```

Rules:
- Primary action: `btn btn-primary`
- Secondary/alternate: `btn btn-outline-secondary` or `btn btn-outline-primary` (when blue-toned context)
- Destructive: `btn btn-danger` (confirmation required — see Delete pattern)
- All header buttons are **full size** (not `btn-sm`)
- Use `+ Label` prefix for "add new" actions — no emoji in button labels

---

### Inline Add / Edit Forms

When an add or edit form lives on the same page as the list (not a modal or separate page):

```razor
@if (showAddForm)
{
    <div class="card mb-4">
        <div class="card-body">
            <h6 class="card-subtitle mb-3 text-muted">Add New Item</h6>
            <EditForm Model="dto" OnValidSubmit="HandleSave">
                <DataAnnotationsValidator />
                <div class="row g-2">
                    <div class="col-md-6">
                        <label class="form-label">Field Label</label>
                        <InputText @bind-Value="dto.Field" class="form-control" placeholder="..." />
                        <ValidationMessage For="() => dto.Field" class="text-danger small" />
                    </div>
                </div>
                @if (formError is not null)
                {
                    <div class="alert alert-danger mt-3">@formError</div>
                }
                <div class="mt-3">
                    <button type="submit" class="btn btn-primary btn-sm">Save</button>
                    <button type="button" class="btn btn-link btn-sm" @onclick="CloseForm">Cancel</button>
                </div>
            </EditForm>
        </div>
    </div>
}
```

Rules:
- Wrapper: `<div class="card mb-4"><div class="card-body">`
- Form title: `<h6 class="card-subtitle mb-3 text-muted">` — keep to ≤ 5 words
- Grid: `<div class="row g-2">` with `col-md-X` columns — prefer 3, 4, or 6 column widths
- Submit: `btn btn-primary btn-sm`; Cancel: `btn btn-link btn-sm`
- Form-level errors: `<div class="alert alert-danger mt-3">` (not `.text-danger`)
- Field-level errors: `<ValidationMessage class="text-danger small" />`
- Optional fields: label text ends with ` <span class="text-muted">(optional)</span>`
- Labels with currency inputs: use `<div class="input-group"><span class="input-group-text">$</span> ...`

---

### Modals

Use modals when the action is a context switch (e.g., scheduling a goal from a list page). Use inline forms for simple add/edit on the same page.

```razor
@if (showModal)
{
    <div class="modal-backdrop fade show"></div>
    <div class="modal fade show d-block" tabindex="-1" role="dialog">
        <div class="modal-dialog" role="document">   @* add modal-lg if needed *@
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Modal Title</h5>
                    <button type="button" class="btn-close" @onclick="CloseModal"></button>
                </div>
                <EditForm Model="dto" OnValidSubmit="HandleSave">
                    <DataAnnotationsValidator />
                    <div class="modal-body">
                        @* fields using mb-3 blocks *@
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-outline-secondary" @onclick="CloseModal">Cancel</button>
                        <button type="submit" class="btn btn-primary">Save</button>
                    </div>
                </EditForm>
            </div>
        </div>
    </div>
}
```

Rules:
- No JS — Blazor conditional renders the modal
- Backdrop: `<div class="modal-backdrop fade show">` immediately before the modal div
- Fields inside modal body: use `<div class="mb-3">` blocks (not `row g-2` grid)
- Modal footer buttons: Cancel (`btn-outline-secondary`) left, Submit (`btn-primary`) right
- Use `.modal-lg` for forms with many fields or complex layouts

---

### Tables

```razor
<div class="table-responsive">
    <table class="table table-hover align-middle mb-0">
        <thead>
            <tr class="small text-uppercase text-muted">
                <th>Column</th>
                <th class="text-end">Amount</th>
                <th></th>  @* actions column — no heading *@
            </tr>
        </thead>
        <tbody>
            @foreach (var item in items)
            {
                <tr>
                    <td>@item.Name</td>
                    <td class="text-end fw-semibold">@item.Amount.ToString("C")</td>
                    <td class="text-end">
                        @* action buttons *@
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

Rules:
- Always wrap in `<div class="table-responsive">`
- Base classes: `table table-hover align-middle mb-0`
- `table-sm` for dense data (planned flows, templates)
- Header row: `class="small text-uppercase text-muted"` — no `table-light` on thead
- Numeric/currency columns: `text-end` on both `<th>` and `<td>`; `fw-semibold` on amounts
- Muted secondary data: `text-muted small`
- Totals footer: `<tfoot>` with `fw-semibold` — use `table-light` only on `<tfoot>`
- Action column: empty `<th>`, right-aligned `<td class="text-end">`

---

### Cards

Cards are the primary content container throughout the app.

```razor
@* Standard content card *@
<div class="card mb-4">
    <div class="card-body">
        <!-- content -->
    </div>
</div>

@* Card with header *@
<div class="card mb-4">
    <div class="card-header d-flex justify-content-between align-items-center py-2">
        <span class="fw-semibold">Section Title</span>
        <span class="text-muted small">supplemental info</span>
    </div>
    <div class="card-body">
        <!-- content -->
    </div>
</div>

@* Card with list group (no card-body padding) *@
<div class="card mb-4">
    <div class="card-header py-2">
        <span class="fw-semibold">Title</span>
    </div>
    <ul class="list-group list-group-flush">
        <li class="list-group-item">...</li>
    </ul>
</div>
```

Rules:
- Always `mb-4` between sibling cards
- Card header padding: `py-2` (compact)
- Card header layout: `d-flex justify-content-between align-items-center`
- Card header title: `<span class="fw-semibold">` (not `<h6>` or `<strong>`)
- Never put a card directly inside another card's `card-body`

---

### Empty States

```razor
<div class="card text-center py-5">
    <div class="card-body text-muted">
        <svg ...class="mb-3 text-muted" width="36" height="36"...><!-- icon --></svg>
        <p class="mb-1 fw-semibold">Nothing here yet.</p>
        <p class="mb-0 small">Explanatory sentence. Optional action below.</p>
        @* optional: *@
        <div class="mt-3">
            <button class="btn btn-sm btn-outline-secondary" @onclick="...">Take action</button>
        </div>
    </div>
</div>
```

Rules:
- Always use the card pattern — never `.alert` for empty states
- SVG icon: 36×36, `class="mb-3 text-muted"` — include when the emptiness needs visual weight
- Heading line: `fw-semibold`, `mb-1`
- Description: `small`, `mb-0`
- Action button (optional): `btn btn-sm btn-outline-secondary`, inside `mt-3` div

---

### Badges

Account type badge color mapping (use everywhere accounts appear):

```csharp
AccountType.Income     → "bg-success"
AccountType.Expense    → "bg-danger"
AccountType.Checking   → "bg-primary"
AccountType.Savings    → "bg-info"
AccountType.Credit     → "bg-warning"
AccountType.Investment → "bg-secondary"
```

Status badge color mapping:

```csharp
Draft    → "bg-secondary"
Active   → "bg-primary"
Closed   → "bg-success"
```

Rules:
- Always `me-1` after a badge that precedes text
- Badge text: use the enum value as-is (do not force `.ToLower()`)
- Never use `bg-light text-dark` for semantic states — only for genuinely neutral/unknown values

---

### Progress Bars

Three named sizes — **do not use inline `style="height:..."`**:

| Class | Height | Use |
|-------|--------|-----|
| `.progress-lg` | 20px | Full goal progress with percentage text inside |
| `.progress-md` | 10px | Summary/dashboard sparklines |
| `.progress-sm` | 6px | Compact context (activity steps, plan rows) |
| `.progress-xs` | 3px | Micro inline use (Pipeline card) |

Add these to `wwwroot/app.css`:
```css
.progress-lg { height: 20px; }
.progress-md { height: 10px; }
.progress-sm { height: 6px;  }
.progress-xs { height: 3px;  }
```

Color rules:
- `bg-success` — complete / ≥ 100%
- `bg-warning` — at risk / overdue / partially tracked
- `bg-primary` — in progress (default)
- `bg-danger` — over budget / negative variance

---

### Alerts & Notifications

```razor
@* Page-level error (dismissible) *@
@if (errorMessage is not null)
{
    <div class="alert alert-danger alert-dismissible mt-3">
        @errorMessage
        <button type="button" class="btn-close" @onclick="() => errorMessage = null"></button>
    </div>
}

@* Contextual info/nudge — use above the relevant content *@
<div class="alert alert-info d-flex justify-content-between align-items-center py-2 mb-4">
    <span>Message text here.</span>
    <a href="/page" class="btn btn-sm btn-outline-primary ms-3">Action →</a>
</div>
```

Rules:
- Form-level save/delete errors: `alert-danger`, dismissible
- Onboarding nudges: `alert-info` or `alert-warning`
- Success confirmations: `alert-success` — auto-dismiss after 3s or make dismissible
- Never use `.text-danger` alone for error messages visible after a form submit — use the full alert

---

### Delete Confirmation

All deletes must confirm before execution:

```razor
private async Task DeleteItem(Guid id, string label)
{
    var confirmed = await JS.InvokeAsync<bool>("confirm", $"Delete \"{label}\"? This cannot be undone.");
    if (!confirmed) return;
    try
    {
        await Service.DeleteAsync(id, userId);
        await LoadData();
    }
    catch (Exception ex)
    {
        errorMessage = $"Could not delete: {ex.Message}";
    }
}
```

Delete button (in tables / list rows):
```razor
<button class="btn btn-sm btn-danger" title="Delete" @onclick="() => DeleteItem(item.Id, item.Name)">
    <svg width="14" height="14" ...><!-- trash icon --></svg>
</button>
```

Rules:
- Always `JS.InvokeAsync<bool>("confirm", ...)` — never delete silently
- Confirm message format: `Delete "{label}"? This cannot be undone.`
- Button: `btn btn-sm btn-danger` (not `btn-outline-danger`)
- Always catch exceptions and surface via `errorMessage`

---

### Loading States

```razor
@* Page-level loading *@
@if (items is null)
{
    <p class="text-muted">Loading…</p>
}

@* Submit button with spinner *@
<button type="submit" class="btn btn-primary btn-sm" disabled="@isSaving">
    @if (isSaving)
    {
        <span class="spinner-border spinner-border-sm me-1" role="status"></span>
    }
    Save
</button>
```

Rules:
- Use `<p class="text-muted">Loading…</p>` for page-level data loading (no spinner needed)
- Use inline spinner only on submit buttons that trigger async operations
- Disable the button while `isSaving == true`

---

### Color Palette

> **The values below are the single source of truth for all styling decisions. When adding new UI elements, always reach for a color already in this palette. Do not introduce new hex values without updating this guide first.**

Dark mode is manual (`[data-theme="dark"]` on `<html>`) — Bootstrap 5.1 has no native dark mode. All overrides live in `wwwroot/app.css`. The nav rail is always dark regardless of theme.

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
