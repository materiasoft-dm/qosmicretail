using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LiteDB;

namespace Mercurius.Repo.Services
{
    public class SqlScriptToLiteDbConverter
    {
        private readonly string[] _scriptPaths;
        private readonly ILiteDatabase _liteDb;
        private readonly bool _ownsDatabase;
        private SqlConversionResult? _currentResult;

        // Preferred ctor: share the singleton LiteDatabase from DI so the converter
        // doesn't open a second connection to the same file.
        public SqlScriptToLiteDbConverter(string[] scriptPaths, ILiteDatabase liteDb)
        {
            _scriptPaths = scriptPaths;
            _liteDb = liteDb;
            _ownsDatabase = false;
        }

        // Legacy ctor kept for callers that only have a path on hand (e.g. one-off
        // tooling). Opens its own connection; LiteDB shared-mode handles coexistence.
        public SqlScriptToLiteDbConverter(string[] scriptPaths, string liteDbPath)
        {
            _scriptPaths = scriptPaths;
            _liteDb = new LiteDatabase(liteDbPath);
            _ownsDatabase = true;
        }

        public SqlConversionResult Convert()
        {
            var result = new SqlConversionResult();
            _currentResult = result;
            try
            {
                return ConvertCore(result, _liteDb);
            }
            finally
            {
                _currentResult = null;
                if (_ownsDatabase && _liteDb is IDisposable d)
                {
                    d.Dispose();
                }
            }
        }

        private SqlConversionResult ConvertCore(SqlConversionResult result, ILiteDatabase liteDb)
        {

            foreach (var scriptPath in _scriptPaths)
            {
                if (!File.Exists(scriptPath))
                {
                    result.Warnings.Add($"Script not found: {scriptPath}");
                    continue;
                }

                result.Warnings.Add($"Processing: {Path.GetFileName(scriptPath)}");

                // Read file with UTF-8, UTF-16, or auto-detect encoding
                var content = ReadFileWithEncoding(scriptPath);

                // Split by GO to get batches, then find INSERT statements
                var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                var insertStatements = new List<string>();
                var currentInsert = "";

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(currentInsert))
                        {
                            insertStatements.Add(currentInsert);
                        }
                        currentInsert = trimmed;
                    }
                    else if (!string.IsNullOrEmpty(currentInsert))
                    {
                        currentInsert += " " + trimmed;

                        // End of INSERT when we find a line ending with ); or just the statement ends
                        if (trimmed.EndsWith(");") || trimmed.EndsWith(")"))
                        {
                            insertStatements.Add(currentInsert);
                            currentInsert = "";
                        }
                    }
                }

                // Add any remaining insert
                if (!string.IsNullOrEmpty(currentInsert))
                {
                    insertStatements.Add(currentInsert);
                }

                // Group by table name
                var tableGroups = insertStatements
                    .Select(s => new { Statement = s, Table = ExtractTableName(s) })
                    .Where(x => !string.IsNullOrEmpty(x.Table))
                    .GroupBy(x => x.Table);

