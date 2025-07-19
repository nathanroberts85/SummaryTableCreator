// File: Parser/HeadingParser.cs
using System;
using System.Collections.Generic;
using System.IO;
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

            string actualFilePath = filePath;
            string? tempFilePath = null;

            try
            {
                // Try to open the file directly first
                using var testDoc = WordprocessingDocument.Open(filePath, false);
                testDoc.Dispose();
            }
            catch (IOException)
            {
                // File is locked, create a temporary copy
                tempFilePath = Path.GetTempFileName();
                File.Delete(tempFilePath); // Delete the empty temp file
                tempFilePath = Path.ChangeExtension(tempFilePath, ".docx");
                File.Copy(filePath, tempFilePath, true);
                actualFilePath = tempFilePath;
                Logger.Warning($"Document is open in Word - using temporary copy for parsing");
            }

            try
            {
                using var doc = WordprocessingDocument.Open(actualFilePath, false);
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
                    var outlineLevel = para.ParagraphProperties?.OutlineLevel?.Val?.Value;

                    // Count Heading1 occurrences to detect section by index
                    if ((string.Equals(style, "Heading1", StringComparison.OrdinalIgnoreCase) || 
                        outlineLevel == 0) && // Outline level is 0-indexed, so level 1 = value 0
                        !string.IsNullOrWhiteSpace(text)) // Skip empty headings
                    {
                        heading1Count++;
                        if (!insideSection && heading1Count == sectionNumber)
                        {
                            insideSection = true;
                            Logger.Warning($"\n[+] Entering section {sectionNumber}");
                            continue;
                        }
                        else if (insideSection && heading1Count > sectionNumber)
                        {
                            Logger.Warning($"\n[+] Exiting section {sectionNumber}");
                            break;
                        }
                    }

                    if (insideSection)
                    {
                        // New phase (Heading2 or outline level 2)
                        if ((string.Equals(style, "Heading2", StringComparison.OrdinalIgnoreCase) || 
                            outlineLevel == 1) && // Outline level is 0-indexed, so level 2 = value 1
                            !string.IsNullOrWhiteSpace(text)) // Skip empty headings
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

                            Logger.Info($"\n[+] Phase {phaseCounter}");
                            continue;
                        }

                        // New issue (Heading3 or outline level 3)
                        if ((string.Equals(style, "Heading3", StringComparison.OrdinalIgnoreCase) || 
                            outlineLevel == 2) && // Outline level is 0-indexed, so level 3 = value 2
                            !string.IsNullOrWhiteSpace(text)) // Skip empty headings
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
            finally
            {
                // Clean up temporary file if we created one
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Could not delete temporary file: {ex.Message}");
                    }
                }
            }
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
