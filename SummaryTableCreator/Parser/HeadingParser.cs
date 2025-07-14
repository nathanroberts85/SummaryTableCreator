// File: Parser/HeadingParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SummaryTableCreator.Models;
using SummaryTableCreator.UI;
using SummaryTableCreator.Recommendations;

namespace SummaryTableCreator.Parser
{
    /// <summary>
    /// Parses a Word document by simulating the document flow: detecting Heading1, Heading2, and Heading3
    /// and generating references dynamically based on the user-provided top-level section number.
    /// </summary>
    public class HeadingParser
    {
        private RecommendationDatabase? _recommendationDb;

        public HeadingParser()
        {
            // Don't create the database in constructor
        }

        private RecommendationDatabase GetRecommendationDatabase()
        {
            if (_recommendationDb == null)
            {
                _recommendationDb = new RecommendationDatabase();
            }
            return _recommendationDb;
        }

        /// <summary>
        /// Parses the document for issues under the specified section number, incrementing phase and issue counters
        /// according to Heading2 and Heading3 styles, and extracts CVSS scores from the following table.
        /// </summary>
        /// <param name="filePath">Full path to the .docx file.</param>
        /// <param name="sectionNumber">Top-level section number to target (e.g. 6).</param>
        /// <returns>Enumerable of parsed HeadingResult entries.</returns>
        public IEnumerable<HeadingResult> Parse(string filePath, int sectionNumber)
        {
            //Logger.Info($"Parsing document via Open XML: {filePath}");

            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart.Document.Body;
            var elements = body.Elements<OpenXmlElement>()
                .Where(e => e is Paragraph || e is Table)
                .ToList();

            bool insideSection = false;
            int phaseCounter = 0;
            int issueCounter = 0;
            int heading1Count = 0;

            foreach (var el in elements)
            {
                if (el is Paragraph para)
                {
                    var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    var text = para.InnerText?.Trim() ?? string.Empty;

                    // Count Heading1 occurrences to detect section by index
                    if (string.Equals(style, "Heading1", StringComparison.OrdinalIgnoreCase))
                    {
                        heading1Count++;
                        if (!insideSection && heading1Count == sectionNumber)
                        {
                            insideSection = true;
                            Logger.Info($"[+] Entering section {sectionNumber} (Heading1 occurrence #{heading1Count})");
                            continue;
                        }
                        else if (insideSection && heading1Count > sectionNumber)
                        {
                            Logger.Info($"[+] Exiting section {sectionNumber}");
                            break;
                        }
                    }

                    if (insideSection)
                    {
                        // New phase (Heading2)
                        if (string.Equals(style, "Heading2", StringComparison.OrdinalIgnoreCase))
                        {
                            phaseCounter++;
                            issueCounter = 0;
                            
                            // Capture Heading2 data for summary table
                            yield return new HeadingResult
                            {
                                Issue = text.Trim(),
                                Reference = $"{sectionNumber}.{phaseCounter}",
                                CvssScore = null,
                                Recommendation = string.Empty,
                                IsPhaseHeader = true
                            };
                            
                            Logger.Info($"[+] Phase {phaseCounter} started");
                            continue;
                        }

                        // New issue (Heading3)
                        if (string.Equals(style, "Heading3", StringComparison.OrdinalIgnoreCase))
                        {
                            issueCounter++;
                            var reference = $"{sectionNumber}.{phaseCounter}.{issueCounter}";
                            var issueText = text.Split(new[] { ',' }, 2)[0].Trim();
                            Logger.Okay($"{reference}: {issueText}");

                            // Locate next table for CVSS
                            double? cvss = null;
                            var next = para.NextSibling();
                            while (next != null && !(next is Table))
                                next = next.NextSibling();
                            if (next is Table table)
                                cvss = ExtractCvssFromTable(table);

                            // Get recommendation from database (lazy initialization)
                            var recommendation = GetRecommendationDatabase().GetRecommendation(issueText);

                            yield return new HeadingResult
                            {
                                Issue = issueText,
                                Reference = reference,
                                CvssScore = cvss,
                                Recommendation = recommendation,
                                IsPhaseHeader = false
                            };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts the first token of the heading text as the numeric reference.
        /// </summary>
        private string ExtractReference(string headingText)
        {
            var token = headingText.Split(new[] { ' ', '\t' }, 2)[0];
            return Regex.IsMatch(token, "^\\d+(\\.\\d+)*$") ? token : string.Empty;
        }

        /// <summary>
        /// From the given Table, reads the CVSS score in row index 2, column index 1,
        /// taking the first numeric token.
        /// </summary>
        private double? ExtractCvssFromTable(Table table)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 3) return null;
            var targetRow = rows[2];
            var cells = targetRow.Elements<TableCell>().ToList();
            if (cells.Count < 2) return null;
            var rawText = string.Concat(
                cells[1].Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text)
            ).Trim();
            var firstToken = rawText.Split(new[] { ' ' }, 2)[0];
            return double.TryParse(firstToken, out var score) ? score : (double?)null;
        }
    }
}
