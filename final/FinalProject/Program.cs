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

    // ===== CSV PARSING WITH QUOTE HANDLING =====
    /// <summary>
    /// Parses a CSV row respecting quoted fields for values containing commas or newlines
    /// </summary>
    private string[] ParseCsvRow(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    // Toggle quote mode
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // Field separator
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString());
        return fields.ToArray();
    }

    private void LoadCardDatabase()
    {
        string dataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cards.csv");

        // Create sample database if missing
        if (!File.Exists(dataFile))
        {
            File.WriteAllText(dataFile,
                "Name,ManaCost,Colors,Type,Category,IsLand,OracleText\n" +
                "Island,0, ,Land,Land,true,\"\"\n" +
                "Forest,0, ,Land,Land,true,\"\"\n" +
                "Swamp,0, ,Land,Land,true,\"\"\n" +
                "Mountain,0, ,Land,Land,true,\"\"\n" +
                "Plains,0, ,Land,Land,true,\"\"\n" +
                "Watery Grave,0,U;B,Land — Island Swamp,Land,true,\"\"\n" +
                "Sol Ring,1, ,Artifact,Artifact,false,\"\"\n" +
                "Swords to Plowshares,1, ,Instant,Removal,false,\"Remove target creature or planeswalker.\"\n" +
                "Rhystic Study,3,U,Enchantment,Draw,false,\"Whenever an opponent casts a spell, you may draw a card unless that player pays {1}.\"\n" +
                "Nicol Bolas Dragon God,6,U;B;R,Planeswalker,Planeswalker,false,\"\"\n");
        }

        try
        {
            foreach (var row in File.ReadAllLines(dataFile).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                var fields = ParseCsvRow(row);
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
                string oracleText = "";

                if (fields.Length >= 3)
                    colors = fields[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
                if (fields.Length >= 4)
                    type = fields[3].Trim();
                if (fields.Length >= 5)
                    category = fields[4].Trim();
                if (fields.Length >= 6)
                    isLand = bool.TryParse(fields[5], out bool isl) && isl;
                if (fields.Length >= 7)
                    oracleText = fields[6].Trim().Trim('"');

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
                    IsLand = isLand,
                    OracleText = oracleText
                };

                cardDatabase[name] = record;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load card database: " + ex.Message, "Deck Evaluator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ===== HELPER METHODS FOR EFFICIENT CARD FILTERING =====
    
    /// <summary>
    /// Determines if a card is a non-land card (used consistently throughout the application)
    /// </summary>
    private bool IsNonLand(Card card)
    {
        return !card.IsLand && (card.Type == null || card.Type.IndexOf("Land", StringComparison.OrdinalIgnoreCase) < 0);
    }

    /// <summary>
    /// Gets all non-land cards grouped by category (efficient single-pass operation)
    /// </summary>
    private Dictionary<string, int> GetNonLandCategories(Deck deck)
    {
        return deck.cards
            .Where(IsNonLand)
            .GroupBy(c => c.Category ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());
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
            int numSimulations = 1000000;
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

            // Display comprehensive evaluation results  
            resultsTextBox.Clear();

            // ===== TITLE AND METADATA =====
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 12, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.DarkBlue;
            resultsTextBox.AppendText("Simulation Results\n");
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Regular);
            resultsTextBox.SelectionColor = Color.Black;
            resultsTextBox.AppendText($"({numSimulations} games, {maxTurns} turns each)\n\n");

            // ===== DECK COMPOSITION =====
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.AppendText("Deck Composition:\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            int landCount = deck.LandCount;
            int nonLandCount = deck.cards.Count - landCount;
            resultsTextBox.AppendText($"• Total Cards: {deck.cards.Count}\n");
            resultsTextBox.AppendText($"• Lands: {landCount} ({(landCount * 100.0 / deck.cards.Count):F1}%)\n");
            resultsTextBox.AppendText($"• Non-Land Spells: {nonLandCount} ({(nonLandCount * 100.0 / deck.cards.Count):F1}%)\n\n");

            // ===== MANA SIMULATION STATISTICS =====
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.AppendText("Mana Statistics (from 1,000,000 game simulations):\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            resultsTextBox.AppendText($"• Average missed land drops: {results.AverageMissedLands:F2}\n");
            resultsTextBox.AppendText($"• Average lands played by turn {maxTurns}: {results.AverageLandsPlayed:F2}\n");
            resultsTextBox.AppendText($"• Land drop success rate: {(100 - (results.AverageMissedLands / maxTurns * 100)):F1}%\n");
            resultsTextBox.AppendText($"• Average playable cards across hand: {results.AverageCardsPlayable:F2}\n");
            resultsTextBox.AppendText($"• Average idle turns (nothing to play): {results.AverageIdleTurns:F2}\n\n");

            // ===== MANA CURVE ANALYSIS =====
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
                    for (int i = 0; i < kv.Value; i++)
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

            // ===== MANA BRACKET DISTRIBUTION =====
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("📈 Mana Brackets (Grouped):\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            resultsTextBox.SelectionColor = Color.Black;
            var manaBrackets = deck.GetManaBrackets();
            foreach (var kv in manaBrackets.OrderBy(k => k.Key))
            {
                int percentage = nonLandCount > 0 ? (kv.Value * 100 / nonLandCount) : 0;
                resultsTextBox.AppendText($"• {kv.Key} mana: {kv.Value} cards ({percentage}%)\n");
            }
            resultsTextBox.AppendText("\n");

            // ===== CARD TYPE BREAKDOWN =====
            resultsTextBox.SelectionFont = new Font(resultsTextBox.Font.FontFamily, 10, FontStyle.Bold);
            resultsTextBox.AppendText("Card Types:\n");
            resultsTextBox.SelectionFont = resultsTextBox.Font;
            var categories = GetNonLandCategories(deck);
            if (categories.Any())
            {
                foreach (var kv in categories.OrderByDescending(k => k.Value))
                {
                    int percentage = nonLandCount > 0 ? (kv.Value * 100 / nonLandCount) : 0;
                    resultsTextBox.AppendText($"• {kv.Key}: {kv.Value} cards ({percentage}%)\n");
                }
            }
            else
            {
                resultsTextBox.AppendText("• No spells found\n");
            }
            resultsTextBox.AppendText("\n");

            // ===== RECOMMENDATIONS =====
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
        Task<(int cost, List<string> colors, string type, string category, string oracleText)?>? commanderDataTask = null;
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
            string oracleText = "";
            bool isLand = false;
            if (commanderData.HasValue)
            {
                manaCost = commanderData.Value.cost;
                colors = commanderData.Value.colors;
                type = commanderData.Value.type;
                oracleText = commanderData.Value.oracleText;

                if (cardDatabase.TryGetValue(commanderName.Trim(), out var rec))
                {
                    isLand = rec.IsLand;
                }
                else
                {
                    isLand = type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            deck.AddCard(new Card { Name = commanderName, IsLand = isLand, ManaCost = manaCost, Colors = colors, Type = type, Category = category, OracleText = oracleText });
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
            string oracleText = "";
            bool isLand = false;

            if (cardData.HasValue)
            {
                manaCost = cardData.Value.cost;
                colors = cardData.Value.colors;
                type = cardData.Value.type;
                category = cardData.Value.category;
                oracleText = cardData.Value.oracleText;

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
                deck.AddCard(new Card { Name = cardName, IsLand = isLand, ManaCost = manaCost, Colors = colors, Type = type, Category = category, OracleText = oracleText });
            }
        }

        return deck;
    }

    private Task<(int cost, List<string> colors, string type, string category, string oracleText)?> GetCardData(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<(int, List<string>, string, string, string)?>((0, new List<string>(), "", "Unknown", ""));

        if (cardDatabase.TryGetValue(name.Trim(), out var rec))
        {
            return Task.FromResult<(int, List<string>, string, string, string)?>(
                (rec.ManaCost, rec.Colors, rec.Type, rec.Category, rec.OracleText ?? ""));
        }

        // Fallback: if name not in DB, attempt case-insensitive partial matching
        var match = cardDatabase.Values.FirstOrDefault(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return Task.FromResult<(int, List<string>, string, string, string)?>(
                (match.ManaCost, match.Colors, match.Type, match.Category, match.OracleText ?? ""));
        }

        // Not found
        return Task.FromResult<(int, List<string>, string, string, string)?>((0, new List<string>(), "Unknown", "Unknown", ""));
    }

    private class CardDbRecord
    {
        public string Name { get; set; }
        public int ManaCost { get; set; }
        public List<string> Colors { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public bool IsLand { get; set; }
        public string OracleText { get; set; }
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
    public string? OracleText { get; set; } // Card ability text from Scryfall
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
    public int CardsPlayable { get; set; }
    public int IdleTurns { get; set; }
}

public class EvaluationResults
{
    public double AverageMissedLands { get; set; }
    public double AverageLandsPlayed { get; set; }
    public double AverageCardsPlayable { get; set; }
    public double AverageIdleTurns { get; set; }
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
            AverageLandsPlayed = results.Average(r => r.LandsPlayed),
            AverageCardsPlayable = results.Average(r => r.CardsPlayable),
            AverageIdleTurns = results.Average(r => r.IdleTurns)
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
        int cardsPlayable = 0;
        int idleTurns = 0;

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

        // Count playable spells in opening hand
        cardsPlayable += hand.Count(c => !c.IsLand);

        // Simulate turns
        for (int turn = 1; turn <= maxTurns; turn++)
        {
            if (shuffledDeck.Count == 0) break;

            var drawnCard = shuffledDeck[0];
            shuffledDeck.RemoveAt(0);
            hand.Add(drawnCard);

            // In goldfish, we play a land if we have one
            var landInHand = hand.FirstOrDefault(c => c.IsLand);
            bool playedAnything = false;

            if (landInHand != null)
            {
                hand.Remove(landInHand);
                landsPlayed++;
                playedAnything = true;
            }
            else
            {
                missedLands++;
            }

            // Count how many non-land spells could be played (simplified: count available in hand)
            int playableSpells = hand.Count(c => !c.IsLand);
            cardsPlayable += playableSpells;

            // If nothing was played this turn, it's an idle turn
            if (!playedAnything && playableSpells == 0)
            {
                idleTurns++;
            }
        }

        return new SimulationResult 
        { 
            MissedLands = missedLands, 
            LandsPlayed = landsPlayed,
            CardsPlayable = cardsPlayable,
            IdleTurns = idleTurns
        };
    }

    private void GenerateRecommendations(EvaluationResults results, Deck deck)
    {
        results.Recommendations.Add("\n=== MANA & LAND ANALYSIS ===");
        
        // Land count and mana recommendations
        int landCount = deck.LandCount;
        int nonLandCount = deck.cards.Count - landCount;
        double landPercentage = (landCount * 100.0 / deck.cards.Count);
        
        if (results.AverageMissedLands > 2)
        {
            results.Recommendations.Add($"🚨 HIGH PRIORITY: Avg {results.AverageMissedLands:F2} missed land drops. Add {Math.Ceiling(results.AverageMissedLands)} more lands (currently {landCount} / {landPercentage:F1}%)");
        }
        else if (results.AverageMissedLands < 0.2)
        {
            results.Recommendations.Add($"💡 Excess lands: Avg {results.AverageMissedLands:F2} missed drops suggests {landCount - 2}-{landCount - 4} lands might work better");
        }
        else
        {
            results.Recommendations.Add($"✓ Lands optimal: {landCount} lands ({landPercentage:F1}%) - {results.AverageMissedLands:F2} avg missed drops");
        }

        // Mana Curve Projection
        results.Recommendations.Add("\n=== MANA CURVE PROJECTION ===");
        var manaCurve = deck.GetManaCurve();
        int lowCost = manaCurve.Where(kv => kv.Key <= 2).Sum(kv => kv.Value);
        int midCost = manaCurve.Where(kv => kv.Key >= 3 && kv.Key <= 4).Sum(kv => kv.Value);
        int highCost = manaCurve.Where(kv => kv.Key >= 5).Sum(kv => kv.Value);
        
        double lowPct = (lowCost * 100.0 / nonLandCount);
        double midPct = (midCost * 100.0 / nonLandCount);
        double highPct = (highCost * 100.0 / nonLandCount);
        
        results.Recommendations.Add($"Early (0-2): {lowCost} cards ({lowPct:F1}%) - {'░░░░░░░░░░'.Substring(0, (int)(lowPct/10))}");
        results.Recommendations.Add($"Mid (3-4):   {midCost} cards ({midPct:F1}%) - {'░░░░░░░░░░'.Substring(0, (int)(midPct/10))}");
        results.Recommendations.Add($"Late (5+):   {highCost} cards ({highPct:F1}%) - {'░░░░░░░░░░'.Substring(0, (int)(highPct/10))}");

        if (highCost > lowCost * 1.5)
        {
            results.Recommendations.Add($"📊 Top-heavy curve detected. Add {Math.Ceiling((highCost - lowCost) / 2.0)} low-cost spells.");
        }
        else if (lowCost > highCost * 2)
        {
            results.Recommendations.Add($"📊 Consider adding {Math.Ceiling((lowCost - highCost) / 3.0)} higher-cost finishers.");
        }
        else
        {
            results.Recommendations.Add($"✓ Mana curve is well-balanced");
        }

        // Creature Analysis (using Type field for accuracy)
        results.Recommendations.Add("\n=== CREATURE & SPELL BREAKDOWN ===");
        int creatureCount = deck.cards.Count(c => !c.IsLand && c.Type != null && c.Type.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0);
        int instantCount = deck.cards.Count(c => !c.IsLand && c.Type != null && c.Type.IndexOf("Instant", StringComparison.OrdinalIgnoreCase) >= 0);
        int sorceryCount = deck.cards.Count(c => !c.IsLand && c.Type != null && c.Type.IndexOf("Sorcery", StringComparison.OrdinalIgnoreCase) >= 0);
        int artifactCount = deck.cards.Count(c => !c.IsLand && c.Type != null && c.Type.IndexOf("Artifact", StringComparison.OrdinalIgnoreCase) >= 0);
        int enchantmentCount = deck.cards.Count(c => !c.IsLand && c.Type != null && c.Type.IndexOf("Enchantment", StringComparison.OrdinalIgnoreCase) >= 0);
        int planeswalkerCount = deck.cards.Count(c => !c.IsLand && c.Type != null && c.Type.IndexOf("Planeswalker", StringComparison.OrdinalIgnoreCase) >= 0);

        if (creatureCount < 10)
            results.Recommendations.Add($"⚠️  Creatures: {creatureCount} - Add {10 - creatureCount} for better board presence");
        else if (creatureCount < 20)
            results.Recommendations.Add($"✓ Creatures: {creatureCount} - Good foundation for board development");
        else if (creatureCount <= 28)
            results.Recommendations.Add($"✓ Creatures: {creatureCount} - Strong creature count");
        else
            results.Recommendations.Add($"⚠️  Creatures: {creatureCount} - Consider focusing on instant/sorcery strategy");

        if (instantCount + sorceryCount >= 12)
            results.Recommendations.Add($"✓ Instants/Sorceries: {instantCount + sorceryCount} - Good spell base for interaction");
        else
            results.Recommendations.Add($"⚠️  Instants/Sorceries: {instantCount + sorceryCount} - Add {8 - (instantCount + sorceryCount)} more for protection/removal");

        if (artifactCount > 0)
            results.Recommendations.Add($"Artifacts: {artifactCount} - {(artifactCount < 5 ? "Utility pieces" : "Strong artifact synergy")}");

        if (enchantmentCount > 0)
            results.Recommendations.Add($"Enchantments: {enchantmentCount}");

        if (planeswalkerCount > 0)
            results.Recommendations.Add($"Planeswalkers: {planeswalkerCount}");

        // Playability Analysis
        results.Recommendations.Add("\n=== PLAYABILITY METRICS ===");
        double cardsPerTurn = results.AverageCardsPlayable / 10.0; // Over 10 turns
        results.Recommendations.Add($"Average cards playable per turn: {cardsPerTurn:F2}");
        results.Recommendations.Add($"Average idle turns: {results.AverageIdleTurns:F2} out of 10");
        
        if (results.AverageIdleTurns > 2)
            results.Recommendations.Add($"⚠️  Too many idle turns. Consider adding more low-cost cards or mana acceleration.");
        else if (results.AverageIdleTurns < 0.5)
            results.Recommendations.Add($"✓ Excellent consistency - plenty to play each turn!");
    }
}