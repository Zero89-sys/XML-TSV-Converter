using Dapper;
using System.Data.SQLite;
using System.Xml.Linq;

namespace XML_TSV_to_db3
{
    /// <summary>
    /// Utility for universal data format conversion to SQLite database.
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            int format;
            try
            {
                Console.WriteLine("======= DB3 Converter =======");
                Console.WriteLine("Choose format (1 - XML, 2 - TSV):");

                // User input validation
                while (!int.TryParse(Console.ReadLine(), out format) || (format != 1 && format != 2))
                {
                    Console.WriteLine("Invalid input. Use 1 or 2.");
                }

                // Retrieving paths and removing quotes
                Console.WriteLine("Source file path:");
                string sourcePath = Console.ReadLine()?.Trim().Replace("\"", "") ?? "";

                Console.WriteLine("Output DB3 path:");
                string dbPath = Console.ReadLine()?.Trim().Replace("\"", "") ?? "";

                // Check if the file exists
                if (!File.Exists(sourcePath))
                {
                    Console.WriteLine("Source file not found.");
                    return;
                }

                // Preparing a clean database
                if (File.Exists(dbPath)) File.Delete(dbPath);

                // XML logic
                if (format == 1)
                {
                    ConvertXmlToDb(sourcePath, dbPath);
                }
                // TSV logic
                else
                {
                    ConvertTSVtoDb(sourcePath, dbPath);
                }

                Console.WriteLine("\nProcessing finished successfully.");
            }
            // Checking for errors
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        // Replace the allColumns collection with this:
        static IEnumerable<(string Key, string Value)> FlattenElement(XElement el, string prefix = "")
        {
            string name = string.IsNullOrEmpty(prefix) ? el.Name.LocalName : $"{prefix}_{el.Name.LocalName}";
            var children = el.Elements().ToList();

            if (children.Count == 0)
            {
                // Leaf node — return its text value
                yield return (name, el.Value);
            }
            else
            {
                // Has children — recurse
                foreach (var child in children)
                    foreach (var pair in FlattenElement(child, name)) // Recursively flattens nested XML elements into a flat key-value structure.
                        yield return pair;
            }
        }

        /// <summary>
        /// Parses XML and dynamically creates a SQLite table based on discovered tags.
        /// </summary>
        static void ConvertXmlToDb(string xmlPath, string dbPath)
        {
            static string SafeParam(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"[^\w]", "_");

            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();

            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;

            if (root == null)
            {
                Console.WriteLine("Empty or invalid XML.");
                return;
            }

            // Entry elements = direct children of root
            var entries = root.Elements().ToList();
            if (entries.Count == 0)
            {
                Console.WriteLine("No records found.");
                return;
            }

            // Collect all unique flattened column names
            var allColumns = entries
                .SelectMany(e => e.Elements().SelectMany(el => FlattenElement(el)))
                .Select(p => p.Key)
                .Distinct()
                .ToList();

            using var transaction = conn.BeginTransaction();

            // Create table
            string cols = string.Join(", ", allColumns.Select(c => $"[{c}] TEXT"));
            conn.Execute($"CREATE TABLE data (Id INTEGER PRIMARY KEY AUTOINCREMENT, {cols})", transaction: transaction);
            // Insert rows
            int count = 0;
            foreach (var entry in entries)
            {
                // Flatten all fields for this entry, join duplicates with 、
                var flat = entry.Elements()
                                .SelectMany(el => FlattenElement(el))
                                .GroupBy(p => p.Key)
                                .ToDictionary(g => g.Key, g => (object)string.Join("、", g.Select(p => p.Value)));

                // Fill missing columns with empty string
                var paramDict = allColumns.ToDictionary(
                    col => col,
                    col => flat.ContainsKey(col) ? flat[col] : (object)""
                );

                string colNames = string.Join(", ", paramDict.Keys.Select(k => $"[{k}]"));
                string paramNames = string.Join(", ", paramDict.Keys.Select(k => "@" + SafeParam(k)));
                var safeDict = paramDict.ToDictionary(kv => SafeParam(kv.Key), kv => kv.Value);

                conn.Execute($"INSERT INTO data ({colNames}) VALUES ({paramNames})", safeDict, transaction: transaction);

                count++;
                if (count % 500 == 0) ProgressBar(count, entries.Count);
            }

            transaction.Commit();
            ProgressBar(1, 1);
            Console.WriteLine($"\nDone! Imported {count} records.");
        }

        /// <summary>
        /// Imports TSV data.The first row is automatically used for column names.
        /// </summary>
        static void ConvertTSVtoDb(string tsvPath, string dbPath)
        {
            static string SafeParam(string name) =>
                System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"[^\w]", "_");

            // Ask BEFORE the loop
            Console.WriteLine("Does the TSV file have a header row? (y/n):");
            bool hasHeader = Console.ReadLine()?.Trim().ToLower() == "y";

            var lines = File.ReadLines(tsvPath, System.Text.Encoding.UTF8)
                            .Select(l => l.TrimEnd('\r'));
            string[]? header = null;

            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();

            int current = 0;
            long totalLines = File.ReadLines(tsvPath).LongCount();

            using var transaction = conn.BeginTransaction();

            foreach (var line in lines)
            {
                if (header == null)
                {
                    var firstRow = line.Split('\t');
                    firstRow[0] = firstRow[0].TrimStart('\uFEFF');

                    if (hasHeader)
                    {
                        header = firstRow;
                        string createCols = string.Join(", ", header.Select(h => $"[{h.Trim()}] TEXT"));
                        conn.Execute($"CREATE TABLE data (Id INTEGER PRIMARY KEY AUTOINCREMENT, {createCols})", transaction: transaction);
                        continue; // skip inserting header row as data
                    }
                    else
                    {
                        header = Enumerable.Range(1, firstRow.Length)
                                           .Select(i => $"Col{i}")
                                           .ToArray();

                        string createCols = string.Join(", ", header.Select(h => $"[{h}] TEXT"));
                        conn.Execute($"CREATE TABLE data (Id INTEGER PRIMARY KEY AUTOINCREMENT, {createCols})", transaction: transaction);
                    }
                }

                // Single insert block used for all data rows (including first row if no header)
                var values = line.Split('\t');
                var safeParamDict = new Dictionary<string, object>();
                var colNames = new List<string>();
                var paramNames = new List<string>();

                for (int i = 0; i < Math.Min(header.Length, values.Length); i++)
                {
                    string original = header[i].Trim();
                    string safe = SafeParam(original);
                    colNames.Add($"[{original}]");
                    paramNames.Add("@" + safe);
                    safeParamDict[safe] = values[i];
                }

                string sql = $"INSERT INTO data ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)})";
                conn.Execute(sql, safeParamDict, transaction: transaction);

                current++;
                if (current % 100 == 0) ProgressBar(current, (int)totalLines);
            }

            transaction.Commit();
            ProgressBar(1, 1);
        }

        /// <summary>
        /// Draw a simple ASCII progress bar to the current console line.
        /// </summary>
        static void ProgressBar(long current, long total, int width = 30)
        {
            if (total == 0) return;
            double ratio = (double)current / total;
            int progressWidth = (int)(ratio * width);
            Console.Write($"\r[{new string('█', progressWidth)}{new string('░', width - progressWidth)}] {Math.Round(ratio * 100)}%");
        }
    }
}