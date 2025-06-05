using Newtonsoft.Json;
using System.Net.Http.Headers;

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
    }
}
