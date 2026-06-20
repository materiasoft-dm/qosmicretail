# Mercurius Project Summary

## Overview
Inventory and Sales Management System converted from Blazor Server + SQLite to ASP.NET MVC + LiteDB.

## Architecture Changes

### Database
- **Before**: SQLite (Entity Framework Core)
- **After**: LiteDB (NoSQL document database)
- **Location**: `C:\temp\Mercurius\Mercurius\mercurius.litedb`

### UI Framework
- **Before**: Blazor Server + MVC Hybrid
- **After**: Pure ASP.NET MVC + jQuery
- **Theme**: Metronic v8

### Authentication
- **System**: ASP.NET Core Identity with LiteDB
- **Admin User**: admin@mercurius.com / Admin@123
- **Authorization**: Claims-based with role permissions

## Controllers Updated to LiteDB

| Controller | Status | Notes |
|------------|--------|-------|
| ProductsController | ✅ | Full CRUD with repository pattern |
| UsersController | ✅ | Uses Identity RoleManager |
| RolesManagerController | ✅ | Role and permission management |
| InvoicesController | ✅ | Invoice management |
| AccountController | ✅ | Login, register, profile |
| LocationSelectorController | ✅ | Location selection |
| SuppliersController | ✅ | Supplier management |
| AdjustmentReasonsController | ✅ | Config |
| ColorsController | ✅ | Config |
| LocationsController | ✅ | Config |
| ProductCategoriesController | ✅ | Config |
| SizesController | ✅ | Config |

## Data Migration Status

### SQL Scripts Found
- `client1_backup.sql` - 8.7MB (11,537 INSERT statements)
- `Demo_intial.sql` - 213KB (351 INSERT statements)

### Current State
- ✅ LiteDB database created (360KB)
- ✅ Identity data (users, roles) migrated
- ❌ Business data (products, invoices) - NOT migrated
- ⚠️ SQL scripts have different schema than LiteDB models

### Schema Differences
SQL Server tables have different column names than LiteDB models:
- SQL: `CreatedDate` → LiteDB: `CreateDate`
- SQL: `UpdatedDate` → LiteDB: `UpdateDate`
- SQL: `IsActive` → LiteDB: `StatusId`
- SQL: `ProductCategoryId` → LiteDB: `CategoryId`
- And many more...

## Key Files

### Repository Pattern
- `Mercurius.Repo/Repositories/IRepository.cs`
- `Mercurius.Repo/Repositories/IUnitOfWork.cs`
- `Mercurius.Repo/Repositories/LiteDbRepository.cs`
- `Mercurius.Repo/Repositories/LiteDbUnitOfWork.cs`

### ViewComponents (replaced Blazor)
- `Mercurius/ViewComponents/HeaderViewComponent.cs`
- `Mercurius/ViewComponents/SidebarViewComponent.cs`

### Logging
- `Mercurius/Services/LoggerService.cs`
- `Mercurius/Middleware/ExceptionHandlingMiddleware.cs`
- Logs: `/logs/mercurius_YYYYMMDD.log`

## Running the Application

```bash
cd C:\temp\Mercurius\Mercurius\Mercurius
dotnet run
```

Access at: **http://localhost:5094**

## Known Issues

1. **Search on Roles Edit page** - Shows unrelated results when searching
2. **Data Import** - SQL scripts not yet imported to LiteDB
3. **NuGet Config** - Had to fix path from D:\ to C:\temp\

## TODO

- [ ] Fix search functionality on Roles Edit page
- [ ] Import SQL script data to LiteDB (schema mapping required)
- [ ] Add more unit tests
- [ ] Optimize LiteDB queries with indexes
- [ ] Add data backup/restore functionality

## Test Suite

- **Location**: `Mercurius.Tests/`
- **Framework**: xUnit
- **Tests**: 68 tests covering Repository, UnitOfWork, Helpers
- **Run**: `dotnet test` in Mercurius.Tests folder

## URLs

| Page | URL |
|------|-----|
| Login | /Identity/Account/Login |
| Products | /Products |
| Invoices | /Invoices |
| Roles Manager | /RolesManager |
| Users | /Users |
| System Logs | /Logs |
| Dashboard | / (redirects to Products) |

## Technologies

- ASP.NET Core 10
- LiteDB 5.0.15
- jQuery 3.6.0
- DataTables 1.13.4
- Bootstrap 5
- Metronic Theme v8

## Last Updated

2025-01-01

## Notes for Future Development

1. To add a new controller:
   - Inject `IUnitOfWork` in constructor
   - Use `_unitOfWork.GetRepository<Entity>()` for data access
   - Call `_unitOfWork.SaveChangesAsync()` to commit

2. To add a new entity:
   - Create model class with `Id` property
   - Add to `LiteDbContext` if needed
   - Repository pattern handles CRUD automatically

3. Database location:
   - LiteDB: `C:\temp\Mercurius\Mercurius\mercurius.litedb`
   - Identity: `C:\temp\Mercurius\Mercurius\identity.db` (SQLite - kept for Identity)
