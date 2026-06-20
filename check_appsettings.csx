using LiteDB;
using System;
using System.Linq;

// Check both databases for AppSetting data
var dbPaths = new[] {
    @"c:\Users\markj\OneDrive - MSFT\Mercurius\Mercurius\Mercurius\mercurius.litedb",
    @"c:\Users\markj\OneDrive - MSFT\Mercurius\Mercurius\Mercurius\veramay.litedb"
};

foreach (var dbPath in dbPaths)
{
    Console.WriteLine($"\n=== Checking {System.IO.Path.GetFileName(dbPath)} ===");
    
    using (var db = new LiteDatabase(dbPath))
    {
        // List all collections
        Console.WriteLine("Collections: " + string.Join(", ", db.GetCollectionNames()));
        
        // Check AppSetting collection
        var appSettings = db.GetCollection("AppSetting");
        Console.WriteLine($"\nAppSetting count: {appSettings.Count()}");
        
        if (appSettings.Count() > 0)
        {
            foreach (var setting in appSettings.FindAll())
            {
                Console.WriteLine($"  Key: {setting["Key"]}, Value: {setting["Value"]}");
            }
        }
        
        // Also check AppSettings (with 's')
        var appSettings2 = db.GetCollection("AppSettings");
        Console.WriteLine($"\nAppSettings count: {appSettings2.Count()}");
        
        if (appSettings2.Count() > 0)
        {
            foreach (var setting in appSettings2.FindAll())
            {
                Console.WriteLine($"  Key: {setting["Key"]}, Value: {setting["Value"]}");
            }
        }
    }
}

Console.WriteLine("\nDone!");