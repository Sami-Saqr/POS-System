# CarPartManager - Handover Document

## Status
**Date:** 2026-10-24
**Task:** Implement "المصروفات" (Expenses) screen as a real DB-backed module
**Branch:** `feature-expenses` (from `main`)
**Status:** ✅ Implementation COMPLETE — build verified, manual UI testing pending

---

## 1. Inspect-first findings

- **The Expenses screen was an empty placeholder**: `MainViewModel.NavigateTo` fell through to
  `PlaceholderViewModel` for tag `"Expenses"`; no `ExpensesViewModel`/`ExpensesView` existed.
- **No DI container**: services are constructed directly (`new SqliteXxxService()`) inside ViewModels.
  `CarPartsDbContext` is instantiated per call (`CreateContext() => new CarPartsDbContext()`), context
  lifetime is per-operation.
- **Schema mechanism**: the project does **not** use EF Core migrations. `DbInitializer.InitializeAsync()`
  calls `EnsureCreatedAsync()` then runs **idempotent raw DDL** (`CREATE TABLE IF NOT EXISTS ...`) plus
  idempotent seed helpers. The new Expenses tables follow exactly this mechanism (no migration files added).
- **Payment methods already exist as constant lists** in other ViewModels (e.g. `CustomersViewModel`):
  `نقدي` (default), `شبكة / فيزا`, `تحويل بنكي`, `شيك`. Reused as-is (no new table).
- **No real user/auth system** exists — only a display string (`MainViewModel.CurrentUser`). Expenses
  therefore store user as a nullable text field (`CreatedBy` / `ModifiedBy`), and the UI shows it when present.
- **SQLite cannot aggregate `decimal` in SQL** — the established project pattern (see `SqliteDashboardService`)
  is `AsNoTracking().ToListAsync()` then `Sum(...)` **in memory**. `SqliteExpenseService` follows this.
- **No test project exists on `main`** (`CarPartManager.Tests` on disk contains only stale `bin/obj`
  artifacts, zero tracked files). Per the rules, no test project and no packages were added to the main project.

## 2. Implementation

### Data layer
- Entities: `ExpenseModel`, `ExpenseCategoryModel`, `ExpenseHistoryModel` (+ POCO `ExpenseSummaryModel`).
- `CarPartsDbContext`: 3 DbSets and model config — money `decimal(18,2)`, index on `ExpenseDate`,
  composite index on `(CategoryId, ExpenseDate)`, index on category `Name`; FK `Expense.CategoryId →
  ExpenseCategory.Id` with `DeleteBehavior.Restrict` (a category with expenses can never be hard-deleted);
  FK `ExpenseHistory.ExpenseId → Expense.Id` with `Cascade`. Computed/display-only properties are `Ignore`d.
- `DbInitializer`: `EnsureExpenseTablesCreatedAsync()` (idempotent `CREATE TABLE IF NOT EXISTS` for the three
  tables + indexes) and `SeedDefaultExpenseCategoriesAsync()` (idempotent seed using `NameNormalizer`):
  إيجار، كهرباء ومياه، مرتبات، نقل وشحن، صيانة، تسويق، مستلزمات، أخرى. Wired into `EnsureTablesCreatedAsync()`.
- `Services/NameNormalizer.cs`: `Normalize` (trim + collapse inner whitespace) and `AreEquivalent`
  (OrdinalIgnoreCase) — used for duplicate-category detection.
- `Services/IExpenseService.cs` + `Services/SqliteExpenseService.cs`: all data logic (DB-side filtering,
  `AsNoTracking` for read lists, async everywhere). Exceptions are wrapped in `InvalidOperationException`
  with clear Arabic messages (never swallowed).

### Business rules implemented
- **Cancel, never delete**: `CancelExpenseAsync(id, reason, user)` sets `IsCancelled`, `CancellationDate`,
  `CancellationReason`; the row is kept. Cancelled expenses are excluded from *every* total (summary cards,
  filtered grid total, top category). No hard delete API exists.
- **Categories**: add + deactivate/reactivate; never hard-deleted when used (`Restrict` FK + UI refuses);
  duplicate names prevented via normalized comparison.
