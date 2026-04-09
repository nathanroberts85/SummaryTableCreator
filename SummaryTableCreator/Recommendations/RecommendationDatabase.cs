using System.Text.Json;
using System.Text.Json.Serialization;
using SummaryTableCreator.UI;

namespace SummaryTableCreator.Recommendations
{
    public class RecommendationDatabase
    {
        private readonly Dictionary<string, string> _issueDescriptions;
        private readonly Dictionary<string, string> _recommendations;
        private const string FINDINGS_DIRECTORY = "data";

        public RecommendationDatabase()
        {
            _issueDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _recommendations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadIssueDescriptions();
        }

        /// <summary>
        /// Reloads all JSON files from the data directory.
        /// </summary>
        public void Reload()
        {
            _issueDescriptions.Clear();
            _recommendations.Clear();
            LoadIssueDescriptions();
        }

        /// <summary>
        /// Gets the issue table description for a given issue title.
        /// </summary>
        /// <param name="issueTitle">The title of the issue from the document.</param>
        /// <returns>The ISSUE_TABLE text, or empty string if no match found.</returns>
        public string GetIssueDescription(string issueTitle)
        {
            if (string.IsNullOrWhiteSpace(issueTitle))
                return string.Empty;

            // Try exact match only (case-insensitive)
            var exactMatch = _issueDescriptions.Keys
                .FirstOrDefault(key => string.Equals(key, issueTitle, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                return ProcessNewlines(_issueDescriptions[exactMatch]);
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the recommendation for a given issue title.
        /// </summary>
        /// <param name="issueTitle">The title of the issue from the document.</param>
        /// <returns>The RECOMMENDATION text, or empty string if no match found.</returns>
        public string GetRecommendation(string issueTitle)
        {
            if (string.IsNullOrWhiteSpace(issueTitle))
                return string.Empty;

            // Try exact match only (case-insensitive)
            var exactMatch = _recommendations.Keys
                .FirstOrDefault(key => string.Equals(key, issueTitle, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                return ProcessNewlines(_recommendations[exactMatch]);
            }

            return string.Empty;
        }

        /// <summary>
        /// Converts literal \n and \r escape sequences to actual newlines.
        /// </summary>
        private string ProcessNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Replace literal \n\n with paragraph breaks
            text = text.Replace("\\n\\n", "\n\n");
            // Replace literal \n with single newline
            text = text.Replace("\\n", "\n");
            // Replace literal \r\n with newline
            text = text.Replace("\\r\\n", "\n");

            return text;
        }

        private void LoadIssueDescriptions()
        {
            try
            {
                // Check if Findings directory exists
                if (!Directory.Exists(FINDINGS_DIRECTORY))
                {
                    Logger.Warning($"Findings directory '{FINDINGS_DIRECTORY}' not found. Issue descriptions will be empty.");
                    return;
                }

                // Get all JSON files in the directory
                var jsonFiles = Directory.GetFiles(FINDINGS_DIRECTORY, "*.json");

                if (jsonFiles.Length == 0)
                {
                    Logger.Warning($"No JSON files found in '{FINDINGS_DIRECTORY}' directory.");
                    return;
                }

                // Process each JSON file
                foreach (var jsonFile in jsonFiles)
                {
                    var fileName = Path.GetFileName(jsonFile);
                    try
                    {
                        LoadFromFile(jsonFile, fileName);
                    }
                    catch (JsonException ex)
                    {
                        Logger.Error($"  Invalid JSON in '{fileName}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"  Failed to load '{fileName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load findings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads findings from a single JSON file and adds them to the dictionaries.
        /// </summary>
        private void LoadFromFile(string filePath, string fileName)
        {
            var json = File.ReadAllText(filePath);
            
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                Logger.Warning($"  '{fileName}' root element is not an array, skipping.");
                return;
            }

            int entryIndex = 0;
            foreach (var element in root.EnumerateArray())
            {
                entryIndex++;
                try
                {
                    // Parse ISSUE_TITLE (can be string or array of objects)
                    List<string> issueTitles = new List<string>();
                    
                    if (element.TryGetProperty("ISSUE_TITLE", out var issueTitleProp))
                    {
                        if (issueTitleProp.ValueKind == JsonValueKind.String)
                        {
                            // Simple string format
                            var title = issueTitleProp.GetString();
                            if (!string.IsNullOrWhiteSpace(title))
                                issueTitles.Add(title);
                        }
                        else if (issueTitleProp.ValueKind == JsonValueKind.Array)
                        {
                            // Array format (can be strings or objects)
                            foreach (var titleElement in issueTitleProp.EnumerateArray())
                            {
                                if (titleElement.ValueKind == JsonValueKind.String)
                                {
                                    var title = titleElement.GetString();
                                    if (!string.IsNullOrWhiteSpace(title))
                                        issueTitles.Add(title);
                                }
                                else if (titleElement.ValueKind == JsonValueKind.Object)
                                {
                                    // Object with "text" property
                                    if (titleElement.TryGetProperty("text", out var textProp) && 
                                        textProp.ValueKind == JsonValueKind.String)
                                    {
                                        var title = textProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(title))
                                            issueTitles.Add(title);
                                    }
                                }
                            }
                        }
                    }

                    if (issueTitles.Count == 0)
                    {
                        Logger.Warning($"  Entry {entryIndex} in '{fileName}' has no valid ISSUE_TITLE, skipping.");
                        continue;
                    }

                    // Get ISSUE_TABLE (always a string, can be empty)
                    string issueTable = string.Empty;
                    if (element.TryGetProperty("ISSUE_TABLE", out var issueTableProp))
                    {
                        if (issueTableProp.ValueKind == JsonValueKind.String)
                        {
                            issueTable = issueTableProp.GetString() ?? string.Empty;
                        }
                    }

                    // Get RECOMMENDATION (can be string or array - take first item if array)
                    string recommendation = string.Empty;
                    if (element.TryGetProperty("RECOMMENDATION", out var recProp))
                    {
                        if (recProp.ValueKind == JsonValueKind.String)
                        {
                            recommendation = recProp.GetString() ?? string.Empty;
                        }
                        else if (recProp.ValueKind == JsonValueKind.Array)
                        {
                            // Take the first item from the array
                            foreach (var item in recProp.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    recommendation = item.GetString() ?? string.Empty;
                                    break; // Take only the first item
                                }
                            }
                        }
                        else if (recProp.ValueKind == JsonValueKind.Null)
                        {
                            recommendation = string.Empty;
                        }
                        else
                        {
                            Logger.Warning($"  Entry {entryIndex} in '{fileName}' has unexpected RECOMMENDATION type: {recProp.ValueKind}, using empty string.");
                        }
                    }

                    // Add all title variations to both dictionaries
                    foreach (var title in issueTitles)
                    {
                        if (_issueDescriptions.ContainsKey(title))
                        {
                            Logger.Warning($"  Duplicate title found: '{title}' in '{fileName}' (overwriting with latest)");
                        }
                        _issueDescriptions[title] = issueTable;
                        _recommendations[title] = recommendation;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"  Error parsing entry {entryIndex} in '{fileName}': {ex.Message}");
                    continue; // Skip this entry and continue with the next
                }
            }
        }
    }
}