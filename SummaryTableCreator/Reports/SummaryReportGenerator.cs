// File: Reports/SummaryReportGenerator.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SummaryTableCreator.Models;
using SummaryTableCreator.UI;
// Explicitly using the OpenXML Color to avoid ambiguity
using OpenXmlColor = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace SummaryTableCreator.Reports
{
    /// <summary>
    /// Generates a summary report Word document with a table of issues ordered by CVSS score.
    /// </summary>
    public static class SummaryReportGenerator
    {
        // Template resource name
        private const string TEMPLATE_RESOURCE_NAME = "SummaryTableCreator.Resources.SummaryTableTemplate.docx";
        
        // Font constants
        private const string FONT_NAME = "Calibri Light";
        private const int FONT_SIZE = 12;

        // Color constants
        private const string COLOR_CRITICAL = "57257c"; // Purple
        private const string COLOR_HIGH = "FF0000";  // Red
        private const string COLOR_MEDIUM = "ed7d31"; // Dark Orange
        private const string COLOR_LOW = "00b0f0";   // Light Blue
        private const string COLOR_INFO = "19b861";  // Light Green
        private const string ROW_BACKGROUND = "F2F2F2"; // Light gray
        private const string HEADER_BACKGROUND = "DDDDDD"; // Medium gray

        /// <summary>
        /// Creates a new Word document containing only a summary table using an embedded template.
        /// </summary>
        /// <param name="originalPath">Path to the imported document (used to determine output location).</param>
        /// <param name="results">Parsed heading results.</param>
        /// <param name="useSingleLevelCvss">When true, uses simplified 3-level CVSS rating. When false, uses 5-level rating.</param>
        /// <param name="showCvssScore">When true, shows CVSS score in brackets (e.g., "Critical (10.0)"). When false, shows only the rating (e.g., "Critical").</param>
        public static void Generate(string originalPath, IEnumerable<HeadingResult> results, bool useSingleLevelCvss = false, bool showCvssScore = false)
        {
            // Determine output path
            var folder = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(originalPath);
            var outputPath = Path.Combine(folder, fileName + "_summary.docx");

            //Logger.Info($"Creating summary report from template: {outputPath}");
            Logger.Info($"[+] Using {(useSingleLevelCvss ? "Non-CHECK" : "CHECK")} CVSS rating system");

            // Extract template from embedded resources and save to output location
            ExtractTemplateToFile(outputPath);

            // Open the template and populate it with data
            using (var doc = WordprocessingDocument.Open(outputPath, true))
            {
                PopulateTemplate(doc, results, useSingleLevelCvss, showCvssScore);
            }

            Logger.Okay("[+] Summary report generation complete");
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
        /// Populates the template with data.
        /// </summary>
        private static void PopulateTemplate(WordprocessingDocument doc, IEnumerable<HeadingResult> results, bool useSingleLevelCvss, bool showCvssScore)
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                throw new InvalidOperationException("Document body not found in template.");
            }
            
            // Find the table in the template (assuming it's the first table)
            var table = body.Descendants<Table>().FirstOrDefault();
            if (table == null)
            {
                throw new InvalidOperationException("No table found in template.");
            }

            // Sort results by descending CVSS
            var ordered = results
                .OrderByDescending(r => r.CvssScore ?? 0)
                .ToList();

            // Group findings by issue text to handle duplicates
            var issueGroups = ordered
                .GroupBy(r => r.Issue)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Process each heading result, but only add the first occurrence of each issue
            int rowIndex = 0;
            var processedIssues = new HashSet<string>();
            
            // Check if there's already a blank data row in the template
            var existingRows = table.Elements<TableRow>().ToList();
            bool hasBlankRow = existingRows.Count > 1; // More than just header row
            bool usedBlankRow = false;

            foreach (var r in ordered)
            {
                // If we've already processed this issue, skip it
                if (processedIssues.Contains(r.Issue))
                {
                    continue;
                }
                
                // Mark this issue as processed
                processedIssues.Add(r.Issue);
                
                // Map CVSS to rating based on selected rating system
                var rating = MapRating(r.CvssScore, useSingleLevelCvss);
                var formattedRating = showCvssScore ? FormatRatingWithScore(rating, r.CvssScore) : rating;

                // Get current issue group and get all distinct references
                var currentIssueGroup = issueGroups[r.Issue];

                // Collect all references from the issue group
                var allReferences = currentIssueGroup
                    .Select(item => item.Reference)
                    .Where(reference => !string.IsNullOrEmpty(reference))
                    .Distinct()
                    .OrderBy(reference => reference, new NaturalStringComparer()) // Sort references numerically
                    .ToList();

                var referenceText = string.Join(",", allReferences);

                // Use existing blank row for first entry, then create new rows
                if (hasBlankRow && !usedBlankRow)
                {
                    PopulateExistingRow(existingRows[1], r.Issue, formattedRating, referenceText, r.Recommendation ?? string.Empty, rating);
                    usedBlankRow = true;
                }
                else
                {
                    // Add new row to table
                    AddDataRowToTable(table, r.Issue, formattedRating, referenceText, r.Recommendation ?? string.Empty, rating);
                }
                rowIndex++;
            }
        }

        /// <summary>
        /// Comparer for natural string sorting (e.g., "6.1.3" comes before "6.10.1")
        /// </summary>
        public class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                var xParts = x.Split('.').Select(int.Parse).ToArray();
                var yParts = y.Split('.').Select(int.Parse).ToArray();

                int minLength = Math.Min(xParts.Length, yParts.Length);
                for (int i = 0; i < minLength; i++)
                {
                    int comparison = xParts[i].CompareTo(yParts[i]);
                    if (comparison != 0) return comparison;
                }

                return xParts.Length.CompareTo(yParts.Length);
            }
        }

        /// <summary>
        /// Populates an existing blank row with data.
        /// </summary>
        private static void PopulateExistingRow(TableRow row, string issue, string criticality, string reference, string recommendation, string rating)
        {
            var cells = row.Elements<TableCell>().ToList();
            if (cells.Count >= 4)
            {
                // Clear existing content and populate with new data
                cells[0].RemoveAllChildren<Paragraph>();
                cells[0].AppendChild(CreateParagraphWithText(issue, rating, true));
                
                cells[1].RemoveAllChildren<Paragraph>();
                cells[1].AppendChild(CreateParagraphWithText(criticality, rating, true));
                
                cells[2].RemoveAllChildren<Paragraph>();
                cells[2].AppendChild(CreateParagraphWithText(reference, rating, false));
                
                cells[3].RemoveAllChildren<Paragraph>();
                cells[3].AppendChild(CreateParagraphWithText(recommendation, rating, false));
            }
        }

        /// <summary>
        /// Adds a data row to the existing table.
        /// </summary>
        private static void AddDataRowToTable(Table table, string issue, string criticality, string reference, string recommendation, string rating)
        {
            var row = new TableRow();

            row.Append(
                CreateSimpleCell(issue, rating, true),
                CreateSimpleCell(criticality, rating, true),
                CreateSimpleCell(reference, rating, false),
                CreateSimpleCell(recommendation, rating, false)
            );
            
            table.AppendChild(row);
        }

        /// <summary>
        /// Creates a simple cell with only font and color formatting.
        /// </summary>
        private static TableCell CreateSimpleCell(string text, string rating, bool isColored)
        {
            var tc = new TableCell();

            if (text.Contains(","))
            {
                var paragraph = new Paragraph();
                
                // Split the text by commas
                string[] lines = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .ToArray();
                
                // Add each reference with line breaks between them
                for (int i = 0; i < lines.Length; i++)
                {
                    var runProps = new RunProperties();
                    runProps.AppendChild(new RunFonts { Ascii = FONT_NAME, HighAnsi = FONT_NAME, ComplexScript = FONT_NAME });
                    runProps.AppendChild(new FontSize { Val = (FONT_SIZE * 2).ToString() });
                    
                    if (isColored)
                    {
                        runProps.AppendChild(new Bold());
                        runProps.AppendChild(new OpenXmlColor { Val = GetColorForRating(rating) });
                    }
                    
                    paragraph.AppendChild(new Run(runProps, new Text(lines[i])));
                    
                    if (i < lines.Length - 1)
                    {
                        paragraph.AppendChild(new Break());
                    }
                }
                
                tc.AppendChild(paragraph);
            }
            else
            {
                var paragraph = new Paragraph();
                var runProps = new RunProperties();
                runProps.AppendChild(new RunFonts { Ascii = FONT_NAME, HighAnsi = FONT_NAME, ComplexScript = FONT_NAME });
                runProps.AppendChild(new FontSize { Val = (FONT_SIZE * 2).ToString() });
                
                if (isColored)
                {
                    runProps.AppendChild(new Bold());
                    runProps.AppendChild(new OpenXmlColor { Val = GetColorForRating(rating) });
                }
                
                paragraph.AppendChild(new Run(runProps, new Text(text)));
                tc.AppendChild(paragraph);
            }
            
            return tc;
        }

        /// <summary>
        /// Creates a paragraph with text and optional coloring.
        /// </summary>
        private static Paragraph CreateParagraphWithText(string text, string rating, bool isColored)
        {
            if (text.Contains(","))
            {
                var paragraph = new Paragraph();
                
                // Split the text by commas
                string[] lines = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .ToArray();
                
                // Add each reference with line breaks between them
                for (int i = 0; i < lines.Length; i++)
                {
                    var runProps = new RunProperties();
                    runProps.AppendChild(new RunFonts { Ascii = FONT_NAME, HighAnsi = FONT_NAME, ComplexScript = FONT_NAME });
                    runProps.AppendChild(new FontSize { Val = (FONT_SIZE * 2).ToString() });
                    
                    if (isColored)
                    {
                        runProps.AppendChild(new Bold());
                        runProps.AppendChild(new OpenXmlColor { Val = GetColorForRating(rating) });
                    }
                    
                    paragraph.AppendChild(new Run(runProps, new Text(lines[i])));
                    
                    if (i < lines.Length - 1)
                    {
                        paragraph.AppendChild(new Break());
                    }
                }
                
                return paragraph;
            }
            else
            {
                var paragraph = new Paragraph();
                var runProps = new RunProperties();
                runProps.AppendChild(new RunFonts { Ascii = FONT_NAME, HighAnsi = FONT_NAME, ComplexScript = FONT_NAME });
                runProps.AppendChild(new FontSize { Val = (FONT_SIZE * 2).ToString() });
                
                if (isColored)
                {
                    runProps.AppendChild(new Bold());
                    runProps.AppendChild(new OpenXmlColor { Val = GetColorForRating(rating) });
                }
                
                paragraph.AppendChild(new Run(runProps, new Text(text)));
                return paragraph;
            }
        }

        /// <summary>
        /// Gets the appropriate color for a rating
        /// </summary>
        private static string GetColorForRating(string rating)
        {
            switch (rating.ToUpper())
            {
                case "CRITICAL": return COLOR_CRITICAL;
                case "HIGH": return COLOR_HIGH;
                case "MEDIUM": return COLOR_MEDIUM;
                case "LOW": return COLOR_LOW;
                case "INFO":
                case "INFORMATIONAL": return COLOR_INFO;
                default: return "000000"; // Black
            }
        }

        /// <summary>
        /// Format the rating with the CVSS score in parentheses
        /// </summary>
        private static string FormatRatingWithScore(string rating, double? cvss)
        {
            if (!cvss.HasValue)
                return rating;

            return $"{rating} ({cvss:F1})";
        }

        private static string MapRating(double? cvss, bool useSingleLevelCvss)
        {
            if (!cvss.HasValue) return string.Empty;

            if (useSingleLevelCvss)
            {
                // Simplified 4-level rating system when checkbox is checked
                if (cvss >= 7.0) return "High";
                if (cvss >= 4.0) return "Medium";
                if (cvss >= 1.0) return "Low";
                return "INFORMATIONAL";
            }
            else
            {
                // Detailed 5-level rating system when checkbox is unchecked
                if (cvss >= 9.0) return "Critical";
                if (cvss >= 7.0) return "High";
                if (cvss >= 4.0) return "Medium";
                if (cvss >= 1.0) return "Low";
                return "INFO";
            }
        }
    }
}