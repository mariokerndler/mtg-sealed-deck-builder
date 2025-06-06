using Spectre.Console;

namespace SealedDeckBuilder
{
    internal class DeckBuilder
    {
        public static Deck BuildDeck(Deck pool, List<DraftsimCardRating> ratings, List<string> keywords)
        {
            var deck = new Deck();

            var keywordCounts = Evaluator.GetKeywordCount(pool);
            var kindredCounts = Evaluator.GetKindredCount(pool);
            var ratingDict = CreateRatingDictionary(ratings);

            // Evaluate Cards
            var evaluatedCards = pool.MainDeck
                .Select(e => new { Entry = e, Score = Evaluator.EvaluateCard(e.Card, ratingDict, keywordCounts, kindredCounts) })
                .OrderByDescending(e => e.Score)
                .ToList();

            foreach (var entry in evaluatedCards)
            {
                AnsiConsole.MarkupLine($"[green]{entry.Entry.Amount}x {entry.Entry.Card.name} - Score: {entry.Score:F2}[/]");
            }

            // Build deck

            return deck;
        }

        private static Dictionary<string, float> CreateRatingDictionary(List<DraftsimCardRating> ratings)
        {
            return ratings.ToDictionary(c => c.name, c => c.myrating);
        }
    }
}
