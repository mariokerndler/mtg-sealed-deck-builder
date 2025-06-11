using Spectre.Console;

namespace SealedDeckBuilder
{
    internal enum SynergyTag
    {
        Landfall,
        Ramp,
        DeathTrigger,
        SacrificeOutlet,
        GraveyardPayoff,
        SelfMill,
        TokenProducer,
        TokenPayoff,
        SpellPayoff,
        InstantSorcery,
        Proliferate,
        CounterGiver,
        ArtifactProducer,
        ArtifactPayoff,
        Equipment,
        Enchantment,
        EnchantmentPayoff,
        Lifeloss,
        Lifegain,
        LifegainPayoff,
        Draw,
        DrawPayoff,
        Discard,
        Madness,
        Reanimation,
        BigCreature,
        FightBite,
    }

    internal static class SynergyEvaluator
    {
        private static readonly Dictionary<SynergyTag, HashSet<SynergyTag>> SynergyMap = new()
        {
            [SynergyTag.Landfall] = [SynergyTag.Ramp],
            [SynergyTag.DeathTrigger] = [SynergyTag.SacrificeOutlet],
            [SynergyTag.GraveyardPayoff] = [SynergyTag.SelfMill],
            [SynergyTag.SpellPayoff] = [SynergyTag.InstantSorcery],
            [SynergyTag.TokenPayoff] = [SynergyTag.TokenProducer],
            [SynergyTag.Proliferate] = [SynergyTag.CounterGiver],
            [SynergyTag.ArtifactPayoff] = [SynergyTag.ArtifactProducer],
            [SynergyTag.EnchantmentPayoff] = [SynergyTag.Enchantment],
            [SynergyTag.LifegainPayoff] = [SynergyTag.Lifegain],
            [SynergyTag.Madness] = [SynergyTag.Discard],
            [SynergyTag.Reanimation] = [SynergyTag.SelfMill],
            [SynergyTag.DrawPayoff] = [SynergyTag.Draw],
            [SynergyTag.FightBite] = [SynergyTag.BigCreature],
        };

        public static HashSet<SynergyTag> GetSynergyTags(Card card)
        {
            var tags = new HashSet<SynergyTag>();
            var text = card.oracle_text.ToLowerInvariant();
            var typeLine = card.type_line;

            if (text.Contains("landfall")) tags.Add(SynergyTag.Landfall);
            if (text.Contains("search your library for a land") || text.Contains("basic land") || text.Contains("put a land"))
                tags.Add(SynergyTag.Ramp);
            if (text.Contains("when") && text.Contains("dies")) tags.Add(SynergyTag.DeathTrigger);
            if (text.Contains("sacrifice a creature") || text.Contains("sacrifice another"))
                tags.Add(SynergyTag.SacrificeOutlet);
            if (text.Contains("graveyard") && (text.Contains("number of") || text.Contains("cards in your graveyard")))
                tags.Add(SynergyTag.GraveyardPayoff);
            if (text.Contains("mill") || (text.Contains("put the top") && text.Contains("graveyard")))
                tags.Add(SynergyTag.SelfMill);
            if (text.Contains("create") && text.Contains("token")) tags.Add(SynergyTag.TokenProducer);
            if (text.Contains("for each token") || text.Contains("whenever a token")) tags.Add(SynergyTag.TokenPayoff);
            if (text.Contains("proliferate")) tags.Add(SynergyTag.Proliferate);
            if (text.Contains("+1/+1 counter") || text.Contains("put a +1/+1 counter"))
                tags.Add(SynergyTag.CounterGiver);
            if (typeLine.Contains("Instant") || typeLine.Contains("Sorcery")) tags.Add(SynergyTag.InstantSorcery);
            if (text.Contains("whenever you cast an instant") || text.Contains("cast a sorcery"))
                tags.Add(SynergyTag.SpellPayoff);
            if (text.Contains("create") && text.Contains("artifact")) tags.Add(SynergyTag.ArtifactProducer);
            if (typeLine.Contains("Artifact") && !typeLine.Contains("Creature")) tags.Add(SynergyTag.ArtifactPayoff);
            if (typeLine.Contains("Enchantment")) tags.Add(SynergyTag.Enchantment);
            if (text.Contains("for each enchantment") || text.Contains("enchantment you control"))
                tags.Add(SynergyTag.EnchantmentPayoff);

            if (text.Contains("lose") && text.Contains("life")) tags.Add(SynergyTag.Lifeloss);
            if (text.Contains("gain") && text.Contains("life")) tags.Add(SynergyTag.Lifegain);
            if (text.Contains("whenever you gain life") || text.Contains("if you gained life")) tags.Add(SynergyTag.LifegainPayoff);

            if (text.Contains("draw a card")) tags.Add(SynergyTag.Draw);
            if (text.Contains("whenever you draw a card")) tags.Add(SynergyTag.DrawPayoff);

            if (text.Contains("discard a card")) tags.Add(SynergyTag.Discard);
            if (text.Contains("madness")) tags.Add(SynergyTag.Madness);

            if (text.Contains("return target creature") && text.Contains("graveyard")) tags.Add(SynergyTag.Reanimation);

            if (text.Contains("fight") || text.Contains("deals damage equal to its power"))
                tags.Add(SynergyTag.FightBite);
            if (card.type_line.Contains("Creature") && card.oracle_text.Contains("power") && card.cmc >= 5)
                tags.Add(SynergyTag.BigCreature);

            return tags;
        }

