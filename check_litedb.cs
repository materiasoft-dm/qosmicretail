using LiteDB;

var dbPath = @"c:\Users\markj\OneDrive - MSFT\Mercurius\Mercurius\veramay.litedb";
if (!File.Exists(dbPath))
{
    Console.WriteLine($"Database not found at: {dbPath}");
    return;
}

using var db = new LiteDatabase(dbPath);
var settings = db.GetCollection("AppSetting");

Console.WriteLine("All AppSetting records:");
foreach (var doc in settings.FindAll())
{
    Console.WriteLine($"  {doc["Key"]}: {doc["Value"]}");
}

// Check specifically for Shopify
var shopifyToken = settings.FindOne(Query.EQ("Key", "Shopify.AccessToken"));
Console.WriteLine($"\nShopify.AccessToken: {(shopifyToken != null ? shopifyToken["Value"] : "NOT FOUND")}");

var shopifyUrl = settings.FindOne(Query.EQ("Key", "Shopify.StoreUrl"));
Console.WriteLine($"Shopify.StoreUrl: {(shopifyUrl != null ? shopifyUrl["Value"] : "NOT FOUND")}");