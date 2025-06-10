using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SealedDeckBuilder
{
    internal class ScryfallApi
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerSettings _jsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public ScryfallApi()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SealedDeckBuilder/1.0");
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<Card?> GetCardByNameAsync(string name)
        {
            var url = $"https://api.scryfall.com/cards/named?exact={Uri.EscapeDataString(name)}";

            try
            {
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var card = JsonConvert.DeserializeObject<Card>(json, _jsonSettings);
                return card;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<List<string>?> FetchKeywordListAsync()
        {
            var endpoints = new[]
            {
                "https://api.scryfall.com/catalog/keyword-abilities",
                "https://api.scryfall.com/catalog/keyword-actions",
                "https://api.scryfall.com/catalog/ability-words"
            };

            var allKeywords = new List<string>();

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _http.GetStringAsync(endpoint);
                    using var doc = JsonDocument.Parse(response);
                    if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        foreach (var item in dataElement.EnumerateArray())
                            allKeywords.Add(item.GetString() ?? "");
                    }
                }
                catch
                {
                    // Ignore individual failures
                }
            }

            return allKeywords.Distinct().Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        }

        public async Task<List<Card>?> FetchCardsForSetAsync(string setCode)
        {
            var url = $"https://api.scryfall.com/cards/search?sort=rarity&q=set:{setCode.ToLower()}";
            var hasMore = true;
            var cards = new List<Card>();

            try
            {
                while (hasMore)
                {
                    var response = await _http.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                        return null;

                    var json = await response.Content.ReadAsStringAsync();
                    var cardSet = JsonConvert.DeserializeObject<CardSet>(json, _jsonSettings);
                    if (cardSet == null || cardSet.data == null || cardSet.data.Count == 0)
                        return null;

                    cards.AddRange(cardSet.data);
                    hasMore = cardSet.has_more;
                    url = cardSet.next_page;
                }
            }
            catch
            { }

            return cards;
        }

        public class CardSet
        {
            public int total_cards { get; set; } = 0;
            public bool has_more { get; set; } = false;
            public string next_page { get; set; } = string.Empty;
            public List<Card> data { get; set; } = [];
        }
    }
}
