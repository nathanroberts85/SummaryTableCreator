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

            Logger.Info($"[+] Using {(useSingleLevelCvss ? "CHECK" : "Non-CHECK")} CVSS rating system");

            // Get phases and group issues by phase
            var phases = results.Where(r => r.IsPhaseHeader).OrderBy(r => r.Reference).ToList();
            var issues = results.Where(r => !r.IsPhaseHeader).ToList();

            using (var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook))
            {
                CreateWorkbook(document, phases, issues, useSingleLevelCvss, showCvssScore, includeRecommendations);
            }

            Logger.Okay("[+] Excel report generation complete");
        }

        private static void CreateWorkbook(SpreadsheetDocument document, List<HeadingResult> phases,
            List<HeadingResult> issues, bool useSingleLevelCvss, bool showCvssScore, bool includeRecommendations)
        {
            // Create workbook and worksheet parts
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            uint sheetId = 1;

            foreach (var phase in phases)
            {
                // Get issues for this phase
                var phaseIssues = issues.Where(i => i.Reference.StartsWith(phase.Reference + "."))
                                       .OrderByDescending(i => i.CvssScore ?? 0)
                                       .ToList();

                // Create worksheet for this phase
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetName = SanitizeSheetName(phase.Issue);

                var sheet = new Sheet()
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = sheetName
                };
                sheets.Append(sheet);

                // Populate worksheet with phase issues
                PopulateWorksheet(worksheetPart, phaseIssues, useSingleLevelCvss, showCvssScore, includeRecommendations);
            }
        }

        private static void PopulateWorksheet(WorksheetPart worksheetPart, List<HeadingResult> phaseIssues,
            bool useSingleLevelCvss, bool showCvssScore, bool includeRecommendations)
        {
            var worksheet = new Worksheet();
            var sheetData = new SheetData();

            // Add header row with the requested columns
            var headerRow = new Row() { RowIndex = 1 };
            headerRow.Append(
                CreateCell("Reference", CellValues.String),
                CreateCell("Risk", CellValues.String),
                CreateCell("Name", CellValues.String),
                CreateCell("Affected Host/IP", CellValues.String),
                CreateCell("Description", CellValues.String),
                CreateCell("Solution", CellValues.String)
            );

            sheetData.Append(headerRow);

            // Add data rows
            uint rowIndex = 2;
            var processedIssues = new HashSet<string>();

            foreach (var issue in phaseIssues)
            {
                if (processedIssues.Contains(issue.Issue))
                    continue;

                processedIssues.Add(issue.Issue);

                var rating = MapRating(issue.CvssScore, useSingleLevelCvss);
                var formattedRating = showCvssScore ? FormatRatingWithScore(rating, issue.CvssScore) : rating;

                // Get solution text (recommendation if enabled, otherwise blank)
                var solutionText = includeRecommendations ? (issue.Recommendation ?? "") : "";

                var dataRow = new Row() { RowIndex = rowIndex++ };
                dataRow.Append(
                    CreateCell(issue.Reference, CellValues.String),        // Reference
                    CreateCell(formattedRating, CellValues.String),        // Risk
                    CreateCell(issue.Issue, CellValues.String),            // Name
                    CreateCell("", CellValues.String),                     // Affected Host/IP (blank)
                    CreateCell("", CellValues.String),                     // Description (blank)
                    CreateCell(solutionText, CellValues.String)            // Solution (recommendations)
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

        // Reuse existing mapping methods from SummaryReportGenerator
        private static string MapRating(double? cvss, bool useSingleLevelCvss)
        {
            if (!cvss.HasValue) return string.Empty;

            if (useSingleLevelCvss)
            {
                if (cvss >= 7.0) return "High";
                if (cvss >= 4.0) return "Medium";
                if (cvss >= 1.0) return "Low";
                return "Informational";
            }
            else
            {
                if (cvss >= 9.0) return "Critical";
                if (cvss >= 7.0) return "High";
                if (cvss >= 4.0) return "Medium";
                if (cvss >= 1.0) return "Low";
                return "Informational";
            }
        }

        private static string FormatRatingWithScore(string rating, double? cvss)
        {
            if (!cvss.HasValue) return rating;
            return $"{rating} ({cvss:F1})";
        }
    }
}