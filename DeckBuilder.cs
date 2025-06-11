using Spectre.Console;

namespace SealedDeckBuilder
{
    internal static class DeckBuilder
    {
        private static readonly Dictionary<int, int> DesiredCurve = new()
        {
            { 1, 2 }, { 2, 5 }, { 3, 5 }, { 4, 4 }, { 5, 3 }, { 6, 2 }
        };

        public static Deck BuildDeck(Deck pool, List<DraftsimCardRating> ratings, List<string> keywords)
        {
            var deck = new Deck();

            // 1. Evaluate all cards in the pool
            var keywordCounts = Evaluator.GetKeywordCount(pool);
            var kindredCounts = Evaluator.GetKindredCount(pool);
            var ratingDict = CreateRatingDictionary(ratings);
            var evaluatedCards = pool.MainDeck
                .SelectMany(e =>
                    Enumerable.Repeat(
                        new { Entry = new DeckEntry(1, e.Card), Score = Evaluator.EvaluateCard(e.Card, ratingDict, keywordCounts, kindredCounts, pool) },
                        e.Amount))
                .OrderByDescending(e => e.Score)
                .ToList();

            // 2. Analyze color strengths
            var colorScores = evaluatedCards
                .SelectMany(e => e.Entry.Card.colors.Select(c => (Color: c, Score: e.Score * e.Entry.Amount)))
                .GroupBy(cs => cs.Color)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Score));

