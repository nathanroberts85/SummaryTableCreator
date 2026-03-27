using System.Text.Json;
using System.Text.Json.Serialization;
using SummaryTableCreator.UI;

namespace SummaryTableCreator.Recommendations
{
    public class RecommendationDatabase
    {
        private readonly Dictionary<string, string> _issueDescriptions;
        private const string FINDINGS_DIRECTORY = "data";

        public RecommendationDatabase()
        {
            _issueDescriptions = LoadIssueDescriptions();
        }

        /// <summary>
        /// Gets the issue table description for a given issue title.
        /// </summary>
        /// <param name="issueTitle">The title of the issue from the document.</param>
        /// <returns>The ISSUE_TABLE text, or empty string if no match found.</returns>
        public string GetRecommendation(string issueTitle)
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

        private Dictionary<string, string> LoadIssueDescriptions()
        {
            var issueDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Check if Findings directory exists
                if (!Directory.Exists(FINDINGS_DIRECTORY))
                {
                    Logger.Warning($"Findings directory '{FINDINGS_DIRECTORY}' not found. Issue descriptions will be empty.");
                    return issueDescriptions;
                }

                // Get all JSON files in the directory
                var jsonFiles = Directory.GetFiles(FINDINGS_DIRECTORY, "*.json");

                if (jsonFiles.Length == 0)
                {
                    Logger.Warning($"No JSON files found in '{FINDINGS_DIRECTORY}' directory.");
                    return issueDescriptions;
                }

                //Logger.Info($"Loading findings from {jsonFiles.Length} JSON file(s)...");

                // Process each JSON file
                foreach (var jsonFile in jsonFiles)
                {
                    var fileName = Path.GetFileName(jsonFile);
                    try
                    {
                        LoadFromFile(jsonFile, issueDescriptions);
                       // Logger.Info($"  Loaded: {fileName}");
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

                //Logger.Info($"Total loaded: {issueDescriptions.Count} issue title variations");
                return issueDescriptions;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load findings: {ex.Message}");
                return issueDescriptions;
            }
        }

        /// <summary>
        /// Loads findings from a single JSON file and adds them to the dictionary.
        /// </summary>
        private void LoadFromFile(string filePath, Dictionary<string, string> issueDescriptions)
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new IssueTitleConverter() }
            };
            var findingsList = JsonSerializer.Deserialize<List<FindingItem>>(json, options);

            if (findingsList == null || !findingsList.Any())
            {
                return;
            }

            foreach (var item in findingsList)
            {
                if (item.ISSUE_TITLE == null || item.ISSUE_TITLE.Count == 0)
                    continue;

                var issueTable = item.ISSUE_TABLE ?? string.Empty;

                // Add all title variations (text field only) as keys pointing to the same ISSUE_TABLE
                foreach (var titleVariation in item.ISSUE_TITLE)
                {
                    // Only use the text field for matching
                    if (!string.IsNullOrWhiteSpace(titleVariation.Text))
                    {
                        if (issueDescriptions.ContainsKey(titleVariation.Text))
                        {
                            Logger.Warning($"  Duplicate title found: '{titleVariation.Text}' (overwriting with latest)");
                        }
                        issueDescriptions[titleVariation.Text] = issueTable;
                    }
                }
            }
        }

        private class FindingItem
        {
            [JsonPropertyName("ISSUE_TITLE")]
            public List<TitleVariation> ISSUE_TITLE { get; set; } = new List<TitleVariation>();

            [JsonPropertyName("ISSUE_TABLE")]
            public string? ISSUE_TABLE { get; set; }
        }

        private class TitleVariation
        {
            [JsonPropertyName("label")]
            public string Label { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }

        /// <summary>
        /// Custom converter to handle ISSUE_TITLE as either:
        /// - Array of strings (old format)
        /// - Array of objects with label/text (new format)
        /// </summary>
        private class IssueTitleConverter : JsonConverter<List<TitleVariation>>
        {
            public override List<TitleVariation> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var variations = new List<TitleVariation>();

                if (reader.TokenType == JsonTokenType.String)
                {
                    // Single string value (old format)
                    var text = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        variations.Add(new TitleVariation { Text = text });
                    }
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray)
                            break;

                        if (reader.TokenType == JsonTokenType.String)
                        {
                            // Old format: array of strings
                            var text = reader.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                variations.Add(new TitleVariation { Text = text });
                            }
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            // New format: array of objects with label/text
                            var variation = JsonSerializer.Deserialize<TitleVariation>(ref reader, options);
                            if (variation != null && !string.IsNullOrWhiteSpace(variation.Text))
                            {
                                variations.Add(variation);
                            }
                        }
                    }
                }

                return variations;
            }

            public override void Write(Utf8JsonWriter writer, List<TitleVariation> value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }
}