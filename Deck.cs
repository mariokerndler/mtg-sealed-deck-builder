
using Spectre.Console;

namespace SealedDeckBuilder
{
    internal class Deck
    {
        public List<DeckEntry> MainDeck { get; } = [];
        public float DeckRating { get; private set; } = 0.0f;

        internal static async Task<Deck?> GenerateSealedPool(string setCode, int packCount)
        {
            // Fetch cards from Scryfall API
            var scryfallApi = new ScryfallApi();
            var cards = await scryfallApi.FetchCardsForSetAsync(setCode);

            if (cards == null || cards.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Failed to fetch cards for set code: {0}[/]", setCode);
                return null;
            }

            var deck = new Deck();
            var random = new Random();

            var commons = cards.Where(c => c.rarity == "common" && !c.type_line.Contains("Basic Land")).ToList();
            var uncommons = cards.Where(c => c.rarity == "uncommon").ToList();
            var rares = cards.Where(c => c.rarity == "rare").ToList();
            var mythics = cards.Where(r => r.rarity == "mythic").ToList();

            for (int i = 0; i < packCount; i++)
            {
                // Add one rare or mythic rare
                if (mythics.Count > 0 && random.NextDouble() < 0.25)
                {
                    var rareCard = mythics[random.Next(mythics.Count)];
                    deck.MainDeck.Add(new DeckEntry(1, rareCard));
                }
                else if (rares.Count > 0)
                {
                    var rareCard = rares[random.Next(rares.Count)];
                    deck.MainDeck.Add(new DeckEntry(1, rareCard));
                }
                // Add three uncommons
                for (int j = 0; j < 3; j++)
                {
                    if (uncommons.Count > 0)
                    {
                        var uncommonCard = uncommons[random.Next(uncommons.Count)];
                        deck.MainDeck.Add(new DeckEntry(1, uncommonCard));
                    }
                }
                // Add ten commons
                for (int j = 0; j < 10; j++)
                {
                    if (commons.Count > 0)
                    {
                        var commonCard = commons[random.Next(commons.Count)];
                        deck.MainDeck.Add(new DeckEntry(1, commonCard));
                    }
                }
            }

            return deck;
        }
    }

    internal record DeckEntry(int Amount, Card Card);
}
