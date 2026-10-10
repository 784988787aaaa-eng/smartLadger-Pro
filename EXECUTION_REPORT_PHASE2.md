# Phase 2 Execution Report — Inventory, Warehouse & Multi-Unit Engine
Date: 2026-10-10
Repository: https://github.com/784988787aaaa-eng/smartLadger-Pro
Branch: main

## Delivery status
**IMPLEMENTATION COMMITTED — VERIFICATION NOT YET CONFIRMED.** The changes below were committed directly to `main`. This session did not have a runnable local .NET 10 Windows workspace, and the GitHub connector did not return workflow-run results for the latest push. Therefore this report does not claim a successful build, zero warnings, or a 100% passing test suite.

## Implemented

### Domain and schema
- Added product commercial metadata properties: product code, category ID, major/minor selling prices, minimum selling price, reorder point, low-stock threshold, updated timestamp, total minor-unit stock, and formatted dual-unit stock display.
- Added `ProductBatch` domain record with batch number, expiry, remaining minor-unit quantity, cost, supplier, purchase reference, and creation timestamp.
- Added additive SQLite migration v11. Existing product columns and rows are retained. Migration adds `CategoryId`, `MajorUnit`, `MinorUnit`, `SellingPriceMajor`, `SellingPriceMinor`, `MinSellingPrice`, `ReorderLevel`, and `UpdatedAt`; the existing `ConversionFactor` column is ensured for legacy databases.
- Added `ProductBatches` with supplier and purchase-reference metadata, plus category, non-empty barcode uniqueness, and batch-product indexes.
- Existing products are backfilled from their legacy unit names, sale price, purchase price, alert threshold, and creation date. A partial unique barcode index permits multiple legacy blank barcodes while enforcing uniqueness for populated values.

### Repository and stock transactions
- Product create/update/list operations now persist and read the added commercial metadata.
- Product search includes name, barcode, and category; added exact barcode lookup.
- Added batch creation and active-batch listing. Batch receipt inserts the lot and updates on-hand stock inside one SQLite transaction.
- Added stock-movement retrieval for a product.
- Outbound stock movements consume available lots in expiry-first order within the same transaction as the movement and on-hand stock update. Legacy stock not assigned to a lot remains usable after lot quantities are exhausted.
- When an authenticated session is supplied, batch receipt also records an inbound purchase movement in the same transaction.

### ViewModel and WPF
- Added a reusable dual-unit conversion utility for major/minor quantity breakdown, quantity entry conversion, and deriving a minor-unit price from a major-unit price.
- Added save-and-new and save-and-close commands, Ctrl+Enter/Ctrl+S bindings, and internal barcode generation for products without a manufacturer barcode.
- Added batch entry validation for quantity, cost, optional expiry date, and optional batch number.
- Product selection loads both the chronological stock card and active batches.
- Updated the inventory KPI strip to show active SKU count, cost valuation, retail valuation, and low-stock count.
- Added search by name/barcode/category, stock status filter chips, dual-unit quantity display, category, cost, retail and wholesale price columns, and batch/stock-card tabs.

## Tests added or updated
- `InventoryUnitConversionTests.cs`: major/minor breakdown, combined quantity conversion, and unit-price conversion.
- `ProductPersistenceTests.cs`: create/update metadata round-trip, barcode/name/category search, and duplicate-barcode rejection.
- `BatchStockTests.cs`: batch receipt, lot valuation, outbound deduction from the earliest-expiring lot, and on-hand balance consistency.
- Updated database bootstrap test to expect schema version 11.

## Verification record
- Changes were written to `main` through GitHub file commits.
- The tests above were added/updated but **were not executed in this session**.
- No verified Release build result or current green CI run was available through the connected GitHub actions interface at report time.
- No local build can be asserted; zero-warning compilation and a 100% test pass rate remain unverified.

## Remaining work before acceptance
1. Obtain a completed Windows .NET 10 Release build and run the full test suite; resolve any compile, XAML, analyzer, or test failures until green.
2. Verify migration v11 against a copy of a real v10 database, including databases with legacy duplicate non-empty barcodes; resolve any index-creation conflicts without silently discarding user data.
3. Add regression coverage for restoring/deleting batch-linked stock movements so lot quantities are reversed consistently with on-hand stock.
4. Complete end-to-end UI verification for keyboard focus/search, product form layout, price conversion and display, and batch entry in the actual WPF application.
5. Validate inventory valuation semantics when some legacy stock has no batch records and document whether valuation is weighted-average or FIFO/FEFO.
6. Add an explicit category-management workflow if users need to create/rename normalized categories instead of entering category names as text.

## Phase 2 commit references
- `2c7add375d9bd0570571392b41a2bcd8bc1b859c` — product and batch domain extensions
- `329da14992bc6d0f670fc7d6914c37dbbdcd18f0` — repository interface additions
- `90e8e09eb311015034d92e3844e9da344d95fd8f` — SQLite migration v11
- `e03f141738507aabd07d1f57b4f1857bf1ae0945` — product metadata persistence and batch APIs
- `cb8568aff32bcfa02e69df0a690f843e04d908e9` — dual-unit calculation utility
- `87ac5318e83f3413db5ec19749e4367404567da1` — unit conversion tests
- `bb79d6ca2a7abee637e4e8f25080de2cae019abd`, `ce1affd5f22f687daba064861242ef362c41c5d2` — inventory ViewModel commands and pricing
- `90935cf82345eba4e18f90f7252c5a1350e9ff94`, `6e287e629a30cced957817167045021381bcd48a`, `e289afbbbb348f0ac68bb68c311132dbce34456d`, `ee8cc01160a3f8fb7587c33f479c82847645b52e` — inventory UI, batch inspector, dual-unit grid and filter chips
- `302e0eaff570337123fbfa8c7803362570c4be2a`, `5e7681ca87ad4fe490c904921b961015dd5a89d6`, `79c25abcd0bebfe6c97f9b2b51c3b7946721cd0b`, `c21ca032bd4031a0d07333a7ff84362ff7cd0c51` — batch loading, receipt movement logging and atomic batch deduction
- `88d261ee3413fa617d240e2e2f6fbdd7ae4bbf16` — product persistence tests
- `ae8e92f456b50c56c89c109c629b72c2f02d15db`, `566fd6aac4baa90704e5e4a2b9f9f85e273e9651`, `b96d6634e0a574c63e10f3af5872a1745114cd1e` — batch integration tests
- `c12a3c13b58ab2eb494d9f1bca447061e22d1c35` — schema bootstrap test update
- `8a213e78a319a73ddb8320c9ccb9d2184e9b370b` — formatted dual-unit stock display
- `d2294410d6186a53b8181ed44511b6f4803d13fe` — use barcode as the persisted product-code identity
