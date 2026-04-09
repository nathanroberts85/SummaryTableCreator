// File: Parser/HeadingParser.cs
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SummaryTableCreator.Models;
using SummaryTableCreator.Recommendations;
using SummaryTableCreator.UI;
using System.Text.RegularExpressions;

namespace SummaryTableCreator.Parser
{
    /// <summary>
    /// Parses a Word document by detecting Heading1 (sections), Heading2 (phases), 
    /// Heading3 (severity), and Heading4 (findings).
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
        /// Reloads the recommendation database from JSON files.
        /// </summary>
        public void ReloadDatabase()
        {
            GetRecommendationDatabase().Reload();
        }

        /// <summary>
        /// Parses the document for findings under the specified section number.
        /// New format: Heading2 = Phase, Heading3 = Severity, Heading4 = Finding
        /// </summary>
        /// <param name="filePath">Full path to the .docx file.</param>
        /// <param name="sectionNumber">Top-level section number to target (e.g. 6).</param>
        /// <param name="includeRecommendations">Whether to fetch recommendations from the database.</param>
        /// <returns>Enumerable of parsed HeadingResult entries.</returns>
        public IEnumerable<HeadingResult> Parse(string filePath, int sectionNumber, bool includeRecommendations = true)
        {
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
                
                // Get all elements, including those inside content controls
                var allElements = GetAllElements(body).ToList();

                bool insideSection = false;
                int heading1Count = 0;
                int phaseCounter = 0;
                int severityCounter = 0;
                int findingCounter = 0;
                
                string currentPhase = string.Empty;
                string currentSeverity = string.Empty;

                foreach (var item in allElements)
                {
                    if (item.Element is Paragraph para)
                    {
                        var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                        var text = para.InnerText?.Trim() ?? string.Empty;
                        var outlineLevel = para.ParagraphProperties?.OutlineLevel?.Val?.Value;

                        // Count Heading1 occurrences to detect section by index
                        if ((string.Equals(style, "Heading1", StringComparison.OrdinalIgnoreCase) ||
                            outlineLevel == 0) &&
                            !string.IsNullOrWhiteSpace(text))
                        {
                            heading1Count++;
                            if (!insideSection && heading1Count == sectionNumber)
                            {
                                insideSection = true;
                                Logger.Info($"\n[+] Entering section {sectionNumber}");
                                continue;
                            }
                            else if (insideSection && heading1Count > sectionNumber)
                            {
                                Logger.Info($"\n[+] Exiting section {sectionNumber}");
                                break;
                            }
                        }

                        if (insideSection)
                        {
                            // Determine heading level by style first, then fall back to text analysis
                            var headingLevel = DetermineHeadingLevel(style, outlineLevel, text, sectionNumber);

                            if (headingLevel == 4) // Heading4 (Finding)
                            {
                                findingCounter++;
                                var findingTitle = CleanHeadingText(text, sectionNumber, 4);
                                var reference = $"{sectionNumber}.{phaseCounter}.{severityCounter}.{findingCounter}";
                                
                                Logger.Okay($"    {reference}: {findingTitle}");

                                // Get issue data from database if enabled
                                string issueDescription = string.Empty;
                                string recommendation = string.Empty;
                                
                                if (includeRecommendations)
                                {
                                    var db = GetRecommendationDatabase();
                                    issueDescription = db.GetIssueDescription(findingTitle);
                                    recommendation = db.GetRecommendation(findingTitle);
                                }

                                yield return new HeadingResult
                                {
                                    Issue = findingTitle,
                                    Reference = reference,
                                    IsPhaseHeader = false,
                                    PhaseName = currentPhase,
                                    Severity = currentSeverity,
                                    Title = findingTitle,
                                    IssueDescription = issueDescription,
                                    CvssScore = null,
                                    Recommendation = recommendation
                                };
                            }
                            else if (headingLevel == 3) // Heading3 (Severity)
                            {
                                severityCounter++;
                                findingCounter = 0;
                                currentSeverity = CleanHeadingText(text, sectionNumber, 3);

                                Logger.Info($"\n  [+] Severity: {currentSeverity}");
                            }
                            else if (headingLevel == 2) // Heading2 (Phase)
                            {
                                phaseCounter++;
                                severityCounter = 0;
                                findingCounter = 0;
                                currentPhase = CleanHeadingText(text, sectionNumber, 2);

                                yield return new HeadingResult
                                {
                                    Issue = currentPhase,
                                    Reference = $"{sectionNumber}.{phaseCounter}",
                                    IsPhaseHeader = true,
                                    PhaseName = currentPhase,
                                    Title = currentPhase,
                                    Severity = string.Empty,
                                    IssueDescription = string.Empty
                                };

                                Logger.Info($"\n[+] Phase {phaseCounter}: {currentPhase}");
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
        /// Determines the heading level based on style, outline level, or text pattern.
        /// Returns 0 if not a heading, 2-4 for Heading2-4.
        /// </summary>
        private int DetermineHeadingLevel(string? style, int? outlineLevel, string text, int sectionNumber)
        {
            // Check explicit style/outline level (primary method)
            if (string.Equals(style, "Heading4", StringComparison.OrdinalIgnoreCase) || outlineLevel == 3)
                return 4;
            if (string.Equals(style, "Heading3", StringComparison.OrdinalIgnoreCase) || outlineLevel == 2)
                return 3;
            if (string.Equals(style, "Heading2", StringComparison.OrdinalIgnoreCase) || outlineLevel == 1)
                return 2;

            // Fall back to text pattern matching for edge cases (check most specific first)
            // Pattern: 6.1.2 Severity (just the word, like "High", "Medium", "Low")
            if (Regex.IsMatch(text, $@"^{sectionNumber}\.\d+\.\d+\s+(High|Medium|Low|Critical|Informational)$", RegexOptions.IgnoreCase))
                return 3;
            
            // Pattern: 6.1 Phase Name
            if (Regex.IsMatch(text, $@"^{sectionNumber}\.\d+\s+\S"))
                return 2;

            return 0; // Not a heading
        }

        /// <summary>
        /// Removes numbering prefix from heading text.
        /// </summary>
        private string CleanHeadingText(string text, int sectionNumber, int headingLevel)
        {
            string pattern = headingLevel switch
            {
                2 => $@"^{sectionNumber}\.\d+\s+",           // 6.1 Phase
                3 => $@"^{sectionNumber}\.\d+\.\d+\s+",      // 6.1.2 Severity
                4 => $@"^{sectionNumber}\.\d+\.\d+\.\d+\s+", // 6.1.2.3 Finding
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(pattern))
            {
                return Regex.Replace(text, pattern, string.Empty).Trim();
            }

            return text.Trim();
        }

        /// <summary>
        /// Recursively gets all elements from the body, including those inside content controls.
        /// Returns both the element and its parent for navigation purposes.
        /// </summary>
        private IEnumerable<(OpenXmlElement Element, OpenXmlElement? Parent)> GetAllElements(OpenXmlElement element, OpenXmlElement? parent = null)
        {
            foreach (var child in element.Elements())
            {
                // If it's a paragraph or table, yield it
                if (child is Paragraph || child is Table)
                {
                    yield return (child, parent);
                }
                // If it's a content control (SdtElement), recursively process its contents
                else if (child is SdtElement)
                {
                    foreach (var nested in GetAllElements(child, element))
                    {
                        yield return nested;
                    }
                }
                // For other containers, recursively process
                else if (child.HasChildren)
                {
                    foreach (var nested in GetAllElements(child, element))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>
        /// Extracts the issue description from paragraphs following the heading.
        /// Stops at the next heading or after a certain number of paragraphs.
        /// </summary>
        private string ExtractIssueDescription(OpenXmlElement headingElement)
        {
            var descriptionParts = new List<string>();
            var next = headingElement.NextSibling();
            int maxParagraphs = 10;
            int count = 0;

            while (next != null && count < maxParagraphs)
            {
                // Handle paragraphs directly
                if (next is Paragraph para)
                {
                    var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    var outlineLevel = para.ParagraphProperties?.OutlineLevel?.Val?.Value;
                    
                    if (style != null && (
                        style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ||
                        outlineLevel.HasValue))
                    {
                        break;
                    }

                    var text = para.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        descriptionParts.Add(text);
                    }
                    count++;
                }
                // Handle content controls that might contain paragraphs
                else if (next is SdtElement sdt)
                {
                    var innerParas = sdt.Descendants<Paragraph>();
                    foreach (var innerPara in innerParas)
                    {
                        var style = innerPara.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                        var outlineLevel = innerPara.ParagraphProperties?.OutlineLevel?.Val?.Value;
                        
                        if (style != null && (
                            style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ||
                            outlineLevel.HasValue))
                        {
                            return string.Join(" ", descriptionParts);
                        }

                        var text = innerPara.InnerText?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            descriptionParts.Add(text);
                        }
                        count++;
                        
                        if (count >= maxParagraphs) break;
                    }
                }
                else if (next is Table)
                {
                    break;
                }

                next = next.NextSibling();
            }

            return string.Join(" ", descriptionParts);
        }

        /// <summary>
        /// From the given Table, reads the CVSS score in row index 2, column index 1,
        /// taking the first numeric token.
        /// [DEPRECATED - kept for backward compatibility]
        /// </summary>
        [Obsolete("CVSS extraction no longer used in new format")]
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
