using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
