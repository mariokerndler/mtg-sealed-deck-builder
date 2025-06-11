using Newtonsoft.Json;
using Spectre.Console;

namespace SealedDeckBuilder
{
    internal class SynergyDefinition
    {
        public string Tag { get; set; } = string.Empty;
        public List<string> Matches { get; set; } = [];
    }

    internal class RuleDefinition
    {
        public string Tag { get; set; } = string.Empty;
        public List<string> OracleKeywords { get; set; } = [];
        public List<string> TypeLineKeywords { get; set; } = [];
        public List<string> OracleExclusions { get; set; } = [];

        public int? MinimumCMC { get; set; } // for backward compatibility
        public int? CmcMin { get; set; }
        public int? CmcMax { get; set; }
        public int? CmcEqual { get; set; }

        [JsonIgnore]
        public Func<Card, bool>? CustomCondition { get; set; }

        public bool Matches(Card card)
        {
            if (card.oracle_text == null || card.type_line == null)
                return false;

            if (OracleKeywords.Any(k => !card.oracle_text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (OracleExclusions.Any(k => card.oracle_text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (TypeLineKeywords.Any(k => !card.oracle_text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (MinimumCMC.HasValue && card.cmc < MinimumCMC.Value)
                return false;

            if (CmcMin.HasValue && card.cmc < CmcMin.Value)
                return false;

            if (CmcMax.HasValue && card.cmc > CmcMax.Value)
                return false;

            if (CmcEqual.HasValue && card.cmc != CmcEqual.Value)
                return false;

            if (CustomCondition != null && !CustomCondition(card))
                return false;

            return true;
        }
    }

    internal class SynergyConfig
    {
        public List<SynergyDefinition> Synergies { get; set; } = [];
        public List<RuleDefinition> Rules { get; set; } = [];
    }

    internal static class SynergyEvaluator
    {
        private static readonly Dictionary<string, HashSet<string>> SynergyMap = [];
        private static readonly Dictionary<string, RuleDefinition> RulesByTag = [];

        static SynergyEvaluator()
        {
            if (SynergyMap.Count > 0 && RulesByTag.Count > 0) return; // Already populated

            SynergyMap.Clear();
            RulesByTag.Clear();
            var rulePath = Path.Combine(AppContext.BaseDirectory, "data\\synergy_rules.json");
            LoadSynergyConfig(rulePath);
        }

        public static HashSet<string> GetSynergyTags(Card card)
        {
            var tags = new HashSet<string>();
            var text = card.oracle_text.ToLowerInvariant();
            var typeLine = card.type_line;

            foreach (var rule in RulesByTag.Values)
            {
                bool oracleMatch = rule.OracleKeywords.All(s => text.Contains(s, StringComparison.InvariantCultureIgnoreCase));
                bool typeLineMatch = rule.TypeLineKeywords == null || rule.TypeLineKeywords.All(s => typeLine.Contains(s));

                if (oracleMatch && typeLineMatch)
                {
                    tags.Add(rule.Tag);
                }
            }

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

            foreach (var (from, to, weight) in links.OrderByDescending(l => l.weight))
            {
                table.AddRow(from, to, weight.ToString("0.00"));
            }

            AnsiConsole.Write(table);
        }

        private static void LoadSynergyConfig(string rulePath)
        {
            if (!File.Exists(rulePath))
            {
                AnsiConsole.MarkupLine("[red]Synergy configuration file not found: {0}[/]", rulePath);
                return;
            }

            var json = File.ReadAllText(rulePath);

            if (json == null || string.IsNullOrWhiteSpace(json))
            {
                AnsiConsole.MarkupLine("[red]Synergy configuration file is empty or invalid: {0}[/]", rulePath);
                return;
            }

            var config = JsonConvert.DeserializeObject<SynergyConfig>(json);

            if (config == null) return;

            foreach (var synergy in config.Synergies)
            {
                SynergyMap[synergy.Tag] = [.. synergy.Matches];
            }

            foreach (var rule in config.Rules)
            {
                RulesByTag[rule.Tag] = rule;
            }

            // Add any custom rule logic here:
            if (RulesByTag.TryGetValue("MyCustomTag", out var myCustomRule))
            {
                myCustomRule.CustomCondition = card =>
                {
                    return card.oracle_text.Contains("combo", StringComparison.OrdinalIgnoreCase) &&
                           card.cmc % 2 == 0;
                };
            }
        }
    }
}
