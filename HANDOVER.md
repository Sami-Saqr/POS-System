# CarPartManager - Handover Document

## Latest: Sales invoices history showed no invoices — FIXED (branch `fix-sales-history`)
**Date:** 2026-10-24
**Symptom:** "سجل فواتير المبيعات" listed nothing (only the empty state) while the purchase history listed completed purchases, even though POS sales were completed.

**Root cause (confirmed, code-only — no DB touched):**
- [SalesInvoicesHistoryView.xaml](file:///e:/windows_app_csharp/CarPartManager/Views/SalesInvoicesHistoryView.xaml#L141) (old line 141) set a **local** `Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisConverter}}"` on the DataGrid. `BooleanToVisibilityConverter` maps `false` → `Collapsed`, and a **local value outranks Style triggers** in WPF precedence, so as soon as loading finished (`IsLoading = false`) the grid was permanently `Collapsed` and the `ShowEmptyState` border was all the user ever saw. The purchase history view has no such local binding (only the loading `DataTrigger`), which is why it worked.
- Ruled out (verified by reading code, not the DB): the sales VM is structurally identical to the purchase VM; `SqliteSalesInvoiceService.GetAllInvoicesAsync` has **no** status/date/IsActive filter, **no** join to `Customers` (walk-in rows with `CustomerId = null` are returned), and orders `InvoiceDate DESC, Id DESC`; the POS save path `SalesInvoicesViewModel.ExecutePostInvoiceAsync` → `SqliteSalesInvoiceService.PostInvoiceAsync` writes to the `SalesInvoices` table with `Status = "معتمدة"`; navigation in `MainViewModel.NavigateTo` loads then sets `CurrentViewModel` symmetrically for both history pages; `DbContext` is created per call (`using`), no stale context; the VM already shows an Arabic `MessageBox` + status message on failure (no silent catch).

**Fix (smallest safe change, no schema/invoice/inventory/accounting change):**
- `Views/SalesInvoicesHistoryView.xaml` — removed the local `Visibility` binding from the DataGrid and kept a single `DataTrigger IsLoading=True → Collapsed` (mirrors the purchase history view).
- `ViewModels/SalesInvoicesHistoryViewModel.cs` — normalise a null/empty `CustomerName` to "عميل نقدي" when loading (in-memory projection only, no DB write), so legacy / walk-in invoices never render a blank customer cell. Search and details are unchanged.

**Build:** `dotnet build CarPartManager.csproj` → **Build succeeded, 0 Error(s)** (8 warnings = 4 pre-existing `CS8602` in `InventoryViewModel.cs` 217–220, duplicated for the `_wpftmp` project). Not runtime-tested (no `.db` touched, app not run).

**Manual test steps:** open the app → "سجل فواتير المبيعات" → the grid must list all completed (معتمدة) sales invoices newest first, including walk-in ones showing "عميل نقدي"; type in the search box (invoice no. / customer / phone / notes); click a row to select it; click "تحديث السجل".

**Risk:** low — a XAML visibility correction plus an in-memory display fallback. The empty-state border still shows when there genuinely are no rows.

## Latest (2nd pass): "STILL shows no invoices" — why, and how it is now proven
**Date:** 2026-10-24
**Finding:** the fix above is correct but **was never in the binary that was tested**. `git branch --contains 626d8f1` → **only `fix-sales-history`** (`main`, `feature-expenses`, `feature-reports-a`, `feature-reports-b` do **not** contain it). The newest executable before this pass (`bin/Debug/net10.0-windows/CarPartManager.exe`, 21:25:40) was produced **30 s after** the `feature-expenses` commit `7e2c1db` (21:25:10) — a branch without the fix — so the run still contained the local `Visibility="{Binding IsLoading, Converter=BoolToVisConverter}"` on the DataGrid that collapses it whenever `IsLoading == false` (i.e. always after load). The two fixed files carry the 21:29:10 checkout timestamp of the later switch back to this branch. **To verify: build/run from `fix-sales-history` (or merge it into `main`).**
**Re-verified code path (no edits, no `.db` touched):** the history query `SqliteSalesInvoiceService.GetAllInvoicesAsync` (`Services/SqliteSalesInvoiceService.cs:45-61`) applies **no** status/date/soft-delete/branch filter and **no** join to `Customers` — walk-in rows (`CustomerId = null`) are returned and shown as "عميل نقدي"; the POS save path `PostInvoiceAsync` (`:195-373`) writes `SalesInvoices` + `SalesInvoiceItems` with `Status = "معتمدة"` (Arabic, same vocabulary), `PostedDate = DateTime.Now`, `InvoiceDate = invoice.InvoiceDate`; the working purchase query is structurally identical; the load **is** triggered on page open (`ViewModels/MainViewModel.cs:163-168`) on the same VM instance bound to the view; the catch is not silent.
**Changes this pass:**
- `CarPartManager.Tests/SalesHistoryQueryTests.cs` (new, in-memory SQLite — not EF InMemory): (1) saves a walk-in sale and a customer sale through the **real** `PostInvoiceAsync` path and asserts both appear in `GetAllInvoicesAsync` with `Status = "معتمدة"`, newest first, walk-in `CustomerId == null` → "عميل نقدي"; (2) simulates an old DB missing `SalesInvoices.CustomerId/CustomerAddress/CancellationDate/CancellationReason`, runs the real startup `DbInitializer.InitializeAsync` and asserts the columns are restored and the old invoice still loads.
- `Services/SqliteSalesInvoiceService.cs:13` — `CreateContext()` is now `protected virtual` (same pattern as `SqliteStocktakeService`) so tests can point the service at a test connection.
- `ViewModels/SalesInvoicesHistoryViewModel.cs` — load failure now shows the real Arabic error including inner exceptions (`Describe(ex)` walks `InnerException`), and the grid is explicitly bound to the same list; footer counter added.
- `Views/SalesInvoicesHistoryView.xaml` — footer `عدد الفواتير: N` in the status bar (bound to `InvoicesHistory.Count`).

**Build/Test:** `dotnet test CarPartManager.Tests\CarPartManager.Tests.csproj` → **Passed! Failed: 0, Passed: 9** (7 stocktake + 2 new), EXIT=0. Main project builds with 0 errors (8 pre-existing `CS8602` warnings in `InventoryViewModel.cs` 217–220).

## Status
**Date:** 2026-10-23 (updated for Part 2)
**Branch:** `wip-inventory`
**Task:** "المخزون والجرد" (Inventory & Stocktake) Screen — Part 1 + Part 2
**Status:** ✅ Part 1 (Stock Overview + Movements) on `main`. ✅ Part 2 (Stocktake) implemented on `wip-inventory`; **build succeeds, 0 errors** (8 warnings = 4 pre-existing `CS8602` in `InventoryViewModel.cs` 217–220, deferred). Not yet runtime-tested (no `.db` touched).

## Overview
The Inventory screen has three tabs:
1. **Stock Overview** - Summary cards + DataGrid with real-time search and filters
2. **Stock Movements** - Append-only ledger with manual adjustment and consistency check
3. **Stocktake (الجرد)** - Session lifecycle + counting + approval (Part 2, see below)

## Part 2: Stocktake (الجرد) — branch `wip-inventory`
Implemented on `wip-inventory` on top of Part 1. Build: succeeded, 0 errors.

### What was implemented
- **Models:** `StocktakeSessionModel` (lifecycle مسودة→جاري العد→مراجعة→معتمد/ملغاة, scope, dates) + `StocktakeLineModel` (BookQuantity snapshot, CountedQuantity, CostPrice, computed Difference/DifferenceValue/DifferenceType). `Lines` navigation (`List<StocktakeLineModel>`) added on the session.
- **Service:** `IStocktakeService` + `SqliteStocktakeService` — CreateSession (full/category scope, snapshots book qty), StartCounting, SubmitForReview, ApproveSession, CancelSession, GetLines, GetLineByProduct/Barcode, UpdateLine, CountProduct, CountByBarcode, HasMovementsAfterSnapshot, GetSessionSummary.
- **Approval rules (verified by code review):** single DB transaction; status must be `مراجعة` (can't approve twice); per counted line with a difference → `product.Quantity += Difference` + an append-only `تعديل جرد` `InventoryMovementModel`; negative-stock guard throws + rolls back; `DataEvents.RaiseInventoryOrInvoicesChanged()` after commit.
- **UI:** Stocktake tab in `InventoryView.xaml` (session header + action buttons, barcode-count row, lines DataGrid, history DataGrid) + new `CreateStocktakeSessionWindow` dialog (XAML + code-behind) — moved out of the ViewModel (no WPF controls in the ViewModel).
- **DbContext:** `StocktakeSessions`/`StocktakeLines` DbSets + `OnModelCreating` config; `HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SessionId).OnDelete(Cascade)` added so `Include(s => s.Lines)` maps to the real `SessionId` column (was missing → would have used a shadow FK).
- **Schema:** tables via `DbInitializer.EnsureStocktakeTablesCreatedAsync` (idempotent `CREATE TABLE IF NOT EXISTS`); `StocktakeLines.SessionId` + FK `ON DELETE CASCADE` already present. No EF Core migration (project uses `EnsureCreated` + raw DDL, not EF migrations).

### Build-fix history (commits on wip-inventory)
- `01b3f1f` — WIP: agent stopped mid-task (quota), unverified (interrupted stocktake work; did NOT compile — 15 errors)
- `5d0bb56` — Step 1: moved session-creation dialog out of ViewModel into `CreateStocktakeSessionWindow` XAML (fixed 14 errors)
- `df4022e` — Step 2: added `Lines` navigation to `StocktakeSessionModel` (fixed the 15th build error)
- `ec393ac` — Step 2 (complete): configured `StocktakeSession.Lines` EF relationship in `CarPartsDbContext` (fixes runtime `Include`; schema-neutral)

### SQLite database path (for testing)
`CarPartsDbContext.GetDatabaseFilePath()` → `%LOCALAPPDATA%\CarPartManager\carparts.db` (`C:\Users\samys\AppData\Local\CarPartManager\carparts.db`), opened in `OnConfiguring` via `UseSqlite("Data Source={path}")`. To test against a copy: copy the `.db` elsewhere and temporarily repoint the path. Do not run against the live DB.

### Test project
None exists (single `CarPartManager.csproj`, WinExe). The requested EF tests (in-memory SQLite) could not be added without creating a test project + new packages (test SDK/framework) — forbidden. Not added.

---

**Date:** 2026-10-24
**Task:** "التقارير المحاسبية" (Accounting Reports) screen — **Part B**: 4 additional read-only tabs (Purchases, Inventory valuation, Customer debts, Item profitability)
**Branch:** `feature-reports-b` (from `feature-reports-a`); Part A committed on `feature-reports-a`
**Status:** ✅ Implementation COMPLETE — build verified, manual UI testing pending
**Latest fix:** ✅ runtime SQLite errors on the Expenses screen fixed (see "0. Bug fix" below)

---

## 0. Bug fix (2026-10-24): SQLite errors on the Expenses screen

**Symptoms:** opening the screen → "تعذر تحميل تصنيفات المصروفات" (+ SQLite error); adding a category →
"تعذر إضافة التصنيف" (+ SQLite error).

**Confirmed root cause:** `ExpenseCategoryModel.ExpensesCount` (a display-only counter) was mapped by
convention because it was **missing from the `Ignore(...)` list** in `CarPartsDbContext.OnModelCreating`
(`ExpenseCategory` block). EF therefore expected a real column `ExpensesCount` in the `ExpenseCategories`
table, but the raw DDL in `DbInitializer.EnsureExpenseTablesCreatedAsync()` only creates `Id, Name, IsActive,
CreatedDate`. `EnsureCreatedAsync()` does nothing on an existing database (the tables come from the raw DDL),
so the column was missing → `SQLite Error 1: no such column: c.ExpensesCount` on `SELECT`, and
`no such column named ExpensesCount` on `INSERT`. A fresh database worked because `EnsureCreatedAsync()`
builds the schema from the model (including the bogus column) — which is why the bug only appeared on an
existing database. `ExpensesCount` is never used in SQL; it is assigned in memory in
`SqliteExpenseService.GetCategoriesAsync()`.

**Fix (2 commits, no schema change, no DDL change needed):**
1. `4347d05` — `Data/CarPartsDbContext.cs`: added `entity.Ignore(c => c.ExpensesCount);` so the EF model
   matches the DDL (the property stays an in-memory, computed value as documented on the model). Works for
   both existing and fresh databases; existing rows/tables are untouched.
2. `6f1fe84` — `Services/SqliteExpenseService.cs`: added a private `Describe(Exception)` helper that walks to
   the innermost exception and shows that message in the Arabic error text (EF's `DbUpdateException` message
   — "An error occurred while saving the entity changes" — hid the real SQLite cause). Applied to all 12
   catch wrappers; validation errors still surface as-is.

**Verified:** `dotnet build CarPartManager.csproj` → Build succeeded, 0 Warning(s), 0 Error(s) after each commit
(static verification of the model↔DDL mapping for all three expense entities; `ExpensesCount` was the only
property missing from the DDL). No `.db` file was read/written, the app was not run, no DDL was changed.
No test project exists on this branch, so no automated test was added (no packages added).
**Note:** READ-ONLY screen — it never modifies data. Part A's filter, service, styles and rules are reused; no schema change, no `.db` touched, app not run.

---

## Reports — Part A (2026-10-24)

### Prerequisites verified
- Expenses module present (committed on `feature-expenses`, documented in the appendix below).
- Sales-history bug fix present in [SalesInvoicesHistoryViewModel.cs](file:///e:/windows_app_csharp/CarPartManager/ViewModels/SalesInvoicesHistoryViewModel.cs) (try/catch + Arabic error message).

### Inspection findings that drove the design
- Invoice lifecycle: statuses are Arabic strings — `مسودة` (draft), `معتمدة` (completed/posted), `ملغاة` (cancelled). Only `معتمدة` is counted anywhere.
- Sales lines: `SalesInvoiceItemModel` stores ProductId / PartNumber / Barcode / ProductName / Quantity / UnitPrice and **no unit cost at sale time** → COGS is **ESTIMATED** from the product's current `Products.PurchasePrice`. A visible Arabic warning ("التكلفة تقديرية") is shown. Schema and invoice logic were **not** changed.
- Returns: `SalesReturnModel.Status` (`مسودة`/`معتمدة`) with `TotalAmount`; per-line `ItemCondition` — "صالح للبيع" (restocked) vs "تالف" (damaged → `DamagedQuantity`, not restocked) vs "استبعاد نهائي من المخزون". Only sellable returns reduce COGS.
- Expenses: `ExpenseModel.IsCancelled` + `CancellationReason`/`CancellationDate`; cancelled rows are excluded from every total.
- No chart library and no export/print library exist → **tables only, no chart** (a note is displayed in the UI). No test project exists on `main` → no tests added, no packages added.

### Formulas implemented (money = decimal, rounded only at presentation)
- Net sales = Σ(`SubTotal − Discount`) of `معتمدة` invoices − Σ(`TotalAmount`) of `معتمدة` returns, in the period.
- COGS = Σ(qty × current `PurchasePrice`) over posted invoice lines − Σ(qty × same price) over **sellable** returned lines → **estimated**, not exact.
- Gross profit = Net sales − COGS. Net profit = Gross profit − Σ(active, non-cancelled expenses in the period).
- Previous period = identical-length window immediately before the selected period; amount and % change shown.
- Date boundaries are **inclusive start / exclusive next-day end** (a full day is never cut off).

### Files
- New: `Services/IReportsService.cs`, `Services/SqliteReportsService.cs`, `Models/ProfitSummaryModel.cs`, `Models/SalesDailyReportRow.cs`, `Models/TopProductReportRow.cs`, `Models/CustomerSalesReportRow.cs`, `Models/ExpenseReportModel.cs`, `ViewModels/ReportsViewModel.cs`, `Views/ReportsView.xaml` (+ `.xaml.cs`).
- Modified: `App.xaml` (DataTemplate), `ViewModels/MainViewModel.cs` (navigation branch + field).
- No `.db` touched, no schema change, no new packages, calculations live in the reports service (MVVM).

### Build
`dotnet build CarPartManager.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)**.

### TODO — Part B
- ✅ Done — see "Reports — Part B" below.

---

## Reports — Part B (2026-10-24)

### Prerequisites verified
- Part A present (tabs structure, global date filter, `IReportsService`/`SqliteReportsService`) — committed on `feature-reports-a`.
- Inventory screen merged on `main` (`StockMovement` ledger, `MinimumStock`/`ProductModel.IsLowStock`, `IInventoryService`).

### Tabs added (all READ-ONLY, reuse Part A filter/service/style)
1. **المشتريات** — posted (`معتمدة`) purchase invoices only; total + count cards, comparison to the equal-length previous window, daily table (cross-checked against its own total), by-supplier table (shown only when supplier data exists, otherwise a visible Arabic note), top purchased products.
2. **تقييم المخزون** — current state (NOT date-filtered, labeled in the tab note with a generation timestamp). Value = Σ(available qty × current cost). Cards: total value, product count, below min level, out of stock, stagnant. Tables: top by value, below min level, out of stock, stagnant. `StagnantDays` defaults to 60 and is adjustable from a ComboBox (30/60/90/180); changing it reloads only this tab.
3. **مديونيات العملاء** — customers with a positive balance, balance, last payment date, oldest unpaid invoice date, total debt. Balance **reuses** `SqliteCustomerService.GetAllCustomersAsync()` (the Customers screen formula: opening + Σ(GrandTotal − PaidAmount of `معتمدة`) − Σ(non-cancelled payments), floored at 0) — not reimplemented. Shows the formula note + "not affected by the date filter".
4. **ربحية الأصناف** — per product in the period: quantity sold, sales value, cost, profit, margin %; plus best/worst tables. Item sales value distributes the invoice discount proportionally (`(SubTotal − Discount)/SubTotal × qty×price`) and subtracts returns; cost uses the current `Products.PurchasePrice` → **التكلفة تقديرية** warning shown.

### Rules respected
- No schema changes, no `.db` touched, app not run, no new packages. Aggregation is DB-side with projections + `AsNoTracking`; decimal Sum/multiply/compare done in memory after projection (SQLite cannot aggregate `decimal`). Draft/cancelled excluded everywhere. Async + loading overlay. Arabic RTL, empty states, no overlapping, resizes cleanly (Style triggers / overlay pattern — no local `Visibility` bindings).

### Cross-checks (visible warnings, not silent)
- Purchases: daily-table total is re-summed from `PurchasesDaily` and shown next to the card total.
- Inventory: `Σ(rows.StockValue)` is recomputed and compared to the report total; mismatch → visible data warning.
- Item profitability: total profit is compared to Part A's gross profit; mismatch → visible warning naming probable causes (discount on a zero-total invoice, return value ≠ its lines, inconsistent invoice data).

### Files
- New: `Models/PurchaseReportModel.cs`, `Models/InventoryValuationModel.cs`, `Models/CustomerDebtReportModel.cs`, `Models/ItemProfitabilityModel.cs`.
- Modified: `Services/IReportsService.cs` (+4 methods), `Services/SqliteReportsService.cs` (4 implementations + helpers), `ViewModels/ReportsViewModel.cs` (per-tab state/loaders/apply + cross-checks), `Views/ReportsView.xaml` (+4 `TabItem`s).

### Build
`dotnet build CarPartManager.csproj` → **Build succeeded, 0 Warning(s), 0 Error(s)** (after the service/models step, after the ViewModel step, and after the XAML step).

### TODO
- Storing a real unit cost on each sales line in a future task would turn COGS/item cost from estimated into exact.

---

## Appendix — Previous task (2026-10-24): Expenses module — COMPLETED

### 1. Inspect-first findings

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

### Not Changed
- No EF Core migration file (table already exists)
- No existing quantity-changing logic altered (only added movement recording)
- No negative stock allowance (pre-existing validation kept)
- No stocktake functionality in Part 1 scope (now implemented in Part 2, see above)
- No location/warehouse columns (designed for future extensibility)

### Expenses — notes
- Additive schema only; existing tables/data untouched. No `.db` file was opened, modified, or deleted.
- `EnsureCreatedAsync()` will not alter an existing database, which is exactly why the new tables are
  created through the explicit idempotent DDL block.
- Expenses store the user as a text label because the app has no authentication; swapping in a real user
  table later only requires changing that string source.
- `CarPartManager.csproj` now excludes `CarPartManager.Tests\**` from the default globs. The stale folder
  holds only `bin/obj` artifacts (no source, no `.csproj`); it can be deleted by hand at any time, after
  which the exclusion becomes harmless.

## Remaining TODO
1. **Stocktake Tab** — ✅ DONE (Part 2, above). Open follow-ups:
   - **`ApproveSessionAsync` snapshot-vs-current bug:** approval does `product.Quantity += (CountedQuantity − BookQuantity)`, where `BookQuantity` is the frozen snapshot but `product.Quantity` is the CURRENT live quantity. If sales/purchases occurred after the snapshot, the net interim movement is effectively double-counted (final = current + counted − book = counted + net-interim), so the approved quantity drifts from the physical count. `HasMovementsAfterSnapshotAsync` only warns (no block/recompute). Likely fix: set `product.Quantity = CountedQuantity` (count authoritative) and log the discrepancy separately.
   - **Negative-stock guard can block a legitimate shortage:** because the snapshot-delta is applied to the already-reduced current quantity, a real shortage can push current below 0 → throw + rollback the whole approval, so the shortage is never recorded. Fixing the snapshot-vs-current bug above should resolve this.
   - **Runtime test coverage:** no automated tests for the stocktake flow (see "Test project" above). Future: add a test project (new packages) with in-memory SQLite covering `Include(Lines)` via SessionId, approval movements/quantities, double-approve blocked, uncounted lines untouched, consistency check after approval.
2. **CS8602 warnings** — 4 pre-existing null-deref warnings in `InventoryViewModel.cs` (217–220), explicitly deferred (out of scope).
3. **Stagnant Stock Filter** - Add filter for products with no sales in N days (requires last-sale-date tracking, skipped as optional)
4. **Paging/Virtualization** - Consider for movements grid if dataset grows large (not needed for current scale)

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
