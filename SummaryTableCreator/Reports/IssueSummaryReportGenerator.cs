// File: Reports/IssueSummaryReportGenerator.cs
using System.Reflection;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SummaryTableCreator.Models;
using SummaryTableCreator.UI;

namespace SummaryTableCreator.Reports
{
    /// <summary>
    /// Generates a summary report showing issue counts by phase and severity.
    /// </summary>
    public static class IssueSummaryReportGenerator
    {
        // Template resource name
        private const string TEMPLATE_RESOURCE_NAME = "SummaryTableCreator.Resources.IssueSummaryTableTemplate.docx";

        // Font constants
        private const string FONT_NAME = "Calibri Light";
        private const int FONT_SIZE = 12;

        /// <summary>
        /// Creates a new Word document containing an issue summary table using an embedded template.
        /// </summary>
        /// <param name="originalPath">Path to the imported document (used to determine output location).</param>
        /// <param name="results">Parsed heading results.</param>
        /// <param name="useSingleLevelCvss">When true, removes the Critical column and uses simplified rating.</param>
        public static void Generate(string originalPath, IEnumerable<HeadingResult> results, bool useSingleLevelCvss = false)
        {
            // Determine output path
            var folder = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(originalPath);
            var outputPath = Path.Combine(folder, "IssueSummaryTable.docx");

            //Logger.Info($"[+] Creating issue summary report: {outputPath}");
            Logger.Info($"[+] Using {(useSingleLevelCvss ? "CHECK" : "Non-CHECK")} CVSS rating system");

            // Extract template from embedded resources and save to output location
            ExtractTemplateToFile(outputPath);

            // Open the template and populate it with data
            using (var doc = WordprocessingDocument.Open(outputPath, true))
            {
                PopulateTemplate(doc, results, useSingleLevelCvss);
            }

            Logger.Okay("[+] Issue summary report generation complete");
        }

