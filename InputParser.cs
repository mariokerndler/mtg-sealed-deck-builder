using Spectre.Console;

namespace SealedDeckBuilder
{
    internal class InputParser
    {
        public static async Task<Deck> ParseInput(string inputFule)
        {
            // Prepare the input file
            var cardEntries = File.ReadAllLines(inputFule)
                   .Where(line => !string.IsNullOrWhiteSpace(line))
                   .Select(line => line.Trim())
                   .ToList();

            var scryfallApi = new ScryfallApi();
            var deck = new Deck();

            foreach (var cardEntry in cardEntries)
            {
                // Validate the card entry format: "amount name"
                var split = cardEntry.Split(' ', 2);
                if (split.Length != 2 || !int.TryParse(split[0], out int amount) || string.IsNullOrWhiteSpace(split[1]))
                {
                    AnsiConsole.MarkupLine($"[red]Invalid entry: {cardEntry}[/]");
                    continue;
                }

                var name = split[1].Trim();

                // Check if card already exists in the deck
                var existing = deck.MainDeck.FirstOrDefault(e => e.Card.name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // If card already exists, just increase the amount
                    deck.MainDeck.Remove(existing);
                    deck.MainDeck.Add(new DeckEntry(existing.Amount + amount, existing.Card));
                }
                else
                {
                    var card = await scryfallApi.GetCardByNameAsync(name);

                    if (card != null)
                    {
                        deck.MainDeck.Add(new DeckEntry(amount, card));
                    }
                }
            }

            return deck;
        }
    }
}