            // 3. Boost color scores for fixing lands
            var fixingLands = pool.MainDeck
                .Where(e => IsFixingLand(e.Card))
                .SelectMany(e => Enumerable.Repeat(new DeckEntry(1, e.Card), e.Amount))
                .ToList();
            foreach (var land in fixingLands)
            {
                foreach (var fixColor in GetFixColors(land.Card))
                {
                    colorScores.TryAdd(fixColor, 0);
                    if (land.Card.oracle_text.Contains("search", StringComparison.OrdinalIgnoreCase))
                        colorScores[fixColor] += 0.5f;
                    else if (land.Card.oracle_text.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase))
                        colorScores[fixColor] += 0.8f;
                    else
                        colorScores[fixColor] += 1.0f;
                }
            }

            // 4. Determine main colors
            var topColors = colorScores
                .OrderByDescending(c => c.Value)
                .Take(2)
                .Select(c => c.Key)
                .ToHashSet();

            // 5. Separate lands and non-lands
            var nonLandCards = evaluatedCards.Where(e => !e.Entry.Card.type_line.Contains("Land", StringComparison.OrdinalIgnoreCase)).ToList();
            var landCards = evaluatedCards.Where(e => e.Entry.Card.type_line.Contains("Land", StringComparison.OrdinalIgnoreCase)).ToList();

            // 6. Select best spells (non-lands)
            var curveCounts = new Dictionary<int, int>();
            var bestSpells = new List<(DeckEntry entry, float score)>();
            foreach (var item in nonLandCards)
            {
                var card = item.Entry.Card;
                var colors = card.colors.ToHashSet();
                var cmcBucket = Math.Clamp((int)Math.Floor(card.cmc), 1, 6);
                curveCounts.TryAdd(cmcBucket, 0);

                if (colors.Count > 0 && !colors.Any(c => topColors.Contains(c)))
                {
                    if (item.Score < 3.5f || !colors.All(c => CanSplashColor(c, fixingLands.Select(l => l.Card), topColors)))
                        continue;
                }

                for (int i = 0; i < item.Entry.Amount; i++)
                {
                    if (bestSpells.Count >= 23 || curveCounts[cmcBucket] >= DesiredCurve.GetValueOrDefault(cmcBucket, 2))
                        break;

                    bestSpells.Add((new DeckEntry(1, card), item.Score));
                    curveCounts[cmcBucket]++;
                }
            }
            // Fill up to 23 spells if needed
            if (bestSpells.Count < 23)
            {
                foreach (var item in nonLandCards)
                {
                    if (bestSpells.Any(e => e.entry.Card.name == item.Entry.Card.name))
                        continue;
                    var cardColors = item.Entry.Card.colors.ToHashSet();
                    if (cardColors.Count > 0 && !cardColors.Any(c => topColors.Contains(c)))
                    {
                        if (item.Score < 3.5f || !cardColors.All(c => CanSplashColor(c, fixingLands.Select(l => l.Card), topColors)))
                            continue;
                    }
                    bestSpells.Add((item.Entry, item.Score));
                    if (bestSpells.Count == 23)
                        break;
                }
            }

            // 7. Land selection
            var deckColors = GetDeckColors(bestSpells.Select(b => b.entry), topColors);
            const int totalLandsNeeded = 17;
            var selectedLands = new List<DeckEntry>();
            // Utility lands first
            var utilityLandCandidates = landCards.Where(e => IsUtilityLand(e.Entry.Card) && deckColors.Overlaps(GetFixColors(e.Entry.Card))).OrderByDescending(e => e.Score).ToList();
            foreach (var item in utilityLandCandidates.Where(item => item.Score >= 3.0f && selectedLands.Count < totalLandsNeeded))
            {
                selectedLands.Add(item.Entry);
            }
            // Fixing lands next
            var lands = selectedLands;
            selectedLands.AddRange(from land in fixingLands let fixColors = GetFixColors(land.Card).ToList() where fixColors.Any(c => deckColors.Contains(c)) && lands.Count < totalLandsNeeded && lands.All(l => l.Card.name != land.Card.name) select land);
            // Trim if too many
            if (selectedLands.Count > totalLandsNeeded)
            {
                selectedLands = [.. selectedLands
                    .OrderByDescending(l => evaluatedCards.FirstOrDefault(e => e.Entry.Card.name == l.Card.name)?.Score ?? 0)
                    .Take(totalLandsNeeded)];
            }
            // Basic lands to fill
            int basicsNeeded = totalLandsNeeded - selectedLands.Count;
            var manaSymbols = bestSpells
                .SelectMany(e => e.entry.Card.mana_cost.Split('{', '}'))
                .Where(s => s.Length == 1 && "WUBRG".Contains(s))
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count());
            var landDistribution = deckColors
                .ToDictionary(c => c, c => manaSymbols.GetValueOrDefault(c, 0));
            var totalSymbols = landDistribution.Values.Sum();
            var basicLands = landDistribution
                .ToDictionary(c => c.Key, c => totalSymbols > 0 ? (int)Math.Round(basicsNeeded * (c.Value / (float)totalSymbols)) : 0);
            var currentTotal = basicLands.Values.Sum();
            if (currentTotal != basicsNeeded)
            {
                var mainColor = landDistribution.OrderByDescending(x => x.Value).First().Key;
                basicLands[mainColor] += basicsNeeded - currentTotal;
            }

            // 8. Enforce 40-card deck
            var spellsToAdd = 40 - (selectedLands.Count + basicLands.Values.Sum());
            var finalSpells = bestSpells.Take(spellsToAdd).ToList();

            // 9. Assemble deck
            foreach (var group in finalSpells.GroupBy(s => s.entry.Card.name))
            {
                var count = group.Count();
                var card = group.First().entry.Card;
                deck.MainDeck.Add(new DeckEntry(count, card));
            }
            foreach (var land in selectedLands)
                deck.MainDeck.Add(land);
            foreach (var land in basicLands.Where(land => land.Value > 0))
            {
                deck.MainDeck.Add(new DeckEntry(land.Value, new Card
                {
                    name = GetBasicLandName(land.Key),
                    type_line = $"Basic Land — {land.Key}",
                    oracle_text = $"Add {{{land.Key}}}.",
                    colors = [land.Key]
                }));
            }

            return deck;
        }

        private static string GetBasicLandName(string color)
        {
            return color switch
            {
                "W" => "Plains",
                "U" => "Island",
                "B" => "Swamp",
                "R" => "Mountain",
                "G" => "Forest",
                _ => throw new ArgumentException($"Invalid color: {color}")
            };
        }

        private static Dictionary<string, float> CreateRatingDictionary(List<DraftsimCardRating> ratings)
        {
            return ratings.ToDictionary(c => c.name, c => c.myrating);
        }

        private static bool IsFixingLand(Card card) =>
            card.type_line.Contains("Land", StringComparison.OrdinalIgnoreCase)
            && (
                // Check for traditional dual land templating
                (card.oracle_text.Contains("Add", StringComparison.OrdinalIgnoreCase)
                && card.oracle_text.Count(c => "WUBRG".Contains(c)) >= 2)
                // Check for other common dual land templating
                || card.oracle_text.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("choose one", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("as an additional cost", StringComparison.OrdinalIgnoreCase)
                // Check for fetch lands
                || card.oracle_text.Contains("search your library for a", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("search for a", StringComparison.OrdinalIgnoreCase)
                // Check for any-color lands
                || card.oracle_text.Contains("Add one mana of any color", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("Add {C}", StringComparison.OrdinalIgnoreCase)
            );

        private static bool IsUtilityLand(Card card) =>
            card.type_line.Contains("Land", StringComparison.OrdinalIgnoreCase)
            && !IsFixingLand(card)
            && (
                card.oracle_text.Contains("draw", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("scry", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("investigate", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("create", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("token", StringComparison.OrdinalIgnoreCase)
            );

        private static IEnumerable<string> GetFixColors(Card card)
        {
            // Check for any-color lands
            if (card.oracle_text.Contains("Add one mana of any color", StringComparison.OrdinalIgnoreCase)
                || card.oracle_text.Contains("Add {C}", StringComparison.OrdinalIgnoreCase))
            {
                yield return "W";
                yield return "U";
                yield return "B";
                yield return "R";
                yield return "G";
                yield break;
            }

            // Check for traditional mana adding
            foreach (var c in new[] { 'W', 'U', 'B', 'R', 'G' })
            {
                if (card.oracle_text.Contains($"Add {{{c}}}", StringComparison.OrdinalIgnoreCase))
                    yield return c.ToString();
            }

            // Check for other common dual land templating
            if (card.oracle_text.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var c in new[] { 'W', 'U', 'B', 'R', 'G' })
                {
                    if (card.oracle_text.Contains($"{{{c}}}", StringComparison.OrdinalIgnoreCase))
                        yield return c.ToString();
                }
            }

            // Check for fetch lands
            if (card.oracle_text.Contains("search", StringComparison.OrdinalIgnoreCase))
            {
                // Check for specific land types that indicate colors
                if (card.oracle_text.Contains("Plains", StringComparison.OrdinalIgnoreCase))
                    yield return "W";
                if (card.oracle_text.Contains("Island", StringComparison.OrdinalIgnoreCase))
                    yield return "U";
                if (card.oracle_text.Contains("Swamp", StringComparison.OrdinalIgnoreCase))
                    yield return "B";
                if (card.oracle_text.Contains("Mountain", StringComparison.OrdinalIgnoreCase))
                    yield return "R";
                if (card.oracle_text.Contains("Forest", StringComparison.OrdinalIgnoreCase))
                    yield return "G";
            }
        }

        private static float GetColorSourceCount(string color, IEnumerable<Card> lands)
        {
            float count = 0;
            foreach (var land in lands)
            {
                if (IsFixingLand(land))
                {
                    var fixColors = GetFixColors(land).ToList();
                    if (fixColors.Contains(color))
                    {
                        // Count fetch lands as 0.5 sources since they're not guaranteed
                        if (land.oracle_text.Contains("search", StringComparison.OrdinalIgnoreCase))
                            count += 0.5f;
                        // Count tapped duals as 0.8 sources
                        else if (land.oracle_text.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase))
                            count += 0.8f;
                        // Count regular duals as 1 source
                        else
                            count += 1.0f;
                    }
                }
                // Count basic lands as 1 source
                else if (land.type_line.Contains("Basic Land", StringComparison.OrdinalIgnoreCase)
                    && land.oracle_text.Contains($"Add {{{color}}}", StringComparison.OrdinalIgnoreCase))
                {
                    count += 1.0f;
                }
            }
            return count;
        }

        private static bool CanSplashColor(string color, IEnumerable<Card> fixingLands, HashSet<string> mainColors)
        {
            ArgumentNullException.ThrowIfNull(mainColors);
            // Count how many sources we have for this color
            var sources = GetColorSourceCount(color, fixingLands);

            // We need at least 2.5 sources to consider a color splashable
            // This accounts for fetch lands and tapped duals
            return sources >= 2.5f;
        }

        private static HashSet<string> GetDeckColors(IEnumerable<DeckEntry> spells, HashSet<string> mainColors)
        {
            var colors = new HashSet<string>(mainColors);
            foreach (var spell in spells)
            {
                foreach (var color in spell.Card.colors)
                {
                    colors.Add(color);
                }
            }
            return colors;
        }
    }
}
