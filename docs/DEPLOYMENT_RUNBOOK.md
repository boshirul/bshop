# KhanShop deployment and rollback runbook

This runbook is for staging and production deployment.

## Required inputs

- Release/version identifier
- Target environment name
- PostgreSQL host, port, database, username, and password
- HTTPS certificate and public hostname
- JWT issuer, audience, and signing key
- Data-protection key location with persistent storage
- Allowed frontend origin
- Initial Admin account for first deployment only

## Pre-deployment checklist

1. Confirm all code is built from the intended release source.
2. Run backend build and tests:

   ```powershell
   dotnet restore RetailShop.sln
   dotnet build RetailShop.sln --no-restore
   dotnet test RetailShop.sln --no-build
   ```

3. Run frontend production build:

   ```powershell
   cd frontend
   pnpm install --frozen-lockfile
   pnpm build
   ```

4. Back up the target database. See `docs/BACKUP_RESTORE.md`.
5. Confirm `Database:InitialiseOnStartup` policy:
   - Staging: may be `true` for automatic migration.
   - Production: prefer explicit migration during a maintenance window.
6. Confirm secrets are not using development defaults.
7. Confirm UAT sign-off is complete.

## Deployment steps

1. Put the system into a maintenance window if replacing production.
2. Stop the current API service.
3. Deploy backend files.
4. Deploy frontend files to the web host/static host.
5. Apply EF migrations:

   ```powershell
   dotnet tool run dotnet-ef database update `
     --project src\RetailShop.Infrastructure `
     --startup-project src\RetailShop.Api
   ```

6. Start API service.
7. Start frontend host.
8. Verify:
   - `GET /api/health`
   - Login
   - Dashboard
   - POS page
   - Reports page
   - Hangfire dashboard `/jobs`
9. Run the smoke script:

   ```powershell
   .\tools\smoke-test.ps1 `
     -BaseUrl "https://api.example.com" `
     -FrontendUrl "https://app.example.com" `
     -Email "admin@example.com" `
     -Password "<secure-password>"
   ```

## Rollback procedure

Rollback is required if core login, POS, stock, sale, purchase, or database
reconciliation fails after deployment.

1. Stop frontend traffic or enable maintenance page.
2. Stop API service.
3. Restore the database backup captured before deployment.
4. Redeploy previous backend artifact.
5. Redeploy previous frontend artifact.
6. Start API and frontend.
7. Run smoke tests.
8. Reconcile:
   - Latest sale invoice number
   - Current stock for high-volume products
   - Customer due total
   - Supplier payable total
9. Document root cause and decide whether to retry deployment.

## Post-launch monitoring

Monitor for at least one business day:

- API logs and unhandled exceptions
- Login failures and rate-limit spikes
- Hangfire failed jobs
- Database size and slow queries
- POS sale creation latency
- Stock ledger consistency
- Customer/supplier ledger consistency
- Backup completion

## Production configuration rules

- Use HTTPS only.
- Set strong JWT signing key through environment or secret store.
- Keep data-protection keys on persistent storage.
- Restrict CORS to the production frontend origin.
- Disable development admin seed password after first deployment.
- Keep database credentials out of source control.
