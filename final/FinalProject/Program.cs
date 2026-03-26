using System;
using System.IO;
using System.Windows.Forms;

#nullable enable

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && HasFlag(args, "--train"))
        {
            RunHeadlessTraining(args);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DeckEvaluatorForm());
    }

    private static void RunHeadlessTraining(string[] args)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "training_run.log");
        static void AppendLog(string path, string message)
        {
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }

        string? deckPath = GetArgValue(args, "--deck");
        if (string.IsNullOrWhiteSpace(deckPath) || !File.Exists(deckPath))
        {
            Console.WriteLine("Training mode requires a valid deck file path. Example:");
            Console.WriteLine("FinalProject.exe --train --deck C:/path/deck.txt --commander \"Atraxa, Praetors' Voice\" --batches 8 --simulations 150000 --turns 10");
            AppendLog(logPath, "Training aborted: deck file path missing or invalid.");
            return;
        }

        string deckText = File.ReadAllText(deckPath);
        string commander = GetArgValue(args, "--commander") ?? string.Empty;
        int batches = GetIntArgValue(args, "--batches", 6);
        int simulations = GetIntArgValue(args, "--simulations", 150000);
        int turns = GetIntArgValue(args, "--turns", 10);
        bool onDraw = HasFlag(args, "--on-draw");

            Console.WriteLine("Starting Fizban the Fabulous headless goldfish training...");
        Console.WriteLine($"Deck file: {deckPath}");
        Console.WriteLine($"Commander: {(string.IsNullOrWhiteSpace(commander) ? "(none)" : commander)}");
        Console.WriteLine($"Batches: {batches} | Simulations per batch: {simulations} | Turns: {turns} | On draw: {onDraw}");
        AppendLog(logPath, $"Training start | deck={deckPath} | commander={commander} | batches={batches} | simulations={simulations} | turns={turns} | onDraw={onDraw}");

        try
        {
            using var form = new DeckEvaluatorForm();
            var results = form.RunHeadlessTrainingAsync(
                deckText,
                commander,
                simulations,
                turns,
                batches,
                onDraw,
                message => Console.WriteLine(message)).GetAwaiter().GetResult();

            Console.WriteLine("Training finished.");
            Console.WriteLine($"Estimated power: {results.EstimatedPowerLevel:F1}/10 | {results.EstimatedBracket}");
            Console.WriteLine($"Mana efficiency: {results.AverageManaEfficiency:F1}% | Idle turns: {results.AverageIdleTurns:F2}");
            Console.WriteLine($"Commander cast rate: {results.CommanderCastRate:F1}% | Learned games: {results.LearningGamesSeen}");
            AppendLog(logPath, $"Training complete | learnedGames={results.LearningGamesSeen} | power={results.EstimatedPowerLevel:F1} | bracket={results.EstimatedBracket}");
        }
        catch (Exception ex)
        {
            AppendLog(logPath, $"Training failed | {ex}");
            throw;
        }
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.Exists(args, arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static int GetIntArgValue(string[] args, string key, int fallback)
    {
        string? value = GetArgValue(args, key);
        return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
    }
}
