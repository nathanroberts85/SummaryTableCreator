using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SummaryTableCreator.Models;
using SummaryTableCreator.UI;

namespace SummaryTableCreator.Reports
{
    /// <summary>
    /// Generates a consolidated findings summary report with one table per phase.
    /// </summary>
    public static class FindingsSummaryReportGenerator
    {
        // Template resource name - uses the same template as SummaryReportGenerator
        private const string TEMPLATE_RESOURCE_NAME = "SummaryTableCreator.Resources.Summary_of_Findings.docx";

        /// <summary>
        /// Creates a Word document with one table per phase showing findings.
        /// </summary>
        /// <param name="originalPath">Path to the imported document (used to determine output location).</param>
        /// <param name="results">Parsed heading results.</param>
        public static void Generate(string originalPath, IEnumerable<HeadingResult> results)
        {
            var folder = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var outputPath = Path.Combine(folder, "Summary_of_Findings.docx");

            Logger.Info("[+] Generating consolidated findings summary report");

            // Extract template from embedded resources and save to output location
            ExtractTemplateToFile(outputPath);

            // Open the template and populate it with data
            using (var doc = WordprocessingDocument.Open(outputPath, true))
            {
                PopulateTemplate(doc, results);
            }

            Logger.Okay($"[+] Report saved: {outputPath}");
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
        /// Populates the template with findings data.
        /// </summary>
        private static void PopulateTemplate(WordprocessingDocument doc, IEnumerable<HeadingResult> results)
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                throw new InvalidOperationException("Document body not found in template.");
            }

            // Preserve section properties (page orientation, margins, etc.) from template
            var sectionProperties = body.Elements<SectionProperties>().FirstOrDefault();
            var preservedSectionProps = sectionProperties?.CloneNode(true) as SectionProperties;

            // Clear existing content from template (but keep section properties)
            body.RemoveAllChildren();

            // Group findings by phase - maintain document order (don't sort)
            var phases = results
                .Where(r => r.IsPhaseHeader)
                .ToList();

            var issues = results
                .Where(r => !r.IsPhaseHeader)
                .ToList();

            // Add Issue Summary section at the top
            AddIssueSummarySection(body, phases, issues);

            // Add spacing between summary and phase tables
            body.AppendChild(new Paragraph());

            // Add phase tables
            int phaseNumber = 1;
            foreach (var phase in phases)
            {
                // Get all findings for this phase - maintain document order (don't sort)
                var findings = results
                    .Where(r => !r.IsPhaseHeader && r.PhaseName == phase.PhaseName)
                    .ToList();

                if (findings.Any())
                {
                    // Add phase heading with sequential phase number
                    AddPhaseHeading(body, phase, phaseNumber);

                    // Add table for this phase
                    AddFindingsTable(body, findings);

                    // Add spacing between phases
                    body.AppendChild(new Paragraph());
                    
                    phaseNumber++;
                }
            }

            // Re-add section properties at the end of the body (required for page settings)
            if (preservedSectionProps != null)
            {
                body.AppendChild(preservedSectionProps);
            }

            doc.MainDocumentPart.Document.Save();
        }

        /// <summary>
        /// Adds the Issue Summary section with heading and table.
        /// </summary>
        private static void AddIssueSummarySection(Body body, List<HeadingResult> phases, List<HeadingResult> issues)
        {
            // Add "Issue Summary" heading (not bold)
            var summaryHeading = new Paragraph(
                new ParagraphProperties(
                    new ParagraphStyleId() { Val = ReportStyles.SECTION_HEADING }
                ),
                new Run(new Text("Issue Summary"))
            );
            body.AppendChild(summaryHeading);

            // Calculate summary statistics
            var summaryStats = CreateSummaryStats(phases, issues);

            // Create and add summary table
            var summaryTable = CreateIssueSummaryTable(summaryStats, phases.Count);
            body.AppendChild(summaryTable);
        }

