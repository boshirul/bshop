# KhanShop UAT scenarios

Use this checklist for staging acceptance before production launch. Run each
scenario with the listed role and record pass/fail, evidence, and defects.

## Roles

| Role | Main coverage |
| --- | --- |
| Admin | Users, roles, settings, imports, jobs, audit, full operations |
| Manager | Inventory, purchases, approvals, reports, notifications |
| Salesperson | POS, quotations, customers, returns intake, warranty intake |

## Core scenarios

### 1. Master data and settings

- Admin updates shop, invoice, tax, and system settings.
- Manager creates category, brand, unit, product model, product, customer, and supplier.
- Verify product code/barcode and customer/supplier codes are generated.
- Verify Salesperson cannot access user-management or role-management screens.

Acceptance:

- Changes are visible on edit/list screens.
- Unauthorized screens are blocked by route guards and API permissions.

### 2. Inventory opening stock and adjustment

- Manager records opening stock for a product.
- Manager requests positive and negative stock adjustments.
- Admin/Manager approves one adjustment and rejects another.
- Verify current stock and stock ledger reflect only approved movements.

Acceptance:

- No stock change exists without stock transaction history.
- Negative stock is blocked.
- Reversal creates compensating transactions.

### 3. Purchase and supplier accounting

- Manager creates a purchase invoice with multiple products.
- Record partial supplier payment.
- Create a purchase return where applicable.
- Verify supplier ledger and payable balance.

Acceptance:

- Stock, weighted-average cost, purchase status, and supplier balance reconcile.

### 4. POS sale and customer due

- Salesperson creates a cash sale.
- Salesperson creates a due sale against a customer.
- Record a customer receipt and allocate against due invoices.
- Print/view sale invoice, receipt, and customer statement.

Acceptance:

- Sale ledger, stock deduction, due balance, and receipt allocation reconcile.
- Discount and price-change permissions behave as configured.

### 5. Quotations

- Salesperson creates quotation.
- Send/accept/reject quotation.
- Convert accepted quotation to POS sale.

Acceptance:

- Quotation status history is correct.
- Converted sale uses quotation products and prices.

### 6. Returns and complaints

- Salesperson submits a return request against an invoice.
- Manager approves/refunds/adjusts due as applicable.
- Verify returned stock bucket and customer ledger.

Acceptance:

- Approved returns update stock and financial ledgers once.
- Rejected returns do not change stock or balances.

### 7. Serial numbers and warranty

- Register serial numbers for a serialized product.
- Sell a serialized product.
- Lookup warranty by serial and invoice.
- Create, approve, and resolve a warranty claim.

Acceptance:

- Serial status and warranty stock bucket transitions are correct.
- Duplicate or expired warranty claims are blocked.

### 8. Online store and online orders

- Public user browses `/online-store` and checks out.
- Manager confirms order, assigns courier, cancels one order, and delivers another.
- Verify stock reservation/release/delivery ledger entries.

Acceptance:

- Pending order does not reserve stock.
- Confirmed order reserves stock.
- Cancel releases reserved stock.
- Delivery deducts reserved stock.

### 9. Reports and dashboard reconciliation

- Review dashboard totals.
- Run sales, profit, inventory, customer dues, and operations reports.
- Compare report totals with sale list, stock list, customer ledger, and order statuses.

Acceptance:

- Reports reconcile to source records for the selected date range.

### 10. Data exchange

- Download customer/supplier/product/opening-stock templates.
- Preview imports with valid and invalid rows.
- Commit valid rows.
- Download error file for rejected rows.
- Export each data type and verify export log.

Acceptance:

- Invalid rows cannot write business data.
- Opening-stock import creates stock transactions.
- Product import cannot override system-generated product code or barcode.

### 11. Notifications and background jobs

- Run low-stock, due, warranty, order, and daily notification jobs manually.
- Verify job run records.
- Copy and dismiss generated messages.
- Open Hangfire dashboard `/jobs` with an authorized Admin/Manager.

Acceptance:

- Re-running jobs does not duplicate messages for the same deduplication key.
- Unauthorized users cannot access job controls.

### 12. Device and print checks

- Test desktop, tablet, and mobile layouts for dashboard, POS, inventory, reports, and online store.
- Print A4 sale invoice, receipt, customer statement, and warranty/return documents.
- Test barcode scanner input in POS/product lookup.

Acceptance:

- Layouts remain usable.
- Print output aligns with shop requirements.
- Scanner behaves like keyboard input and submits expected values.

## Sign-off table

| Area | Owner | Result | Evidence | Defects |
| --- | --- | --- | --- | --- |
| Master data/settings |  |  |  |  |
| Inventory |  |  |  |  |
| Purchases |  |  |  |  |
| POS/customer dues |  |  |  |  |
| Quotations |  |  |  |  |
| Returns |  |  |  |  |
| Warranty |  |  |  |  |
| Online orders |  |  |  |  |
| Reports |  |  |  |  |
| Data exchange |  |  |  |  |
| Notifications/jobs |  |  |  |  |
| Print/device checks |  |  |  |  |