- **Edit audit (chosen minimal mechanism)**: alongside `CreatedAt/ModifiedAt` (+ `CreatedBy/ModifiedBy`
  when a user is available), every edit and every cancellation appends a row to `ExpenseHistories`
  recording change type, previous/new amount, previous/new category name, previous/new date and a reason.
  This is the "small history table" option — the simplest mechanism that guarantees the original data is
  never silently lost, with no impact on the Expense table shape. History is viewable in the UI
  (per-row "السجل" action → collapsible audit panel).
- **Isolation**: expenses touch only their own tables — no inventory, customer, or sales logic is referenced.

### UI/UX
- `Views/ExpensesView.xaml`: Arabic RTL, 4 metric cards (اليوم / الشهر الحالي / الشهر السابق / أعلى تصنيف
  هذا الشهر), filter bar (search, category, payment method, date range, include-cancelled, clear),
  collapsible category-management panel, collapsible audit panel, grid with loading/empty/error/success
  Arabic states and a status bar showing the filtered **active** total and count.
- `Views/ExpenseDialogWindow.xaml` (add/edit) and `Views/CancelExpenseDialogWindow.xaml` (cancel with
  mandatory reason) follow the existing dialog pattern (`RequestClose` + `ShowDialog() == true`).
- Dialog: date defaults to today; **Save enables the moment category + positive amount are valid**
  (`UpdateSourceTrigger=PropertyChanged` on the amount box; `CanSave` re-evaluated and
  `SaveCommand.RaiseCanExecuteChanged()` called from the setters). Optional fields (description, reference,
  notes) never block saving. Invalid input shows a clear Arabic message. Arabic-Indic digits are accepted in
  the amount field via `PhoneHelper.NormalizeDigits` + culture-invariant parsing (`٫`/`,` → `.`).
- The grid uses a `DataGrid.Style` with `DataTrigger`s for visibility — **not** a local `Visibility` value —
  to avoid the dependency-property-precedence bug previously found in `SalesInvoicesHistoryView.xaml`.

## 3. How the schema is created

No EF migrations. On startup `DbInitializer.InitializeAsync()` runs `EnsureCreatedAsync()` (which, for a
pre-existing database, is a no-op) and then `EnsureExpenseTablesCreatedAsync()`, which executes raw
`CREATE TABLE IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS` statements, followed by
`SeedDefaultExpenseCategoriesAsync()`. All statements are additive and idempotent: existing data is
untouched, no `.db` file was deleted or recreated, and repeated runs are safe.

## 4. Changed / new files

```
New:
  Models/ExpenseModel.cs
  Models/ExpenseCategoryModel.cs
  Models/ExpenseHistoryModel.cs
  Models/ExpenseSummaryModel.cs
  Services/NameNormalizer.cs
  Services/IExpenseService.cs
  Services/SqliteExpenseService.cs
  ViewModels/ExpensesViewModel.cs
  ViewModels/ExpenseDialogViewModel.cs
  ViewModels/CancelExpenseDialogViewModel.cs
  Views/ExpensesView.xaml (+ .xaml.cs)
  Views/ExpenseDialogWindow.xaml (+ .xaml.cs)
  Views/CancelExpenseDialogWindow.xaml (+ .xaml.cs)

Modified:
  Data/CarPartsDbContext.cs     (3 DbSets + model configuration)
  Data/DbInitializer.cs         (expense tables + default category seed)
  ViewModels/MainViewModel.cs   (cached _expensesViewModel + navigation branch)
  App.xaml                      (ExpensesViewModel -> ExpensesView DataTemplate)
  CarPartManager.csproj         (excludes the stale CarPartManager.Tests bin/obj artifacts from the glob,
                                 which otherwise caused CS0579 duplicate assembly attributes)
```

## 5. Build result

