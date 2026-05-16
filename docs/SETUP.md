# VYRON Laundry Marketplace v3.0 — Setup Guide

A trust-driven laundry marketplace with:
- **Customer Mobile App** (.NET 9 MAUI)
- **Rider Mobile App** (.NET 9 MAUI)
- **Admin Web Portal** (ASP.NET Core 8 MVC — mobile responsive)
- **Store Owner Portal** (same web app, role-aware)
- **Backend API** (ASP.NET Core 8 Web API, JWT, SignalR, Hangfire)

---

## ✨ What's new in v3

| Change | Impact |
|---|---|
| **GUID primary keys** everywhere (`uniqueidentifier` / `uuid`) | No autoincrement collisions, safe for distributed inserts |
| **Database provider switch** via `appsettings.json` | One config value → SQL Server *or* PostgreSQL |
| **Mobile-responsive admin portal** | Hamburger drawer + card-stack tables under 768px |
| **Store Owner portal** | Owners log in with phone, see only their stores, accept orders, reply to disputes |
| **MAUI apps on .NET 9** | Modern target frameworks, latest packages, no `Frame`/`HasShadow` |
| **Normalized schema with indexes** | Phone, OrderNumber, RefreshToken (unique); StoreId+ServiceType, Entity+EntityId composite; CreatedAt/Status hot-path |

---

## 🛠 Prerequisites

| Tool | Version |
|---|---|
| Visual Studio 2022 | 17.8+ (with ASP.NET and .NET Multi-platform App UI workloads) |
| .NET SDK | **8.0** (for API and Admin) and **9.0** (for MAUI mobile apps) |
| SQL Server | 2019+ (or LocalDB) — **OR** PostgreSQL 14+ |
| Android SDK / iOS SDK | for mobile builds (set up automatically by VS) |

---

## 🚀 Quick Start (SQL Server, default)

### 1. Restore solution

```bash
cd vyron-marketplace-v3
dotnet restore Vyron.sln
```

### 2. Configure database

Edit `Vyron.API/appsettings.json` and `Vyron.Admin/appsettings.json`:

```json
"Database": { "Provider": "SqlServer" },
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;User=sa;Password=YOUR_PWD;Database=VYRONDB;Encrypt=True;TrustServerCertificate=True"
}
```

### 3. Create the schema

**Option A — let EF migrate on first run** (recommended):

```bash
cd Vyron.API
dotnet ef migrations add InitialV3
dotnet ef database update
```

**Option B — run the SQL script** in `docs/VYRONDB_SqlServer_Setup.sql` manually, then let EF seed data on first API start.

### 4. Run the API

```bash
cd Vyron.API
dotnet run
```

API: `https://localhost:5001` · Swagger: `https://localhost:5001/swagger` · SignalR: `/hubs/tracking` · Hangfire: `/hangfire`

### 5. Run the Admin portal

```bash
cd Vyron.Admin
dotnet run
```

Open `https://localhost:7001` (port shown in console).

---

## 🐘 Switching to PostgreSQL

### 1. Install PostgreSQL 14+ and create a role/database

```bash
createdb vyrondb
createuser vyron --pwprompt
psql vyrondb -c "GRANT ALL ON SCHEMA public TO vyron;"
```

### 2. Update both `appsettings.json` files:

```json
"Database": { "Provider": "PostgreSQL" },
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=vyrondb;Username=vyron;Password=YOUR_PWD"
}
```

### 3. Apply migrations

```bash
cd Vyron.API
dotnet ef migrations add InitialV3_Pg
dotnet ef database update
```

The same C# code and entity model work on both providers — the `DatabaseConfiguration.AddVyronDatabase()` extension switches at runtime.

---

## 🔑 Demo credentials (seeded by EF migrations)

| Role | Identifier | Password | Lands on |
|---|---|---|---|
| Admin | `admin` | `Vyron@Admin2024!` | `/Home` (admin dashboard) |
| Store Owner | `+2348022222222` *(Fresh & Clean)* | `StoreOwner@2024` | `/StoreOwner/Dashboard` |
| Store Owner | `+2348033333333` *(Sparkle & Shine)* | `StoreOwner@2024` | `/StoreOwner/Dashboard` |
| Store Owner | `+2348044444444` *(Royal Wash)* | `StoreOwner@2024` | `/StoreOwner/Dashboard` |
| Rider | `+2348011111111` | (OTP-only via mobile app) | Rider mobile app |

> ⚠️ **Production**: replace these defaults. Use real password hashing or OTP-based login for store owners.

---

## 📱 Mobile apps (.NET 9 MAUI)

Both `Vyron.CustomerApp` and `Vyron.DriverApp` target:

```xml
<TargetFrameworks>net9.0-android;net9.0-ios;net9.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net9.0-windows10.0.19041.0</TargetFrameworks>
```

Packages: `Microsoft.Maui.Controls 9.0.30`, `CommunityToolkit.Maui 11.0.0`, `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.AspNetCore.SignalR.Client 9.0.0`.

### API URL

Edit `AppConstants.cs` in each mobile project to point at your API host:

| Target | URL |
|---|---|
| Android emulator → host PC | `http://10.0.2.2:5000/` |
| iOS simulator | `http://localhost:5000/` |
| Physical device on LAN | `http://<your PC's IP>:5000/` |

### Build/run from Visual Studio

