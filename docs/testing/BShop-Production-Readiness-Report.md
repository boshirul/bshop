# BShop Production Readiness Report

**Assessment date:** 2026-10-22
**Status:** In progress — durable checkpoint 1
**Current recommendation:** **NOT READY**

This report records executed evidence, not planned work presented as passed. The full matrix is in `bshop-screen-action-matrix.md`.

## 1. Coverage

| Measure | Current evidence |
|---|---:|
| Routed screens/route variants inventoried | 53 |
| API controller surfaces inventoried | 31 |
| Angular unit tests executed | 45 passed / 45 |
| Backend tests executed | 143 passed, 1 failed, 8 skipped / 152 |
| Browser E2E journeys completed in this checkpoint | 1 partial public-store journey |
| Load stages completed in this checkpoint | 0 |
| Database-writing functional scenarios completed | 0 |

The inventory includes non-sidebar routes, dialogs and action workflows. Most functional actions remain `Pending`, so they are not included in the passed count.

## 2. Build and automated regression baseline

| Check | Result | Evidence |
|---|---|---|
| .NET solution build | PASS | Debug build, 0 warnings, 0 errors |
| Backend suite | FAIL | 143 pass; PDFsharp license failure; 8 PostgreSQL tests skipped |
| Angular unit suite | PASS | 10 files, 45 tests |
| Angular IIS production build | PASS | output `frontend/dist/retail-shop-web`; initial bundle 639.63 kB raw |
| `git diff --check` | PASS with line-ending notices | no whitespace errors; LF/CRLF warnings only |
| Browser E2E | PARTIAL | Real Chrome exercised IIS storefront at a narrow mobile-like viewport; authenticated admin session was also observed |
| EF pending-model/migration check | PENDING | database identity and backup gate first |

### Known automated failure

`ManagementReportExporterTests.ExportPdf_UsesReadableCurrencyFallbackAndCreatesMultiplePagesForLongReports` fails with `PdfSharpLicenseException: The loaded license is not valid.` This is a release blocker for required PDF functionality and has not been skipped, modified or hidden.

### Skipped tests

Eight `PostgreSqlFinanceIntegrationTests` are skipped. They cover migration objects, deferred balance constraints, concurrency/exactly-once behavior, immutability, fiscal exclusion, outer transactions and reversals. Per user direction, no disposable PostgreSQL environment will be requested. They remain unverified and are not counted as passes.

## 3. Deployment baseline

| Item | Observed |
|---|---|
| IIS API health | PASS — `GET /BShopApi/api/health` returned Healthy |
| Reported API version | `1.0.0.0` |
| Deployed frontend `index.html` timestamp | 2026-10-21 20:11:58 local |
| Deployed API DLL timestamp | 2026-10-21 17:22:08 local |
| OS | Windows 11 Pro, build 26300 |
| Memory | 31,962 MB physical |
| Load generator location | Same machine as IIS/PostgreSQL (when executed) |

The differing deployment timestamps now correspond to an observed parity defect: the IIS storefront header disappears after scrolling, while current source contains the sticky host rule. The API is reachable and healthy. The safeguarded deployment script was attempted but correctly refused to run because the process was not elevated; no IIS files changed.

### Database safety baseline

- Running IIS configuration identifies `localhost:5432/khanshop`; PostgreSQL confirmed `current_database()=khanshop` on local IPv6 loopback.
- Fresh backup: `backups/production-readiness/khanshop-pre-readiness-20261021-223022.dump`.
- Custom-format archive size: 326,448 bytes; `pg_restore -l` returned 606 archive entries, confirming readable contents.
- All 24 migrations through `20261020190359_FixJournalBalanceTrigger` are applied.
- Baseline counts: Sales 7; SalePayments 7; Purchases 2; PurchasePayments 2; CustomerLedgerEntries 10; SupplierLedgerEntries 4; StockBalances 3; StockTransactions 21; ExpenseVouchers 1; FinanceAccounts 24; JournalEntries 1.
- No database writes were performed by readiness testing after the backup; all database evidence in this checkpoint was read-only.

## 4. Module results

