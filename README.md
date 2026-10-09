# KhanShop Retail Management System

KhanShop is a web-based retail inventory, POS, warranty, and online-order
management system. The implementation follows the requirements in
`Full Software Blueprint.docx` and the sequence in
`KhanShop Software Implementation Plan.docx`.

## Technology

- ASP.NET Core Web API on .NET 10
- Angular
- PostgreSQL with EF Core
- Dapper for reporting queries
- ASP.NET Core Identity and JWT (Phase 2)
- Hangfire background jobs (Phase 14)

## Architecture

The backend is a modular monolith:

- `RetailShop.Api` — HTTP endpoints, middleware, and composition root
- `RetailShop.Application` — use cases, DTOs, interfaces, and validation
- `RetailShop.Domain` — entities, value objects, enums, and business rules
- `RetailShop.Infrastructure` — EF Core, persistence, and external services
- `RetailShop.Reporting` — Dapper queries and document exports
- `RetailShop.Shared` — stable cross-cutting contracts and helpers
- `RetailShop.Tests` — automated unit and integration tests

Dependencies point inward: Domain contains business rules, Application
coordinates use cases, and infrastructure is composed by the API.

## Backend setup

1. Install the .NET 10 SDK and PostgreSQL.
2. Configure the connection string with an environment variable:

   ```powershell
   $env:ConnectionStrings__RetailShopDatabase = "Host=localhost;Port=5432;Database=khanshop;Username=postgres;Password=your-password"
   ```

   Alternatively, create the ignored file
   `src/RetailShop.Api/appsettings.Local.json`:

   ```json
   {
     "ConnectionStrings": {
       "RetailShopDatabase": "Host=localhost;Port=5432;Database=khanshop;Username=postgres;Password=your-password"
     },
     "Database": {
       "InitialiseOnStartup": true
     }
   }
   ```

3. Restore, build, and test:

   ```powershell
   dotnet restore RetailShop.sln
   dotnet build RetailShop.sln --no-restore
   dotnet test RetailShop.sln --no-build
   ```

4. Run the API:

   ```powershell
   dotnet run --project src/RetailShop.Api
   ```

The initial health endpoint is `GET /api/health`.

## Authentication foundation

Phase 2 uses ASP.NET Core Identity, JWT access tokens, rotating HTTP-only
refresh-token cookies, roles, and independent permissions.

On the first development start, set `Database:InitialiseOnStartup` to `true`.
The application applies migrations and seeds:

- Roles: Admin, Manager, Salesperson
- Permission catalog and default role permissions

The Angular administration screens are available at `/user-management` and
`/audit-log`, backed by `/api/users`, `/api/roles`, `/api/permissions`, and
`/api/audit-log`.
- Development administrator: `admin@khanshop.local`

The development password is configured in `appsettings.Development.json`.
Change that password and replace the JWT signing key before any shared or
production deployment. Password-reset delivery currently writes the reset
token to development logs until an email or SMS provider is configured.

## Product catalog

Phase 3 provides product master data and catalog management:

- Categories and subcategories
- Brands and product models
- Units of measure
- Searchable and paged product list
- Product create, edit, and soft delete
- Generated product codes and EAN-13 barcodes
- Pricing, warranty, serial-number, VAT, and online-sale settings
- Product image metadata and primary-image selection

The Angular screens are available from **Products** in the authenticated
navigation. The API routes are `/api/products`, `/api/categories`,
`/api/subcategories`, `/api/brands`, `/api/product-models`, and `/api/units`.
All write operations require the appropriate product-management permission.

## Settings and business contacts

The remainder of Phase 3 includes:

- Shop, invoice, tax, and system settings
- Configurable cash, card, mobile-banking, bank-transfer, and other payment methods
- Searchable customer and supplier directories
- Generated customer and supplier codes
- Credit limits, contact details, active status, notes, soft deletion, and audit logging

Authenticated screens are available at `/customers`, `/suppliers`, and `/settings`.
Representative development records are seeded through the verified local API workflow.

## Inventory foundation

Phase 4 establishes inventory as a controlled ledger:

- Opening stock is recorded as stock transactions
- Available, reserved, damaged, and warranty quantities are tracked separately
- Weighted-average cost is updated by cost-bearing stock increases
- Stock-adjustment requests require approval or rejection before changing balances
- Approved adjustments can be reversed through compensating ledger entries
- Optimistic concurrency and serializable database transactions prevent negative stock
- Current-stock, low-stock, damaged-stock, ledger, opening-stock, and adjustment screens

The Angular inventory workspace is available at `/inventory`. Stock history is
immutable, and products with stock history must be marked inactive instead of deleted.

