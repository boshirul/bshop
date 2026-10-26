# BShop Screen and Action Matrix

Generated from the Angular route tree and API controllers on 2026-10-21. This is the durable inventory for production-readiness testing. `Pending` means the behavior has not yet been exercised in a real browser against IIS; it is not a pass.

## Coverage summary

- 53 routed screens or route variants discovered (including public/authentication routes).
- 31 API controller surfaces discovered.
- Secondary workflows include product media dialogs, quick-customer dialog, cart drawer, invoice print, role-permission editor, import preview/commit, opening-balance setup, and journal drill-down.
- Automated baseline: 45 Angular tests passed; 143 backend tests passed, 8 PostgreSQL tests skipped, 1 PDF export test failed.

## Screen inventory

| Module | Route / surface | Screen | Permission | Principal actions | API / entities | Required acceptance tests | Automation / status |
|---|---|---|---|---|---|---|---|
| Authentication | `/auth/login` | Login | Public | Sign in, validation, forgot-password navigation | `POST api/auth/login`; users/refresh tokens | valid/invalid login, throttling, safe errors | Unit coverage; IIS browser pending |
| Authentication | `/auth/forgot-password` | Forgot Password | Public | Submit email, return to login | `POST api/auth/forgot-password`; identity/email | generic response, invalid email, provider failure, rate limit | Unit coverage; email delivery blocked without test sender |
| Authentication | `/auth/reset-password` | Reset Password | Public token | New/confirm password, visibility, login return | `POST api/auth/reset-password`; identity/reset tokens | valid/expired/replayed token, policy, no URL leakage | Unit/backend coverage; IIS pending |
| Profile | `/my-profile` | My Profile | Authenticated | Edit profile, crop/upload photo, change password | `api/auth/profile`, `profile/photo`, `change-password`; users/media | save/cancel, crop/zoom, upload failure, cleanup | Angular unit coverage; IIS pending |
| Dashboard | `/dashboard` | Operations Dashboard | Authenticated | KPI/date navigation | `GET api/dashboard/summary`; sales/stock/dues | KPI definitions, period filters, stock-value reconciliation | Pending; known 868,600 vs GL 158,600 investigation |
| Products | `/products` | Product List | Authenticated | Search, filter, paginate, edit, deactivate | `GET/DELETE api/products`; products | filtering, pagination, protected delete/history | Pending |
| Products | `/products/new` | New Product | `products.manage` | Save/cancel, prices, tax, variation/model, media | `POST api/products`; products/media/models | uniqueness, invalid values, upload restrictions | Pending |
| Products | `/products/:id/edit` | Edit Product | `products.manage` | Update/cancel, media ordering/primary/delete | `PUT api/products/{id}` and media endpoints | persisted update, optimistic errors, history safety | Pending |
| Master data | `/products/categories` | Categories | Authenticated; mutations backend-authorized | Create/update/delete | `api/categories`; categories | duplicate/protected delete, notifications | Pending |
| Master data | `/products/subcategories` | Subcategories | Same | Create/update/delete | `api/subcategories` | parent validation, duplicate/protected delete | Pending |
| Master data | `/products/brands` | Brands | Same | Create/update/delete | `api/brands` | duplicate/protected delete | Pending |
| Master data | `/products/models` | Product Models | Same | Create/update/delete | `api/product-models` | duplicate/linked-product protection | Pending |
| Master data | `/products/units` | Units | Same | Create/update/delete | `api/units` | duplicate/linked-product protection | Pending |
| Inventory | `/inventory` | Current Stock | `inventory.view` | Search, paginate, view ledger | `GET api/stocks/current`; stock balances | balance/valuation consistency, pagination | Pending |
| Inventory | `/inventory/low-stock` | Low Stock | `inventory.view` | Search/view, notification context | `GET api/stocks/low-stock` | threshold/date behavior | Pending |
| Inventory | `/inventory/damaged` | Damaged Stock | `inventory.view` | View damaged balances | `GET api/stocks/damaged` | damaged ownership/value | Pending |
| Inventory | `/inventory/opening` | Opening Stock | `inventory.openingStock` | Save/cancel opening stock | `POST api/stocks/opening`; balances/transactions | duplicate/negative prevention, weighted average | Pending |
| Inventory | `/inventory/ledger` | Stock Ledger | `inventory.view` | Filters, pagination, source drill-down | `GET api/stocks/ledger`; stock transactions | date/product filters, running quantities | Pending |
| Inventory | `/inventory/adjustments` | Adjustments | `inventory.view`; action policies | Create, approve, reject, reverse | adjustment endpoints; adjustments/stock | atomicity, rejected no stock change, reversal | Backend unit coverage; IIS pending |
| Suppliers | `/suppliers` | Supplier Management | `suppliers.view` | CRUD, search | `api/suppliers`; suppliers | validation, duplicate, protected delete | Pending |
| Purchases | `/purchase` | Purchase List | `purchases.view` | Search, paginate, open details | `GET api/purchases`; purchases | filters/pagination/totals | Pending |
| Purchases | `/purchase/new` | New Purchase | `purchases.manage` | Lines, payment/credit, save/cancel | `POST api/purchases`; purchases/details/stock/AP | validation, duplicate submit, atomic stock/cost | Pending |
| Purchases | `/purchase/:id` | Purchase Detail | `purchases.view` | View, pay, return | `GET api/purchases/{id}` | totals/payment/status/history | Pending |
| Purchases | `/purchase/:id/payment` | Supplier Payment | `purchases.paySupplier` | Record payment/cancel | `POST .../payments`; payment/AP ledger | partial/full, duplicate, mapping/rollback | Pending |
| Purchases | `/purchase/:id/return` | Purchase Return | `purchases.manage` | Return quantities/cancel | `POST .../returns`; returns/stock/AP | bounds, stock impact, finance reversal | Pending |
| Purchases | `/purchase/supplier-ledger` | Supplier Ledger | `purchases.view` | Filter/export | supplier ledger endpoints | running payable/reconciliation | Pending |
| POS | `/sales-pos` | Checkout | `sales.create` | Search/barcode, cart, customer, mixed payment, sale | `POST api/sales`; sales/details/payments/stock | cash/credit/mixed, discount/VAT, idempotency/rollback | Pricing unit coverage; IIS pending |
| Sales | `/sales-pos/history` | Sales History | `sales.view` | Search, paginate, detail/print | `GET api/sales` | filters/pagination | Pending |
| Sales | `/sales-pos/:id` | Sale Detail | `sales.view` | Print, payment, cancel/return navigation | sale detail/print/cancel | immutable totals, cancellation/reversal | Pending |
| Sales | `/sales-pos/:id/payment` | Sale Payment | `customerAccounts.collectDue` | Record payment/cancel | `POST .../payments`; payments/AR | partial/full, no duplicate revenue | Pending |
| Sales | `/sales-pos/customer-ledger` | Customer Ledger | `sales.view` | Filter/drill-down | customers ledger | running balance | Pending |
| Customers | `/customers` | Customer Management | `customers.view` | CRUD/search | `api/customers`; customers | normalized mobile duplicate, protected delete | Backend coverage; IIS pending |
| Customer accounts | `/customer-accounts` | Dues Dashboard | `customerAccounts.view` | Filter/outstanding drill-down | `GET .../dues`; customer ledger | totals/reconciliation | Pending |
| Customer accounts | `/customer-accounts/aging` | Aging | Same | Aging/date filters | `GET .../aging` | bucket boundaries/totals | Pending |
| Customer accounts | `/customer-accounts/statements` | Statements | Same | Select customer/date/export | customer statement/ledger | opening/running/closing | Pending |
| Customer accounts | `/customer-accounts/receipts` | Receipt History | Same | Search/detail/new | receipt endpoints | pagination/status | Pending |
| Customer accounts | `/customer-accounts/receipts/new` | New Receipt | `collectDue` | Allocate payment/save/cancel | `POST .../receipts`; receipts/ledger | overpayment policy, atomicity, no revenue | Pending |
| Customer accounts | `/customer-accounts/receipts/:id` | Receipt Detail | Same | View/print | receipt detail | allocation/audit | Pending |
| Quotations | `/quotations` | Quotation List | `quotations.view` | Search, create, detail | `GET api/quotations` | filters/status | Pending |
| Quotations | `/quotations/new`, `/:id/edit` | Quotation Editor | `quotations.manage` | Save/cancel, lines | POST/PUT quotations | validation/status edit rules | Pending |
| Quotations | `/quotations/:id` | Quotation Detail | `quotations.view` | Send, accept, reject, convert | quotation action endpoints | transition matrix, exactly-once conversion | Pending |
| Returns | `/returns`, `/returns/new`, `/returns/:id` | Sales Returns | `returns.request` plus action policies | Request, approve, reject, inspect | `api/returns`; returns/stock/refunds | bounds, atomic stock/refund/journal | Pending |
| Warranty | `/warranty` + dialogs/tabs | Warranty | `warranty.view` | Register serial, search, create/approve/reject/resolve claim | `api/warranty`; serials/claims | transition/security/history | Pending |
| Expenses | `/expenses` | Expenses & Categories | `expenses.view` plus action policies | Draft/update/post/void; category CRUD | `api/expenses`; expenses/categories | state transitions, payment mapping/journal | Pending |
| Online Store | `/online-store` | Public Catalog | Public | Browse/search/filter, cart drawer/add | `api/online-store/products/categories/policies/media` | responsive images, sticky header, cart persistence/cross-tab | Chrome: catalogue/add/refresh persistence/drawer pass; deployed sticky header fails; read load stable through 10 VUs at 2 aggregate RPS |
| Online Store | `/online-store/products/:id` | Product Detail | Public | View variation/media, add to cart | product detail/media endpoints | stock/price refresh, mobile layout | Pending |
| Online Store | `/online-store/checkout` | Guest Checkout | Public | Validate customer/order, submit | `POST api/online-store/checkout`; online orders | validation, idempotency, interrupted retry, no sensitive persistence | Backend/unit coverage; bounded IIS write pending |
| Online Orders | `/online-orders` + detail panel/dialog | Administration | `onlineOrders.view` plus action policies | Confirm, courier, cancel, deliver, post sale | `api/online-orders`; orders/sales | transitions, unpaid delivery, exactly-once sale | Pending |
| Reports | `/reports` | Management Reports | Authenticated/report policies | Filters, preview pagination, CSV/Excel/PDF export | `api/reports/*`; projections | full-filter export independent of preview page | Angular export test passes; PDF fails license |
| Finance | `/finance` | Setup Dashboard | `finance.view` | Readiness, setup/opening assessment | `api/finance/setup*`; finance activation/config | setup mode, authoritative warning, readiness | Pending authenticated IIS |
| Finance | `/finance/accounts` | Chart of Accounts | Finance permissions | Search/create/update/activate | finance accounts | hierarchy/code/posted restrictions | Backend unit coverage; IIS pending |
| Finance | `/finance/financial-accounts` | Financial Accounts | Finance permissions | Create/view book balance | financial accounts | compatible active GL account, masked identifiers | Pending |
| Finance | `/finance/mappings` | Account Mappings | Finance permissions | Configure system/payment/expense mappings | finance mappings | missing/inactive/incompatible validation | Pending |
| Finance | `/finance/journals` | Journal Inquiry | `finance.view` | Filter/page/drill-down/reversal navigation | finance journals | posted-only, totals, reversal links | Backend coverage; IIS pending |
| Finance | `/finance/manual-journals` | Manual Journals | create/post permissions | Draft, lines, post, reverse | manual/reversal endpoints | balanced/period/activation/control account rules | Backend coverage; live posting withheld |
| Finance | `/finance/transfers` | Fund Transfers | `finance.manageTransfers` | Source/destination/fee/post | transfers | validation, no revenue, duplicate/reversal | Backend coverage; live posting withheld |
| Finance | `/finance/reconciliation` | Reconciliation | Finance permission | Enter statement balances, match/complete | reconciliations | difference and audit rules | Backend coverage; pending real evidence |
| Finance | `/finance/periods` | Fiscal Periods | close-period permission | Create/open/soft-close/close/reopen | periods | overlap, invalid transition, closed rejection | Backend coverage; IIS pending |
| Finance | `/finance/reports`, `/finance/reports/:report` | GL/TB/P&L/BS/CF/Dashboard | `finance.reports.view` | Date/account filters, drill-down, export | finance report endpoints | opening/running/closing, reconciliation, provisional labels | Backend coverage; IIS pending |
| Data exchange | `/data-exchange` | Import/Export | `dataExchange.manage` | Templates, preview, commit, export/logs | `api/data-exchange`; staging/import logs | validation, safe commit, pagination/download | Pending; writes require backup |
| Notifications | `/notifications` | Notifications | `notifications.view` | Generate/copy/dismiss | `api/notifications`; notifications/job runs | idempotent jobs, date boundaries, permissions | Backend coverage; IIS pending |
| Settings | `/settings` + tabs | Shop/Invoice/Tax/System/Payment Methods | `settings.view`; mutation policies | Update/cancel settings, payment CRUD | settings/payment methods | validation, retained secrets, notifications | Pending |
| Administration | `/user-management` + role dialog | Users & Roles | `administration.manageUsers` | User CRUD/activation, roles, permissions | users/roles/permissions | create/edit/email/phone/duplicate/least privilege | Source fixes present; IIS retest pending |
| Administration | `/audit-log` | Audit Log | `administration.viewAuditLog` | Search/filter/paginate | `GET api/audit-log`; audit events | safe details, pagination, authorization | Shared pagination source present; IIS pending |

