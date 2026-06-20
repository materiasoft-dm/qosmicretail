// Check mercurius.litedb
using var db1 = new LiteDB.LiteDatabase(@"c:\Users\markj\OneDrive - MSFT\Mercurius\Mercurius\Mercurius\mercurius.litedb");
var settings1 = db1.GetCollection("AppSetting");
Console.WriteLine("=== mercurius.litedb - AppSetting ===");
Console.WriteLine($"Count: {settings1.Count()}");
foreach (var doc in settings1.FindAll())
{
    Console.WriteLine($"  {doc["Key"]}: {doc["Value"]}");
}

// Check veramay.litedb
using var db2 = new LiteDB.LiteDatabase(@"c:\Users\markj\OneDrive - MSFT\Mercurius\Mercurius\Mercurius\veramay.litedb");
var settings2 = db2.GetCollection("AppSetting");
Console.WriteLine("\n=== veramay.litedb - AppSetting ===");
Console.WriteLine($"Count: {settings2.Count()}");
foreach (var doc in settings2.FindAll())
{
    Console.WriteLine($"  {doc["Key"]}: {doc["Value"]}");
}