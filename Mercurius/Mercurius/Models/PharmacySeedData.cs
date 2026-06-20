using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.Extensions.Logging;

namespace Mercurius.Models;

/// <summary>
/// Seeds pharmacy-specific reference data: product categories with
/// custom field definitions, and dosage forms.
/// Called during app startup from SeedDataAsync.
/// </summary>
public static class PharmacySeedData
{
    public static async Task SeedAsync(IUnitOfWork unitOfWork, ILogger logger)
    {
        await SeedProductCategoriesAsync(unitOfWork, logger);
        await SeedCategoryFieldsAsync(unitOfWork, logger);
        await SeedDosageFormsAsync(unitOfWork, logger);
    }

    private static async Task SeedProductCategoriesAsync(IUnitOfWork unitOfWork, ILogger logger)
    {
        var repo = unitOfWork.Repository<ProductCategory>();
        var existing = await repo.GetAllAsync();
        if (existing.Any(c => c.Name == "Prescription Drugs")) return; // already seeded

        var categories = new[]
        {
            new ProductCategory { Name = "Prescription Drugs", Description = "Rx medicines requiring a doctor's prescription", IsActive = true },
            new ProductCategory { Name = "OTC Medicines", Description = "Over-the-counter drugs, no prescription needed", IsActive = true },
            new ProductCategory { Name = "Vitamins & Supplements", Description = "Dietary supplements, vitamins, minerals", IsActive = true },
            new ProductCategory { Name = "Personal Care", Description = "Soap, shampoo, lotion, deodorant, oral care", IsActive = true },
            new ProductCategory { Name = "Baby Care", Description = "Diapers, formula, baby wipes, feeding accessories", IsActive = true },
            new ProductCategory { Name = "Medical Supplies", Description = "Bandages, syringes, gloves, masks, cotton", IsActive = true },
            new ProductCategory { Name = "First Aid", Description = "First aid kits, antiseptics, wound care", IsActive = true },
            new ProductCategory { Name = "Health Devices", Description = "Thermometers, BP monitors, glucometers, nebulizers", IsActive = true },
            new ProductCategory { Name = "Family Planning", Description = "Contraceptives, pregnancy tests, fertility products", IsActive = true },
        };

        await repo.AddRangeAsync(categories);
        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} pharmacy product categories", categories.Length);
    }

    private static async Task SeedCategoryFieldsAsync(IUnitOfWork unitOfWork, ILogger logger)
    {
        var repo = unitOfWork.Repository<CategoryField>();
        var existing = await repo.GetAllAsync();
        if (existing.Any()) return;

        // Fetch categories by name
        var categories = await unitOfWork.Repository<ProductCategory>().GetAllAsync();
        var catDict = categories.ToDictionary(c => c.Name, c => c.Id);

        int GetId(string name) => catDict.TryGetValue(name, out var id) ? id : 0;
        int order = 0;

        var fields = new List<CategoryField>();

        void Add(string cat, string name, string label, string type, string? options = null, bool required = false)
        {
            fields.Add(new CategoryField
            {
                CategoryId = GetId(cat),
                FieldName = name,
                DisplayLabel = label,
                FieldType = type,
                Options = options,
                SortOrder = order++,
                IsRequired = required
            });
        }

        // --- Prescription Drugs ---
        Add("Prescription Drugs", "GenericName", "Generic Name", "text", required: true);
        Add("Prescription Drugs", "Strength", "Strength (e.g. 500mg, 10mg/mL)", "text", required: true);
        Add("Prescription Drugs", "DosageForm", "Dosage Form", "select",
            "[\"Tablet\",\"Capsule\",\"Softgel\",\"Syrup\",\"Suspension\",\"Injection\",\"Ointment\",\"Cream\",\"Drops\",\"Inhaler\",\"Suppository\",\"Powder\",\"Patch\"]", required: true);
        Add("Prescription Drugs", "RegulatoryClass", "Regulatory Class", "select",
            "[\"Rx\",\"S2 (Controlled)\",\"S3 (Highly Controlled)\"]", required: true);
        Add("Prescription Drugs", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Tablet\",\"Capsule\",\"mL\",\"mg\",\"g\",\"Vial\",\"Ampule\",\"Piece\"]", required: true);
        Add("Prescription Drugs", "ExpiryDate", "Expiry Date (if single batch)", "date");
        Add("Prescription Drugs", "StorageRequirement", "Storage", "select",
            "[\"Room Temperature\",\"Refrigerated (2-8\\u00b0C)\",\"Freezer (-20\\u00b0C)\",\"Protect from Light\"]");

        // --- OTC Medicines ---
        Add("OTC Medicines", "GenericName", "Generic Name", "text", required: true);
        Add("OTC Medicines", "Strength", "Strength", "text", required: true);
        Add("OTC Medicines", "DosageForm", "Dosage Form", "select",
            "[\"Tablet\",\"Capsule\",\"Syrup\",\"Cream\",\"Drops\",\"Spray\",\"Lozenge\",\"Powder\"]", required: true);
        Add("OTC Medicines", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Tablet\",\"Capsule\",\"mL\",\"mg\",\"g\",\"Piece\"]", required: true);
        Add("OTC Medicines", "AgeRestriction", "Minimum Age", "select",
            "[\"None\",\"2+\",\"6+\",\"12+\",\"18+\"]");

        // --- Vitamins & Supplements ---
        Add("Vitamins & Supplements", "Form", "Form", "select",
            "[\"Tablet\",\"Softgel\",\"Capsule\",\"Powder\",\"Liquid\",\"Gummy\",\"Chewable\"]", required: true);
        Add("Vitamins & Supplements", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Tablet\",\"Capsule\",\"mL\",\"mg\",\"g\",\"Piece\"]", required: true);
        Add("Vitamins & Supplements", "ServingSize", "Serving Size", "text");
        Add("Vitamins & Supplements", "ServingsPerContainer", "Servings Per Container", "number");

        // --- Personal Care ---
        Add("Personal Care", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"mL\",\"g\",\"Piece\",\"Pack\",\"Set\"]", required: true);
        Add("Personal Care", "Volume", "Volume / Size", "text");
        Add("Personal Care", "SkinType", "Skin Type", "select",
            "[\"All Skin Types\",\"Normal\",\"Dry\",\"Oily\",\"Combination\",\"Sensitive\"]");
        Add("Personal Care", "Scent", "Scent / Fragrance", "text");

        // --- Baby Care ---
        Add("Baby Care", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Piece\",\"Pack\",\"Box\",\"Set\"]", required: true);
        Add("Baby Care", "AgeRange", "Age Range", "select",
            "[\"0-3 months\",\"3-6 months\",\"6-12 months\",\"12+ months\",\"All Ages\"]", required: true);
        Add("Baby Care", "Material", "Material", "text");
        Add("Baby Care", "Hypoallergenic", "Hypoallergenic", "checkbox");

        // --- Medical Supplies ---
        Add("Medical Supplies", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Piece\",\"Pack\",\"Box\",\"Pair\",\"Roll\"]", required: true);
        Add("Medical Supplies", "Sterile", "Sterile", "checkbox");
        Add("Medical Supplies", "Disposable", "Single Use / Disposable", "checkbox");
        Add("Medical Supplies", "Size", "Size / Dimensions", "text");
        Add("Medical Supplies", "Material", "Material", "text");

        // --- First Aid ---
        Add("First Aid", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Piece\",\"Kit\",\"Pack\",\"Box\"]", required: true);
        Add("First Aid", "Contents", "Package Contents", "textarea");
        Add("First Aid", "PieceCount", "Number of Pieces", "number");

        // --- Health Devices ---
        Add("Health Devices", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Piece\",\"Set\",\"Kit\"]", required: true);
        Add("Health Devices", "PowerSource", "Power Source", "select",
            "[\"Battery\",\"Electric (Plug-in)\",\"Manual\",\"USB Rechargeable\"]");
        Add("Health Devices", "BatteryType", "Battery Type", "text");
        Add("Health Devices", "WarrantyMonths", "Warranty (months)", "number");
        Add("Health Devices", "FDARegistration", "FDA Registration No.", "text");

        // --- Family Planning ---
        Add("Family Planning", "UnitOfMeasure", "Unit of Measure", "select",
            "[\"Piece\",\"Pack\",\"Box\",\"Kit\"]", required: true);
        Add("Family Planning", "Type", "Type", "select",
            "[\"Oral Contraceptive\",\"Injectable\",\"Implant\",\"IUD\",\"Barrier\",\"Emergency\"]");
        Add("Family Planning", "PackSize", "Pack Size", "text");

        await repo.AddRangeAsync(fields);
        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} category custom fields across 9 categories", fields.Count);
    }

    private static async Task SeedDosageFormsAsync(IUnitOfWork unitOfWork, ILogger logger)
    {
        var repo = unitOfWork.Repository<DosageForm>();
        var existing = await repo.GetAllAsync();
        if (existing.Any()) return;

        var forms = new[]
        {
            new DosageForm { Name = "Tablet", Description = "Compressed solid dosage form" },
            new DosageForm { Name = "Capsule", Description = "Gelatin shell containing powder or liquid" },
            new DosageForm { Name = "Softgel", Description = "Soft gelatin capsule, typically for oils" },
            new DosageForm { Name = "Syrup", Description = "Liquid oral preparation with sweetener" },
            new DosageForm { Name = "Suspension", Description = "Liquid with suspended particles — shake before use" },
            new DosageForm { Name = "Injection", Description = "Sterile solution for parenteral administration" },
            new DosageForm { Name = "Ointment", Description = "Semi-solid topical preparation" },
            new DosageForm { Name = "Cream", Description = "Emulsion-based topical preparation" },
            new DosageForm { Name = "Drops", Description = "Eye, ear, or nasal drop solution" },
            new DosageForm { Name = "Inhaler", Description = "Metered-dose or dry powder inhaler" },
            new DosageForm { Name = "Suppository", Description = "Solid dosage form for rectal/vaginal use" },
            new DosageForm { Name = "Powder", Description = "Dry powder for reconstitution or direct use" },
            new DosageForm { Name = "Lozenge", Description = "Solid form designed to dissolve slowly in mouth" },
            new DosageForm { Name = "Spray", Description = "Nasal or topical spray" },
            new DosageForm { Name = "Patch", Description = "Transdermal delivery system" },
        };

        await repo.AddRangeAsync(forms);
        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} dosage forms", forms.Length);
    }
}
