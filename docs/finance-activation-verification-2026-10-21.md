# Finance activation verification — 2026-10-21

Environment: local development IIS and PostgreSQL `localhost:5432/khanshop`.

## Safety and baseline

- Fresh backup: `backups/finance-final/khanshop-pre-opening-post-20261021-010321.dump`
- Backup size: 313,148 bytes; `pg_restore --list`: 606 entries.
- Stock: 24 available, 0 reserved, 0 damaged, 0 warranty, 0 supplier claim.
- Moving weighted-average valuation: BDT 158,600.
- Customer receivables: BDT 0; supplier payables: BDT 0.
- Historical operational evidence: Cash receipts BDT 71,000, Cash supplier payments BDT 15,000, bKash receipts BDT 16,500.

## Opening balance

| Test | Source | Journal | Debit | Credit | Expected | Actual | Result |
|---|---|---|---:|---:|---|---|---|
| OPEN-20261020 | Opening batch `01a15a3e-9a90-7eb9-9cd5-039e1fe945d0` | `JRN-20261020-fdc6de0257` | 158,600 | 158,600 | Dr Inventory / Cr Opening Equity | Dr 1300 / Cr 3300 | Pass |
| OPEN-RETRY | Same batch retry | none added | 0 | 0 | Reject duplicate | HTTP 400; one journal remains | Pass |
| IMMUTABILITY | Journal `01a15b0a-e911-72e7-bc26-e2d624f8c5e3` | same | — | — | Direct update rejected | PostgreSQL trigger rejected and description remained unchanged | Pass |

## Statements after opening

- Inventory Asset: BDT 158,600 debit.
- Opening Balance Equity: BDT 158,600 credit.
- Trial Balance: debit BDT 158,600; credit BDT 158,600; difference BDT 0.
- Balance Sheet: assets BDT 158,600; liabilities BDT 0; equity BDT 158,600; difference BDT 0.
- Profit & Loss: revenue BDT 0; COGS BDT 0; net profit BDT 0.
- Cash Flow: beginning BDT 0; movement BDT 0; ending BDT 0.
- Reports remain provisional because Finance is disabled and financial-account reconciliation is incomplete.

## Activation decision

Automatic posting remains disabled. Financial-account reconciliation is blocked by historical Cash/bKash payment evidence and lack of externally verified opening cash/wallet balances. No safe cutover date was explicitly approved. Recommended earliest cutover is a future Asia/Dhaka business date after reconciliation and immediately following the accepted opening position.

No Finance-enabled purchase, sale, receipt, payment, return, transfer, manual journal, cancellation, or online-order scenario was executed.

## Confirmed defect corrected

PostgreSQL `validate_journal_balance()` used a `CASE` expression that referenced incompatible trigger row fields. Migration `20261020190359_FixJournalBalanceTrigger` replaces it with table-specific `IF/ELSE` field access. Opening posting passed after the correction.

## Remaining verification

- Eight disposable PostgreSQL concurrency tests remain skipped by prior user decision.
- IIS deployment is blocked because `bkhan\user` has read/execute but not write access to the IIS destinations.
- PDFsharp licensing remains deferred.

## Final automated verification

- .NET solution build: passed with 0 warnings and 0 errors.
- Focused Finance tests: 31 passed; 8 disposable-PostgreSQL tests skipped.
- Full backend tests: 143 passed, 8 skipped, 1 failed (the deferred PDFsharp licence test).
- Angular tests: 44 passed.
- Angular IIS production build: passed.
- EF pending-model check: no model changes pending.
- `git diff --check`: passed; only line-ending conversion notices were reported.
