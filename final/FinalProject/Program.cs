using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
#nullable enable

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DeckEvaluatorForm());
    }
}

public class DeckEvaluatorForm : Form
{
    private Dictionary<string, CardDbRecord> cardDatabase = new Dictionary<string, CardDbRecord>(StringComparer.OrdinalIgnoreCase);
    private TextBox commanderTextBox;
    private TextBox deckInputTextBox;
    private Button evaluateButton;
    private RichTextBox resultsTextBox;
    private ProgressBar progressBar;
    private Label progressLabel;
    private Button quitButton;

    public DeckEvaluatorForm()
    {
        this.Text = "MTG Commander Deck Evaluator";
        this.Size = new System.Drawing.Size(650, 580);
        this.BackColor = Color.LightGray;

        // Title
        Label titleLabel = new Label();
        titleLabel.Text = "🃏 Magic: The Gathering Commander Deck Evaluator 🃏";
        titleLabel.Font = new Font("Arial", 14, FontStyle.Bold);
        titleLabel.ForeColor = Color.DarkBlue;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.Location = new System.Drawing.Point(10, 10);
        titleLabel.Size = new System.Drawing.Size(620, 30);
        this.Controls.Add(titleLabel);

        // Commander input label
        Label commanderLabel = new Label();
        commanderLabel.Text = "Commander (optional):";
        commanderLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        commanderLabel.Location = new System.Drawing.Point(10, 45);
        commanderLabel.Size = new System.Drawing.Size(150, 20);
        this.Controls.Add(commanderLabel);

        // Commander text box
        commanderTextBox = new TextBox();
        commanderTextBox.Font = new Font("Consolas", 10);
        commanderTextBox.Location = new System.Drawing.Point(160, 45);
        commanderTextBox.Size = new System.Drawing.Size(470, 20);
        this.Controls.Add(commanderTextBox);

        // Deck input label
        Label deckLabel = new Label();
        deckLabel.Text = "Deck List (99 cards):";
        deckLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        deckLabel.Location = new System.Drawing.Point(10, 75);
        deckLabel.Size = new System.Drawing.Size(150, 20);
        this.Controls.Add(deckLabel);

        // Deck input text box
        deckInputTextBox = new TextBox();
        deckInputTextBox.Multiline = true;
        deckInputTextBox.ScrollBars = ScrollBars.Vertical;
        deckInputTextBox.Font = new Font("Consolas", 10);
        deckInputTextBox.Location = new System.Drawing.Point(10, 100);
        deckInputTextBox.Size = new System.Drawing.Size(620, 180);
        this.Controls.Add(deckInputTextBox);

        // Evaluate button
        evaluateButton = new Button();
        evaluateButton.Text = "Evaluate Deck";
        evaluateButton.Font = new Font("Arial", 10, FontStyle.Bold);
        evaluateButton.BackColor = Color.LightGreen;
        evaluateButton.Location = new System.Drawing.Point(10, 290);
        evaluateButton.Size = new System.Drawing.Size(120, 35);
        evaluateButton.Click += EvaluateButton_Click;
        this.Controls.Add(evaluateButton);

        // Quit button
        quitButton = new Button();
        quitButton.Text = "Quit";
        quitButton.Font = new Font("Arial", 10, FontStyle.Bold);
        quitButton.BackColor = Color.LightCoral;
        quitButton.Location = new System.Drawing.Point(140, 290);
        quitButton.Size = new System.Drawing.Size(120, 35);
        quitButton.Click += (s, e) => this.Close();
        this.Controls.Add(quitButton);

        // Progress bar
        progressBar = new ProgressBar();
        progressBar.Location = new System.Drawing.Point(10, 340);
        progressBar.Size = new System.Drawing.Size(620, 25);
        progressBar.Minimum = 0;
        progressBar.Maximum = 1000;
        this.Controls.Add(progressBar);

        // Progress label
        progressLabel = new Label();
        progressLabel.Text = "";
        progressLabel.Font = new Font("Arial", 9);
        progressLabel.Location = new System.Drawing.Point(10, 370);
        progressLabel.Size = new System.Drawing.Size(300, 20);
        this.Controls.Add(progressLabel);

        // Results label
        Label resultsLabel = new Label();
        resultsLabel.Text = "Results:";
        resultsLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        resultsLabel.Location = new System.Drawing.Point(10, 395);
        resultsLabel.Size = new System.Drawing.Size(100, 20);
        this.Controls.Add(resultsLabel);

        // Results text box
        resultsTextBox = new RichTextBox();
        resultsTextBox.ReadOnly = true;
        resultsTextBox.Font = new Font("Arial", 9);
        resultsTextBox.BackColor = Color.WhiteSmoke;
        resultsTextBox.Location = new System.Drawing.Point(10, 420);
        resultsTextBox.Size = new System.Drawing.Size(620, 100);
        this.Controls.Add(resultsTextBox);

        LoadCardDatabase();
    }