        public static float GetSynergyScore(Card a, Card b, int aCopies = 1, int bCopies = 1)
        {
            var aTags = GetSynergyTags(a);
            var bTags = GetSynergyTags(b);

            float score = 0f;
            foreach (var tag in aTags)
            {
                if (SynergyMap.TryGetValue(tag, out var matches))
                {
                    foreach (var other in bTags)
                    {
                        if (matches.Contains(other))
                            score += 0.25f * aCopies * bCopies;
                    }
                }
            }
            return score;
        }

        public static float EvaluateDeckSynergy(Deck deck)
        {
            float synergyTotal = 0f;
            var entries = deck.MainDeck;

            // Precompute tag density
            var tagDensity = new Dictionary<SynergyTag, int>();
            foreach (var entry in entries)
            {
                foreach (var tag in GetSynergyTags(entry.Card))
                {
                    if (!tagDensity.ContainsKey(tag))
                        tagDensity[tag] = 0;
                    tagDensity[tag] += entry.Amount;
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                for (int j = i + 1; j < entries.Count; j++)
                {
                    float baseScore = GetSynergyScore(entries[i].Card, entries[j].Card, entries[i].Amount, entries[j].Amount);
                    // Boost synergy if shared tags are prevalent
                    float densityWeight = 1f;

                    foreach (var tag in GetSynergyTags(entries[i].Card).Intersect(GetSynergyTags(entries[j].Card)))
                    {
                        if (tagDensity.TryGetValue(tag, out var count) && count >= 4)
                            densityWeight += 0.25f;
                    }

                    synergyTotal += baseScore * densityWeight;
                }
            }

            return synergyTotal;
        }


        // Optional negative synergy detection
        public static float EvaluateAntiSynergy(Deck deck)
        {
            float penalty = 0f;
            bool hasAuras = deck.MainDeck.Any(e => e.Card.type_line.Contains("Aura"));
            bool hasFewCreatures = deck.MainDeck.Count(e => e.Card.type_line.Contains("Creature")) < 5;

            if (hasAuras && hasFewCreatures)
                penalty -= 1.0f;

            return penalty;
        }

        public static void PrintSynergyGraph(Deck deck)
        {
            var entries = deck.MainDeck;
            var links = new List<(string from, string to, float weight)>();

            for (int i = 0; i < entries.Count; i++)
            {
                for (int j = i + 1; j < entries.Count; j++)
                {
                    float weight = GetSynergyScore(entries[i].Card, entries[j].Card, 1, 1);
                    if (weight > 0f)
                    {
                        links.Add((entries[i].Card.name, entries[j].Card.name, weight));
                    }
                }
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Card A")
                .AddColumn("Card B")
                .AddColumn("Score");

            foreach (var (from, to, weight) in links.OrderByDescending(l => l.weight).Take(25))
            {
                table.AddRow(from, to, weight.ToString("0.00"));
            }

            AnsiConsole.Write(table);
        }
    }
}
