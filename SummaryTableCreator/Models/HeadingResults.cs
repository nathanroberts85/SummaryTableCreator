namespace SummaryTableCreator.Models
{
    public class HeadingResult
    {
        /// <summary>Heading text or issue description.</summary>
        public string Issue { get; set; }

        /// <summary>Parsed CVSS score (0.0–10.0), null if missing.</summary>
        public double? CvssScore { get; set; }

        /// <summary>Section reference, e.g. "6.1.1".</summary>
        public string Reference { get; set; }

        /// <summary>Recommendation text from the database or blank.</summary>
        public string Recommendation { get; set; }

        /// <summary>True if this is a phase header (Heading2), false for issues (Heading3).</summary>
        public bool IsPhaseHeader { get; set; }
    }
}