## Purchasing and supplier accounting

Phase 5 provides an end-to-end supplier purchasing workflow:

- Direct purchase invoices with supplier invoice references, line discounts, and VAT
- Automatic purchase numbers and latest purchase-price updates
- Atomic stock receipt with weighted-average inventory costing
- Full, partial, and mixed-method supplier payments
- Supplier payable balances derived from an immutable debit/credit ledger
- Partial and full purchase returns with stock and payable reversals
- Protection against overpayment, over-return, negative stock, and duplicate supplier invoices
- Purchase list, invoice detail, payment, return, and supplier-ledger screens

The Angular purchasing workspace is available at `/purchase`. API routes are
`/api/purchases` and `/api/suppliers/{id}/ledger`. Confirming a purchase,
recording its initial payments, updating stock, and posting supplier-ledger
entries occur within one serializable database transaction.

## Sales and point of sale

Phase 6 provides the in-store checkout and customer-account workflow:

- Barcode or product-code scanning with live available-stock visibility
- Cart quantities, authorized price changes, line discounts, and VAT
- Walk-in or registered customers with full, partial, and mixed-method payments
- Customer credit-limit enforcement for sales on due
- Sequential invoice numbers driven by invoice settings
- Atomic stock issue with cost snapshots for stable profit calculation
- Sales history, printable invoice details, and due collection
- Customer balances derived from an immutable debit/credit ledger
- Permission-controlled cancellation with compensating stock and ledger entries
- Protection against overselling, overpayment, walk-in dues, and duplicate cancellation

The Angular POS workspace is available at `/sales-pos`; history and customer
accounts are available beneath the same route. API routes are `/api/sales` and
`/api/customers/{id}/ledger`. Checkout, inventory issue, payments, and customer
ledger posting occur within one serializable database transaction.

## Quotations

Phase 7 provides the customer quotation lifecycle:

- Draft quotations with customer, validity dates, notes, terms, discounts, and VAT
- Permission-controlled price changes and discounts
- Printable quotation details and searchable history
- Draft, sent, accepted, rejected, expired, and converted states
- Automatic expiry and terminal-state validation
- Single-use conversion from an accepted quotation to a sales invoice
- Atomic conversion with stock issue, mixed payments, customer due, and ledger posting
- Customer credit-limit and available-stock validation during conversion
- Traceable quotation-to-sale linkage

The Angular quotation workspace is available at `/quotations`, backed by
`/api/quotations`. Preparing a quotation does not reserve or alter stock;
inventory changes only when an accepted quotation is converted to a sale.

## Customer due collection

Phase 8 completes customer receivables and collection reporting:

- Customer due dashboard with ledger balance and outstanding invoice totals
- FIFO receipt allocation by sale date and invoice number
- Cash, card, bKash, and other configured payment methods
- Printable collection receipts with invoice-level allocations
- Receipt history and payment references
- Current, 31-60, 61-90, and over-90-day aging buckets
- Date-bounded printable customer statements with opening and closing balances
- Permission-controlled manual debit and credit corrections
- Protection against over-collection and duplicate invoice allocation

The Angular customer-account workspace is available at `/customer-accounts`,
backed by `/api/customer-accounts` and `/api/customers/{id}/statement`.
Customer balances remain derived from immutable ledger entries, and each
receipt is posted in one serializable transaction with its invoice allocations.

## Sales returns and complaints

Phase 9 provides the controlled after-sales return workflow:

- Original-invoice lookup with sold, previously returned, and returnable quantities
- Complaint reasons, product condition, notes, and requested resolution
- Permission-separated request and manager/admin approval
- Refund, replacement, customer-due adjustment, and rejection outcomes
- Available, damaged, warranty, and supplier-claim stock routing
- Partial-return support with protection against cumulative over-return
- Approval history and audit logging
- Atomic customer-ledger, sale balance, refund, and stock reconciliation

The Angular returns workspace is available at `/returns`, backed by
`/api/returns`. Approval applies the selected financial and inventory outcome
inside one serializable database transaction.

## Serial numbers and warranty

Phase 10 starts the serialized after-sales workflow:

- Product serial registration with duplicate serial-number protection
- Available serial lookup for serialized products
- POS enforcement for products marked “serial number required”
- Sale-time warranty start and expiry dates based on product warranty months
- Warranty lookup by serial number or invoice number
- Warranty claim creation with complaint, requested resolution, and notes
- Manager/admin claim approval, rejection, and resolution history
- Warranty intake stock, supplier-claim routing, and replacement stock issue
- Protection against unsold, expired, duplicate, or invalid warranty claims

