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

        // Paragraph Styles for Headings
        public const string PHASE_TITLE = "HEADING-11";
        public const string SECTION_HEADING = "BODYHEADING";
        public const string BODY_TEXT = "BODYTEXT";


        // Severity Styles
        public const string COLOR_CRITICAL = "TXTSEVCRIT";    // Burgandy Red
        public const string COLOR_HIGH = "TXTSEVHIGH";        // Red
        public const string COLOR_MEDIUM = "TXTSEVMEDIUM";      // Orange
        public const string COLOR_LOW = "TXTSEVLOW";         // Light Blue
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