                foreach (var group in tableGroups)
                {
                    var tableName = group.Key;
                    var collectionName = GetCollectionName(tableName);

                    try
                    {
                        var collection = liteDb.GetCollection<BsonDocument>(collectionName);

                        // Only seed an empty collection. Never wipe existing user data on startup.
                        if (collection.Count() > 0)
                        {
                            result.Warnings.Add($"Skipping {tableName}: collection already populated ({collection.Count()} docs)");
                            continue;
                        }

                        int count = 0;
                        foreach (var item in group)
                        {
                            var doc = ParseInsertStatement(item.Statement, tableName);
                            if (doc != null)
                            {
                                collection.Insert(doc);
                                count++;
                            }
                        }

                        result.AddResult($"{tableName} (from {Path.GetFileName(scriptPath)})", count);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{tableName}: {ex.Message}");
                    }
                }
            }

            return result;
        }

        private string ReadFileWithEncoding(string path)
        {
            // Try to detect encoding by reading the BOM
            var bytes = File.ReadAllBytes(path);
            
            if (bytes.Length >= 2)
            {
                // UTF-16 LE BOM
                if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                    return Encoding.Unicode.GetString(bytes);
                
                // UTF-16 BE BOM
                if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                    return Encoding.BigEndianUnicode.GetString(bytes);
                
                // UTF-8 BOM
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    return Encoding.UTF8.GetString(bytes);
            }

            // Check if it looks like UTF-16 (even-indexed bytes are null)
            if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0)
                return Encoding.Unicode.GetString(bytes);

            // Default to UTF-8
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private string ExtractTableName(string insert)
        {
            // Try INSERT INTO [dbo].[TableName]
            var match = Regex.Match(insert, @"INSERT\s+INTO\s+\[dbo\]\.\[(\w+)\]", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            // Try INSERT [dbo].[TableName]
            match = Regex.Match(insert, @"INSERT\s+\[dbo\]\.\[(\w+)\]", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            // Try INSERT INTO TableName (without schema)
            match = Regex.Match(insert, @"INSERT\s+INTO\s+(\w+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            return "";
        }

        private string GetCollectionName(string tableName)
        {
            return tableName switch
            {
                "LocationSetting" => "LocationSettings",
                "CustomerContactInformation" => "CustomerContactInformations",
                "BranchContactInformation" => "BranchContactInformations",
                "UserInformation" => "UserInformations",
                "UserCurrentLocation" => "UserCurrentLocations",
                "TransactionIdGenerator" => "TransactionIdGenerators",
                "RoleModuleAccess" => "RoleModuleAccess",
                "ItemMovement" => "ItemMovements",
                "ShipmentArrivalStatus" => "ShipmentArrivalStatus",
                "InvoiceStatus" => "InvoiceStatus",
                "ContactInformation" => "ContactInformations",
                _ => tableName
            };
        }

        private BsonDocument ParseInsertStatement(string insert, string tableName)
        {
            try
            {
                // Parse column names from INSERT INTO [dbo].[Table] (col1, col2, ...) VALUES
                var colMatch = Regex.Match(insert, @"\[dbo\]\.\[\w+\]\s*\((.+?)\)\s*VALUES\s*\(", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!colMatch.Success)
                {
                    // Try without [dbo] schema
                    colMatch = Regex.Match(insert, @"INTO\s+\w+\s*\((.+?)\)\s*VALUES\s*\(", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                }
                
                if (!colMatch.Success)
                    return null;

                var columns = colMatch.Groups[1].Value
                    .Split(',')
                    .Select(c => c.Trim().Trim('[', ']', ' '))
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();

                // Parse values
                var valuesMatch = Regex.Match(insert, @"VALUES\s*\((.+)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!valuesMatch.Success)
                    return null;

                var values = ParseValues(valuesMatch.Groups[1].Value);

                if (values.Count == 0 || columns.Count == 0)
                    return null;

                var doc = new BsonDocument();

                for (int i = 0; i < Math.Min(columns.Count, values.Count); i++)
                {
                    var colName = columns[i];
                    var value = values[i];

                    if (string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip column if we couldn't parse a value
                    if (string.IsNullOrEmpty(value))
                        continue;

                    try
                    {
                        doc[colName] = ParseValue(value, colName);
                    }
                    catch (Exception ex)
                    {
                        // Record skips on the result so callers can see how much data we lost
                        // instead of silently moving on. ParseValue can fail on malformed
                        // numerics, dates the converter doesn't understand, etc.
                        _currentResult?.Warnings.Add(
                            $"Skipped column '{colName}' in {tableName} (value: {Truncate(value, 60)}): {ex.Message}");
                    }
                }

                return doc;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse INSERT for {tableName}: {ex.Message}");
            }
        }

        private static string Truncate(string value, int max)
        {
            return value.Length <= max ? value : value.Substring(0, max) + "…";
        }

        private List<string> ParseValues(string valuesStr)
        {
            var values = new List<string>();
            var current = "";
            var inString = false;
            var inParen = 0;

            for (int i = 0; i < valuesStr.Length; i++)
            {
                var c = valuesStr[i];

                if (c == '(')
                {
                    inParen++;
                    current += c;
                }
                else if (c == ')')
                {
                    inParen--;
                    current += c;
                }
                else if (c == '\'' && (i == 0 || valuesStr[i - 1] != '\\'))
                {
                    inString = !inString;
                    current += c;
                }
                else if (c == ',' && !inString && inParen == 0)
                {
                    values.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrEmpty(current))
                values.Add(current.Trim());

            return values;
        }

        private BsonValue ParseValue(string value, string columnName)
        {
            value = value.Trim();

            if (string.IsNullOrEmpty(value) || string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
                return BsonValue.Null;

            // Remove N prefix for Unicode strings: N'text' -> 'text'
            if (value.StartsWith("N'", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(1);

            // String value: 'text'
            if (value.StartsWith("'") && value.EndsWith("'"))
            {
                var str = value.Substring(1, value.Length - 2);
                // Unescape single quotes
                str = str.Replace("''", "'");
                return new BsonValue(str);
            }

            // CAST(N'2023-05-17T10:11:52.000' AS DateTime)
            var dateMatch = Regex.Match(value, @"CAST\(N?'(.+?)'\s+AS\s+DateTime\)", RegexOptions.IgnoreCase);
            if (dateMatch.Success)
            {
                if (DateTime.TryParse(dateMatch.Groups[1].Value, out var dt))
                    return new BsonValue(dt);
            }

            // CAST(N'2023-05-17' AS Date)
            dateMatch = Regex.Match(value, @"CAST\(N?'(.+?)'\s+AS\s+Date\)", RegexOptions.IgnoreCase);
            if (dateMatch.Success)
            {
                if (DateTime.TryParse(dateMatch.Groups[1].Value, out var dt))
                    return new BsonValue(dt);
            }

            // Boolean: 0 or 1
            if (value == "0" || value == "1")
                return new BsonValue(value == "1");

            // Integer
            if (int.TryParse(value, out var intValue))
                return new BsonValue(intValue);

            // Long
            if (long.TryParse(value, out var longValue))
                return new BsonValue(longValue);

            // Decimal
            if (decimal.TryParse(value, out var decValue))
                return new BsonValue(decValue);

            // Double
            if (double.TryParse(value, out var doubleValue))
                return new BsonValue(doubleValue);

            // GUID
            if (Guid.TryParse(value, out var guid))
                return new BsonValue(guid.ToString());

            // Default to string
            return new BsonValue(value);
        }
    }

    public class SqlConversionResult
    {
        public Dictionary<string, int> Results { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddResult(string name, int count)
        {
            Results[name] = count;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== SQL to LiteDB Conversion Results ===");
            var total = Results.Values.Sum();
            foreach (var kvp in Results)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value} records");
            }
            sb.AppendLine($"  TOTAL: {total} records");
            if (Warnings.Any())
            {
                sb.AppendLine("\nWarnings:");
                foreach (var w in Warnings)
                    sb.AppendLine($"  - {w}");
            }
            if (Errors.Any())
            {
                sb.AppendLine("\nErrors:");
                foreach (var e in Errors)
                    sb.AppendLine($"  - {e}");
            }
            return sb.ToString();
        }
    }
}
