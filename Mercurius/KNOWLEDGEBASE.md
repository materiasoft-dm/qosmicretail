# Mercurius Pharmacy — Knowledgebase

**Version:** 1.0  
**Date:** 2026-05-18  
**Base:** Mercurius Inventory & Sales Management System (ASP.NET Core 9.0, LiteDB, MVC)  
**Target:** Community/Retail Pharmacy Management System

---

## 1. Architecture Overview

```
Mercurius.sln
├── Mercurius/              ← ASP.NET Core MVC web host
├── Mercurius.Repo/          ← Data access (LiteDB via IRepository<T> + IUnitOfWork)
├── Mercurius.Common/        ← Shared types, constants, ModuleRegistry
└── Mercurius.Tests/         ← xUnit (26 tests, all passing)
```

**Key design decisions:**

- **LiteDB** is the sole database — file-based, no SQL Server dependency
- **Generic repository pattern** — `IRepository<T>` + `LiteDbRepository<T>` handles all CRUD
- **Claims-based authorization** — per-page policies in `ModuleRegistry`
- **Metronic v8** theme with jQuery DataTables for UI

---

## 2. Standard vs Custom Fields

### Philosophy

Product has two kinds of data:

| Type | Storage | Examples |
|------|---------|----------|
| **Standard** | Columns on `Product` table | Name, PartCode, Price, Stock, CategoryId |
| **Custom** | `CategoryField` + `ProductField` tables | Strength, DosageForm, UnitOfMeasure, Storage, SkinType, AgeRange |

**Standard fields** are universal — every product has a name, price, and stock count. These live as direct columns.

**Custom fields** vary by category. A "Prescription Drug" needs Strength and Regulatory Class. A "Shampoo" needs Volume and Skin Type. These are defined in `CategoryField` and stored as rows in `ProductField`.

### Why normalized tables, not JSON

| Concern | JSON approach | Normalized approach |
|---------|--------------|-------------------|
| Query "all refrigerated items" | JSON path query, fragile | `FindAsync(pf => pf.CategoryFieldId == storageId && pf.Value == "2-8C")` |
| Audit trail | No per-field history | Add `UpdatedDate` to `ProductField` |
| Validation | Manual string parsing | Parse based on `CategoryField.FieldType` |
| Report generation | Unwieldy | JOIN `Product` ← `ProductField` ← `CategoryField` |

---

## 3. Entity Reference

### 3.1 Product (core)

```csharp
public class Product
{
    int Id                     // PK
    string Name                // Display name
    string PartCode            // SKU / barcode
    string Description
    int? ProductCategoryId     // FK → ProductCategory
    decimal? CurrentCostPrice
    decimal? CurrentSalePrice
    int CurrentStock           // Computed from MedicineBatch totals for medicines
    int ReorderLevel
    bool IsActive
    // ... created/updated tracking ...
    ICollection<MedicineBatch> MedicineBatches  // Batch tracking for medicines
}
```

Product is **unchanged from the original Mercurius** except for the `MedicineBatches` navigation. All pharmacy-specific data goes through `CategoryField` → `ProductField`.

### 3.2 ProductCategory

```
Id, Name, Description, IsActive
```

Seeded with 9 pharmacy categories: Prescription Drugs, OTC Medicines, Vitamins & Supplements, Personal Care, Baby Care, Medical Supplies, First Aid, Health Devices, Family Planning.

### 3.3 CategoryField

Defines *what* fields exist for a category.

```csharp
public class CategoryField
{
    int Id                     // PK
    int CategoryId             // FK → ProductCategory
    string FieldName           // Machine key, e.g. "Strength", "UnitOfMeasure"
    string DisplayLabel        // Human label, e.g. "Strength (mg/mL)"
    string FieldType           // "text", "number", "select", "checkbox", "date", "textarea"
    string? Options            // JSON array for select: ["Option1","Option2"]
    int SortOrder              // Display order
    bool IsRequired            // Validation
}
```

### 3.4 ProductField

Stores *actual values* for a product.

```csharp
public class ProductField
{
    int Id                     // PK
    int ProductId              // FK → Product
    int CategoryFieldId        // FK → CategoryField
    string Value               // Always stored as string; cast based on FieldType
}
```

### 3.5 MedicineBatch

Tracks individual batches/lots of medicine. Enables FEFO (First-Expiry-First-Out) dispensing.

```csharp
public class MedicineBatch
{
    int Id                     // PK
    int ProductId              // FK → Product
    string BatchNumber         // Manufacturer's batch/lot number
    DateTime ExpiryDate        // Used for FEFO and expiry alerts
    DateTime ReceivedDate      // When this batch entered inventory
    decimal UnitCost           // Cost per unit at receipt
    decimal InitialQuantity    // Quantity received
    decimal RemainingQuantity  // Quantity still available
    int? StockReceiptId        // FK → ShipmentArrival (for traceability)
    bool IsActive              // False when fully dispensed or expired
}
```

### 3.6 DosageForm (reference lookup)

```
Tablet, Capsule, Softgel, Syrup, Suspension, Injection, Ointment, Cream,
Drops, Inhaler, Suppository, Powder, Lozenge, Spray, Patch
```

Used as options in the DosageForm select field for medicine categories. Not a direct FK on Product.

---

## 4. Data Flow — Product Create/Edit

### 4.1 Form Load (Create)

1. Page loads with category dropdown (from `ProductCategory`)
2. User selects a category
3. JavaScript calls `GET /Products/GetCategoryFields?categoryId=5`
4. Server returns field definitions
5. JavaScript dynamically renders input elements inside `#custom-fields-container`
6. Input names use prefix `cf_`: `<input name="cf_Strength" />`

### 4.2 Form Load (Edit)

1. Same as Create but with `?productId=10`
2. Server returns field definitions + existing values
3. Form pre-fills existing values

### 4.3 Form Submit

1. Standard Product fields bind to `Product` model
2. Controller saves the product
3. `SaveCustomFieldsAsync(product.Id, Request.Form)` is called:
   - Reads all `cf_*` keys from form data
   - Deletes existing `ProductField` rows for this product
   - Inserts new rows where value is not empty

---

## 5. API Endpoints

### GET /Products/GetCategoryFields

```
Query: ?categoryId=5[&productId=10]
Returns: [{id, fieldName, displayLabel, fieldType, options, isRequired, value}]
```

---

## 6. Indexes (LiteDB)

| Entity | Indexed On | Reason |
|--------|-----------|--------|
| Product | PartCode, Name, IsActive, CurrentStock | Product queries |
| Invoice | InvoiceNumber (unique), CustomerId, StatusId, InvoiceDate | Invoice lookup |
| Customer | FirstName, LastName | Customer search |
| InvoiceItem | ProductId, InvoiceId, StatusId | Invoice line items |
| ItemMovement | TransactionId (unique) | Dedup |
| MedicineBatch | ProductId, ExpiryDate, BatchNumber | FEFO dispensing, expiry queries |
| CategoryField | CategoryId | Fetching fields per category |
| ProductField | ProductId, CategoryFieldId | Product-specific field values |
| DosageForm | Name | Lookup |

---

## 7. Build & Test

```bash
dotnet build Mercurius.sln          # 0 errors, 0 warnings
dotnet test Mercurius.Tests         # 26 passed, 0 failed
dotnet run --project Mercurius      # http://localhost:5094
```

Login: `admin@mercurius.com` / `Admin@123` (configurable: `appsettings.Development.json` → `SeedAdmin`)
