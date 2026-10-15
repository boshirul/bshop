# BShop local IIS deployment

This runbook deploys one local BShop installation to the IIS applications
`http://localhost/BShop/` and `http://localhost/BShopApi/`. It is not a
production-internet deployment guide: use HTTPS, a strong secret store, and a
restricted PostgreSQL account before exposing BShop to other machines.

## Prerequisites

Run these checks from an **elevated** PowerShell session before changing IIS:

```powershell
Get-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Get-ChildItem 'C:\Program Files\IIS\Asp.Net Core Module\V2'
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\URL Rewrite'
```

Install/enable the following if a check is missing:

- IIS Web Server and Management Tools.
- IIS URL Rewrite 2.1 (needed by the Angular SPA fallback).
- The .NET 10 ASP.NET Core Hosting Bundle, matching the deployed runtime.

Do not copy the source tree, `node_modules`, development credentials, or an
`appsettings.Local.json` file into IIS directories.

## Build release artifacts

From the repository root, use a clean working copy and a backed-up target
database:

```powershell
dotnet publish .\src\RetailShop.Api\RetailShop.Api.csproj -c Release --no-restore `
  --disable-build-servers /p:MSBuildEnableWorkloadResolver=false `
  -o .\artifacts\publish\BShopApi

Set-Location .\frontend
pnpm install --frozen-lockfile
pnpm build --configuration production,iis
```

The Angular `iis` configuration writes `/BShop/` as the base URL and uses the
sibling API URL `/BShopApi/api`. Deploy only
`frontend\dist\retail-shop-web\browser\*` to the frontend application.
The included `web.config` rewrites only client-side routes to `index.html`; it
does not rewrite existing files or the `api` namespace.

## Persistent media and keys

Create persistent folders outside both publish directories. Product media is
served only by authorized API endpoints (or the explicitly restricted public
store endpoint); do **not** configure `C:\BShopData\Media` as an IIS virtual
directory or a static-file directory.

```powershell
New-Item -ItemType Directory -Force C:\BShopData\Media\products\images
New-Item -ItemType Directory -Force C:\BShopData\Media\products\videos
New-Item -ItemType Directory -Force C:\BShopData\Media\products\posters
New-Item -ItemType Directory -Force C:\BShopData\Keys
```

Product uploads use new, safe filenames such as
`sony-bravia-43-inch_20261014_204530_125_a83f2c.jpg`. Existing database keys
are not renamed. If an earlier installation uses a different media root, copy
it first, verify every referenced file resolves under the new root, retain the
old root as a backup, then change `MediaStorage__RootPath`. Never move/delete
the old source before this verification. Include `C:\BShopData\Media` and
`C:\BShopData\Keys` in every backup and restore procedure.

## IIS applications (administrator step)

Create two application pools with **No Managed Code**, Integrated pipeline, and
`ApplicationPoolIdentity`: `BShopPool` and `BShopApiPool`. Create two child
applications under `Default Web Site`:

| Application | Physical path | Pool |
|---|---|---|
| `/BShop` | `C:\inetpub\wwwroot\BShop` | `BShopPool` |
| `/BShopApi` | `C:\inetpub\wwwroot\BShopApi` | `BShopApiPool` |

The API publish output contains the ASP.NET Core Module `web.config`; preserve
it. Grant the API pool only the filesystem rights it needs:

```powershell
icacls C:\BShopData\Media /grant 'IIS AppPool\BShopApiPool:(OI)(CI)(M)' /T
icacls C:\BShopData\Keys /grant 'IIS AppPool\BShopApiPool:(OI)(CI)(M)' /T
```

The frontend needs read access only. Do not grant the frontend pool access to
media, key, or PostgreSQL-secret locations.

Set API configuration as IIS application environment variables, not source
files. Required values are:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__RetailShopDatabase=<development/test PostgreSQL connection string>
Jwt__Issuer=<installation issuer>
Jwt__Audience=<installation audience>
Jwt__Key=<at least 32-byte secret>
DataProtection__KeysPath=C:\BShopData\Keys
MediaStorage__RootPath=C:\BShopData\Media
Https__Enabled=false                 # loopback-only HTTP installation
```

`Https__Enabled=false` is allowed only for this local loopback verification.
For a real-user or non-local installation, use an HTTPS binding/certificate and
leave it `true` (the default). Do not set `Database__InitialiseOnStartup=true`
for an established database. Apply reviewed EF migrations explicitly during a
maintenance window, after a backup.

## Deployment, verification, and rollback

1. Stop/recycle only `BShopPool` and `BShopApiPool`.
2. Copy the built frontend browser artifact into `C:\inetpub\wwwroot\BShop`.
3. Copy the API publish artifact into `C:\inetpub\wwwroot\BShopApi`.
4. Configure the API environment values above, then start/recycle both pools.
5. Verify `/BShop/`, `/BShopApi/api/health`, login, a protected route refresh,
   an authorized product-media request, and a public-store media request.
6. Upload a non-production test image, recycle `BShopApiPool`, and confirm it
   still renders. Remove that test data only through the application if needed.

For rollback: stop the two pools, restore the pre-deployment PostgreSQL backup
if a migration was applied, restore the previous frontend/API artifacts, keep
the media root intact, restart the pools, and run the same smoke checks. Never
delete a media root during rollback.
