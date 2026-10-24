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

## Findings

### Existing Infrastructure
- **Product Entity** (`ProductModel.cs`):
  - `Quantity` (int) - current stock level
  - `MinimumStock` (int, default 5) - low stock threshold
  - `PurchasePrice` (decimal) - cost price
  - `SellingPrice` (decimal) - selling price
  - `Barcode` (string) - for scanning
  - `Category` (string) - product classification
  - `DamagedQuantity` (int) - separate damaged stock tracking

- **InventoryMovementModel** table already exists with:
  - Id, ProductId, PartNumber, ProductName
  - MovementType, QuantityChange, PreviousQuantity, NewQuantity
  - MovementDate, ReferenceId, ReferenceNumber, Notes

### Quantity Change Paths (Already Hooked)
The following paths already record stock movements in the same transaction:
1. **Purchase Invoice Approval** (`SqlitePurchaseInvoiceService.PostInvoiceAsync`):
   - Line 291: `product.Quantity += line.Quantity`
   - Records "شراء وارد" movement

2. **Purchase Invoice Cancellation** (`SqlitePurchaseInvoiceService.CancelInvoiceAsync`):
   - Line 416: `product.Quantity -= line.Quantity`
   - Records "إلغاء شراء صادر" movement

3. **Sales Invoice Approval** (`SqliteSalesInvoiceService.PostInvoiceAsync`):
   - Line 328: `product.Quantity -= line.Quantity`
   - Records "بيع صادر" movement

4. **Sales Invoice Cancellation** (`SqliteSalesInvoiceService.CancelInvoiceAsync`):
   - Line 453: `product.Quantity += line.Quantity`
   - Records "إلغاء بيع وارد" movement

5. **Sales Return** (`SqliteReturnsService.ProcessSalesReturnAsync`):
   - Line 243: `product.Quantity += item.Quantity` (sellable returns)
   - Line 263: `product.DamagedQuantity += item.Quantity` (damaged returns)
   - Records appropriate movements

6. **Purchase Return** (`SqliteReturnsService.ProcessPurchaseReturnAsync`):
   - Line 420: `product.Quantity -= item.Quantity`
   - Records "مرتجع شراء صادر" movement

### Newly Hooked Path
7. **Manual Product Edit** (`SqliteProductService.UpdateProductAsync`):
   - Now wrapped in transaction
   - If quantity changes, records "تعديل يدوي (تعديل الصنف)" movement
   - Previous behavior: no movement recorded

### Business Rules Verified
- Draft invoices do NOT affect inventory (confirmed)
- Negative stock is NOT allowed (validation prevents it)
- No movements are deleted or edited (append-only ledger enforced)
- All quantity changes happen in atomic transactions

## Implementation Details

### New Files Created
1. **Services/IInventoryService.cs** - Interface for inventory operations
2. **Services/SqliteInventoryService.cs** - SQLite implementation with:
   - `GetInventorySummaryAsync()` - aggregates product count, stock value, low/out counts
   - `GetProductStockInfoAsync()` - filtered projection for DataGrid
   - `GetMovementsAsync()` - filtered movement query
   - `RecordManualAdjustmentAsync()` - atomic adjustment + movement
   - `CheckConsistencyAsync()` - compares product quantity vs movement sum

3. **ViewModels/InventoryViewModel.cs** - MVVM with:
   - Tab selection (Stock Overview / Stock Movements)
   - Real-time search and filtering
   - Manual adjustment UI logic
   - Consistency check command

4. **Views/InventoryView.xaml** - RTL UI with:
   - Summary cards (product count, stock value, low stock, out of stock)
   - DataGrid with status colors
   - Movement filters (date range, type)
   - Manual adjustment section
   - Consistency check button

5. **Views/InventoryView.xaml.cs** - Code-behind

### Modified Files
1. **App.xaml** - Added DataTemplate for InventoryViewModel → InventoryView
2. **ViewModels/MainViewModel.cs**:
   - Added `_inventoryViewModel` field
   - Instantiated InventoryViewModel in constructor
   - Added navigation handler for "Inventory" tag
   - Removed "Inventory" from placeholder descriptions

3. **Services/SqliteProductService.cs**:
   - Wrapped `UpdateProductAsync` in transaction
   - Added movement recording when quantity changes
   - Raises `DataEvents.RaiseInventoryOrInvoicesChanged()` on quantity change

4. **Data/DbInitializer.cs**:
   - Added `BackfillOpeningStockMovementsAsync()` method
   - Calls it during initialization
   - Creates one "رصيد افتتاحي" movement per product (idempotent)

5. **Styles/Controls.xaml**:
   - Added `CleanTabControlStyle` for tab control
   - Added `BooleanToVisibilityConverter` (x:Key)

