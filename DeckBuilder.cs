using Spectre.Console;

namespace SealedDeckBuilder
{
    internal class DeckBuilder
    {
        private static readonly Dictionary<int, int> _desiredCurve = new()
        {
            { 1, 2 }, { 2, 5 }, { 3, 5 }, { 4, 4 }, { 5, 3 }, { 6, 2 }
        };

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

            // Color
            var colorScores = evaluatedCards
                .SelectMany(e => e.Entry.Card.colors.Select(c => (Color: c, Score: e.Score * e.Entry.Amount)))
                .GroupBy(cs => cs.Color)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Score));

            // Include fixing lands in color scoring boost
            foreach (var land in deck.MainDeck.Where(e => IsFixingLand(e.Card)))
            {
                foreach (var fixColor in GetFixColors(land.Card))
                {
                    if (!colorScores.ContainsKey(fixColor))
                        colorScores[fixColor] = 0;

                    colorScores[fixColor] += 0.8f;
                }
            }

            var topColors = colorScores
                .OrderByDescending(c => c.Value)
                .Take(2)
                .Select(c => c.Key)
                .ToHashSet();
            var splashableColours = GetFixColors(deck.MainDeck.Select(e => e.Card));

            // Mana Curve-Aware Spell Selection
            var curveCounts = new Dictionary<int, int>();
            var bestSpells = new List<(DeckEntry entry, float score)>();

            foreach (var item in evaluatedCards)
            {
                var card = item.Entry.Card;
                var colors = card.colors.ToHashSet();
                int cmcBucket = Math.Clamp((int)Math.Floor(card.cmc), 1, 6);
                if (!curveCounts.ContainsKey(cmcBucket))
                    curveCounts[cmcBucket] = 0;

                if (colors.Count > 0 && !colors.Any(c => topColors.Contains(c)))
                {
                    // Check splash allowance if fixing supports it and it's a bomb
                    if (item.Score < 3.5f || !splashableColours.Intersect(colors).Any())
                        continue;
                }


                if (bestSpells.Count < 23 && curveCounts[cmcBucket] < (_desiredCurve.TryGetValue(cmcBucket, out var max) ? max : 2))
                {
                    bestSpells.Add((item.Entry, item.Score));
                    curveCounts[cmcBucket]++;
                }
            }

            if (bestSpells.Count < 23)
            {
                foreach (var item in evaluatedCards)
                {
                    if (bestSpells.Any(e => e.entry.Card.name == item.Entry.Card.name))
                        continue;

                    var cardColors = item.Entry.Card.colors.ToHashSet();

                    if (cardColors.Count > 0 && !cardColors.Any(c => topColors.Contains(c)))
                    {
                        if (item.Score < 3.5f || !splashableColours.Intersect(cardColors).Any())
                            continue;
                    }

                    bestSpells.Add((item.Entry, item.Score));
                    if (bestSpells.Count == 23)
                        break;
                }
            }

            if (bestSpells.Count < 23)
                AnsiConsole.MarkupLine($"[red] Only selected {bestSpells.Count} spells. Not enough quality cards in pool.[/]");

            AnsiConsole.MarkupLine($"[blue]Suggested Spells:[/]");
            foreach (var entry in bestSpells)
                AnsiConsole.MarkupLine($"[blue]{entry.entry.Amount}x {entry.entry.Card.name} - Score: {entry.score:F2}[/]");

            var suggestedCardSet = bestSpells.Select(b => b.entry.Card.name).ToHashSet();
            var otherCards = evaluatedCards.Where(e => !suggestedCardSet.Contains(e.Entry.Card.name)).ToList();

            AnsiConsole.MarkupLine($"[blue]nOther Playable Cards:[/]");
            foreach (var other in otherCards)
                Console.WriteLine($"{other.Entry.Amount}x {other.Entry.Card.name} - Score: {other.Score:F2}");

            var manaSymbols = bestSpells
                .SelectMany(e => e.entry.Card.mana_cost.Split('{', '}'))
                .Where(s => s.Length == 1 && "WUBRG".Contains(s))
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count());

            var landDistribution = topColors
                .ToDictionary(c => c, c => manaSymbols.TryGetValue(c, out int count) ? count : 0);

            var totalSymbols = landDistribution.Values.Sum();
            var basicLands = landDistribution
                .ToDictionary(c => c.Key, c => (int)Math.Round(17 * (c.Value / (float)totalSymbols)));

            AnsiConsole.MarkupLine($"[blue]Suggested Lands:[/]");
            foreach (var land in basicLands)
                AnsiConsole.MarkupLine($"[blue]{land.Value}x {land.Key}[/]");

            AnsiConsole.MarkupLine($"[blue]Suggested Fixing Lands:[/]");
            foreach (var land in deck.MainDeck.Where(e => IsFixingLand(e.Card)))
            {
                var fixColors = string.Join(", ", GetFixColors(land.Card));
                AnsiConsole.MarkupLine($"[blue]{land.Amount}x {land.Card.name} (fixes: {fixColors})[/]");
            }

            return deck;
        }

        private static Dictionary<string, float> CreateRatingDictionary(List<DraftsimCardRating> ratings)
        {
            return ratings.ToDictionary(c => c.name, c => c.myrating);
        }

        private static bool IsFixingLand(Card card) =>
            card.type_line.Contains("Land", StringComparison.OrdinalIgnoreCase)
            && card.oracle_text.Contains("Add", StringComparison.OrdinalIgnoreCase)
            && card.oracle_text.Count(c => "WUBRG".Contains(c)) >= 2;

        private static IEnumerable<string> GetFixColors(Card card)
        {
            foreach (var c in new[] { 'W', 'U', 'B', 'R', 'G' })
            {
                if (card.oracle_text.Contains($"Add {{{c}}}", StringComparison.OrdinalIgnoreCase))
                    yield return c.ToString();
            }
        }

        private static HashSet<string> GetFixColors(IEnumerable<Card> cards) =>
            cards.SelectMany(card => GetFixColors(card)).ToHashSet();
    }
}
