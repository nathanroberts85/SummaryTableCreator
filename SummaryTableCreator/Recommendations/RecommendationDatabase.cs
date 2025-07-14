using System.Text.Json;
using SummaryTableCreator.UI;

namespace SummaryTableCreator.Recommendations
{
    public class RecommendationDatabase
    {
        private readonly Dictionary<string, string> _recommendations;
        private const string RECOMMENDATIONS_FILE = "recommendations.json";

        public RecommendationDatabase()
        {
            _recommendations = LoadRecommendations();
        }

        public string GetRecommendation(string issueTitle)
        {
            if (string.IsNullOrWhiteSpace(issueTitle))
                return string.Empty;

            // Try exact match first (case-insensitive)
            var exactMatch = _recommendations.Keys
                .FirstOrDefault(key => string.Equals(key, issueTitle, StringComparison.OrdinalIgnoreCase));
            
            if (exactMatch != null)
                return _recommendations[exactMatch];

            // Try partial matching - find the longest matching key
            var partialMatch = _recommendations.Keys
                .Where(key => issueTitle.Contains(key, StringComparison.OrdinalIgnoreCase) || 
                             key.Contains(issueTitle, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(key => key.Length)
                .FirstOrDefault();

            if (partialMatch != null)
                return _recommendations[partialMatch];

            // Try keyword matching
            var issueWords = issueTitle.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var keywordMatch = _recommendations.Keys
                .Where(key => 
                {
                    var keyWords = key.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return issueWords.Intersect(keyWords).Count() >= 2; // At least 2 matching words
                })
                .OrderByDescending(key => 
                {
                    var keyWords = key.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return issueWords.Intersect(keyWords).Count(); // Order by most matching words
                })
                .FirstOrDefault();

            return keywordMatch != null ? _recommendations[keywordMatch] : string.Empty;
        }

        private Dictionary<string, string> LoadRecommendations()
        {
            try
            {
                if (!File.Exists(RECOMMENDATIONS_FILE))
                {
                    Logger.Warning($"[!] Recommendations file '{RECOMMENDATIONS_FILE}' not found. Recommendations will be empty.");
                    return new Dictionary<string, string>();
                }

                var json = File.ReadAllText(RECOMMENDATIONS_FILE);
                var recommendationList = JsonSerializer.Deserialize<List<RecommendationItem>>(json);
                
                if (recommendationList == null || !recommendationList.Any())
                {
                    Logger.Warning("[!] Recommendations file is empty or invalid format.");
                    return new Dictionary<string, string>();
                }

                var recommendations = recommendationList.ToDictionary(
                    item => item.ISSUE, 
                    item => item.RECOMMENDATION,
                    StringComparer.OrdinalIgnoreCase
                );
                
                //Logger.Info($"[+] Loaded {recommendations.Count} recommendations from database");
                return recommendations;
            }
            catch (JsonException ex)
            {
                Logger.Error($"[!] Invalid JSON in recommendations file: {ex.Message}");
                return new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[!] Failed to load recommendations: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private class RecommendationItem
        {
            public string ISSUE { get; set; } = string.Empty;
            public string RECOMMENDATION { get; set; } = string.Empty;
        }
    }
}