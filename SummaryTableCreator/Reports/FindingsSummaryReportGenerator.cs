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
            var outputPath = Path.Combine(folder, "SummaryTableCreator.Summary_of_Findings.docx");

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

            // Group findings by phase
            var phases = results
                .Where(r => r.IsPhaseHeader)
                .OrderBy(r => r.Reference)
                .ToList();

            int phaseNumber = 1;
            foreach (var phase in phases)
            {
                // Get all findings for this phase
                var findings = results
                    .Where(r => !r.IsPhaseHeader && r.PhaseName == phase.PhaseName)
                    .OrderBy(r => r.Reference)
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

            // Column headers
            var headers = new[] { "Risk Rating", "Issue", "Report Reference", "Issue Description" };

            foreach (var header in headers)
            {
                var cell = CreateStyledCell(header, ReportStyles.HEADER_STYLE, false, null);
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

            // Severity cell (with severity-specific paragraph style)
            row.AppendChild(CreateStyledCell(finding.Severity ?? "", ReportStyles.CELL_STYLE, true, finding.Severity));

            // Title cell (bold)
            row.AppendChild(CreateStyledCell(finding.Title ?? "", ReportStyles.CELL_STYLE, false, null, isBold: true));

            // Reference cell
            row.AppendChild(CreateStyledCell(finding.Reference ?? "", ReportStyles.CELL_STYLE, false, null));

            // Issue Description cell
            row.AppendChild(CreateStyledCell(finding.IssueDescription ?? "", ReportStyles.CELL_STYLE, false, null));

            table.AppendChild(row);
        }

        /// <summary>
        /// Creates a table cell with a specified paragraph style and optional severity formatting.
        /// </summary>
        private static TableCell CreateStyledCell(string text, string styleName, bool applySeverityStyle, string? severity, bool isBold = false)
        {
            var cell = new TableCell();

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