1. Set `Vyron.CustomerApp` (or `Vyron.DriverApp`) as startup project.
2. Choose target: `Android Emulator`, `iOS Simulator`, `Windows Machine`, etc.
3. F5 → build → deploy.

### Build from CLI

```bash
dotnet build Vyron.CustomerApp/Vyron.CustomerApp.csproj -f net9.0-android
```

> Modern .NET 9 MAUI patterns used throughout: `Border` (not `Frame`), `RoundRectangle` corner shapes, no `HasShadow`, source-generated MVVM with `[ObservableProperty]` and `[RelayCommand]`.

---

## 📂 Project structure

```
Vyron.sln
├── Vyron.Shared/                # Enums (incl. DatabaseProvider, StoreOwner role)
├── Vyron.API/                   # ASP.NET Core Web API (port 5000)
│   ├── Models/Entities.cs       # 16 entities, all Guid PKs
│   ├── Data/VyronDbContext.cs   # Indexes + FK + seed
│   ├── Persistence/             # Database provider switch
│   ├── Services/                # Auth, Order, Store, Dispute, Review, Payment, Analytics
│   ├── Controllers/             # API + StoreOwnerController
│   ├── Hubs/                    # SignalR
│   └── Program.cs
├── Vyron.Admin/                 # ASP.NET Core MVC (port 5001)
│   ├── Data/AdminDbContext.cs   # Shared entity model, no migrations
│   ├── Services/AdminServices.cs # Repos + analytics
│   ├── Controllers/             # Admin + StoreOwner
│   ├── Views/                   # Razor + StoreOwner views
│   │   ├── Account/             # Login (mobile-friendly)
│   │   ├── Home/                # Admin dashboard
│   │   ├── Orders/, Stores/, Riders/, Disputes/, Reviews/, Payments/, Settings/
│   │   ├── StoreOwner/          # Owner portal
│   │   │   ├── Dashboard.cshtml
│   │   │   ├── Orders.cshtml, OrderDetail.cshtml
│   │   │   ├── Stores.cshtml, StoreEdit.cshtml
│   │   │   ├── Disputes.cshtml, DisputeDetail.cshtml
│   │   │   └── Reviews.cshtml
│   │   └── Shared/_Layout.cshtml # Mobile-responsive layout
│   ├── wwwroot/
│   │   ├── css/admin.css        # Responsive (hamburger, card-stack tables)
│   │   └── js/admin.js          # Drawer toggle, data-labels
│   └── Program.cs               # Cookie auth + DB switch
├── Vyron.CustomerApp/           # .NET 9 MAUI (Android/iOS/MacCat/Win)
├── Vyron.DriverApp/             # .NET 9 MAUI (Android/iOS/MacCat/Win)
└── docs/
    ├── VYRONDB_SqlServer_Setup.sql
    ├── VYRONDB_PostgreSQL_Setup.sql
    └── SETUP.md (this file)
```

---

## 📱 Mobile-responsive admin

The admin portal CSS uses three breakpoints:

| Breakpoint | Behaviour |
|---|---|
| `> 1024px` (desktop) | Sidebar + 4-col stats |
| `≤ 1024px` (tablet) | Sidebar + 2-col stats |
| `≤ 768px` (mobile) | Off-canvas hamburger drawer + 2-col stats + tables become card stack |
| `≤ 480px` (phone) | 1-col stats, full-width buttons |

Test it: open `https://localhost:7001`, then in browser DevTools toggle device toolbar (Ctrl+Shift+M).

---

## 🛡 Store Owner authorization

The `StoreOwnerController` enforces that store owners can only operate on their own stores:

```csharp
if (!IsAdmin && order.Store.OwnerId != CurrentUserId) return Forbid();
```

Owners can only set statuses: `Confirmed`, `Processing`, `Ready`. All other transitions are admin-only.

---

## 🧪 First-run smoke test

1. Run the API (port 5000).
2. Run the Admin portal (port 5001).
3. Browse `https://localhost:5001/`.
4. Log in as `admin` / `Vyron@Admin2024!` — you land on the admin dashboard with seeded data.
5. Log out, then log in as `+2348022222222` / `StoreOwner@2024` — you land on the Store Owner dashboard, scoped to *Fresh & Clean Laundry* only.
6. Resize the browser to 600px wide — the sidebar collapses into a hamburger drawer; tables become card stacks.
7. Open Swagger (`https://localhost:5001/swagger`) to test API endpoints.

---

## 🆘 Troubleshooting

| Problem | Fix |
|---|---|
| `Cannot open database VYRONDB` | Confirm `Database:Provider` matches your connection string. For SQL Server, ensure SQL auth is enabled. |
| `relation "Users" does not exist` (Postgres) | Run `dotnet ef database update` from `Vyron.API/`. |
| `Login failed` for `sa` | Enable mixed-mode auth in SQL Server config, restart the service. |
| Android emulator can't reach API | Use `http://10.0.2.2:5000/` in `AppConstants.cs`, not `localhost`. |
| iOS device can't reach API | Use your PC's LAN IP. Add ATS exception in `Info.plist` for HTTP. |
| Cookie not persisting in admin | Ensure HTTPS in production. For local HTTP-only dev, the cookie SameSite=Lax already works. |

---

## 📜 License

Proprietary — VYRON Laundry / CodeBox Technologies.
