# Mercurius Architecture & Code Audit Report

**Date:** $(Get-Date -Format "yyyy-MM-dd")
**Solution:** `c:\Users\DM\OneDrive - MSFT\Mercurius\Mercurius\Mercurius.sln`
**Framework:** .NET 9.0
**Database:** LiteDB 5.0.15

---

## Executive Summary

The Mercurius solution demonstrates solid architectural patterns with a well-structured layered architecture. The codebase shows good practices in several areas but has critical issues that need immediate attention, particularly around error handling and security.

**Overall Assessment:** ⚠️ **Needs Attention** — Good foundation with significant improvements needed

---

## 1. Critical Issues (Fix Immediately)

### 1.1 Silent Catch Blocks — Data Loss Risk

**Severity:** 🔴 Critical
**Files:**
- `Mercurius/Services/LoggerService.cs` (line 78-80)
- `Mercurius/Middleware/ExceptionHandlingMiddleware.cs` (line 58-60)

**Issue:** Both logging mechanisms silently swallow exceptions, potentially losing critical error information.

```csharp
// LoggerService.cs:78-80
catch
{
    // Silent fail - don't throw from logger
}
```

```csharp
// ExceptionHandlingMiddleware.cs:58-60
catch
{
    // Silently fail - don't let logging crash the app
}
```

**Impact:** If file logging fails (disk full, permissions), errors are completely lost with no visibility.

**Recommendation:** 
- Log to a fallback location (Event Log, console) when file logging fails
- Add metrics/alerting for logging failures
- Consider using a structured logging library like Serilog with sinks

---

### 1.2 Obsolete SmtpClient — Security & Compatibility Risk

**Severity:** 🔴 Critical
**File:** `Mercurius/EmailSender.cs` (line 51)

**Issue:** `System.Net.Mail.SmtpClient` is obsolete in .NET 6+ and marked obsolete since .NET Core 2.0.

```csharp
using var smtpClient = new SmtpClient(_host, _port)
{
    Credentials = new NetworkCredential(_user, _password),
    EnableSsl = _enableSsl,
};
```

**Impact:** 
- Security vulnerabilities in older implementations
- May stop working in future .NET versions
- No support for modern SMTP features

**Recommendation:** Replace with `MailKit` library:
```csharp
using var smtpClient = new SmtpClient();
await smtpClient.ConnectAsync(_host, _port, _enableSsl ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None);
await smtpClient.AuthenticateAsync(_user, _password);
await smtpClient.SendAsync(message);
```

---

### 1.3 Missing Cancellation Token Propagation

**Severity:** 🟠 High
**Files:** Multiple controllers

**Issue:** `CancellationToken` is accepted in action methods but not passed to repository calls.

**Example - `ProductsController.cs`:**
```csharp
public async Task<IActionResult> GetForSaleModal(
    int page = 1,
    int pageSize = 50,
    string? search = null,
    int? categoryId = null,
    CancellationToken cancellationToken = default)  // <- Declared but not used
{
    // ...
    var items = query.OrderBy(p => p.Name).Skip(skip).Limit(pageSize).ToList();
    // ^ Missing: query.ToListAsync(cancellationToken)
}
```

**Impact:** Requests cannot be cancelled on client disconnect, wasting server resources.

**Recommendation:** Pass `cancellationToken` to all async repository methods.

---

## 2. High Priority Enhancements

### 2.1 File.AppendAllText Performance Issue

**Severity:** 🟠 High
**Files:**
- `Mercurius/Services/LoggerService.cs` (line 76)
- `Mercurius/Middleware/ExceptionHandlingMiddleware.cs` (line 58)

**Issue:** `File.AppendAllText` opens/closes the file on every write, causing:
- File system contention under load
- Potential data corruption on concurrent writes
- Poor performance

**Recommendation:** Use a buffered writer or Serilog with async file sink:
```csharp
private static readonly SemaphoreSlim _logSemaphore = new(1, 1);
private static readonly StreamWriter _logWriter;

private async Task WriteToFileAsync(string level, string message, Exception? exception = null)
{
    await _logSemaphore.WaitAsync();
    try
    {
        await _logWriter.WriteLineAsync(logEntry);
        await _logWriter.FlushAsync();
    }
    finally
    {
        _logSemaphore.Release();
    }
}
```

---

### 2.2 Missing Input Validation on Public Endpoints

**Severity:** 🟠 High
**File:** `Mercurius/Controllers/ProductsController.cs` (line 156-200)

**Issue:** `GetForSaleModal` accepts arbitrary `pageSize` without sufficient bounds:
```csharp
if (pageSize > 100) pageSize = 100;  // Only caps at 100
// No minimum enforcement for pageSize < 10
```

**Impact:** Could lead to excessive data transfer or memory usage.

