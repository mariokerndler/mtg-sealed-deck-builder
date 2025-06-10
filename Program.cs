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
                var generateDeck = !File.Exists(settings.InputFile);

                if (string.IsNullOrEmpty(settings.SetCode))
                {
                    AnsiConsole.MarkupLine("[red]Set code is required! Use -s or --set-code option.[/]");
                    return -1;
                }

                // Fetch deck from input file or generate a pool
                Deck? pool;
                if (generateDeck)
                {
                    pool = await Deck.GenerateSealedPool(settings.SetCode, 6);
                }
                else
                {
                    pool = await InputParser.ParseInput(settings.InputFile);
                }

                if (pool == null) return -1;
                PrintDeck(pool);

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

                PrintDeck(deck);

                return 0;
            }

            private static void PrintDeck(Deck deck)
            {
                foreach (var entry in deck.MainDeck)
                {
                    AnsiConsole.MarkupLine($"[green]{entry.Amount}x {entry.Card.name}[/]");
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