The Angular warranty workspace is available at `/warranty`, backed by
`/api/warranty`. Warranty approval and resolution update serial status and
stock buckets inside serializable database transactions.

## Online store and orders

Phase 11 starts the online-commerce workflow:

- Public online product listing with search and available-stock visibility
- Browser cart and public checkout with delivery information and delivery charge
- Website order creation plus support for Facebook, phone, and WhatsApp sources in the API
- Admin online-order list and detail screens
- Order confirmation that reserves available stock
- Courier name and tracking-number assignment
- Cancellation that releases reserved stock
- Delivery that deducts reserved stock
- Order status history and audit logging
- Serializable transactions for reservation, cancellation, and delivery stock movements

The public Angular store is available at `/online-store`; admin order management
is available at `/online-orders`, backed by `/api/online-store` and
`/api/online-orders`.

## Reporting and dashboard

Phase 12 starts management reporting:

- Dashboard KPI cards for today/month sales, customer dues, stock value, low stock,
  pending online orders, pending returns, and pending warranty claims
- Date-range sales report with invoice, customer, total, due, and profit
- Profit summary with revenue, cost, profit, and margin percentage
- Inventory valuation report with available/reserved stock and stock value
- Customer due report ordered by outstanding balance
- Operational status counts for online orders, returns, and warranty claims
- Permission-protected report APIs under `/api/reports`

The Angular dashboard is available at `/dashboard`; management reports are
available at `/reports`. The reporting service currently uses EF Core query
projections and leaves the `RetailShop.Reporting` project available for later
Dapper/export implementations.

## Data exchange

Phase 13 adds controlled CSV import/export workflows:

- CSV templates for products, customers, suppliers, and opening stock
- Import preview with row-level validation before database changes
- Commit of valid rows with downloadable error CSV for rejected rows
- Import batch tracking and export logging
- Opening-stock imports routed through the inventory opening-stock service
- Product import protection for system-generated product codes and barcodes

The Angular data exchange workspace is available at `/data-exchange`, backed by
`/api/data-exchange`.

## Notifications and background jobs

Phase 14 adds Hangfire-backed background jobs and a notification center:

- Hangfire server with PostgreSQL job storage and recurring job registration
- Retry-safe notification generation using unique deduplication keys
- Copyable SMS/WhatsApp/internal messages for low stock, customer dues,
  warranty expiry, online orders, daily sales summary, and job failures
- Job run monitoring with created-message counts and failure text
- Manual job execution from the Angular notification center

The Angular notification center is available at `/notifications`, backed by
`/api/notifications`. Hangfire storage uses the same PostgreSQL connection as
the application.

## Security, reliability, and performance hardening

Phase 15 starts the hardening pass:

- Built-in ASP.NET Core rate limiting for authentication and public online-store
  endpoints
- Stricter checkout throttling to reduce duplicate or abusive public orders
- Security headers for API responses, including content-type protection,
  frame denial, referrer suppression, and browser feature restrictions
- HSTS and HTTPS redirection enabled for non-development environments
- CSV import size and row-count limits for controlled data exchange
- Permission coverage review confirms all business APIs are protected except
  intentional public/auth endpoints

Development still runs over `http://127.0.0.1` when the environment is
Development. Production should provide HTTPS certificates and replace all
development secrets before launch.

## User acceptance, deployment, and launch

Phase 16 launch-readiness assets are available in:

- `docs/UAT_SCENARIOS.md` — role-based end-to-end acceptance scenarios
- `docs/DEPLOYMENT_RUNBOOK.md` — staging/production deployment and rollback
- `docs/BACKUP_RESTORE.md` — PostgreSQL backup, restore, and recovery checks
- `tools/smoke-test.ps1` — API/frontend smoke test for local, staging, or production

Run the local smoke test with:

```powershell
.\tools\smoke-test.ps1 -Password "ChangeMe!12345"
```

## Frontend setup

The Angular application is in `frontend` and uses standalone components,
Angular Material, Tailwind CSS, and lazy feature routes.

Use Node.js 24 and pnpm 11:

```powershell
cd frontend
pnpm install
pnpm start
```

The development server runs at `http://localhost:4200` and proxies `/api`
requests to `http://localhost:5000`.

## Development rules

- Never alter stock without a stock-transaction record.
- Use database transactions for sales, purchases, returns, and stock changes.
- Save the effective cost price on every sale line.
- Use ledgers for customer and supplier balances.
- Use permission checks, validation, audit logging, and soft deletion.
- Keep online orders distinct from POS sales.