**Recommendation:** Add stricter validation:
```csharp
if (pageSize < 10) pageSize = 10;
if (pageSize > 50) pageSize = 50;  // Lower cap for modal
```

---

### 2.3 No Rate Limiting on API Endpoints

**Severity:** 🟠 High
**Files:** All API controllers

**Issue:** No protection against brute-force or abuse.

**Recommendation:** Add rate limiting middleware:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("api", context => 
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress,
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
});
```

---

## 3. Medium Priority Improvements

### 3.1 Batch Size Limits Not Enforced Consistently

**Severity:** 🟡 Medium
**Files:** Multiple controllers

**Issue:** Different controllers use different max page sizes (25, 50, 100, 200).

| Controller | Max Page Size |
|------------|---------------|
| ProductsController | 200 |
| CustomersController | 200 |
| GetForSaleModal | 100 |

**Recommendation:** Define constants for consistent limits:
```csharp
public static class PaginationDefaults
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;
    public const int MaxModalPageSize = 50;
}
```

---

### 3.2 Missing Request/Response Logging

**Severity:** 🟡 Medium
**Files:** All controllers

**Issue:** No structured logging for API requests/responses for debugging.

**Recommendation:** Add middleware for request logging:
```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
    };
});
```

---

### 3.3 No Health Checks

**Severity:** 🟡 Medium
**Files:** `Program.cs`

**Issue:** No health check endpoint for container orchestration (Kubernetes, Docker).

**Recommendation:** Add health checks:
```csharp
builder.Services.AddHealthChecks()
    .AddLiteDB("litedb", configure: db => 
        new LiteDBHealthCheck(db));

