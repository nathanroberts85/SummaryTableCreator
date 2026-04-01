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
        /// <param name="useSingleLevelCvss">When true, treats Critical as High for CHECK reporting requirements.</param>
        /// <param name="includeRecommendations">When true, displays the Issue Description column.</param>
        public static void Generate(string originalPath, IEnumerable<HeadingResult> results, bool useSingleLevelCvss = false, bool includeRecommendations = true)
        {
            var folder = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var outputPath = Path.Combine(folder, "Summary_of_Findings.docx");

            Logger.Info("[+] Generating consolidated findings summary report");
            if (useSingleLevelCvss)
            {
                Logger.Info("[+] Using CHECK reporting mode (Critical treated as High)");
            }

            // Extract template from embedded resources and save to output location
            ExtractTemplateToFile(outputPath);

            // Open the template and populate it with data
            using (var doc = WordprocessingDocument.Open(outputPath, true))
            {
                PopulateTemplate(doc, results, useSingleLevelCvss, includeRecommendations);
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
        private static void PopulateTemplate(WordprocessingDocument doc, IEnumerable<HeadingResult> results, bool useSingleLevelCvss, bool includeRecommendations)
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
            AddIssueSummarySection(body, phases, issues, useSingleLevelCvss);
            
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
                    AddFindingsTable(body, findings, useSingleLevelCvss, includeRecommendations);

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
        private static void AddIssueSummarySection(Body body, List<HeadingResult> phases, List<HeadingResult> issues, bool useSingleLevelCvss)
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
            var summaryStats = CreateSummaryStats(phases, issues, useSingleLevelCvss);

            // Create and add summary table
            var summaryTable = CreateIssueSummaryTable(summaryStats, phases.Count, useSingleLevelCvss);
            body.AppendChild(summaryTable);
        }

        /// <summary>
        /// Creates summary statistics for each phase based on severity counts.
        /// </summary>
        private static Dictionary<string, Dictionary<string, int>> CreateSummaryStats(
            List<HeadingResult> phases, List<HeadingResult> issues, bool useSingleLevelCvss)
        {
            var summary = new Dictionary<string, Dictionary<string, int>>();

            foreach (var phase in phases)
            {
                var phaseIssues = issues.Where(i => i.PhaseName == phase.PhaseName).ToList();

                var stats = new Dictionary<string, int>();

                if (useSingleLevelCvss)
                {
                    // CHECK reporting: Critical becomes High, no Critical column
                    stats["High"] = phaseIssues.Count(i => 
                        i.Severity?.Equals("Critical", StringComparison.OrdinalIgnoreCase) == true ||
                        i.Severity?.Equals("High", StringComparison.OrdinalIgnoreCase) == true);
                    stats["Medium"] = phaseIssues.Count(i => i.Severity?.Equals("Medium", StringComparison.OrdinalIgnoreCase) == true);
                    stats["Low"] = phaseIssues.Count(i => i.Severity?.Equals("Low", StringComparison.OrdinalIgnoreCase) == true);
                    stats["Info"] = phaseIssues.Count(i => 
                        i.Severity?.Equals("Informational", StringComparison.OrdinalIgnoreCase) == true ||
                        i.Severity?.Equals("Info", StringComparison.OrdinalIgnoreCase) == true);
                }
                else
                {
                    // Standard reporting: Separate Critical and High
                    stats["Critical"] = phaseIssues.Count(i => i.Severity?.Equals("Critical", StringComparison.OrdinalIgnoreCase) == true);
                    stats["High"] = phaseIssues.Count(i => i.Severity?.Equals("High", StringComparison.OrdinalIgnoreCase) == true);
                    stats["Medium"] = phaseIssues.Count(i => i.Severity?.Equals("Medium", StringComparison.OrdinalIgnoreCase) == true);
                    stats["Low"] = phaseIssues.Count(i => i.Severity?.Equals("Low", StringComparison.OrdinalIgnoreCase) == true);
                    stats["Info"] = phaseIssues.Count(i => 
                        i.Severity?.Equals("Informational", StringComparison.OrdinalIgnoreCase) == true ||
                        i.Severity?.Equals("Info", StringComparison.OrdinalIgnoreCase) == true);
                }

                stats["Total"] = phaseIssues.Count;
                summary[phase.PhaseName] = stats;
            }

            return summary;
        }

        /// <summary>
        /// Creates the issue summary table with severity counts.
        /// </summary>
        private static Table CreateIssueSummaryTable(Dictionary<string, Dictionary<string, int>> summaryStats, int phaseCount, bool useSingleLevelCvss)
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

            // Define table grid (no GridColumn needed with explicit cell widths)
            var tableGrid = new TableGrid();
            table.AppendChild(tableGrid);

            // Add header row
            AddIssueSummaryHeaderRow(table, useSingleLevelCvss);

            // Add data rows for each phase
            foreach (var kvp in summaryStats.OrderBy(k => k.Key))
            {
                AddIssueSummaryDataRow(table, kvp.Key, kvp.Value, false, useSingleLevelCvss);
            }

            // Calculate and add totals row
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
            AddIssueSummaryDataRow(table, "Total", totals, true, useSingleLevelCvss);

            return table;
        }

        /// <summary>
        /// Adds the header row to the issue summary table.
        /// </summary>
        private static void AddIssueSummaryHeaderRow(Table table, bool useSingleLevelCvss)
        {
            var headerRow = new TableRow();

            // Apply row height
            var rowProperties = new TableRowProperties(
                new TableRowHeight() { Val = (uint)ReportStyles.ROW_HEIGHT }
            );
            headerRow.AppendChild(rowProperties);
            
            var headers = useSingleLevelCvss
                ? new[] { "Description", "High", "Medium", "Low", "Info", "Total" }
                : new[] { "Description", "Critical", "High", "Medium", "Low", "Info", "Total" };
                
            var widths = useSingleLevelCvss
                ? new[] 
                { 
                    ReportStyles.SUMMARY_COL_PHASE,
                    ReportStyles.SUMMARY_COL_HIGH,
                    ReportStyles.SUMMARY_COL_MEDIUM,
                    ReportStyles.SUMMARY_COL_LOW,
                    ReportStyles.SUMMARY_COL_INFO,
                    ReportStyles.SUMMARY_COL_TOTAL
                }
                : new[] 
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
        private static void AddIssueSummaryDataRow(Table table, string phaseName, Dictionary<string, int> stats, bool isTotalsRow, bool useSingleLevelCvss)
        {
            var row = new TableRow();

            // Apply row height
            var rowProperties = new TableRowProperties(
                new TableRowHeight() { Val = (uint)ReportStyles.ROW_HEIGHT }
            );
            row.AppendChild(rowProperties);

            var widths = useSingleLevelCvss
                ? new[] 
                { 
                    ReportStyles.SUMMARY_COL_PHASE,
                    ReportStyles.SUMMARY_COL_HIGH,
                    ReportStyles.SUMMARY_COL_MEDIUM,
                    ReportStyles.SUMMARY_COL_LOW,
                    ReportStyles.SUMMARY_COL_INFO,
                    ReportStyles.SUMMARY_COL_TOTAL
                }
                : new[] 
                { 
                    ReportStyles.SUMMARY_COL_PHASE,
                    ReportStyles.SUMMARY_COL_CRITICAL,
                    ReportStyles.SUMMARY_COL_HIGH,
                    ReportStyles.SUMMARY_COL_MEDIUM,
                    ReportStyles.SUMMARY_COL_LOW,
                    ReportStyles.SUMMARY_COL_INFO,
                    ReportStyles.SUMMARY_COL_TOTAL
                };

            // First column (Phase name) - ALWAYS bold
            row.AppendChild(CreateStyledCell(phaseName, ReportStyles.CELL_STYLE, false, null, true, widths[0]));

            int idx = 1;
            
            // Critical column (only if not CHECK mode)
            if (!useSingleLevelCvss)
            {
                row.AppendChild(CreateStyledCell(stats["Critical"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[idx++]));
            }

            // Severity columns - bold only if totals row
            row.AppendChild(CreateStyledCell(stats["High"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[idx++]));
            row.AppendChild(CreateStyledCell(stats["Medium"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[idx++]));
            row.AppendChild(CreateStyledCell(stats["Low"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[idx++]));
            row.AppendChild(CreateStyledCell(stats["Info"].ToString(), ReportStyles.CELL_STYLE, false, null, isTotalsRow, widths[idx++]));

            // Last column (Total) - ALWAYS bold
            row.AppendChild(CreateStyledCell(stats["Total"].ToString(), ReportStyles.CELL_STYLE, false, null, true, widths[idx]));

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
        private static void AddFindingsTable(Body body, List<HeadingResult> findings, bool useSingleLevelCvss, bool includeRecommendations)
        {
            var table = new Table();

            // Set table to full width (100% of page width) and apply table style
            var tableProperties = new TableProperties(
                new TableStyle() { Val = ReportStyles.TABLE_STYLE },
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
            table.AppendChild(tableGrid);

            // Add header row
            AddHeaderRow(table);

            // Add data rows
            foreach (var finding in findings)
            {
                AddDataRow(table, finding, useSingleLevelCvss, includeRecommendations);
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
        private static void AddDataRow(Table table, HeadingResult finding, bool useSingleLevelCvss, bool includeRecommendations)
        {
            var row = new TableRow();
            var widths = new[] 
            { 
                ReportStyles.FINDINGS_COL_RISK_RATING,
                ReportStyles.FINDINGS_COL_ISSUE,
                ReportStyles.FINDINGS_COL_REFERENCE,
                ReportStyles.FINDINGS_COL_DESCRIPTION
            };

            // Determine severity to display (convert Critical to High if CHECK mode)
            var displaySeverity = finding.Severity;
            if (useSingleLevelCvss && finding.Severity?.Equals("Critical", StringComparison.OrdinalIgnoreCase) == true)
            {
                displaySeverity = "High";
            }

            // Severity cell (with severity-specific paragraph style)
            row.AppendChild(CreateStyledCell(displaySeverity ?? "", ReportStyles.CELL_STYLE, true, displaySeverity, false, widths[0]));

            // Title cell (bold)
            row.AppendChild(CreateStyledCell(finding.Title ?? "", ReportStyles.CELL_STYLE, false, null, isBold: true, cellWidth: widths[1]));

            // Reference cell
            row.AppendChild(CreateStyledCell(finding.Reference ?? "", ReportStyles.CELL_STYLE, false, null, false, widths[2]));

            // Issue Description cell - only populate if includeRecommendations is true, with justified alignment
            var issueDescription = includeRecommendations ? (finding.IssueDescription ?? "") : "";
            row.AppendChild(CreateStyledCell(issueDescription, ReportStyles.CELL_STYLE, false, null, false, widths[3], JustificationValues.Both));

            table.AppendChild(row);
        }

        /// <summary>
 /// Creates a table cell with a specified paragraph style and optional severity formatting.
 /// </summary>
private static TableCell CreateStyledCell(string text, string styleName, bool applySeverityStyle, string? severity, bool isBold = false, decimal? cellWidth = null, JustificationValues? justification = null)
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

    // Determine which style to use - severity style overrides base style if applicable
    var finalStyleName = (applySeverityStyle && !string.IsNullOrEmpty(severity)) 
        ? ReportStyles.GetSeverityColor(severity) 
        : styleName;

    // Handle text with newlines by creating multiple paragraphs or line breaks
    if (text.Contains('\n'))
    {
        // Split by double newline for paragraph breaks
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.None);
        
        foreach (var paragraphText in paragraphs)
        {
            var paragraph = new Paragraph();
            
            // Apply paragraph style and justification
            var paragraphProps = new ParagraphProperties(
                new ParagraphStyleId() { Val = finalStyleName }
            );
            
            // Add justification if specified
            if (justification.HasValue)
            {
                paragraphProps.AppendChild(new Justification() { Val = justification.Value });
            }
            
            paragraph.AppendChild(paragraphProps);

            // Split by single newline for line breaks within paragraph
            var lines = paragraphText.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                var run = new Run();
                
                // Add bold formatting if requested
                if (isBold)
                {
                    var runProps = new RunProperties(new Bold());
                    run.AppendChild(runProps);
                }
                
                run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
                paragraph.AppendChild(run);
                
                // Add line break if not the last line
                if (i < lines.Length - 1)
                {
                    paragraph.AppendChild(new Run(new Break()));
                }
            }
            
            cell.AppendChild(paragraph);
        }
    }
    else
    {
        // Single paragraph (no newlines)
        var paragraph = new Paragraph();
        
        // Apply paragraph style and justification
        var paragraphProps = new ParagraphProperties(
            new ParagraphStyleId() { Val = finalStyleName }
        );
        
        // Add justification if specified
        if (justification.HasValue)
        {
            paragraphProps.AppendChild(new Justification() { Val = justification.Value });
        }
        
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
    }
    
    return cell;
}
    }
}