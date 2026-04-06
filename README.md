# Universal DB3 Converter 🚀

A lightweight C# console utility to convert **XML** and **TSV** data files into **SQLite (DB3)** databases. No predefined schema required—the tool generates tables and columns on the fly based on your data structure.

## ✨ Key Features
- **Dynamic Schema Generation:** Automatically creates SQLite tables from XML tags or TSV headers.
- **XML Flattening:** Handles nested XML structures by flattening them into a relational format (e.g., `Parent_Child`).
- **Interactive CLI:** Simple step-by-step terminal interface with a real-time progress bar.
- **Safe Input:** Automatically cleans file paths and handles special characters in data.

## 🛠️ Tech Stack
- **Language:** C# (.NET 10.0)
- **Database:** [System.Data.SQLite](https://www.sqlite.org/)
- **ORM:** [Dapper](https://github.com/DapperLib/Dapper)
- **XML Parsing:** System.Xml.Linq (XDocument)

## 📦 Dependencies
This project uses the following NuGet packages:
- **[Dapper](https://www.nuget.org/packages/dapper.contrib/)** (v2.1.35+)
- **[System.Data.SQLite](https://www.nuget.org/packages/system.data.sqlite.core/)** (v1.0.118+) - SQLite database engine with the ADO.NET provider.

## How to Use
1. **Select Format:** Launch the application and choose 1 for **XML** or 2 for **TSV**.
2. **Source Path:** Enter the full path to your source file (e.g., `C:\data\input.xml`).
3. **Output Path:** Enter the destination path for the `.db3` file (include the extension).
4. **TSV Options:** If using TSV, specify if the file contains a header row (y/n).
5. **Process:** The tool will display a progress bar. Once finished, the database is ready!
> Note: You have to type the suffix

### Example Transformation (XML)
**Input:**
```xml
<entry>
  <word>Apple</word>
  <category>Fruit</category>
</entry>
```
### Output (SQLite Table 'data'):
| Id | word | category |
|----|------|----------|
| 1  | Apple| Fruit    |



## 📥 Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/
   cd XML, TSV-Converter