app.MapHealthChecks("/health");
```

---

### 3.4 Missing API Versioning

**Severity:** 🟡 Medium
**Files:** All API controllers

**Issue:** No API versioning strategy for future compatibility.

**Recommendation:** Add API versioning:
```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
});
```

---

## 4. Quick Wins

### 4.1 Add Missing `[ProducesResponseType]` Attributes

**Severity:** 🟢 Low
**Files:** All controllers

**Issue:** OpenAPI/Swagger documentation incomplete.

**Recommendation:** Add response type documentation:
```csharp
[ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> DataTable(...)
```

---

### 4.2 Use StringBuilder for Log Entry Construction

**Severity:** 🟢 Low
**File:** `Mercurius/Services/LoggerService.cs` (line 68-75)

**Issue:** Multiple string concatenations create intermediate strings.

**Recommendation:**
```csharp
var sb = new StringBuilder();
sb.AppendLine($"[{timestamp}] [{level}] {message}");
if (exception != null)
{
    sb.AppendLine($"Exception: {exception.GetType().Name}: {exception.Message}");
    // ...
}
var logEntry = sb.ToString();
```

---

### 4.3 Add Correlation IDs for Request Tracing

**Severity:** 🟢 Low
**Files:** `Program.cs`, `Middleware/`

**Recommendation:** Add correlation ID middleware:
```csharp
app.Use(async (context, next) =>
{
    context.Items["CorrelationId"] = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
        ?? Guid.NewGuid().ToString();
    using (LogContext.PushProperty("CorrelationId", context.Items["CorrelationId"]))
    {
        await next();
    }
});
```

---

## 5. Architectural Recommendations

### 5.1 ✅ Good Patterns Observed

The codebase demonstrates several excellent patterns:

| Pattern | Location | Description |
|---------|----------|-------------|
| **Batch Hydration** | `InvoicesController.cs` | Replaces N+1 queries with constant-time batch loading |
| **Server-Side Paging** | `ProductsController.cs`, `CustomersController.cs` | Proper DataTables integration with LiteDB |
| **Anti-Forgery Tokens** | All POST/PUT actions | CSRF protection on state-changing operations |
| **Authorization Policies** | `ModuleRegistry` | Fine-grained permission control per module |
| **Connection=direct** | `Program.cs` | Avoids LiteDB shared mode race conditions |
| **Transaction Support** | `SalesController.cs` | Proper transaction handling for multi-document operations |
| **Nullable Suppression** | `Program.cs` | `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` for legacy POCOs |

---

### 5.2 Recommended Architecture Improvements

#### 5.2.1 Add Result Pattern for Error Handling

**Current:** Exceptions used for flow control
**Recommended:** Use `OneOf` or `FluentResults`:
```csharp
public async Task<Result<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken ct)
{
    // ...
    return Result.Fail<InvoiceDto>("Invoice number already exists");
    // or
    return Result.Ok(invoiceDto);
}
```

---

#### 5.2.2 Consider CQRS for Complex Operations

**Current:** Controllers handle both queries and commands
**Recommended:** Separate query and command handlers for complex operations like invoice creation.

---

#### 5.2.3 Add Event Sourcing for Audit Trail

**Current:** `ZeroStockSaleAuditLog` tracks some events
**Recommended:** Implement event sourcing for complete audit trail of inventory movements.

---

### 5.3 Project Structure Assessment

```
Mercurius/
├── Mercurius.sln              ✅ Good - Single solution with clear projects
├── Mercurius/                 ✅ Good - Main web app
├── Mercurius.Common/          ✅ Good - Shared constants/utilities
├── Mercurius.Repo/            ✅ Good - Data access layer
└── Mercurius.Tests/           ✅ Good - Unit tests project
```

**Recommendation:** Consider adding:
- `Mercurius.Application/` - Business logic layer
- `Mercurius.Infrastructure/` - External integrations (email, file storage)

---

## 6. Security Assessment

### 6.1 ✅ Security Strengths

| Feature | Status | Notes |
|---------|--------|-------|
| Password Policy | ✅ Strong | Requires digit, lowercase, uppercase, special char, 8+ chars |
| Lockout Policy | ✅ Configured | 15 min lockout after 5 failed attempts |
| CSRF Protection | ✅ Enabled | `[ValidateAntiForgeryToken]` on all POSTs |
| Authorization | ✅ Policy-based | Fine-grained module permissions |
| HTTPS Enforcement | ⚠️ Check config | Verify `appsettings.Production.json` |

---

### 6.2 ⚠️ Security Concerns

| Issue | Severity | Recommendation |
|-------|----------|----------------|
| SMTP credentials in config | 🟠 Medium | Use environment variables or Azure Key Vault |
| No IP allowlist | 🟡 Low | Add IP restrictions for admin endpoints |
| No audit logging for auth events | 🟡 Low | Log failed login attempts, role changes |

---

## 7. Performance Assessment

### 7.1 ✅ Performance Strengths

| Feature | Status | Notes |
|---------|--------|-------|
| Batch Hydration | ✅ Excellent | InvoicesController eliminates N+1 |
| Server-Side Paging | ✅ Good | All DataTables use LiteDB native paging |
| Index Creation | ✅ Comprehensive | LiteDbContext creates indexes on startup |
| Connection=direct | ✅ Correct | Avoids shared mode overhead |

---

### 7.2 ⚠️ Performance Concerns

| Issue | Impact | Recommendation |
|-------|--------|----------------|
| File.AppendAllText | Medium | Use buffered async writer |
| No caching | Medium | Add response caching for read-heavy endpoints |
| No query result caching | Medium | Consider caching product categories |

---

## 8. Testing Coverage

**Current Status:** Minimal
- `Mercurius.Tests/NewSaleTests.cs` - Basic tests only

**Recommendations:**
1. Add integration tests for repository layer
2. Add controller tests with `WebApplicationFactory`
3. Add performance tests for batch operations
4. Add concurrency tests for LiteDB operations

---

## 9. Configuration Best Practices

### 9.1 Current Configuration

```json
// appsettings.json
{
  "ConnectionStrings": { "DefaultConnection": "" },  // ⚠️ Empty - LiteDB path in code
  "SeedAdmin": { "Email": "", "Password": "" },      // ⚠️ Should be env vars
  "Smtp": { /* credentials */ }                     // ⚠️ Should be env vars
}
```

### 9.2 Recommendations

1. **Move secrets to environment variables:**
   ```json
   "Smtp:Password": "${SMTP_PASSWORD}"
   ```

2. **Add configuration validation:**
   ```csharp
   builder.Services.AddOptions<SmptSettings>()
       .Bind(builder.Configuration.GetSection("Smtp"))
       .ValidateDataAnnotations();
   ```

3. **Add environment-specific settings:**
   - `appsettings.Development.json`
   - `appsettings.Staging.json`
   - `appsettings.Production.json`

---

## 10. Summary & Prioritized Action Items

### Immediate (This Week)
1. ⬛ Replace `SmtpClient` with MailKit
2. ⬛ Fix silent catch blocks in logging
3. ⬛ Add cancellation token propagation

### Short Term (This Month)
4. ⬛ Add rate limiting
5. ⬛ Implement async buffered file logging
6. ⬛ Add health checks
7. ⬛ Add API versioning

### Medium Term (This Quarter)
8. ⬛ Add Result pattern for error handling
9. ⬛ Implement request/response logging
10. ⬛ Add integration tests
11. ⬛ Move secrets to environment variables

### Long Term (Roadmap)
12. ⬛ Consider CQRS for complex domains
13. ⬛ Add event sourcing for audit trail
14. ⬛ Consider microservices split if needed

---

## Appendix: File Statistics

| Metric | Count |
|--------|-------|
| Total C# Files | ~150+ |
| Controllers | 20+ |
| Models | 44 |
| Services | 5+ |
| Test Files | 1 |

---

*Report generated as part of comprehensive architecture audit.*