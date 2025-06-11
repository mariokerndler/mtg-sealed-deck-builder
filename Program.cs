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

            private static void PrintDeck(Deck deck, bool printSideboad = false, bool printDeckComposition = false)
            {
                var maindeck = deck.MainDeck;
                var spells = maindeck.Where(e => e.Card.type_line.Contains("Instant") || e.Card.type_line.Contains("Sorcery")).ToList();
                var creatures = maindeck.Where(e => e.Card.type_line.Contains("Creature")).ToList();
                var artifacts = maindeck.Where(e => e.Card.type_line.Contains("Artifact")).ToList();
                var enchantments = maindeck.Where(e => e.Card.type_line.Contains("Enchantment")).ToList();
                var lands = maindeck.Where(e => e.Card.type_line.Contains("Land")).ToList();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]Type[/]").LeftAligned())
                    .AddColumn(new TableColumn("[bold]Count[/]").LeftAligned())
                    .AddColumn(new TableColumn("[bold]Colors[/]").LeftAligned())
                    .AddColumn(new TableColumn("[bold]Name[/]").LeftAligned());

                foreach (var entry in creatures)
                {
                    table.AddRow("Creature", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                }

                foreach (var entry in spells)
                {
                    table.AddRow("Spell", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                }

                foreach (var entry in artifacts)
                {
                    table.AddRow("Artifact", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                }

                foreach (var entry in enchantments)
                {
                    table.AddRow("Enchantment", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                }

                foreach (var entry in lands)
                {
                    table.AddRow("Land", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                }

                if (printSideboad)
                {
                    var sideboard = deck.Sideboard;
                    if (sideboard.Count > 0)
                    {
                        table.AddRow("Sideboard", "", "");
                        foreach (var entry in sideboard)
                        {
                            table.AddRow("", entry.Amount.ToString(), entry.Card.GetCardColor(), entry.Card.name);
                        }
                    }
                }

                AnsiConsole.Write(table);

                if (printDeckComposition)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new BreakdownChart()
                        .AddItem("Creatures", creatures.Count, Color.White)
                        .AddItem("Spells", spells.Count, Color.Blue)
                        .AddItem("Artifacts", artifacts.Count, Color.Red)
                        .AddItem("Enchantments", enchantments.Count, Color.Green)
                        .AddItem("Lands", lands.Count, Color.Yellow));
                }
            }

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
