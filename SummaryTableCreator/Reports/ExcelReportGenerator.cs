using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SummaryTableCreator.Models;
using SummaryTableCreator.UI;

namespace SummaryTableCreator.Reports
{
    public static class ExcelReportGenerator
    {
        public static void Generate(string originalPath, IEnumerable<HeadingResult> results,
            bool useSingleLevelCvss = false, bool showCvssScore = false, bool includeRecommendations = true)
        {
            // Determine output path
            var folder = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var outputPath = Path.Combine(folder, "Interim_Report.xlsx");

            Logger.Info($"Using {(useSingleLevelCvss ? "CHECK" : "Non-CHECK")} CVSS rating system");

            // Get phases and issues
            var phases = results.Where(r => r.IsPhaseHeader).ToList();
            var issues = results.Where(r => !r.IsPhaseHeader).ToList();

            using (var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook))
            {
                CreateWorkbook(document, phases, issues, useSingleLevelCvss, includeRecommendations);
            }

            Logger.Okay("Excel report generation complete");
        }

        private static void CreateWorkbook(SpreadsheetDocument document, List<HeadingResult> phases,
            List<HeadingResult> issues, bool useSingleLevelCvss, bool includeRecommendations)
        {
            // Create workbook and worksheet parts
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            uint sheetId = 1;

            foreach (var phase in phases)
            {
                // Get issues for this phase in document order (no sorting)
                var phaseIssues = issues.Where(i => i.PhaseName == phase.PhaseName).ToList();

                // Create worksheet for this phase
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetName = SanitizeSheetName(phase.PhaseName);

                var sheet = new Sheet()
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = sheetName
                };
                sheets.Append(sheet);

                // Populate worksheet with phase issues
                PopulateWorksheet(worksheetPart, phaseIssues, useSingleLevelCvss, includeRecommendations);
            }
        }

        private static void PopulateWorksheet(WorksheetPart worksheetPart, List<HeadingResult> phaseIssues,
            bool useSingleLevelCvss, bool includeRecommendations)
        {
            var worksheet = new Worksheet();
            var sheetData = new SheetData();

            // Add header row
            var headerRow = new Row() { RowIndex = 1 };
            headerRow.Append(
                CreateCell("Reference", CellValues.String),
                CreateCell("Risk", CellValues.String),
                CreateCell("Name", CellValues.String),
                CreateCell("Affected Hosts", CellValues.String),
                CreateCell("Description", CellValues.String),
                CreateCell("Solution", CellValues.String)
            );

            sheetData.Append(headerRow);

            // Add data rows (in document order)
            uint rowIndex = 2;

            foreach (var issue in phaseIssues)
            {
                // Map severity to risk rating (with CHECK mode support)
                var risk = MapSeverityToRisk(issue.Severity, useSingleLevelCvss);

                // Get description and solution from JSON if enabled
                var description = includeRecommendations ? (issue.IssueDescription ?? "") : "";
                var solution = includeRecommendations ? (issue.Recommendation ?? "") : "";

                var dataRow = new Row() { RowIndex = rowIndex++ };
                dataRow.Append(
                    CreateCell(issue.Reference ?? "", CellValues.String),     // Reference (e.g., 6.1.1.1)
                    CreateCell(risk, CellValues.String),                       // Risk (Severity)
                    CreateCell(issue.Title ?? "", CellValues.String),          // Name (Issue Title)
                    CreateCell("", CellValues.String),                         // Affected Hosts (blank)
                    CreateCell(description, CellValues.String),                // Description (ISSUE_TABLE)
                    CreateCell(solution, CellValues.String)                    // Solution (RECOMMENDATION)
                );

                sheetData.Append(dataRow);
            }

            worksheet.Append(sheetData);
            worksheetPart.Worksheet = worksheet;
        }

        private static Cell CreateCell(string text, CellValues dataType)
        {
            return new Cell()
            {
                CellValue = new CellValue(text),
                DataType = dataType
            };
        }

        private static string SanitizeSheetName(string name)
        {
            // Excel sheet names can't contain: \ / ? * [ ]
            // Max length is 31 characters
            var sanitized = name.Replace("\\", "").Replace("/", "").Replace("?", "")
                               .Replace("*", "").Replace("[", "").Replace("]", "");

            return sanitized.Length > 31 ? sanitized.Substring(0, 31) : sanitized;
        }

        /// <summary>
        /// Maps severity string to risk rating, with CHECK mode support.
        /// </summary>
        private static string MapSeverityToRisk(string? severity, bool useSingleLevelCvss)
        {
            if (string.IsNullOrEmpty(severity))
                return string.Empty;

            // Normalize severity for comparison
            var normalizedSeverity = severity.Trim();

            // CHECK mode: treat Critical as High
            if (useSingleLevelCvss && 
                normalizedSeverity.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            {
                return "High";
            }

            // Return severity as-is (case-preserved)
            return normalizedSeverity;
        }
    }
}