| Module | Automated evidence | IIS functional evidence | Outcome |
|---|---|---|---|
| Authentication/Profile | Angular and backend unit coverage exists | Pending | PARTIAL |
| Products/Master Data | Backend coverage exists | Pending | PARTIAL |
| Inventory | Backend coverage exists | Pending | PARTIAL |
| Purchases/Suppliers | Backend coverage exists | Pending | PARTIAL |
| POS/Sales | Pricing/backend coverage exists | Pending | PARTIAL |
| Customers/Accounts | Backend coverage exists | Pending | PARTIAL |
| Quotations/Returns/Warranty/Expenses | Backend coverage exists | Pending | PARTIAL |
| Online Store/Orders | Cart and backend coverage exists | Chrome: catalog, add-to-cart, cart refresh persistence and drawer passed; deployed sticky header failed | PARTIAL/FAIL |
| Finance | Foundation/operations/report unit tests pass | PostgreSQL/browser and activation checks pending | PARTIAL |
| Reports | Angular export behavior passes | PDF export fails licensing | FAIL |
| Settings/Admin/Audit/Data Exchange | Some unit coverage; recent source fixes | IIS retest pending | PARTIAL |

## 5. Defect register

| ID | Severity | Module | Reproduction / actual | Expected | Root cause / fix | Retest |
|---|---|---|---|---|---|---|
| REL-001 | High | Reports/PDF | Run full backend suite; PDF exporter throws invalid-license exception | Required PDF export succeeds | Deferred PDFsharp licensing configuration/implementation | Still failing |
| REL-002 | High until verified | Finance | Eight database-specific integrity/concurrency tests are skipped | PostgreSQL behavior proven | No approved disposable test DB; shared DB cannot be used destructively | Blocked/unverified |
| REL-003 | Medium | Deployment | Frontend/API deployed artifacts have different timestamps and current source has uncommitted newer work | Customer tests use a known coherent release build | Publish parity and elevated IIS deployment verification pending | Pending |
| REL-004 | High until reconciled | Finance/Inventory | Operational dashboard reportedly shows stock value 868,600 while opening inventory journal is 158,600 | Definitions and cutover reconcile, or activation remains blocked | Investigation pending; no balancing entry permitted | Pending |
| REL-005 | Medium | IIS Online Store | Scroll the deployed mobile-width catalog; header/cart disappear | Header/cart remain sticky and accessible | IIS served an older frontend; corrected `app-online-store-header { position: sticky }` exists in current source | PASS after deployment and hard-refresh browser retest |
| REL-006 | Medium | Frontend security headers | IIS frontend HTML lacked `nosniff`, frame protection, referrer and permissions policies | Baseline browser hardening headers present | Source `frontend/public/web.config` only contained rewrite rules | PASS after controlled `web.config` deployment: all expected headers verified |
| REL-007 | Low | Finance journal inquiry | Posted opening journal description still says “PROPOSED ONLY” without a visible status | Immutable posting state is unambiguous | Historical description is correctly immutable but status was implicit | PASS after deployment: explicit POSTED badge visible in Chrome |

## 6. Finance verification

- Known journal reference: `JRN-20261020-fdc6de0257`.
- Expected opening: Inventory Asset debit BDT 158,600; Opening Balance Equity credit BDT 158,600.
- Automatic posting activation is disabled. The latest activation row has mappings/opening approved, but `ReconciliationReady=false`, no cutover date and no approval timestamp.
- Current database baseline: 73 available units across three products; moving-average inventory valuation BDT 868,600; AR BDT 0; AP BDT 0.
- One posted journal exists. Total debits and credits are each BDT 158,600 and the database-wide journal difference is BDT 0.
- The opening batch `01a15a3e-9a90-7eb9-9cd5-039e1fe945d0` is Posted and links to `JRN-20261020-fdc6de0257`.
- The BDT 710,000 stock/GL difference is explained by operational activity after the opening position while automatic posting was disabled: Solar Panel purchase +BDT 870,000, Solar Panel sale cost -BDT 145,000, and Solar Battery sale cost -BDT 15,000. Net unposted inventory movement: +BDT 710,000.
- This is a genuine cutover/reconciliation gap, not a dashboard calculation defect. Finance activation is unsafe until the business approves how these post-opening, pre-activation transactions are represented. No balancing entry or historical backfill was fabricated.
- No journals, opening balances or operational records were created or modified.

## 7. Performance report

A dependency-free, project-scoped read-only harness exists at `tests/performance/storefront-load.mjs`.

| Stage | VUs | Shared rate cap | Requests | HTTP 200 | 429 | p50 | p95 | p99 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Initial limiter probe | 1 | 5 RPS | 143 | 119 | 24 | 2.26 ms | 11.14 ms | 22.79 ms |
| Clean baseline | 1 | 1 RPS | 30 | 30 | 0 | 3.31 ms | 14.27 ms | 22.29 ms |
| Light concurrency | 5 | 2 RPS | 60 | 60 | 0 | 3.30 ms | 4.85 ms | 5.60 ms |
| Moderate concurrency | 10 | 2 RPS | 60 | 60 | 0 | 6.43 ms | 8.22 ms | 8.96 ms |