        /// <summary>
        /// Extracts the embedded template to the specified file path.
        /// </summary>
        private static void ExtractTemplateToFile(string outputPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(TEMPLATE_RESOURCE_NAME))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"Embedded template resource not found: {TEMPLATE_RESOURCE_NAME}");
                }

                using (var fileStream = File.Create(outputPath))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }

        /// <summary>
        /// Populates the template with issue summary data.
        /// </summary>
        private static void PopulateTemplate(WordprocessingDocument doc, IEnumerable<HeadingResult> results, bool useSingleLevelCvss)
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                throw new InvalidOperationException("Document body not found in template.");
            }

            // Find the table in the template
            var table = body.Descendants<Table>().FirstOrDefault();
            if (table == null)
            {
                throw new InvalidOperationException("No table found in template.");
            }

            // If using single level CVSS, remove the Critical column
            if (useSingleLevelCvss)
            {
                RemoveCriticalColumn(table);
            }

            // Get phases and issues - sort phases numerically by reference
            var phases = results.Where(r => r.IsPhaseHeader)
                .OrderBy(r => int.TryParse(r.Reference, out var num) ? num : int.MaxValue)
                .ToList();
            //var phases = results.Where(r => r.IsPhaseHeader).OrderBy(r => r.Reference).ToList();
            var issues = results.Where(r => !r.IsPhaseHeader).ToList();

            // Calculate summary statistics
            var summaryStats = CreateSummaryStats(phases, issues, useSingleLevelCvss);

            // Check if there's already a blank data row in the template
            var existingRows = table.Elements<TableRow>().ToList();
            bool hasBlankRow = existingRows.Count > 1; // More than just header row
            bool usedBlankRow = false;

            // Populate phase rows
            foreach (var phase in phases)
            {
                var stats = summaryStats[phase.Issue];

                if (hasBlankRow && !usedBlankRow)
                {
                    PopulateExistingRow(existingRows[1], phase.Issue, stats, useSingleLevelCvss);
                    usedBlankRow = true;
                }
                else
                {
                    AddDataRowToTable(table, phase.Issue, stats, useSingleLevelCvss);
                }
            }

            // Add totals row
            var totals = CalculateTotals(summaryStats, useSingleLevelCvss);
            AddDataRowToTable(table, "Total", totals, useSingleLevelCvss);
        }

        /// <summary>
        /// Removes the Critical column from the table when using single level CVSS.
        /// </summary>
        private static void RemoveCriticalColumn(Table table)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>().ToList();
                // Assuming Critical is the second column (index 1), remove it
                if (cells.Count > 1)
                {
                    cells[1].Remove();
                }
            }
        }

        /// <summary>
        /// Creates summary statistics for each phase.
        /// </summary>
        private static Dictionary<string, Dictionary<string, int>> CreateSummaryStats(
            List<HeadingResult> phases, List<HeadingResult> issues, bool useSingleLevelCvss)
        {
            var summary = new Dictionary<string, Dictionary<string, int>>();

            foreach (var phase in phases)
            {
                var phaseIssues = issues.Where(i => i.Reference.StartsWith(phase.Reference + ".")).ToList();

                var stats = new Dictionary<string, int>();

                if (useSingleLevelCvss)
                {
                    // Simplified system: High, Medium, Low, Info
                    stats["High"] = phaseIssues.Count(i => MapRating(i.CvssScore, true) == "High");
                    stats["Medium"] = phaseIssues.Count(i => MapRating(i.CvssScore, true) == "Medium");
                    stats["Low"] = phaseIssues.Count(i => MapRating(i.CvssScore, true) == "Low");
                    stats["Info"] = phaseIssues.Count(i => MapRating(i.CvssScore, true) == "INFO");
                }
                else
                {
                    // Detailed system: Critical, High, Medium, Low, Info
                    stats["Critical"] = phaseIssues.Count(i => MapRating(i.CvssScore, false) == "Critical");
                    stats["High"] = phaseIssues.Count(i => MapRating(i.CvssScore, false) == "High");
                    stats["Medium"] = phaseIssues.Count(i => MapRating(i.CvssScore, false) == "Medium");
                    stats["Low"] = phaseIssues.Count(i => MapRating(i.CvssScore, false) == "Low");
                    stats["Info"] = phaseIssues.Count(i => MapRating(i.CvssScore, false) == "INFO");
                }

                stats["Total"] = phaseIssues.Count;
                summary[phase.Issue] = stats;
            }

            return summary;
        }

        /// <summary>
        /// Calculates totals across all phases.
        /// </summary>
        private static Dictionary<string, int> CalculateTotals(Dictionary<string, Dictionary<string, int>> summaryStats, bool useSingleLevelCvss)
        {
            var totals = new Dictionary<string, int>();

            if (useSingleLevelCvss)
            {
                totals["High"] = summaryStats.Values.Sum(s => s["High"]);
                totals["Medium"] = summaryStats.Values.Sum(s => s["Medium"]);
                totals["Low"] = summaryStats.Values.Sum(s => s["Low"]);
                totals["Info"] = summaryStats.Values.Sum(s => s["Info"]);
            }
            else
            {
                totals["Critical"] = summaryStats.Values.Sum(s => s["Critical"]);
                totals["High"] = summaryStats.Values.Sum(s => s["High"]);
                totals["Medium"] = summaryStats.Values.Sum(s => s["Medium"]);
                totals["Low"] = summaryStats.Values.Sum(s => s["Low"]);
                totals["Info"] = summaryStats.Values.Sum(s => s["Info"]);
            }

            totals["Total"] = summaryStats.Values.Sum(s => s["Total"]);
            return totals;
        }

        /// <summary>
        /// Populates an existing blank row with data.
        /// </summary>
        private static void PopulateExistingRow(TableRow row, string description, Dictionary<string, int> stats, bool useSingleLevelCvss)
        {
            var cells = row.Elements<TableCell>().ToList();
            int cellIndex = 0;
            bool isTotalsRow = description.Equals("Total", StringComparison.OrdinalIgnoreCase);

            // Description cell — only bold for totals row
            if (cells.Count > cellIndex)
            {
                cells[cellIndex].RemoveAllChildren();
                cells[cellIndex].AppendChild(CreateParagraphWithText(description, isTotalsRow));
                cellIndex++;
            }

            if (!useSingleLevelCvss)
            {
                // Critical column — only bold for totals row
                if (cells.Count > cellIndex)
                {
                    cells[cellIndex].RemoveAllChildren();
                    cells[cellIndex].AppendChild(CreateParagraphWithText(stats["Critical"].ToString(), isTotalsRow));
                    cellIndex++;
                }
            }

            var columns = useSingleLevelCvss
                ? new[] { "High", "Medium", "Low", "Info", "Total" }
                : new[] { "High", "Medium", "Low", "Info", "Total" };

            for (int i = 0; i < columns.Length; i++)
            {
                if (cells.Count > cellIndex)
                {
                    bool isTotalColumn = (i == columns.Length - 1);
                    bool bold = isTotalsRow || isTotalColumn;
                    cells[cellIndex].RemoveAllChildren();
                    cells[cellIndex].AppendChild(CreateParagraphWithText(stats[columns[i]].ToString(), bold));
                    cellIndex++;
                }
            }
        }



        /// <summary>
        /// Adds a data row to the table.
        /// </summary>
        private static void AddDataRowToTable(Table table, string description, Dictionary<string, int> stats, bool useSingleLevelCvss)
        {
            bool isTotalsRow = description.Equals("Total", StringComparison.OrdinalIgnoreCase);
            var row = new TableRow();

            // Description cell — only bold if it's the Total row
            row.AppendChild(CreateSimpleCell(description, isTotalsRow));

            if (!useSingleLevelCvss)
            {
                // Critical column — bold only for totals row
                row.AppendChild(CreateSimpleCell(stats["Critical"].ToString(), isTotalsRow));
            }

            // Severity columns
            var columns = useSingleLevelCvss
                ? new[] { "High", "Medium", "Low", "Info", "Total" }
                : new[] { "High", "Medium", "Low", "Info", "Total" };

            for (int i = 0; i < columns.Length; i++)
            {
                bool isTotalColumn = (i == columns.Length - 1);
                // Bold if totals row or if total column
                bool bold = isTotalsRow || isTotalColumn;
                row.AppendChild(CreateSimpleCell(stats[columns[i]].ToString(), bold));
            }

            table.AppendChild(row);
        }


        /// <summary>
        /// Creates a simple cell with standard formatting.
        /// </summary>
        private static TableCell CreateSimpleCell(string text, bool bold = false)
        {
            var tc = new TableCell();
            var paragraph = new Paragraph();
            var runProps = new RunProperties();
            runProps.AppendChild(new RunFonts { Ascii = FONT_NAME, HighAnsi = FONT_NAME, ComplexScript = FONT_NAME });
            runProps.AppendChild(new FontSize { Val = (FONT_SIZE * 2).ToString() });
            if (bold)
                runProps.AppendChild(new Bold());
            else
                runProps.AppendChild(new Bold() { Val = false });

            paragraph.AppendChild(new Run(runProps, new Text(text)));
            tc.AppendChild(paragraph);
            return tc;
        }

        /// <summary>
        /// Creates a paragraph with text and standard formatting.
        /// </summary>
        private static Paragraph CreateParagraphWithText(string text, bool bold = false)
        {
            var paragraph = new Paragraph();
            var runProps = new RunProperties();
            runProps.AppendChild(new RunFonts { Ascii = FONT_NAME, HighAnsi = FONT_NAME, ComplexScript = FONT_NAME });
            runProps.AppendChild(new FontSize { Val = (FONT_SIZE * 2).ToString() });
            if (bold)
                runProps.AppendChild(new Bold());
            else
                runProps.AppendChild(new Bold() { Val = false });

            paragraph.AppendChild(new Run(runProps, new Text(text)));
            return paragraph;
        }

        /// <summary>
        /// Maps CVSS score to rating based on the rating system.
        /// </summary>
        private static string MapRating(double? cvss, bool useSingleLevelCvss)
        {
            if (!cvss.HasValue) return string.Empty;

            if (useSingleLevelCvss)
            {
                // Simplified 4-level rating system
                if (cvss >= 7.0) return "High";
                if (cvss >= 4.0) return "Medium";
                if (cvss >= 1.0) return "Low";
                return "INFO";
            }
            else
            {
                // Detailed 5-level rating system
                if (cvss >= 9.0) return "Critical";
                if (cvss >= 7.0) return "High";
                if (cvss >= 4.0) return "Medium";
                if (cvss >= 1.0) return "Low";
                return "INFO";
            }
        }
    }
}