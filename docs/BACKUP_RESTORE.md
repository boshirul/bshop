# PostgreSQL backup and restore runbook

Use this before deployment, before large imports, and before production launch.

## Backup

Set environment variables or replace values explicitly:

```powershell
$env:PGPASSWORD = "<password>"
pg_dump `
  --host localhost `
  --port 5432 `
  --username postgres `
  --format custom `
  --blobs `
  --verbose `
  --file ".\backups\khanshop_$(Get-Date -Format yyyyMMdd_HHmmss).dump" `
  khanshop
```

Verification:

```powershell
pg_restore --list .\backups\khanshop_YYYYMMDD_HHMMSS.dump | Select-Object -First 20
```

Keep at least:

- Last pre-deployment backup
- Last successful daily backup
- Last weekly backup
- Last monthly backup

## Restore to a new database

Do not restore directly over production until the backup has been tested in a
separate database.

```powershell
$env:PGPASSWORD = "<password>"
createdb `
  --host localhost `
  --port 5432 `
  --username postgres `
  khanshop_restore_test

pg_restore `
  --host localhost `
  --port 5432 `
  --username postgres `
  --dbname khanshop_restore_test `
  --verbose `
  ".\backups\khanshop_YYYYMMDD_HHMMSS.dump"
```

Post-restore checks:

```sql
select count(*) from "Products";
select count(*) from "StockTransactions";
select count(*) from "Sales";
select count(*) from "CustomerLedgerEntries";
select count(*) from "SupplierLedgerEntries";
```

## Production restore

1. Stop API and background jobs.
2. Confirm maintenance window.
3. Back up the current failed database state for investigation.
4. Drop and recreate the target database or restore to a fresh database and
   switch connection strings.
5. Restore using `pg_restore`.
6. Start API.
7. Run smoke tests.
8. Reconcile sales, stock, customer dues, supplier payables, and reports.

## Recovery acceptance

Recovery is accepted only when:

- API health is healthy.
- Admin login works.
- Latest sales and stock ledger records are present.
- Dashboard/report totals reconcile with source tables.
- Background jobs start without failed-job spikes.
