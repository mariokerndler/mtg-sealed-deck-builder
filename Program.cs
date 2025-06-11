using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace SealedDeckBuilder
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<AnalysePoolCommand>("analyse")
                    .WithDescription("Analyzes a sealed pool and suggests a 40-card deck.")
                    .WithExample(["analyze", "-i", "pool.txt", "-s", "FIN"]);
            });

            return await app.RunAsync(args);
        }

        public class AnalysePoolSettings : CommandSettings
        {
            [Description("Path to input file with card names, one per line.")]
            [CommandOption("-i|--input-file <INPUT_FILE>")]
            public string InputFile { get; set; } = string.Empty;

            [Description("Set code for which the rankings will be fetched.")]
            [CommandOption("-s|--set-code <SET_CODE>")]
            public string SetCode { get; set; } = string.Empty;
        }

        public class AnalysePoolCommand : AsyncCommand<AnalysePoolSettings>
        {
            public override async Task<int> ExecuteAsync(CommandContext context, AnalysePoolSettings settings)
            {
                AnsiConsole.Write(
                    new FigletText("Sealed Deck Builder")
                        .LeftJustified()
                        .Color(Color.Green));

                if (string.IsNullOrEmpty(settings.SetCode))
                {
                    AnsiConsole.MarkupLine("[red]Set code is required! Use -s or --set-code option.[/]");
                    return -1;
                }

                // Fetch deck from input file or generate a pool
                Deck? pool;
                var generateDeck = !File.Exists(settings.InputFile);
                if (generateDeck)
                {
                    AnsiConsole.MarkupLine($"[green]Generating sealed pool for set code: {settings.SetCode}[/]");
                    pool = await Deck.GenerateSealedPool(settings.SetCode, 6);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]Reading sealed pool from file.[/]");
                    pool = await InputParser.ParseInput(settings.InputFile);
                }

                if (pool == null) return -1;
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("Sealed Pool"));
                PrintDeck(pool, printDeckComposition: true);

                // Fetch card rankings from the provided URL
                var ratings = await DraftsimRatingFetcher.FetchRatingsAsync(settings.SetCode);
                if (ratings == null || ratings.Count == 0) return -1;
                //PrintRatings(ratings);

                // Fetch keywords
                var scryfallAPI = new ScryfallApi();
                var keywords = await scryfallAPI.FetchKeywordListAsync();
                if (keywords == null || keywords.Count == 0) return -1;

                var deck = DeckBuilder.BuildDeck(pool, ratings, keywords);
                if (deck == null) return -1;
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("Generated Deck"));
                PrintDeck(deck, printDeckComposition: true);

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("Deck Export"));
                var printExport = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Do you want to [green]export[/] the deck to a file?")
                    .AddChoices(["Yes", "No"]));

                if (printExport == "No") return 0;
                else
                    deck.PrintDeckForExport();

                return 0;
            }

            private static void PrintDeck(Deck deck, bool printSideboard = false, bool printDeckComposition = false)
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]Type[/]").LeftAligned())
                    .AddColumn(new TableColumn("[bold]Count[/]").LeftAligned())
                    .AddColumn(new TableColumn("[bold]Colors[/]").LeftAligned())
                    .AddColumn(new TableColumn("[bold]Name[/]").LeftAligned());

                // Define type priority (higher up = higher priority)
                var typePriority = new (string Label, Func<Card, bool> Predicate)[]
                {
                    ("Creature",     c => c.type_line.Contains("Creature")),
                    ("Spell",        c => c.type_line.Contains("Instant") || c.type_line.Contains("Sorcery")),
                    ("Artifact",     c => c.type_line.Contains("Artifact")),
                    ("Enchantment",  c => c.type_line.Contains("Enchantment")),
                    ("Land",         c => c.type_line.Contains("Land")),
                };

                // Assign primary type to each card
                var typedEntries = new List<(string Type, DeckEntry Entry)>();
                foreach (var entry in deck.MainDeck)
                {
                    var type = typePriority.FirstOrDefault(p => p.Predicate(entry.Card)).Label ?? "Other";
                    typedEntries.Add((type, entry));
                }

                // Group entries by type label
                var grouped = typedEntries
                    .GroupBy(te => te.Type)
                    .OrderBy(g => typePriority.ToList().FindIndex(tp => tp.Label == g.Key));

                // Render table
                foreach (var group in grouped)
                {
                    foreach (var (type, entry) in group)
                    {
                        table.AddRow(type, entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                    }
                }

                // Add sideboard
                if (printSideboard && deck.Sideboard.Any())
                {
                    table.AddRow("[italic]Sideboard[/]", "", "", "");
                    foreach (var entry in deck.Sideboard)
                    {
                        table.AddRow("", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                    }
                }

                AnsiConsole.Write(table);

                // Deck composition chart
                if (printDeckComposition)
                {
                    AnsiConsole.WriteLine();
                    var chart = new BreakdownChart();

                    foreach (var group in grouped)
                    {
                        var total = group.Sum(te => te.Entry.Amount);
                        if (total > 0)
                        {
                            chart.AddItem(group.Key, total, GetColorForType(group.Key));
                        }
                    }

                    AnsiConsole.Write(chart);
                }
            }

            private static Color GetColorForType(string type) => type switch
            {
                "Creature" => Color.White,
                "Spell" => Color.Blue,
                "Artifact" => Color.Red,
                "Enchantment" => Color.Green,
                "Land" => Color.Yellow,
                _ => Color.Grey
            };


            private static void PrintRatings(List<DraftsimCardRating> ratings)
            {
                ratings.Sort((a, b) => b.myrating.CompareTo(a.myrating));
                foreach (var rating in ratings)
                {
                    AnsiConsole.MarkupLine($"[blue]{rating.name} - Rating: {rating.myrating}[/]");
                }
            }
        }
    }
}
