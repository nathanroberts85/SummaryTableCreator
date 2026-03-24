namespace SummaryTableCreator.Reports
{
    /// <summary>
    /// Constants for Word document style names used in report generation.
    /// These styles should be defined in the Word template or applied programmatically.
    /// </summary>
    public static class ReportStyles
    {
        // Style names from the Word template - update these to match your template
        public const string TABLE_STYLE = "TBLAFFECTEDHOSTS";
        public const string HEADER_STYLE = "TABLEHEADER";
        public const string CELL_STYLE = "TABLECELL";

        // Table Dimensions
        public const decimal ROW_HEIGHT = 566.893M; // Row height in twips

        // Issue Summary Table Column Widths (in twips)
        public const decimal SUMMARY_COL_PHASE = 3605.442M;      // 6.3cm
        public const decimal SUMMARY_COL_CRITICAL = 1774.376M;   // 3.13cm
        public const decimal SUMMARY_COL_HIGH = 1774.376M;       // 3.13cm
        public const decimal SUMMARY_COL_MEDIUM = 1774.376M;     // 3.13cm
        public const decimal SUMMARY_COL_LOW = 1774.376M;        // 3.13cm
        public const decimal SUMMARY_COL_INFO = 1774.376M;       // 3.13cm
        public const decimal SUMMARY_COL_TOTAL = 1774.376M;      // 3.13cm

        // Findings Table Column Widths (in twips)
        public const decimal FINDINGS_COL_RISK_RATING = 1984.13M;      // 3.5cm
        public const decimal FINDINGS_COL_ISSUE = 3123.583M;            // 5.51cm
        public const decimal FINDINGS_COL_REFERENCE = 3112.245M;        // 5.49cm
        public const decimal FINDINGS_COL_DESCRIPTION = 6609.9773M;      // 11.66cm


        // Paragraph Styles for Headings
        public const string PHASE_TITLE = "HEADING-11";
        public const string SECTION_HEADING = "BODYHEADING";
        public const string BODY_TEXT = "BODYTEXT";


        // Severity Styles
        public const string COLOR_CRITICAL = "TXTSEVCRIT";    // Burgandy Red
        public const string COLOR_HIGH = "TXTSEVHIGH";        // Red
        public const string COLOR_MEDIUM = "TXTSEVMEDIUM";    // Orange
        public const string COLOR_LOW = "TXTSEVLOW";          // Light Blue
        public const string COLOR_INFORMATIONAL = "TXTSEVINFO"; // Grey


        /// <summary>
        /// Maps severity text to color code.
        /// </summary>
        public static string GetSeverityColor(string severity)
        {
            return severity?.ToLower() switch
            {
                "critical" => COLOR_CRITICAL,
                "high" => COLOR_HIGH,
                "medium" => COLOR_MEDIUM,
                "low" => COLOR_LOW,
                "informational" or "info" => COLOR_INFORMATIONAL,
                _ => "00b050" // Green for remediated or unknown severity
            };
        }
    }
}