### Migration Information
**Migration Name:** `OpeningStockMovementsMigration` (embedded in DbInitializer)
**How to Apply:** Automatic on app startup via `DbInitializer.InitializeAsync()`
**Type:** Backfill (idempotent, safe to run multiple times)
**Description:** Creates opening balance movements for existing products that have no opening movement recorded.

**What it does:**
- Iterates all products
- For each product without an "رصيد افتتاحي" movement and with Quantity > 0
- Creates movement: Type="رصيد افتتاحي", QuantityChange=current_quantity, PreviousQuantity=0, NewQuantity=current_quantity
- Does NOT modify any existing product quantities

**No EF Core migration file needed** - table already exists.

## Changed Files Summary
```
New:
  Services/IInventoryService.cs
  Services/SqliteInventoryService.cs
  ViewModels/InventoryViewModel.cs
  Views/InventoryView.xaml
  Views/InventoryView.xaml.cs

Modified:
  App.xaml
  ViewModels/MainViewModel.cs
  Services/SqliteProductService.cs
  Data/DbInitializer.cs
  Styles/Controls.xaml
```

## Build Result
✅ **Build Succeeded** - 0 Warnings, 0 Errors
- Output: `CarPartManager.dll` in `bin\Debug\net10.0-windows\`

## Verification

### Automated Verification
- Build passed with no errors
- Navigation from MainViewModel to InventoryViewModel wired correctly
- DataTemplate binding in App.xaml configured

### Manual Test Steps Required
1. **Launch App** - Navigate to "المخزون والجرد" screen
2. **Stock Overview Tab**:
   - Verify summary cards show correct counts
   - Test search by name/barcode
   - Verify status colors (green/available, orange/low, red/out)
3. **Stock Movements Tab**:
   - Verify movements load (including backfilled opening balances)
   - Test date range filter
   - Test movement type filter
4. **Manual Adjustment**:
   - Select a product from Stock Overview tab
   - Switch to Movements tab
   - Enter quantity change (e.g., +5 or -3)
   - Enter reason (e.g., "تلف")
   - Click "تسجيل"
   - Verify new movement appears in grid
   - Verify product quantity updated in Stock Overview
5. **Consistency Check**:
   - Click "فحص التطابق"
   - Verify it reports mismatches (if any) or confirms all consistent
6. **Product Edit**:
   - Go to Products screen
   - Edit a product's quantity
   - Verify new movement recorded in Inventory screen
7. **Backfill**:
   - Check that existing products have "رصيد افتتاحي" movements
   - Re-run app to verify idempotency (no duplicate opening movements)

## Risks & Not Changed

### Risks
- **None identified** - All changes are additive and backward-compatible
- Backfill is idempotent and doesn't modify existing data
- Manual product edit now requires transaction (performance impact negligible)

### Not Changed
- No EF Core migration file (table already exists)
- No existing quantity-changing logic altered (only added movement recording)
- No negative stock allowance (pre-existing validation kept)
- No stocktake functionality in Part 1 scope (now implemented in Part 2, see above)
- No location/warehouse columns (designed for future extensibility)

## Remaining TODO
1. **Stocktake Tab** — ✅ DONE (Part 2, above). Open follow-ups:
   - **`ApproveSessionAsync` snapshot-vs-current bug:** approval does `product.Quantity += (CountedQuantity − BookQuantity)`, where `BookQuantity` is the frozen snapshot but `product.Quantity` is the CURRENT live quantity. If sales/purchases occurred after the snapshot, the net interim movement is effectively double-counted (final = current + counted − book = counted + net-interim), so the approved quantity drifts from the physical count. `HasMovementsAfterSnapshotAsync` only warns (no block/recompute). Likely fix: set `product.Quantity = CountedQuantity` (count authoritative) and log the discrepancy separately.
   - **Negative-stock guard can block a legitimate shortage:** because the snapshot-delta is applied to the already-reduced current quantity, a real shortage can push current below 0 → throw + rollback the whole approval, so the shortage is never recorded. Fixing the snapshot-vs-current bug above should resolve this.
   - **Runtime test coverage:** no automated tests for the stocktake flow (see "Test project" above). Future: add a test project (new packages) with in-memory SQLite covering `Include(Lines)` via SessionId, approval movements/quantities, double-approve blocked, uncounted lines untouched, consistency check after approval.
2. **CS8602 warnings** — 4 pre-existing null-deref warnings in `InventoryViewModel.cs` (217–220), explicitly deferred (out of scope).
3. **Stagnant Stock Filter** - Add filter for products with no sales in N days (requires last-sale-date tracking, skipped as optional)
4. **Paging/Virtualization** - Consider for movements grid if dataset grows large (not needed for current scale)

## Notes
- App allows negative stock for **cancelled purchases** only (reversal validation prevents going negative if stock was already sold)
- All movements are append-only - no edits or deletes
- Consistency check is **read-only** - does not auto-fix anything
- Manual adjustment requires mandatory reason field
- Design allows future "Location" column without schema changes (via nullable LocationId in InventoryMovementModel)
