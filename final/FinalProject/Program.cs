using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

#nullable enable

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && HasFlag(args, "--web"))
        {
            RunPortalApi(args).GetAwaiter().GetResult();
            return;
        }

        if (args.Length > 0 && HasFlag(args, "--train"))
        {
            RunHeadlessTraining(args);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DeckEvaluatorForm());
    }

    private static async Task RunPortalApi(string[] args)
    {
        int port = GetIntArgValue(args, "--port", 5057);
        string prefix = $"http://127.0.0.1:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        using var evaluator = new DeckEvaluatorForm();
        Console.WriteLine($"Fizban local web API listening on {prefix}");
        Console.WriteLine("Use POST /analyze with JSON payload from the web portal.");

        while (true)
        {
            var context = await listener.GetContextAsync();
            await HandlePortalRequestAsync(context, evaluator);
        }
    }

    private static async Task HandlePortalRequestAsync(HttpListenerContext context, DeckEvaluatorForm evaluator)
    {
        try
        {
            AddCorsHeaders(context.Response);

            if (context.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            string path = context.Request.Url?.AbsolutePath?.Trim('/') ?? string.Empty;
            if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonResponseAsync(context.Response, 200, new { ok = true, service = "fizban-local-api" });
                return;
            }

            if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.Equals("analyze", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
                string body = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<PortalAnalysisRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PortalAnalysisRequest();

                PortalAnalysisResponse result = await evaluator.RunPortalAnalysisAsync(request);
                await WriteJsonResponseAsync(context.Response, 200, result);
                return;
            }

            await WriteJsonResponseAsync(context.Response, 404, new { success = false, error = "Endpoint not found." });
        }
        catch (Exception ex)
        {
            await WriteJsonResponseAsync(context.Response, 500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static void AddCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, int statusCode, object payload)
    {
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.Close();
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