        /// <summary>
        /// Creates summary statistics for each phase based on severity counts.
        /// </summary>
        private static Dictionary<string, Dictionary<string, int>> CreateSummaryStats(
            List<HeadingResult> phases, List<HeadingResult> issues)
        {
            var summary = new Dictionary<string, Dictionary<string, int>>();

            foreach (var phase in phases)
            {
                var phaseIssues = issues.Where(i => i.PhaseName == phase.PhaseName).ToList();

                var stats = new Dictionary<string, int>
                {
                    ["Critical"] = phaseIssues.Count(i => i.Severity?.Equals("Critical", StringComparison.OrdinalIgnoreCase) == true),
                    ["High"] = phaseIssues.Count(i => i.Severity?.Equals("High", StringComparison.OrdinalIgnoreCase) == true),
                    ["Medium"] = phaseIssues.Count(i => i.Severity?.Equals("Medium", StringComparison.OrdinalIgnoreCase) == true),
                    ["Low"] = phaseIssues.Count(i => i.Severity?.Equals("Low", StringComparison.OrdinalIgnoreCase) == true),
                    ["Info"] = phaseIssues.Count(i => i.Severity?.Equals("Informational", StringComparison.OrdinalIgnoreCase) == true 
                                                       || i.Severity?.Equals("Info", StringComparison.OrdinalIgnoreCase) == true),
                    ["Total"] = phaseIssues.Count
                };

                summary[phase.PhaseName] = stats;
            }

            return summary;
        }

        /// <summary>
        /// Creates the issue summary table with severity counts.
        /// </summary>
        private static Table CreateIssueSummaryTable(Dictionary<string, Dictionary<string, int>> summaryStats, int phaseCount)
        {
            var table = new Table();

            // Apply table style and properties
            var tableProperties = new TableProperties(
                new TableStyle() { Val = ReportStyles.TABLE_STYLE },
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }, // 100% width
                new TableIndentation() { Width = 0, Type = TableWidthUnitValues.Dxa },
                new TableLook()
                {
                    Val = "04A0",
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = false,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tableProperties);

            // Define table grid with 7 columns
            // Landscape A4 usable width ~15,840 twips (with standard margins)
            var tableGrid = new TableGrid();
            table.AppendChild(tableGrid);

            // Add header row
            AddIssueSummaryHeaderRow(table);

            // Add data rows for each phase
            foreach (var kvp in summaryStats.OrderBy(k => k.Key))
            {
                AddIssueSummaryDataRow(table, kvp.Key, kvp.Value, false);
            }

            // Calculate and add totals row
            var totals = new Dictionary<string, int>
            {
                ["Critical"] = summaryStats.Values.Sum(s => s["Critical"]),
                ["High"] = summaryStats.Values.Sum(s => s["High"]),
                ["Medium"] = summaryStats.Values.Sum(s => s["Medium"]),
                ["Low"] = summaryStats.Values.Sum(s => s["Low"]),
                ["Info"] = summaryStats.Values.Sum(s => s["Info"]),
                ["Total"] = summaryStats.Values.Sum(s => s["Total"])
            };
            AddIssueSummaryDataRow(table, "Total", totals, true);

            return table;
        }

        /// <summary>
        /// Adds the header row to the issue summary table.
        /// </summary>
        private static void AddIssueSummaryHeaderRow(Table table)
        {
            var headerRow = new TableRow();

            // Apply row height
            var rowProperties = new TableRowProperties(
                new TableRowHeight() { Val = (uint)ReportStyles.ROW_HEIGHT }
            );
            headerRow.AppendChild(rowProperties);
            
            var headers = new[] { "Description", "Critical", "High", "Medium", "Low", "Info", "Total" };
            var widths = new[] 
            { 
                ReportStyles.SUMMARY_COL_PHASE,
                ReportStyles.SUMMARY_COL_CRITICAL,
                ReportStyles.SUMMARY_COL_HIGH,
                ReportStyles.SUMMARY_COL_MEDIUM,
                ReportStyles.SUMMARY_COL_LOW,
                ReportStyles.SUMMARY_COL_INFO,
                ReportStyles.SUMMARY_COL_TOTAL
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = CreateStyledCell(headers[i], ReportStyles.HEADER_STYLE, false, null, false, widths[i]);
                headerRow.AppendChild(cell);
            }

            table.AppendChild(headerRow);
        }

