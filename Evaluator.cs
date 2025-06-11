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

            // Evaluate removal power
            bool isInstantOrFast = card.type_line.Contains("Instant") ||
                                   card.oracle_text.Contains("flash", StringComparison.OrdinalIgnoreCase);
            score += GetRemovalScore(card.oracle_text, isInstantOrFast, card.cmc);

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

        private static float GetRemovalScore(string oracleText, bool isInstantOrFast, float cmc = 0)
        {
            oracleText = oracleText.ToLowerInvariant();
            float score = 0f;

            // --- Removal Categories ---
            string[] premium = [
                "destroy target creature",
                "exile target creature",
                "destroy all creatures",
                "exile all creatures",
                "destroy all nonland permanents",
                "exile all nonland permanents",
                "destroy target planeswalker",
                "exile target planeswalker",
                "destroy target permanent",
                "exile target permanent",
                "destroy any target",
                "exile any target"
            ];
            string[] situational = [
                "destroy target tapped creature",
                "destroy target creature with",
                "exile target creature with",
                "exile target nonwhite creature",
                "destroy target artifact or creature",
                "destroy target artifact",
                "destroy target enchantment",
                "destroy target land",
                "fight",
                "deals damage to target creature"
            ];
            string[] weak = [
                "destroy target artifact",
                "destroy target enchantment",
                "destroy target land"
            ];

            // --- Modal Boost ---
            bool isModal = oracleText.Contains("choose one or more") || oracleText.Contains("choose one —");

            // --- Repeatable Effect Boost ---
            bool isRepeatable =
                oracleText.Contains("at the beginning of") ||
                oracleText.Contains("each upkeep") ||
                oracleText.Contains("whenever") ||
                oracleText.Contains("each end step");

            // --- ETB Penalty ---
            bool isETBRemoval = oracleText.Contains("when") &&
                                oracleText.Contains("enters the battlefield") &&
                                (oracleText.Contains("destroy") || oracleText.Contains("exile"));

            // --- Matching & Scoring ---
            if (premium.Any(oracleText.Contains))
                score += 0.4f;
            else if (situational.Any(oracleText.Contains))
                score += isInstantOrFast ? 0.3f : 0.2f;
            else if (weak.Any(oracleText.Contains))
                score += 0.1f;

            // --- Adjustments ---
            if (cmc >= 5 && score > 0.2f && score <= 0.3f)
                score -= 0.1f; // High-cost situational spells are risky

            if (isModal && score >= 0.2f)
                score += 0.15f; // Modal spell with a strong mode is very flexible

            if (isRepeatable)
                score += 0.15f; // Triggered every turn or multiple times = great

            if (isETBRemoval)
                score -= 0.05f; // ETB-based removal is easier to counter or block

            return score;
        }
    }
}