    private void LoadCardDatabase()
    {
        string dataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cards.csv");

        // Create sample database if missing
        if (!File.Exists(dataFile))
        {
            File.WriteAllText(dataFile,
                "Name,ManaCost,Colors,Type,Category,IsLand\n" +
                "Island,0, ,Land,Land,true\n" +
                "Forest,0, ,Land,Land,true\n" +
                "Swamp,0, ,Land,Land,true\n" +
                "Mountain,0, ,Land,Land,true\n" +
                "Plains,0, ,Land,Land,true\n" +
                "Watery Grave,0,U;B,Land — Island Swamp,Land,true\n" +
                "Sol Ring,1, ,Artifact,Artifact,false\n" +
                "Swords to Plowshares,1, ,Instant,Removal,false\n" +
                "Rhystic Study,3,U,Enchantment,Draw,false\n" +
                "Nicol Bolas Dragon God,6,U;B;R,Planeswalker,Planeswalker,false\n");
        }

        try
        {
            foreach (var row in File.ReadAllLines(dataFile).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                var fields = row.Split(',');
                if (fields.Length < 5) continue;

                var name = fields[0].Trim();
                if (string.IsNullOrEmpty(name)) continue;

                int manaCost = 0;
                if (double.TryParse(fields[1], out double mcd))
                {
                    manaCost = (int)Math.Round(mcd);
                }
                var colors = new List<string>();
                string type = "";
                string category = "Unknown";
                bool isLand = false;

                if (fields.Length >= 3)
                    colors = fields[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
                if (fields.Length >= 4)
                    type = fields[3].Trim();
                if (fields.Length >= 5)
                    category = fields[4].Trim();
                if (fields.Length >= 6)
                    isLand = bool.TryParse(fields[5], out bool isl) && isl;

                // Backward compatibility with existing list if no IsLand field.
                if (!isLand && type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0)
                    isLand = true;

                var record = new CardDbRecord
                {
                    Name = name,
                    ManaCost = manaCost,
                    Colors = colors,
                    Type = type,
                    Category = category,
                    IsLand = isLand
                };

                cardDatabase[name] = record;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load card database: " + ex.Message, "Deck Evaluator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void EvaluateButton_Click(object sender, EventArgs e)
    {
        try
        {
            Deck deck = await ParseDeck(deckInputTextBox.Text, commanderTextBox.Text);
            if (deck.cards.Count == 0)
            {
                resultsTextBox.Clear();
                resultsTextBox.SelectionColor = Color.Red;
                resultsTextBox.AppendText("No cards found in deck list.");
                return;
            }

            // Run simulations
            int numSimulations = 1000;
            int maxTurns = 10;

            progressBar.Value = 0;
            progressLabel.Text = "Running simulations...";

            var evaluator = new DeckEvaluator(deck);
            var results = await Task.Run(() => evaluator.RunSimulations(numSimulations, maxTurns, progress =>
            {
                this.Invoke((Action)(() =>
                {
                    progressBar.Value = progress;
                    progressLabel.Text = $"Simulating game {progress}/{numSimulations}";
                }));
            }));

            progressLabel.Text = "Done!";

            // Display results
            resultsTextBox.Clear();

            // Title
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 12, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.DarkBlue;
            resultsTextBox.AppendText("Simulation Results\n");
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Regular);
            resultsTextBox.SelectionColor = Color.Black;
            resultsTextBox.AppendText($"({numSimulations} games, {maxTurns} turns each)\n\n");

            // Stats
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.AppendText("Statistics:\n");

            resultsTextBox.SelectionFont = resultsTextBox.Font;
            resultsTextBox.AppendText($"• Average missed land drops: {results.AverageMissedLands:F2}\n");
            resultsTextBox.AppendText($"• Average lands played by turn {maxTurns}: {results.AverageLandsPlayed:F2}\n\n");

            // Mana Curve
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.DarkBlue;
            resultsTextBox.AppendText("📊 Mana Curve (Detailed):\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            resultsTextBox.SelectionColor = Color.Black;
            var manaCurve = deck.GetManaCurve();
            if (manaCurve.Any())
            {
                foreach (var kv in manaCurve.OrderBy(k => k.Key))
                {
                    string manaSymbols = "";
                    for (int i = 0; i < kv.Key; i++)
                    {
                        manaSymbols += "●";
                    }
                    resultsTextBox.AppendText($"• {kv.Key} mana: {kv.Value} cards {manaSymbols}\n");
                }
            }
            else
            {
                resultsTextBox.AppendText("• No non-land cards found\n");
            }
            resultsTextBox.AppendText("\n");

            // Mana Brackets
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("📈 Mana Brackets (Grouped):\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            resultsTextBox.SelectionColor = Color.Black;
            var manaBrackets = deck.GetManaBrackets();
            foreach (var kv in manaBrackets.OrderBy(k => k.Key))
            {
                resultsTextBox.AppendText($"• {kv.Key} mana: {kv.Value} cards\n");
            }
            resultsTextBox.AppendText("\n");

            // Card Types
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.AppendText("Card Types:\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            var categories = deck.cards.Where(c => !c.IsLand).GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count());
            foreach (var kv in categories.OrderBy(k => k.Key))
            {
                resultsTextBox.AppendText($"• {kv.Key}: {kv.Value} cards\n");
            }
            resultsTextBox.AppendText("\n");

            // Recommendations
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("Recommendations:\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            resultsTextBox.SelectionColor = Color.Black;
            foreach (var rec in results.Recommendations)
            {
                resultsTextBox.AppendText($"• {rec}\n");
            }
        }
        catch (Exception ex)
        {
            resultsTextBox.Clear();
            resultsTextBox.SelectionColor = Color.Red;
            resultsTextBox.AppendText($"Error: {ex.Message}");
        }
    }

    private async Task<Deck> ParseDeck(string deckText, string commanderText)
    {
        var deck = new Deck();

        // Parse commander if provided
        string commanderName = "";
        Task<(int cost, List<string> colors, string type, string category)?>? commanderDataTask = null;
        if (!string.IsNullOrWhiteSpace(commanderText))
        {
            commanderName = commanderText.Trim();
            commanderDataTask = GetCardData(commanderName);
        }

        var lines = deckText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var cardNames = new List<string>();
        var quantities = new List<int>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = trimmed.Split(new[] { ' ' }, 2);
            if (parts.Length < 2 || !int.TryParse(parts[0], out int quantity))
            {
                throw new FormatException($"Invalid line: {trimmed}");
            }

            string cardName = parts[1].Trim();
            cardNames.Add(cardName);
            quantities.Add(quantity);
        }

        var cardDataTasks = cardNames.Select(name => GetCardData(name)).ToArray();
        var cardDatas = await Task.WhenAll(cardDataTasks);

        var commanderData = commanderDataTask != null ? await commanderDataTask : null;

        // Add commander
        if (!string.IsNullOrWhiteSpace(commanderText))
        {
            int manaCost = 0;
            var colors = new List<string>();
            string type = "";
            string category = "Commander";
            bool isLand = false;
            if (commanderData.HasValue)
            {
                manaCost = commanderData.Value.cost;
                colors = commanderData.Value.colors;
                type = commanderData.Value.type;

                if (cardDatabase.TryGetValue(commanderName.Trim(), out var rec))
                {
                    isLand = rec.IsLand;
                }
                else
                {
                    isLand = type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            deck.AddCard(new Card { Name = commanderName, IsLand = isLand, ManaCost = manaCost, Colors = colors, Type = type, Category = category });
        }

        // Add deck cards
        for (int i = 0; i < cardNames.Count; i++)
        {
            string cardName = cardNames[i];
            int quantity = quantities[i];
            var cardData = cardDatas[i];
            int manaCost = 0;
            var colors = new List<string>();
            string type = "";
            string category = "";
            bool isLand = false;

            if (cardData.HasValue)
            {
                manaCost = cardData.Value.cost;
                colors = cardData.Value.colors;
                type = cardData.Value.type;
                category = cardData.Value.category;

                // use database-dependent land flag first
                if (cardDatabase.TryGetValue(cardName.Trim(), out var rec))
                {
                    isLand = rec.IsLand;
                }
                else
                {
                    isLand = type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            else
            {
                // Fallback for cards not in DB
                manaCost = 3; // Default
                category = "Unknown";
                isLand = cardName.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            for (int j = 0; j < quantity; j++)
            {
                deck.AddCard(new Card { Name = cardName, IsLand = isLand, ManaCost = manaCost, Colors = colors, Type = type, Category = category });
            }
        }

        return deck;
    }

    private Task<(int cost, List<string> colors, string type, string category)?> GetCardData(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<(int, List<string>, string, string)?>((0, new List<string>(), "", "Unknown"));

        if (cardDatabase.TryGetValue(name.Trim(), out var rec))
        {
            return Task.FromResult<(int, List<string>, string, string)?>(
                (rec.ManaCost, rec.Colors, rec.Type, rec.Category));
        }

        // Fallback: if name not in DB, attempt case-insensitive partial matching
        var match = cardDatabase.Values.FirstOrDefault(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return Task.FromResult<(int, List<string>, string, string)?>(
                (match.ManaCost, match.Colors, match.Type, match.Category));
        }

        // Not found
        return Task.FromResult<(int, List<string>, string, string)?>((0, new List<string>(), "Unknown", "Unknown"));
    }

    private class CardDbRecord
    {
        public string Name { get; set; }
        public int ManaCost { get; set; }
        public List<string> Colors { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public bool IsLand { get; set; }
    }

    private int ParseManaCost(string manaCost)
    {
        // Simple parser for mana cost like "{2}{U}{U}" -> 4
        int cost = 0;
        var parts = manaCost.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part, out int num))
                cost += num;
            else if (part.Length == 1 && "WUBRG".Contains(part.ToUpper()))
                cost += 1;
        }
        return cost;
    }

    private List<string> ParseColors(string colorStr)
    {
        var colors = new List<string>();
        // Simple parser for ["U", "B"] -> ["U", "B"]
        var clean = colorStr.Trim('[', ']', '"', ' ');
        var parts = clean.Split(new[] { "\",\"", "\", \"", ", " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var color = part.Trim('"');
            if (!string.IsNullOrEmpty(color))
                colors.Add(color);
        }
        return colors;
    }

    private string CategorizeCard(string type, string json)
    {
        if (type.Contains("Land")) return "Land";
        if (type.Contains("Creature")) return "Creature";
        if (type.Contains("Instant") || type.Contains("Sorcery"))
        {
            string oracle = "";
            if (json.Contains("\"oracle_text\""))
            {
                var oracleStart = json.IndexOf("\"oracle_text\":") + 15;
                var oracleEnd = json.IndexOf("\"", oracleStart);
                oracle = json.Substring(oracleStart, oracleEnd - oracleStart).ToLower();
            }
            if (oracle.Contains("draw") && !oracle.Contains("mill")) return "Card Draw";
            if (oracle.Contains("destroy") || oracle.Contains("exile") || oracle.Contains("remove") || oracle.Contains("bounce") || oracle.Contains("return")) return "Removal";
            if (oracle.Contains("counter") && oracle.Contains("spell")) return "Counterspell";
            if (oracle.Contains("tutor") || oracle.Contains("search") || oracle.Contains("look")) return "Tutor";
            if (oracle.Contains("gain life") || oracle.Contains("life") || oracle.Contains("lifelink")) return "Life Gain";
            if (oracle.Contains("damage") || oracle.Contains("burn") || oracle.Contains("lightning") || oracle.Contains("fire")) return "Direct Damage";
            if (oracle.Contains("create") && oracle.Contains("token")) return "Token Generation";
            if (oracle.Contains("mill") || oracle.Contains("graveyard")) return "Mill";
            if (oracle.Contains("discard") || oracle.Contains("force")) return "Discard";
            if (oracle.Contains("pump") || oracle.Contains("boost") || oracle.Contains("+") && oracle.Contains("/")) return "Pump Spell";
            if (oracle.Contains("recurring") || oracle.Contains("recur")) return "Recurring";
            if (oracle.Contains("board wipe") || oracle.Contains("destroy all") || oracle.Contains("exile all")) return "Board Wipe";
            if (oracle.Contains("protection") || oracle.Contains("hexproof") || oracle.Contains("indestructible")) return "Protection";
            if (oracle.Contains("sacrifice") || oracle.Contains("sac")) return "Sacrifice Outlet";
            if (oracle.Contains("copy") || oracle.Contains("clone")) return "Copy Effect";
            if (oracle.Contains("extra turn") || oracle.Contains("take an extra turn")) return "Extra Turn";
            if (oracle.Contains("shuffle") && oracle.Contains("library")) return "Library Manipulation";
            if (oracle.Contains("flashback") || oracle.Contains("retrace")) return "Flashback";
            if (oracle.Contains("surveil") || oracle.Contains("scry")) return "Information";
            return "Spell";
        }
        if (type.Contains("Artifact"))
        {
            if (type.Contains("Creature")) return "Artifact Creature";
            if (type.Contains("Equipment")) return "Equipment";
            if (type.Contains("Vehicle")) return "Vehicle";
            return "Artifact";
        }
        if (type.Contains("Enchantment"))
        {
            if (type.Contains("Aura")) return "Aura";
            return "Enchantment";
        }
        if (type.Contains("Planeswalker")) return "Planeswalker";
        if (type.Contains("Battle")) return "Battle";
        return "Other";
    }
}

public class Card
{
    public string? Name { get; set; }
    public bool IsLand { get; set; }
    public int ManaCost { get; set; }
    public List<string> Colors { get; set; } = new List<string>();
    public string? Type { get; set; }
    public string? Category { get; set; } // e.g., Card Draw, Removal, etc.
}

public class Deck
{
    public List<Card> cards = new List<Card>();

    public void AddCard(Card card)
    {
        cards.Add(card);
    }

    public List<Card> GetShuffledDeck()
    {
        var shuffled = new List<Card>(cards);
        Random rand = new Random();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }

    public int LandCount => cards.Count(c => c.IsLand);
    public Dictionary<int, int> GetManaCurve()
    {
        var groups = cards.Where(c => !c.IsLand).GroupBy(c => c.ManaCost);
        var dict = new Dictionary<int, int>();
        foreach (var group in groups)
        {
            dict[group.Key] = group.Count();
        }
        return dict;
    }

    public Dictionary<string, int> GetManaBrackets()
    {
        var nonLands = cards.Where(c => !c.IsLand);
        var brackets = new Dictionary<string, int>
        {
            ["0-2"] = nonLands.Count(c => c.ManaCost >= 0 && c.ManaCost <= 2),
            ["3-4"] = nonLands.Count(c => c.ManaCost >= 3 && c.ManaCost <= 4),
            ["5-6"] = nonLands.Count(c => c.ManaCost >= 5 && c.ManaCost <= 6),
            ["7+"] = nonLands.Count(c => c.ManaCost >= 7)
        };
        return brackets;
    }
}

public class SimulationResult
{
    public int MissedLands { get; set; }
    public int LandsPlayed { get; set; }
}

public class EvaluationResults
{
    public double AverageMissedLands { get; set; }
    public double AverageLandsPlayed { get; set; }
    public List<string> Recommendations { get; set; } = new List<string>();
}

public class DeckEvaluator
{
    private Deck deck;

    public DeckEvaluator(Deck deck)
    {
        this.deck = deck;
    }

    public EvaluationResults RunSimulations(int numSimulations, int maxTurns, Action<int> progressCallback = null)
    {
        var results = new List<SimulationResult>();

        for (int i = 0; i < numSimulations; i++)
        {
            var simResult = RunSingleSimulation(maxTurns);
            results.Add(simResult);
            progressCallback?.Invoke(i + 1);
        }

        var evalResults = new EvaluationResults
        {
            AverageMissedLands = results.Average(r => r.MissedLands),
            AverageLandsPlayed = results.Average(r => r.LandsPlayed)
        };

        // Generate recommendations
        GenerateRecommendations(evalResults, deck);

        return evalResults;
    }

    private SimulationResult RunSingleSimulation(int maxTurns)
    {
        var shuffledDeck = deck.GetShuffledDeck();
        var hand = new List<Card>();
        int landsPlayed = 0;
        int missedLands = 0;

        // Draw opening hand (7 cards)
        for (int i = 0; i < 7; i++)
        {
            if (shuffledDeck.Count > 0)
            {
                var card = shuffledDeck[0];
                shuffledDeck.RemoveAt(0);
                hand.Add(card);
                if (card.IsLand)
                {
                    landsPlayed++;
                }
            }
        }

        // Simulate turns
        for (int turn = 1; turn <= maxTurns; turn++)
        {
            if (shuffledDeck.Count == 0) break;

            var drawnCard = shuffledDeck[0];
            shuffledDeck.RemoveAt(0);
            hand.Add(drawnCard);

            // In goldfish, we play a land if we have one
            var landInHand = hand.FirstOrDefault(c => c.IsLand);
            if (landInHand != null)
            {
                hand.Remove(landInHand);
                landsPlayed++;
            }
            else
            {
                missedLands++;
            }
        }

        return new SimulationResult { MissedLands = missedLands, LandsPlayed = landsPlayed };
    }

    private void GenerateRecommendations(EvaluationResults results, Deck deck)
    {
        // Land count recommendation
        int landCount = deck.LandCount;
        if (results.AverageMissedLands > 2)
        {
            results.Recommendations.Add("Consider adding more lands. You missed too many land drops.");
        }
        else if (results.AverageMissedLands < 0.5)
        {
            results.Recommendations.Add("You might have too many lands. Consider reducing land count.");
        }
        else
        {
            results.Recommendations.Add("Land count seems balanced.");
        }

        // Mana curve
        var manaCurve = deck.GetManaCurve();
        int lowCost = manaCurve.Where(kv => kv.Key <= 2).Sum(kv => kv.Value);
        int midCost = manaCurve.Where(kv => kv.Key >= 3 && kv.Key <= 4).Sum(kv => kv.Value);
        int highCost = manaCurve.Where(kv => kv.Key >= 5).Sum(kv => kv.Value);

        if (highCost > lowCost + midCost)
        {
            results.Recommendations.Add("Your mana curve is top-heavy. Add more low-cost spells.");
        }
        else if (lowCost > midCost + highCost)
        {
            results.Recommendations.Add("Too many low-cost spells. Add some higher-cost cards for better curve.");
        }
        else
        {
            results.Recommendations.Add("Mana curve looks good.");
        }

        // Card types
        var categories = deck.cards.Where(c => !c.IsLand).GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count());
        int totalSpells = deck.cards.Count(c => !c.IsLand);

        // Card Draw
        if (categories.ContainsKey("Card Draw"))
        {
            int count = categories["Card Draw"];
            if (count < 8)
                results.Recommendations.Add($"Card Draw: {count} (suggest adding {8 - count} more for better consistency)");
            else if (count > 15)
                results.Recommendations.Add($"Card Draw: {count} (consider reducing to avoid flooding)");
            else
                results.Recommendations.Add($"Card Draw: {count} (balanced)");
        }
        else
        {
            results.Recommendations.Add("Card Draw: 0 (add 8-12 card draw spells for better deck consistency)");
        }

        // Removal
        if (categories.ContainsKey("Removal"))
        {
            int count = categories["Removal"];
            if (count < 6)
                results.Recommendations.Add($"Removal: {count} (suggest adding {6 - count} more for better board control)");
            else if (count > 12)
                results.Recommendations.Add($"Removal: {count} (consider reducing to focus on other strategies)");
            else
                results.Recommendations.Add($"Removal: {count} (balanced)");
        }
        else
        {
            results.Recommendations.Add("Removal: 0 (add 6-10 removal spells for board control)");
        }

        // Creatures
        if (categories.ContainsKey("Creature"))
        {
            int count = categories["Creature"];
            if (count < 15)
                results.Recommendations.Add($"Creatures: {count} (suggest adding {15 - count} more for better board presence)");
            else if (count > 25)
                results.Recommendations.Add($"Creatures: {count} (consider reducing to focus on other card types)");
            else
                results.Recommendations.Add($"Creatures: {count} (balanced)");
        }
        else
        {
            results.Recommendations.Add("Creatures: 0 (add 15-25 creatures for board presence)");
        }

        // Counterspells
        if (categories.ContainsKey("Counterspell"))
        {
            int count = categories["Counterspell"];
            if (count > 8)
                results.Recommendations.Add($"Counterspells: {count} (consider reducing if not playing control)");
            else
                results.Recommendations.Add($"Counterspells: {count} (reasonable for control decks)");
        }

        // Tutors
        if (categories.ContainsKey("Tutor"))
        {
            int count = categories["Tutor"];
            if (count > 6)
                results.Recommendations.Add($"Tutors: {count} (consider reducing to avoid mana flood)");
            else
                results.Recommendations.Add($"Tutors: {count} (good for consistency)");
        }

        // Board Wipes
        if (categories.ContainsKey("Board Wipe"))
        {
            int count = categories["Board Wipe"];
            if (count > 4)
                results.Recommendations.Add($"Board Wipes: {count} (consider reducing to avoid over-reliance)");
            else
                results.Recommendations.Add($"Board Wipes: {count} (good for control)");
        }

        // Protection
        if (categories.ContainsKey("Protection"))
        {
            int count = categories["Protection"];
            results.Recommendations.Add($"Protection: {count} (ensure they protect key threats)");
        }

        // Extra Turn
        if (categories.ContainsKey("Extra Turn"))
        {
            int count = categories["Extra Turn"];
            if (count > 2)
                results.Recommendations.Add($"Extra Turn: {count} (powerful but consider redundancy)");
            else
                results.Recommendations.Add($"Extra Turn: {count} (great finishers)");
        }

        // Artifacts
        if (categories.ContainsKey("Artifact"))
        {
            int count = categories["Artifact"];
            results.Recommendations.Add($"Artifacts: {count} (ensure they fit your mana curve)");
        }

        // Equipment
        if (categories.ContainsKey("Equipment"))
        {
            int count = categories["Equipment"];
            results.Recommendations.Add($"Equipment: {count} (attach to your creatures)");
        }

        // Enchantments
        if (categories.ContainsKey("Enchantment"))
        {
            int count = categories["Enchantment"];
            results.Recommendations.Add($"Enchantments: {count} (ensure they provide value)");
        }

        // Planeswalkers
        if (categories.ContainsKey("Planeswalker"))
        {
            int count = categories["Planeswalker"];
            if (count > 4)
                results.Recommendations.Add($"Planeswalkers: {count} (consider reducing to focus on other strategies)");
            else
                results.Recommendations.Add($"Planeswalkers: {count} (reasonable number)");
        }

        // Other categories
        foreach (var kv in categories.Where(kv => !new[] { "Card Draw", "Removal", "Creature", "Counterspell", "Tutor", "Board Wipe", "Protection", "Extra Turn", "Artifact", "Equipment", "Enchantment", "Planeswalker" }.Contains(kv.Key)))
        {
            results.Recommendations.Add($"{kv.Key}: {kv.Value} (consider if this fits your strategy)");
        }
    }
}