        /// <summary>
        /// Adds a data row to the issue summary table.
        /// </summary>
        private static void AddIssueSummaryDataRow(Table table, string phaseName, Dictionary<string, int> stats, bool isTotalsRow)
        {
            var row = new TableRow();
    
            // Apply row height (1 cm = 567 twips)
            var rowProperties = new TableRowProperties(
                new TableRowHeight() { Val = (uint)(ReportStyles.ROW_HEIGHT) }
            );
            row.AppendChild(rowProperties);

            var widths = new[] 
            { 
                ReportStyles.SUMMARY_COL_PHASE,
                ReportStyles.SUMMARY_COL_CRITICAL,
                ReportStyles.SUMMARY_COL_HIGH,
                ReportStyles.SUMMARY_COL_MEDIUM,
                ReportStyles.SUMMARY_COL_LOW,
                ReportStyles.SUMMARY_COL_INFO,
                ReportStyles.SUMMARY_COL_TOTAL
            };

            // First column (Phase name) - ALWAYS bold (both regular rows and totals row)
            row.AppendChild(CreateStyledCell(phaseName, ReportStyles.CELL_STYLE, false, null, true, widths[0]));

            // Severity columns - bold only if totals row
            row.AppendChild(CreateStyledCell(stats["Critical"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[1]));
            row.AppendChild(CreateStyledCell(stats["High"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[2]));
            row.AppendChild(CreateStyledCell(stats["Medium"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[3]));
            row.AppendChild(CreateStyledCell(stats["Low"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[4]));
            row.AppendChild(CreateStyledCell(stats["Info"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[5]));

            // Last column (Total) - ALWAYS bold
            row.AppendChild(CreateStyledCell(stats["Total"].ToString(), ReportStyles.CELL_STYLE, false, null, true, widths[6]));

            table.AppendChild(row);
        }

        /// <summary>
        /// Adds a phase heading to the document using HEADING111 style.
        /// Format: "Phase X - Phase Name"
        /// </summary>
        private static void AddPhaseHeading(Body body, HeadingResult phase, int phaseNumber)
        {
            // Build heading text: "Phase X - Phase Name"
            var headingText = $"Phase {phaseNumber} - {phase.PhaseName}";
            
            var heading = new Paragraph(
                new ParagraphProperties(
                    new ParagraphStyleId() { Val = ReportStyles.PHASE_TITLE }
                ),
                new Run(
                    new RunProperties(new Bold()),
                    new Text(headingText)
                )
            );
            body.AppendChild(heading);
        }

        /// <summary>
        /// Creates a findings table for a phase.
        /// </summary>
        private static void AddFindingsTable(Body body, List<HeadingResult> findings)
        {
            var table = new Table();

            // Set table to full width (100% of page width) and apply table style
            var tableProperties = new TableProperties(
                new TableStyle() { Val = ReportStyles.TABLE_STYLE }, // Apply TBLAFFECTEDHOSTS style
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableIndentation() { Width = 0, Type = TableWidthUnitValues.Dxa },
                new TableLook() 
                { 
                    Val = "04A0",
                    FirstRow = true,
                    LastRow = false,
                    FirstColumn = false,
                    LastColumn = false,
                    NoHorizontalBand = false,
                    NoVerticalBand = true
                }
            );
            table.AppendChild(tableProperties);

            // Define table grid with 4 columns
            var tableGrid = new TableGrid();
            tableGrid.AppendChild(new GridColumn() { Width = "1500" });  // Risk Rating - 15%
            tableGrid.AppendChild(new GridColumn() { Width = "2500" });  // Issue - 25%
            tableGrid.AppendChild(new GridColumn() { Width = "1500" });  // Reference - 15%
            tableGrid.AppendChild(new GridColumn() { Width = "4500" });  // Description - 45%
            table.AppendChild(tableGrid);

            // Add header row
            AddHeaderRow(table);

            // Add data rows
            foreach (var finding in findings)
            {
                AddDataRow(table, finding);
            }

            body.AppendChild(table);
        }

        /// <summary>
        /// Adds the header row to the table using TABLEHEADER style.
        /// </summary>
        private static void AddHeaderRow(Table table)
        {
            var headerRow = new TableRow();

            // Column headers and widths
            var headers = new[] { "Risk Rating", "Issue", "Report Reference", "Issue Description" };
            var widths = new[] 
            { 
                ReportStyles.FINDINGS_COL_RISK_RATING,
                ReportStyles.FINDINGS_COL_ISSUE,
                ReportStyles.FINDINGS_COL_REFERENCE,
                ReportStyles.FINDINGS_COL_DESCRIPTION
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = CreateStyledCell(headers[i], ReportStyles.HEADER_STYLE, false, null, false, widths[i]);
                headerRow.AppendChild(cell);
            }

            table.AppendChild(headerRow);
        }

        /// <summary>
        /// Adds a data row to the table using TABLECELL style.
        /// </summary>
        private static void AddDataRow(Table table, HeadingResult finding)
        {
            var row = new TableRow();
            var widths = new[] 
            { 
                ReportStyles.FINDINGS_COL_RISK_RATING,
                ReportStyles.FINDINGS_COL_ISSUE,
                ReportStyles.FINDINGS_COL_REFERENCE,
                ReportStyles.FINDINGS_COL_DESCRIPTION
            };

            // Severity cell (with severity-specific paragraph style)
            row.AppendChild(CreateStyledCell(finding.Severity ?? "", ReportStyles.CELL_STYLE, true, finding.Severity, false, widths[0]));

            // Title cell (bold)
            row.AppendChild(CreateStyledCell(finding.Title ?? "", ReportStyles.CELL_STYLE, false, null, isBold: true, cellWidth: widths[1]));

            // Reference cell
            row.AppendChild(CreateStyledCell(finding.Reference ?? "", ReportStyles.CELL_STYLE, false, null, false, widths[2]));

            // Issue Description cell
            row.AppendChild(CreateStyledCell(finding.IssueDescription ?? "", ReportStyles.CELL_STYLE, false, null, false, widths[3]));

            table.AppendChild(row);
        }

        /// <summary>
        /// Creates a table cell with a specified paragraph style and optional severity formatting.
        /// </summary>
        private static TableCell CreateStyledCell(string text, string styleName, bool applySeverityStyle, string? severity, bool isBold = false, decimal? cellWidth = null)
        {
            var cell = new TableCell();

            // Apply cell width if specified (using decimal for precision, converted to string)
            if (cellWidth.HasValue)
            {
                var cellProperties = new TableCellProperties(
                    new TableCellWidth() { Width = cellWidth.Value.ToString("0.###"), Type = TableWidthUnitValues.Dxa }
                );
                cell.AppendChild(cellProperties);
            }

            // Create paragraph with style reference
            var paragraph = new Paragraph();
            
            // Determine which style to use - severity style overrides base style if applicable
            var finalStyleName = (applySeverityStyle && !string.IsNullOrEmpty(severity)) 
                ? ReportStyles.GetSeverityColor(severity) 
                : styleName;
            
            // Apply paragraph style
            var paragraphProps = new ParagraphProperties(
                new ParagraphStyleId() { Val = finalStyleName }
            );
            paragraph.AppendChild(paragraphProps);

            // Create run with text
            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            
            // Add bold formatting if requested
            if (isBold)
            {
                var runProps = new RunProperties(new Bold());
                run.InsertAt(runProps, 0);
            }

            paragraph.AppendChild(run);
            cell.AppendChild(paragraph);
            
            return cell;
        }

    }
}