The clean stages covered catalogue browsing, search, product detail and checkout-policy reads. There were no 5xx responses. The 429 responses in the probe confirm the configured 120 requests/minute per-IP `PublicStore` policy. Because throughput was deliberately held below that policy and runs were only 30 seconds, these results demonstrate stability at 10 simulated users / 2 aggregate RPS, not maximum capacity. Checkout writes, media throughput, CPU/memory trends, PostgreSQL connection pressure and sustained higher-stage capacity remain unmeasured.

Environment caveat: the eventual generator, IIS and PostgreSQL will share one Windows 11 machine, so results will describe this local installation only and cannot be treated as public-hosting capacity.

## 8. Security and deployment readiness

Verified so far:

- API health endpoint responds under IIS.
- Source builds without .NET compiler warnings.
- Authentication/permissions exist at Angular route and backend policy boundaries by source inspection.
- No database write, migration, activation, external email or external payment was initiated.

Outstanding:

- Database identity, migration history and fresh verified backup.
- Authenticated authorization matrix and privilege-escalation checks.
- Upload/MIME/path traversal, CORS/security headers and rate-limit evidence.
- IIS source parity, persistent keys/secrets/media preservation, publish/rollback drill.
- Browser coverage at 375/768/1366/1920.
- Background-job status and duplicate execution check.
- Customer installation/upgrade/rollback runbook.

## 9. Final recommendation

**NOT READY.** Current explicit blockers are:

1. Required PDF export fails due to PDFsharp licensing.
2. Real browser and cross-module workflows have not yet been completed against the current IIS build.
3. Finance inventory/opening variance is unresolved; activation must not be forced.
4. Database-specific Finance integrity/concurrency tests remain unverified.
5. Online Store load/capacity measurements have not yet been executed.
6. Deployed artifact parity with current source is not proven.

### Real-browser evidence

Chrome opened `http://localhost/BShop/online-store` against IIS. The catalogue loaded three current products with live prices and availability. Adding Solar Panel updated the count to 1, displayed accessible status feedback, survived browser refreshes and deployment, and the cart drawer showed quantity 1 and subtotal BDT 15,000. The drawer remained usable at the observed narrow viewport. After the 2026-10-22 frontend deployment and a hard refresh, the storefront header, Shop link, cart link and quantity badge remained visible while scrolling through product cards. The sticky-header acceptance check now passes.

The authenticated admin shell was also observed at `/BShop/sales-pos`, with Finance, Users & Roles and Audit Log links visible. Finance `/finance` loaded in Setup Mode with required mappings Complete, opening Posted, reconciliation Pending, inventory 73 / BDT 868,600, and balanced journal totals BDT 158,600. `/finance/journals` displayed `JRN-20261020-fdc6de0257` with equal debit/credit totals and, after deployment/hard refresh, an explicit POSTED badge. No sale, checkout or Finance write was submitted.

### Required elevated publish command

Run from an **Administrator PowerShell** session:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File D:\Workshop\KhanShop\scripts\deploy-local-iis-finance.ps1
```

The script stages a fresh Release API publish, backs up both IIS targets, excludes deployed `appsettings.json`, `web.config`, `.keys`, logs, media and uploads, validates Robocopy exit codes, and attempts rollback on failure.

The frontend `web.config` was subsequently deployed separately with a timestamped rollback copy at `C:\inetpub\wwwroot\BShop\web.config.backup-20261022-012933`. IIS now returns `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`, and `X-Permitted-Cross-Domain-Policies: none`. Frontend HTTP status and API health both remain successful.

## 10. Resume queue

1. Establish authenticated browser automation safely; execute high-priority CRUD and permission journeys.
2. Resolve/approve treatment of the BDT 710,000 post-opening pre-activation inventory movement; do not activate beforehand.
3. Execute a clean post-rate-window load baseline and progressive read stages.
4. Execute public storefront responsive tests with real browser evidence.
5. Activate only if all supported readiness checks pass after the cutover gap is resolved.
6. Perform bounded `BSHOP-TEST-` writes after safety gates; use supported reversal/cancellation, not deletion of journals.
7. Resolve the PDF blocker at the final licensed implementation stage and rerun the full suite.