`dotnet build CarPartManager.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)** (exit code 0).
No test project exists on `main`, so **no automated tests were added** (adding them would require adding
packages, which the task forbids).

## 6. Verification

### Verified (by reading code + build)
- Project compiles cleanly with all new files.
- Navigation: `Expenses` nav item now resolves to the real `ExpensesViewModel`; the placeholder branch and
  the placeholder description entry for `Expenses` were removed.
- `App.xaml` maps `ExpensesViewModel` → `ExpensesView`.
- All read queries filter in the database and use `AsNoTracking`; cancelled rows are excluded from every
  total (summary queries filter `IsCancelled == false` in SQL; the grid total additionally sums only
  non-cancelled rows in memory).
- Decimal parsing/aggregation follows the project conventions; money values are `decimal`.

### Manual tests required (not executed — the app must not be run)
1. Open المصروفات → summary cards and grid load without error; empty state appears when there is no data.
2. مصروف جديد → Save stays disabled until a category is chosen and the amount is a positive number; enter
   an Arabic-Indic amount (e.g. `١٢٥٫٥٠`) and confirm it saves as `125.50`.
3. Save a new expense → it appears in the grid; today's card and the month card increase.
4. Edit that expense → change the amount/category → the per-row "السجل" panel shows the old values.
5. Cancel an expense with a reason → status becomes "ملغاة", and **all** totals drop accordingly; the row
   is still present when "عرض الملغاة" is checked.
6. إدارة التصنيفات → add a category, try adding a near-duplicate (e.g. extra spaces / different case) and
   confirm it is rejected; deactivate a used category and confirm it cannot be deleted and still resolves
   for its existing expenses.
7. Filters: search by description, category, payment method, date range, include-cancelled — confirm the
   filtered total reflects active rows only and that an invalid range (from > to) shows an Arabic message.
8. Resize the window and confirm no overlapping columns and correct text trimming + tooltips.

## 7. Risks & notes

- Additive schema only; existing tables/data untouched. No `.db` file was opened, modified, or deleted.
- `EnsureCreatedAsync()` will not alter an existing database, which is exactly why the new tables are
  created through the explicit idempotent DDL block.
- Expenses store the user as a text label because the app has no authentication; swapping in a real user
  table later only requires changing that string source.
- `CarPartManager.csproj` now excludes `CarPartManager.Tests\**` from the default globs. The stale folder
  holds only `bin/obj` artifacts (no source, no `.csproj`); it can be deleted by hand at any time, after
  which the exclusion becomes harmless.

## 8. Remaining TODO

1. Attach receipts/attachments to an expense (not requested in this task).
2. Expense reporting/export (monthly breakdown by category, CSV/print) — belongs to the Reports screen.
3. Batch operations (bulk cancel/re-categorise) — deferred; not needed at the current scale.
4. Optional: add a dedicated test project for expense totals/uniqueness/cancel-flow (requires a new test
   project + packages, which was out of scope here).

---

## Appendix — Previous task (2026-10-23): "المخزون والجرد" (Inventory & Stocktake, Part 1) — COMPLETED

Implemented the Inventory screen with two tabs: **Stock Overview** (summary cards + DataGrid with real-time
search/filters) and **Stock Movements** (append-only ledger with manual adjustment + consistency check).
Stocktake itself was reserved for a future task.

Quantity-change paths already hooked: purchase approval/cancellation, sales approval/cancellation, sales
returns (sellable + damaged), purchase returns; plus newly hooked: manual product edit
(`SqliteProductService.UpdateProductAsync`, now transactional and movement-recording).
Business rules verified then: drafts do not affect inventory, negative stock is not allowed, movements are
append-only, and all quantity changes happen in atomic transactions.

New files (that task): `Services/IInventoryService.cs`, `Services/SqliteInventoryService.cs`,
`ViewModels/InventoryViewModel.cs`, `Views/InventoryView.xaml` (+ `.xaml.cs`).
Modified: `App.xaml`, `ViewModels/MainViewModel.cs`, `Services/SqliteProductService.cs`,
`Data/DbInitializer.cs` (`BackfillOpeningStockMovementsAsync`, idempotent "رصيد افتتاحي" backfill),
`Styles/Controls.xaml` (`CleanTabControlStyle`, `BooleanToVisibilityConverter`).
No EF Core migration file was needed then either — the table already existed.
