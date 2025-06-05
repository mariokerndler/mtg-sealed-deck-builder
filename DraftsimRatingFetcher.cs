using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace SealedDeckBuilder
{
    public class DraftsimCardRating
    {
        private string _name = string.Empty;
        public string name
        {
            get => _name;
            set
            {
                if (value != null && value is string str)
                {
                    var n = str.Trim();
                    _name = n.Replace('_', ' ');
                }
            }
        }
        public float myrating { get; set; } = 0f;
    }

    internal class DraftsimRatingFetcher
    {
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static async Task<List<DraftsimCardRating>> FetchRatingsAsync(string setCode)
        {
            var url = $"https://draftsim.com/generated/{setCode}.js";

            using var httpClient = new HttpClient();
            var jsContent = await httpClient.GetStringAsync(url);

            var match = Regex.Match(jsContent, @"var\s+\w+\s+=\s+(\[.*\]);?", RegexOptions.Singleline);
            if (!match.Success)
                throw new Exception("Could not parse ratings JS file.");

            var jsonArray = match.Groups[1].Value;

            // Parse JSON into list of rating objects
            var ratings = JsonConvert.DeserializeObject<List<DraftsimCardRating>>(jsonArray, _jsonSettings);

            return ratings ?? [];
        }
    }
}
