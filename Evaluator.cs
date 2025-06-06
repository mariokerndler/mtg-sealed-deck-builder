namespace SealedDeckBuilder
{
    internal class Evaluator
    {
        public static Dictionary<string, int> GetKeywordCount(Deck deck)
        {
            return deck.MainDeck
                .SelectMany(e => e.Card.keywords.Where(k => e.Card.oracle_text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(k => k)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public static Dictionary<string, int> GetKindredCount(Deck deck)
        {
            return deck.MainDeck
                .SelectMany(e => e.Card.type_line.Split('—').LastOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
                .GroupBy(k => k)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public static float EvaluateCard(Card card, Dictionary<string, float> ratings, Dictionary<string, int> keywordCounts, Dictionary<string, int> kindredCount)
        {
            // Apply rating for card
            float score = ratings.TryGetValue(card.name, out var baseRating) ? baseRating : 1.0f;

            // Prefer 2-4 drops
            if (card.type_line.Contains("Creature") && card.cmc >= 2 && card.cmc <= 4)
                score += 0.2f;

            // Removal count: prioritize interaction
            string[] removalKeywords = ["destroy", "exile", "damage", "fight", "deathtouch"];
            if (removalKeywords.Any(k => card.oracle_text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                score += 0.4f;

            // Keyword matching
            foreach (var keyword in keywordCounts.Keys)
            {
                if (card.oracle_text.Contains(keyword, StringComparison.OrdinalIgnoreCase) && keywordCounts[keyword] > 1)
                    score += 0.15f * keywordCounts[keyword];
            }

            // Kindred boost
            var creatureTypes = card.type_line.Split('—').LastOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            foreach (var type in creatureTypes)
            {
                if (kindredCount.TryGetValue(type, out var count) && count >= 3)
                    score += 0.2f;
            }

            // Fixing/Land Support
            if (card.type_line.Contains("Land") &&
                (card.oracle_text.Contains("Add one mana of any color", StringComparison.OrdinalIgnoreCase) ||
                card.oracle_text.Contains("Add", StringComparison.OrdinalIgnoreCase) && card.oracle_text.Contains("mana")))
            {
                score += 0.3f;
            }

            return score;
        }

    }
}