## Secondary and cross-module workflows

| Workflow | Surfaces | Acceptance condition | Status |
|---|---|---|---|
| Purchase to Inventory to Finance | Purchase editor/detail, stock, supplier ledger, GL/TB | Atomic stock/cost/AP/journal; payment clears AP | Pending; Finance activation unresolved |
| POS to Inventory to Finance | POS, sale detail, stock ledger, P&L | Revenue once, captured COGS, stock decrement, balanced GL | Pending |
| Credit Sale to Receipt | POS, dues, receipt, statement, GL | AR established then cleared; no second revenue | Pending |
| Return | Return request/detail, stock/refund, journal | Approved-only movement; exact reversal/adjustment | Pending |
| Online Order to Sale | Store checkout, order admin, linked sale | Idempotent order/sale, no duplicate stock/accounting; delivery not payment | Pending |
| Finance Transfer | Transfer, account statement, cash flow | Source/down destination/up; fee separate; internal transfer eliminated | Pending |

## Test queue and safety gates

1. Prove IIS deployment matches current source before interpreting UI behavior.
2. Verify and back up `khanshop` before any synthetic write.
3. Obtain a supported authenticated test session without resetting the only administrator.
4. Run public storefront browser tests and read-only load stages first.
5. Run bounded `BSHOP-TEST-` writes only after backup verification and baseline capture.
6. Keep Finance disabled if inventory/opening/cutover/reconciliation readiness differs.
7. Resolve the PDFsharp licensing failure before a release requiring PDF export can be READY.
