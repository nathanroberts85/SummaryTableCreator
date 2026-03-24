namespace SummaryTableCreator.Models
{
    public class HeadingResult
    {
        /// <summary>Heading text or issue title.</summary>
        public string Issue { get; set; }

        /// <summary>Parsed CVSS score (0.0–10.0), null if missing.</summary>
        [Obsolete("Use Severity instead for new format")]
        public double? CvssScore { get; set; }

        /// <summary>Section reference, e.g. "6.1.1".</summary>
        public string Reference { get; set; }

        /// <summary>Recommendation text from the database or blank.</summary>
        [Obsolete("Use IssueDescription instead for new format")]
        public string Recommendation { get; set; }

        /// <summary>True if this is a phase header (Heading2), false for issues (Heading3/4).</summary>
        public bool IsPhaseHeader { get; set; }

        // New properties for updated format
        /// <summary>Severity of the issue (from Heading3 parent).</summary>
        public string Severity { get; set; }

        /// <summary>Issue title (from Heading4).</summary>
        public string Title { get; set; }

        /// <summary>Detailed issue description.</summary>
        public string IssueDescription { get; set; }

        /// <summary>Name of the phase this issue belongs to (Heading2 text).</summary>
        public string PhaseName { get; set; }
    }
}
