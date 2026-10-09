# Reporting & Documents

- `PdfDocumentService`: A4, A5, Thermal 80mm, Thermal 58mm.
- RTL content direction.
- Business identity is loaded from SQLite.
- Arabic amount-to-words engine is isolated and testable.
- `ExcelExportService`: numeric amount cells, custom `#,##0.00` format, RTL worksheet view.
- Developer attribution: م/ منصور قطينه للبرمجيات — 774004399.


## Current acceptance status — 2026-10-07
Automated PDF/Excel tests: 3/3 PASS. A4/A5/80mm/58mm are verified as generated PDF profiles. Physical printer is NOT VERIFIED. Manual report-screen acceptance is NOT VERIFIED because the current UI acceptance batch is blocked by A-001.
