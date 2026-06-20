# Database Migration Guide

This document explains the database architecture and how to migrate between different database technologies.

## Current Architecture

### Repository Pattern

The application uses the **Repository Pattern** with **Unit of Work** to abstract database operations. This makes it easy to swap database technologies without changing business logic.

```
┌─────────────────────────────────────────────────────────────┐
│                    Business Layer                            │
│  (Controllers, Services, Helpers)                            │
└──────────────────────┬──────────────────────────────────────┘
                       │ Uses
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Repository Abstractions                         │
│  - IRepository<T>                                            │
│  - IUnitOfWork                                               │
└──────────────────────┬──────────────────────────────────────┘
                       │ Implemented by
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Database Implementations                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  LiteDB      │  │   SQLite     │  │  SQL Server  │      │
│  │  (Active)    │  │  (Legacy)    │  │  (Future)    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## Current State

### Active Database: LiteDB
- **File**: `mercurius.litedb`
- **Contains**: All data (Identity + Business)
- **Status**: ✅ Active

### Legacy Database: SQLite
- **Files**: `identity.db`, `mercurius.db`
- **Contains**: Historical data
- **Status**: ⏳ Available for migration

## How to Migrate to Another Database

### Step 1: Create New Repository Implementation

Create a new implementation of `IRepository<T>` for your target database:

```csharp
// Example: SqlServerRepository.cs
public class SqlServerRepository<T> : IRepository<T> where T : class
{
    private readonly SqlConnection _connection;
    
    public SqlServerRepository(SqlConnection connection)
    {
        _connection = connection;
    }
    
    public async Task<T?> GetByIdAsync(int id)
    {
        // SQL Server implementation
        var result = await _connection.QuerySingleOrDefaultAsync<T>(
            "SELECT * FROM {TableName} WHERE Id = @id", 
            new { id });
        return result;
    }
    
    // ... implement other methods
}
```

### Step 2: Create New Unit of Work

```csharp
// Example: SqlServerUnitOfWork.cs
public class SqlServerUnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;
    
    public SqlServerUnitOfWork(SqlConnection connection)
    {
        _connection = connection;
    }
    
    public IRepository<T> Repository<T>() where T : class
    {
        return new SqlServerRepository<T>(_connection);
    }
    
    public async Task BeginTransactionAsync()
    {
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
    }
    
    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            _transaction = null;
        }
    }
    
    // ... implement other methods
}
```

### Step 3: Update Program.cs

Replace LiteDB registration with your new database:

```csharp
// BEFORE (LiteDB):
builder.Services.AddSingleton<LiteDbContext>(sp => 
    new LiteDbContext(liteDbConnectionString));
builder.Services.AddScoped<IUnitOfWork>(sp => 
    sp.GetRequiredService<LiteDbContext>().CreateUnitOfWork());

// AFTER (SQL Server):
var sqlConnectionString = builder.Configuration.GetConnectionString("SqlServer");
builder.Services.AddScoped<SqlConnection>(sp => 
    new SqlConnection(sqlConnectionString));
builder.Services.AddScoped<IUnitOfWork>(sp => 
    new SqlServerUnitOfWork(sp.GetRequiredService<SqlConnection>()));
```

### Step 4: Create Migration Service

Create a migration service to move data from LiteDB to your new database:

```csharp
public class LiteDbToSqlServerMigration
{
    public async Task MigrateAsync()
    {
        // Read from LiteDB
        using var liteDb = new LiteDatabase("mercurius.litedb");
        var products = liteDb.GetCollection<Product>("products").FindAll();
        
        // Write to SQL Server
        using var sqlConnection = new SqlConnection(sqlServerConnectionString);
        foreach (var product in products)
        {
            await sqlConnection.ExecuteAsync(
                "INSERT INTO Products (...) VALUES (...)", 
                product);
        }
    }
}
```

### Step 5: Run Migration

```csharp
// In Program.cs or a separate console app
using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<LiteDbToSqlServerMigration>();
    await migrationService.MigrateAsync();
}
```

## Supported Databases

### Currently Implemented
- ✅ **LiteDB** - Document database, single file, embedded
- ✅ **SQLite** - Relational database, single file (legacy)

### Easy to Add
- 🔄 **SQL Server** - Implement `IRepository<T>` with Dapper or EF Core
- 🔄 **PostgreSQL** - Implement `IRepository<T>` with Npgsql
- 🔄 **MongoDB** - Implement `IRepository<T>` with MongoDB driver
- 🔄 **Cosmos DB** - Implement `IRepository<T>` with Cosmos SDK

## Key Files

### Repository Abstractions
- `Mercurius.Repo/Repositories/IRepository.cs` - Generic repository interface
- `Mercurius.Repo/Repositories/IUnitOfWork.cs` - Transaction management

### LiteDB Implementation
- `Mercurius.Repo/Repositories/LiteDbRepository.cs` - LiteDB repository
- `Mercurius.Repo/Repositories/LiteDbUnitOfWork.cs` - LiteDB transactions
- `Mercurius.Repo/Repositories/LiteDbContext.cs` - LiteDB context

### Migration
- `Mercurius.Repo/Migrations/DataMigrationService.cs` - SQLite → LiteDB migration

## Best Practices

### 1. Always Use Interfaces
```csharp
// ✅ Good: Depends on abstraction
public class ProductService
{
    private readonly IRepository<Product> _productRepository;
    
    public ProductService(IUnitOfWork unitOfWork)
    {
        _productRepository = unitOfWork.Repository<Product>();
    }
}

// ❌ Bad: Depends on concrete implementation
public class ProductService
{
    private readonly LiteDbRepository<Product> _productRepository;
}
```

### 2. Use Unit of Work for Transactions
```csharp
// ✅ Good: Transaction across multiple operations
public async Task CreateOrderAsync(Order order)
{
    using var unitOfWork = _unitOfWork;
    await unitOfWork.BeginTransactionAsync();
    
    try
    {
        await unitOfWork.Repository<Order>().AddAsync(order);
        await unitOfWork.Repository<Inventory>().UpdateAsync(inventory);
        await unitOfWork.CommitTransactionAsync();
    }
    catch
    {
        await unitOfWork.RollbackTransactionAsync();
        throw;
    }
}
```

### 3. Keep Migrations Idempotent
```csharp
// ✅ Good: Can run multiple times safely
public async Task MigrateAsync()
{
    if (await _repository.CountAsync() > 0)
    {
        _logger.LogInformation("Data already exists, skipping migration");
        return;
    }
    // ... migration logic
}
```

## Troubleshooting

### LiteDB Connection Issues
```csharp
// Use shared connection for multiple processes
var connectionString = "Filename=mercurius.litedb;Connection=shared";
```

### Migration Performance
- Use bulk insert operations
- Process data in batches
- Create indexes after migration

### Data Integrity
- Always backup before migration
- Verify counts after migration
- Run data validation checks

## Future Roadmap

1. **Phase 1**: Complete LiteDB migration ✅
2. **Phase 2**: Add SQL Server support (if needed)
3. **Phase 3**: Add cloud database support (Cosmos DB, etc.)
4. **Phase 4**: Implement read replicas for scaling

## Questions?

If you need to migrate to a different database:
1. Create new `IRepository<T>` implementation
2. Create new `IUnitOfWork` implementation
3. Update `Program.cs` DI registration
4. Create migration service
5. Test thoroughly before switching

The architecture is designed to make this process straightforward!
