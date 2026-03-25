using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

#nullable enable

public class DeckEvaluatorForm : Form
{
    private static readonly HttpClient Http = new HttpClient();

    private readonly Dictionary<string, CardDbRecord> cardDatabase = new Dictionary<string, CardDbRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> missingCardNames = new List<string>();

    private TextBox commanderTextBox = null!;
    private TextBox moxfieldLinkTextBox = null!;
    private TextBox deckInputTextBox = null!;
    private Label deckCountLabel = null!;
    private Button evaluateButton = null!;
    private Button importMoxfieldButton = null!;
    private RichTextBox resultsTextBox = null!;
    private ProgressBar progressBar = null!;
    private Label progressLabel = null!;
    private Button quitButton = null!;
    private CheckBox onDrawCheckBox = null!;
    private Button saveReportButton = null!;

    public DeckEvaluatorForm()
    {
        Text = "MTG Commander Deck Evaluator";
        Size = new Size(900, 740);
        MinimumSize = new Size(860, 700);
        BackColor = Color.FromArgb(246, 248, 252);
        StartPosition = FormStartPosition.CenterScreen;

        var titleLabel = new Label
        {
            Text = "Magic: The Gathering Commander Deck Evaluator",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 12),
            Size = new Size(840, 36)
        };
        Controls.Add(titleLabel);

        var commanderLabel = new Label
        {
            Text = "Commander:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 65, 81),
            Location = new Point(20, 58),
            Size = new Size(110, 24)
        };
        Controls.Add(commanderLabel);

        commanderTextBox = new TextBox
        {
            Font = new Font("Segoe UI", 10),
            Location = new Point(130, 56),
            Size = new Size(730, 26)
        };
        Controls.Add(commanderTextBox);

        var moxfieldLabel = new Label
        {
            Text = "Moxfield URL:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 65, 81),
            Location = new Point(20, 92),
            Size = new Size(110, 24)
        };
        Controls.Add(moxfieldLabel);

        moxfieldLinkTextBox = new TextBox
        {
            Font = new Font("Segoe UI", 10),
            Location = new Point(130, 90),
            Size = new Size(570, 26),
            PlaceholderText = "https://www.moxfield.com/decks/..."
        };
        Controls.Add(moxfieldLinkTextBox);

        importMoxfieldButton = new Button
        {
            Text = "Import From Moxfield",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(710, 88),
            Size = new Size(150, 30)
        };
        importMoxfieldButton.FlatAppearance.BorderSize = 0;
        importMoxfieldButton.Click += ImportMoxfieldButton_Click;
        Controls.Add(importMoxfieldButton);

        var deckLabel = new Label
        {
            Text = "Deck List (99 cards):",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 65, 81),
            Location = new Point(20, 126),
            Size = new Size(150, 24)
        };
        Controls.Add(deckLabel);

        deckCountLabel = new Label
        {
            Text = "Cards Entered: 0",
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.FromArgb(75, 85, 99),
            Location = new Point(180, 128),
            Size = new Size(260, 20)
        };
        Controls.Add(deckCountLabel);

        deckInputTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Cascadia Mono", 10),
            Location = new Point(20, 152),
            Size = new Size(840, 260),
            BorderStyle = BorderStyle.FixedSingle
        };
        deckInputTextBox.TextChanged += (_, _) => UpdateDeckCountLabel();
        Controls.Add(deckInputTextBox);

        evaluateButton = new Button
        {
            Text = "Evaluate Deck",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(20, 422),
            Size = new Size(150, 36)
        };
        evaluateButton.FlatAppearance.BorderSize = 0;
        evaluateButton.Click += EvaluateButton_Click;
        Controls.Add(evaluateButton);

        quitButton = new Button
        {
            Text = "Quit",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(180, 422),
            Size = new Size(110, 36)
        };
        quitButton.FlatAppearance.BorderSize = 0;
        quitButton.Click += (_, _) => Close();
        Controls.Add(quitButton);

        onDrawCheckBox = new CheckBox
        {
            Text = "On the draw",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(55, 65, 81),
            Location = new Point(300, 428),
            Size = new Size(120, 24)
        };
        Controls.Add(onDrawCheckBox);

        saveReportButton = new Button
        {
            Text = "Save Report",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(107, 114, 128),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(430, 422),
            Size = new Size(110, 36)
        };
        saveReportButton.FlatAppearance.BorderSize = 0;
        saveReportButton.Click += SaveReportButton_Click;
        Controls.Add(saveReportButton);

        progressBar = new ProgressBar
        {
            Location = new Point(20, 468),
            Size = new Size(840, 18),
            Minimum = 0,
            Maximum = 1000
        };
        Controls.Add(progressBar);

        progressLabel = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(75, 85, 99),
            Location = new Point(20, 490),
            Size = new Size(760, 20)
        };
        Controls.Add(progressLabel);

        var resultsLabel = new Label
        {
            Text = "Results:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 65, 81),
            Location = new Point(20, 514),
            Size = new Size(100, 24)
        };
        Controls.Add(resultsLabel);

        resultsTextBox = new RichTextBox
        {
            ReadOnly = true,
            Font = new Font("Segoe UI", 10),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(20, 540),
            Size = new Size(840, 150)
        };
        Controls.Add(resultsTextBox);

        UpdateDeckCountLabel();
        LoadCardDatabase();
    }

    private string[] ParseCsvRow(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    currentField.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (current == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(current);
            }
        }

        fields.Add(currentField.ToString());
        return fields.ToArray();
    }

    private static string? ExtractMoxfieldDeckId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string trimmed = input.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Host.IndexOf("moxfield.com", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int decksIndex = Array.FindIndex(segments, segment => segment.Equals("decks", StringComparison.OrdinalIgnoreCase));
            if (decksIndex >= 0 && decksIndex + 1 < segments.Length)
            {
                string candidate = segments[decksIndex + 1].Trim();
                if (!candidate.Equals("public", StringComparison.OrdinalIgnoreCase)
                    && Regex.IsMatch(candidate, @"^[A-Za-z0-9_-]{6,}$"))
                {
                    return candidate;
                }
            }
        }

        var linkMatch = Regex.Match(trimmed, @"moxfield\.com/decks/([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
        if (linkMatch.Success)
            return linkMatch.Groups[1].Value;

        return Regex.IsMatch(trimmed, @"^[A-Za-z0-9_-]{6,}$") ? trimmed : null;
    }

    private static HttpRequestMessage CreateWebRequest(string url, string acceptHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept", acceptHeader);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Referrer = new Uri("https://www.moxfield.com/");
        request.Headers.TryAddWithoutValidation("Origin", "https://www.moxfield.com");
        return request;
    }

    private static async Task<string?> TryDownloadTextAsync(string url, string acceptHeader)
    {
        try
        {
            using var request = CreateWebRequest(url, acceptHeader);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
    }

    private static void AppendCardsFromBoard(JsonElement board, List<string> lines)
    {
        JsonElement cardsElement;
        if (board.ValueKind == JsonValueKind.Object && board.TryGetProperty("cards", out var nestedCards) && nestedCards.ValueKind == JsonValueKind.Object)
        {
            cardsElement = nestedCards;
        }
        else if (board.ValueKind == JsonValueKind.Object)
        {
            cardsElement = board;
        }
        else
        {
            return;
        }

        foreach (var property in cardsElement.EnumerateObject())
        {
            JsonElement cardEntry = property.Value;
            int quantity = 1;
            if (cardEntry.TryGetProperty("quantity", out var quantityElement))
            {
                if (quantityElement.TryGetInt32(out int intQuantity))
                    quantity = Math.Max(1, intQuantity);
                else if (quantityElement.TryGetDouble(out double doubleQuantity))
                    quantity = Math.Max(1, (int)Math.Round(doubleQuantity));
            }

            string? name = null;
            if (cardEntry.TryGetProperty("card", out var cardObject)
                && cardObject.ValueKind == JsonValueKind.Object
                && cardObject.TryGetProperty("name", out var nameElement)
                && nameElement.ValueKind == JsonValueKind.String)
            {
                name = nameElement.GetString();
            }
            else if (cardEntry.TryGetProperty("name", out var directName) && directName.ValueKind == JsonValueKind.String)
            {
                name = directName.GetString();
            }

            if (!string.IsNullOrWhiteSpace(name))
                lines.Add($"{quantity} {name}");
        }
    }

    private static bool TryCollectMoxfieldMainboard(JsonElement root, List<string> lines)
    {
        if (root.TryGetProperty("boards", out var boards)
            && boards.ValueKind == JsonValueKind.Object
            && boards.TryGetProperty("mainboard", out var mainboard)
            && mainboard.ValueKind == JsonValueKind.Object)
        {
            AppendCardsFromBoard(mainboard, lines);
        }

        if (!lines.Any() && root.TryGetProperty("mainboard", out var rootMainboard) && rootMainboard.ValueKind == JsonValueKind.Object)
            AppendCardsFromBoard(rootMainboard, lines);

        if (!lines.Any() && root.TryGetProperty("cards", out var cards) && cards.ValueKind == JsonValueKind.Object)
            AppendCardsFromBoard(cards, lines);

        return lines.Any();
    }

    private static string ExtractCommanderFromMoxfieldHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        foreach (var pattern in new[]
        {
            "A Commander deck featuring (?<name>.+?) by",
            @"- Commander \((?<name>.+?)\)"
        })
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
                return WebUtility.HtmlDecode(match.Groups["name"].Value).Trim();
        }

        return string.Empty;
    }

    private static List<(string Name, int Quantity)> ParseMoxfieldMirrorEntries(string markdown)
    {
        var entries = new List<(string Name, int Quantity)>();
        if (string.IsNullOrWhiteSpace(markdown))
            return entries;

        string normalized = markdown.Replace("\r\n", "\n");
        int contentIndex = normalized.IndexOf("Markdown Content:", StringComparison.OrdinalIgnoreCase);
        if (contentIndex >= 0)
            normalized = normalized.Substring(contentIndex + "Markdown Content:".Length);

        string[] endMarkers =
        {
            "Tokens (",
            "Considering (",
            "## Cost Analysis",
            "## Color Analysis",
            "## Sample Hand",
            "## Comments",
            "##  Comments",
            "## Similar Decks",
            "## Additional Links"
        };

        int endIndex = endMarkers
            .Select(marker => normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (endIndex > 0)
            normalized = normalized.Substring(0, endIndex);

        foreach (Match match in Regex.Matches(normalized, @"(?ms)^\s*(?<name>[^\r\n][^\r\n]*?)\s*$\n+\s*x(?<qty>\d+)\s*$\n+\s*!\["))
        {
            string name = WebUtility.HtmlDecode(match.Groups["name"].Value).Trim();
            if (name.Length == 0)
                continue;

            if (!int.TryParse(match.Groups["qty"].Value, out int quantity) || quantity <= 0)
                continue;

            entries.Add((name, quantity));
        }

        return entries;
    }

    private static (string Commander, string DeckList)? TryParseMoxfieldMirrorDeck(string markdown, string commanderHint)
    {
        var entries = ParseMoxfieldMirrorEntries(markdown);
        if (!entries.Any())
            return null;

        string commander = commanderHint.Trim();
        int totalQuantity = entries.Sum(entry => entry.Quantity);

        if (string.IsNullOrWhiteSpace(commander) && totalQuantity >= 100 && entries[0].Quantity == 1)
        {
            commander = entries[0].Name;
            entries.RemoveAt(0);
        }
        else if (!string.IsNullOrWhiteSpace(commander)
            && totalQuantity >= 100
            && entries[0].Quantity == 1
            && entries[0].Name.Equals(commander, StringComparison.OrdinalIgnoreCase))
        {
            entries.RemoveAt(0);
        }

        if (!entries.Any())
            return null;

        return (commander, string.Join(Environment.NewLine, entries.Select(entry => $"{entry.Quantity} {entry.Name}")));
    }

    private void UpdateDeckCountLabel()
    {
        int total = 0;
        var lines = deckInputTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            var parts = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out int quantity))
                total += Math.Max(0, quantity);
            else
                total += 1;
        }

        deckCountLabel.Text = $"Cards Entered: {total}";
        deckCountLabel.ForeColor = total == 99 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(75, 85, 99);
    }

    private static string? ExtractCommanderFromDeck(JsonElement root)
    {
        if (root.TryGetProperty("boards", out var boards)
            && boards.ValueKind == JsonValueKind.Object
            && boards.TryGetProperty("commanders", out var commandersBoard)
            && commandersBoard.ValueKind == JsonValueKind.Object
            && commandersBoard.TryGetProperty("cards", out var commanderCards)
            && commanderCards.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in commanderCards.EnumerateObject())
            {
                var entry = property.Value;
                if (entry.TryGetProperty("card", out var cardObject)
                    && cardObject.ValueKind == JsonValueKind.Object
                    && cardObject.TryGetProperty("name", out var nameElement)
                    && nameElement.ValueKind == JsonValueKind.String)
                {
                    return nameElement.GetString();
                }
            }
        }

        return null;
    }

    private async Task<(string Commander, string DeckList)> ImportDeckFromMoxfieldAsync(string input)
    {
        string? deckId = ExtractMoxfieldDeckId(input);
        if (string.IsNullOrWhiteSpace(deckId))
            throw new InvalidOperationException("Please provide a valid Moxfield deck URL or deck id.");

        string canonicalDeckUrl = $"https://www.moxfield.com/decks/{deckId}";

        var endpoints = new[]
        {
            $"https://api2.moxfield.com/v2/decks/all/{deckId}",
            $"https://api2.moxfield.com/v3/decks/all/{deckId}",
            $"https://api2.moxfield.com/v2/decks/{deckId}",
            $"https://api2.moxfield.com/v3/decks/{deckId}"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var request = CreateWebRequest(endpoint, "application/json, text/plain, */*");
                using var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    continue;

                string json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                string commander = ExtractCommanderFromDeck(root) ?? string.Empty;
                var lines = new List<string>();
                TryCollectMoxfieldMainboard(root, lines);
                if (!lines.Any())
                    throw new InvalidOperationException("Could not parse deck cards from Moxfield response.");

                lines = lines.OrderBy(line => line, StringComparer.OrdinalIgnoreCase).ToList();
                return (commander, string.Join(Environment.NewLine, lines));
            }
            catch
            {
                // Try next endpoint.
            }
        }

        string commanderHint = string.Empty;
        string? pageHtml = await TryDownloadTextAsync(canonicalDeckUrl, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        if (!string.IsNullOrWhiteSpace(pageHtml))
            commanderHint = ExtractCommanderFromMoxfieldHtml(pageHtml);

        string? mirroredMarkdown = await TryDownloadTextAsync($"https://r.jina.ai/http://{canonicalDeckUrl}", "text/plain, text/markdown, */*");
        if (!string.IsNullOrWhiteSpace(mirroredMarkdown))
        {
            var parsedMirrorDeck = TryParseMoxfieldMirrorDeck(mirroredMarkdown, commanderHint);
            if (parsedMirrorDeck.HasValue)
                return parsedMirrorDeck.Value;
        }

        throw new InvalidOperationException("Unable to import deck from Moxfield. The public API is currently blocking direct requests from this app, and the public page fallback could not recover the deck list. Verify the deck is public and try the full deck URL.");
    }

    private async void ImportMoxfieldButton_Click(object? sender, EventArgs e)
    {
        try
        {
            importMoxfieldButton.Enabled = false;
            progressLabel.Text = "Importing Moxfield deck...";

            var imported = await ImportDeckFromMoxfieldAsync(moxfieldLinkTextBox.Text);
            if (!string.IsNullOrWhiteSpace(imported.Commander))
                commanderTextBox.Text = imported.Commander;

            deckInputTextBox.Text = imported.DeckList;
            UpdateDeckCountLabel();
            progressLabel.Text = "Moxfield deck imported successfully.";

            resultsTextBox.Clear();
            resultsTextBox.SelectionFont = new Font("Segoe UI", 13, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.FromArgb(17, 24, 39);
            resultsTextBox.AppendText("Moxfield Import\n");

            void Header(string text)
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                resultsTextBox.SelectionColor = Color.FromArgb(30, 64, 175);
                resultsTextBox.AppendText(text + "\n");
            }

            void Body(string text)
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                resultsTextBox.SelectionColor = Color.FromArgb(31, 41, 55);
                resultsTextBox.AppendText(text + "\n");
            }

            Body(new string('-', 50));
            if (!string.IsNullOrWhiteSpace(imported.Commander))
                Body($"Commander: {imported.Commander}");

            var importedLines = imported.DeckList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int totalImported = 0;
            foreach (var line in importedLines)
            {
                var parts = line.Trim().Split(new[] { ' ' }, 2);
                if (parts.Length > 0 && int.TryParse(parts[0], out int quantity))
                    totalImported += quantity;
                else
                    totalImported += 1;
            }

            Body($"Cards: {totalImported} total | {importedLines.Length} unique");
            Header("Deck List");
            foreach (var line in importedLines.Take(40))
                Body($"  {line}");
            if (importedLines.Length > 40)
                Body($"  ... and {importedLines.Length - 40} more");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Moxfield Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            progressLabel.Text = "Moxfield import failed.";
        }
        finally
        {
            importMoxfieldButton.Enabled = true;
        }
    }

    private void SaveReportButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(resultsTextBox.Text))
        {
            MessageBox.Show("Nothing to save yet. Run an evaluation first.", "Save Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save Deck Report",
            Filter = "Text File (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = "DeckReport.txt"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, resultsTextBox.Text);
            progressLabel.Text = $"Report saved to {Path.GetFileName(dialog.FileName)}";
        }
    }

    private async Task PrefetchMissingCardsAsync(IEnumerable<string> cardNames)
    {
        missingCardNames.Clear();
        var missing = cardNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && !cardDatabase.ContainsKey(name.Trim()))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!missing.Any())
            return;

        progressLabel.Text = $"Fetching {missing.Count} unknown card(s) from Scryfall...";
        foreach (var name in missing)
        {
            bool resolved = await TryFetchFromScryfallAsync(name);
            if (!resolved)
                missingCardNames.Add(name);
        }
    }

    private async Task<bool> TryFetchFromScryfallAsync(string name)
    {
        try
        {
            foreach (var queryType in new[] { "exact", "fuzzy" })
            {
                string url = $"https://api.scryfall.com/cards/named?{queryType}={Uri.EscapeDataString(name)}";
                using var response = await Http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    continue;

                string json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.TryGetProperty("object", out var objectProperty) && objectProperty.GetString() == "error")
                    continue;

                string cardName = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? name : name;
                int cmc = root.TryGetProperty("cmc", out var cmcProperty) ? (int)Math.Round(cmcProperty.GetDouble()) : 3;
                string typeLine = root.TryGetProperty("type_line", out var typeProperty) ? typeProperty.GetString() ?? string.Empty : string.Empty;

                string oracleText = string.Empty;
                if (root.TryGetProperty("oracle_text", out var oracleProperty))
                {
                    oracleText = oracleProperty.GetString() ?? string.Empty;
                }
                else if (root.TryGetProperty("card_faces", out var faces) && faces.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var face in faces.EnumerateArray())
                    {
                        if (face.TryGetProperty("oracle_text", out var faceOracle))
                            parts.Add(faceOracle.GetString() ?? string.Empty);
                    }
                    oracleText = string.Join(" // ", parts);
                }

                var colors = new List<string>();
                if (root.TryGetProperty("colors", out var colorProperty) && colorProperty.ValueKind == JsonValueKind.Array)
                {
                    foreach (var color in colorProperty.EnumerateArray())
                    {
                        if (color.GetString() is string colorCode && colorCode.Length > 0)
                            colors.Add(colorCode);
                    }
                }

                bool isLand = typeLine.IndexOf("Land", StringComparison.OrdinalIgnoreCase) >= 0;
                var record = new CardDbRecord
                {
                    Name = cardName,
                    ManaCost = isLand ? 0 : cmc,
                    Colors = colors,
                    Type = typeLine,
                    Category = string.Empty,
                    IsLand = isLand,
                    OracleText = oracleText
                };

                cardDatabase[cardName] = record;
                if (!cardName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    cardDatabase[name] = record;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void LoadCardDatabase()
    {
        string dataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cards.csv");
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
                if (string.IsNullOrWhiteSpace(row))
                    continue;

                var fields = ParseCsvRow(row);
                if (fields.Length < 5)
                    continue;

                string name = fields[0].Trim();
                if (name.Length == 0)
                    continue;

                int manaCost = 0;
                if (double.TryParse(fields[1], out double parsedManaCost))
                    manaCost = (int)Math.Round(parsedManaCost);

                var colors = fields.Length >= 3
                    ? fields[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(color => color.Trim()).Where(color => color.Length > 0).ToList()
                    : new List<string>();
                string type = fields.Length >= 4 ? fields[3].Trim() : string.Empty;
                string category = fields.Length >= 5 ? fields[4].Trim() : "Unknown";
                bool isLand = false;
                string oracleText = string.Empty;

                if (fields.Length >= 8)
                {
                    isLand = bool.TryParse(fields[6], out bool parsedIsLand) && parsedIsLand;
                    oracleText = fields[7].Trim().Trim('"');
                }
                else if (fields.Length >= 6)
                {
                    isLand = bool.TryParse(fields[5], out bool parsedIsLand) && parsedIsLand;
                    if (fields.Length >= 7)
                        oracleText = fields[6].Trim().Trim('"');
                }

                if (!isLand && type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0)
                    isLand = true;

                cardDatabase[name] = new CardDbRecord
                {
                    Name = name,
                    ManaCost = manaCost,
                    Colors = colors,
                    Type = type,
                    Category = category,
                    IsLand = isLand,
                    OracleText = oracleText
                };
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load card database: " + ex.Message, "Deck Evaluator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyOracleTextCategoryTags(Card card)
    {
        if (card.IsLand)
            return;

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(card.Type))
        {
            if (card.Type.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Creature");
            if (card.Type.IndexOf("Enchantment", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Enchantment");
            if (card.Type.IndexOf("Artifact", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Artifact");
            if (card.Type.IndexOf("Planeswalker", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Planeswalker");
            if (card.Type.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Battle");
            if (card.Type.IndexOf("Instant", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Instant");
            if (card.Type.IndexOf("Sorcery", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Sorcery");
            if (card.Type.IndexOf("Land", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Land");

            if (card.Type.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string normalized = card.Type.Replace("â€”", "—");
                int dashIndex = normalized.IndexOf('—');
                if (dashIndex >= 0 && dashIndex + 1 < normalized.Length)
                {
                    string subtypePart = normalized.Substring(dashIndex + 1).Trim();
                    var subtypeTokens = subtypePart.Split(new[] { ' ', '/', '\\', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in subtypeTokens)
                    {
                        if (token.Length > 1 && token.All(char.IsLetter))
                            tags.Add($"Tribe:{token}");
                    }
                }
            }
        }

        string oracle = card.OracleText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(oracle))
        {
            string oracleLower = oracle.ToLowerInvariant();
            if ((oracleLower.Contains("draw") && oracleLower.Contains("card")) || oracleLower.Contains("investigate"))
                tags.Add("Card Draw");
            if ((oracleLower.Contains("destroy") && oracleLower.Contains("target"))
                || (oracleLower.Contains("exile") && oracleLower.Contains("target"))
                || (oracleLower.Contains("fight") && oracleLower.Contains("target"))
                || (oracleLower.Contains("damage") && oracleLower.Contains("target"))
                || oracleLower.Contains("each opponent sacrifices")
                || oracleLower.Contains("target player sacrifices"))
                tags.Add("Removal");
            if (oracleLower.Contains("counter target spell") || oracleLower.Contains("counter up to"))
                tags.Add("Counterspell");
            if (oracleLower.Contains("create") && oracleLower.Contains("token"))
                tags.Add("Token Generation");
            if (oracleLower.Contains("search your library")
                || oracleLower.Contains("search target player's library")
                || oracleLower.Contains("reveal it and put it into your hand"))
                tags.Add("Tutor");
            if ((oracleLower.Contains("return target") || oracleLower.Contains("put target"))
                && oracleLower.Contains("from your graveyard"))
                tags.Add("Recursion");
            if ((oracleLower.Contains("add {") || oracleLower.Contains("create a treasure") || oracleLower.Contains("create treasure"))
                && !tags.Contains("Land"))
                tags.Add("Ramp");
            if (oracleLower.Contains("destroy all") || oracleLower.Contains("exile all") || (oracleLower.Contains("each creature") && oracleLower.Contains("gets -")))
                tags.Add("Board Wipe");
            if (oracleLower.Contains("hexproof") || oracleLower.Contains("indestructible") || oracleLower.Contains("protection from") || oracleLower.Contains("ward"))
                tags.Add("Protection");
            if (oracleLower.Contains("gain") && oracleLower.Contains("life"))
                tags.Add("Life Gain");
            if (oracleLower.Contains("mill") || (oracleLower.Contains("puts the top") && oracleLower.Contains("into their graveyard")))
                tags.Add("Mill");

            if (tags.Contains("Creature"))
            {
                if (oracleLower.Contains("flying")) tags.Add("Flying");
                if (oracleLower.Contains("trample")) tags.Add("Trample");
                if (oracleLower.Contains("deathtouch")) tags.Add("Deathtouch");
                if (oracleLower.Contains("lifelink")) tags.Add("Lifelink");
                if (oracleLower.Contains("vigilance")) tags.Add("Vigilance");
                if (oracleLower.Contains("haste")) tags.Add("Haste");
                if (oracleLower.Contains("hexproof")) tags.Add("Hexproof");
                if (oracleLower.Contains("ward")) tags.Add("Ward");
            }
        }

        if (!tags.Any())
            return;

        var currentTags = (card.Category ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        currentTags.UnionWith(tags);
        card.Category = string.Join(", ", currentTags);
    }

    private async void EvaluateButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var rawInputLines = deckInputTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var prefetchNames = rawInputLines
                .Select(line =>
                {
                    var parts = line.Trim().Split(new[] { ' ' }, 2);
                    return parts.Length == 2 && int.TryParse(parts[0], out _) ? parts[1].Trim() : string.Empty;
                })
                .Where(name => name.Length > 0)
                .ToList();
            if (!string.IsNullOrWhiteSpace(commanderTextBox.Text))
                prefetchNames.Add(commanderTextBox.Text.Trim());
            await PrefetchMissingCardsAsync(prefetchNames);

            Deck deck = await ParseDeck(deckInputTextBox.Text, commanderTextBox.Text);
            if (deck.Cards.Count == 0)
            {
                resultsTextBox.Clear();
                resultsTextBox.SelectionColor = Color.Red;
                resultsTextBox.AppendText("No cards found in deck list.");
                return;
            }

            int numSimulations = 1000000;
            int maxTurns = 10;
            progressBar.Value = 0;
            progressLabel.Text = "Running simulations...";

            bool onDraw = onDrawCheckBox.Checked;
            var evaluator = new DeckEvaluator(deck);
            var results = await Task.Run(() => evaluator.RunSimulations(numSimulations, maxTurns, onDraw, progress =>
            {
                Invoke((Action)(() =>
                {
                    int scaled = (int)Math.Round(progress * (double)progressBar.Maximum / numSimulations);
                    scaled = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, scaled));
                    progressBar.Value = scaled;
                    progressLabel.Text = $"Simulating game {progress}/{numSimulations} ({scaled}/{progressBar.Maximum})";
                }));
            }));

            progressLabel.Text = "Done!";
            RenderResults(deck, results, numSimulations, maxTurns, onDraw);
        }
        catch (Exception ex)
        {
            resultsTextBox.Clear();
            resultsTextBox.SelectionColor = Color.Red;
            resultsTextBox.AppendText($"Error: {ex.Message}");
        }
    }

    private void RenderResults(Deck deck, EvaluationResults results, int numSimulations, int maxTurns, bool onDraw)
    {
        resultsTextBox.Clear();

        void Header(string text)
        {
            resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.FromArgb(30, 64, 175);
            resultsTextBox.AppendText(text + "\n");
        }

        void Body(string text)
        {
            resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
            resultsTextBox.SelectionColor = Color.FromArgb(31, 41, 55);
            resultsTextBox.AppendText(text + "\n");
        }

        resultsTextBox.SelectionFont = new Font("Segoe UI", 13, FontStyle.Bold);
        resultsTextBox.SelectionColor = Color.FromArgb(17, 24, 39);
        resultsTextBox.AppendText("Deck Evaluation Report\n");
        Body($"{numSimulations:N0} simulations across {maxTurns} turns");
        Body(new string('-', 72));

        if (missingCardNames.Any())
        {
            resultsTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Italic);
            resultsTextBox.SelectionColor = Color.FromArgb(180, 83, 9);
            resultsTextBox.AppendText($"⚠ {missingCardNames.Count} card(s) not in local DB — resolved via Scryfall or defaults: {string.Join(", ", missingCardNames.Take(6))}{(missingCardNames.Count > 6 ? "…" : string.Empty)}\n");
        }

        Body($"Simulation: AI Goldfish Bot — mulligans, tutor targets, draw sequencing, reactive spell restraint | On the draw: {(onDraw ? "Yes" : "No")}");

        int landCount = deck.LandCount;
        int nonLandCount = deck.Cards.Count - landCount;

        Header("Summary");
        Body($"Cards: {deck.Cards.Count} | Lands: {landCount} ({(landCount * 100.0 / deck.Cards.Count):F1}%) | Spells: {nonLandCount} ({(nonLandCount * 100.0 / deck.Cards.Count):F1}%)");
        Body($"Land hit rate: {(100 - (results.AverageMissedLands / maxTurns * 100)):F1}% | Lands by T{maxTurns}: {results.AverageLandsPlayed:F2} | Idle turns: {results.AverageIdleTurns:F2}");
        Body($"Avg spells cast/game: {results.AverageSpellsCast:F1} ({results.AverageSpellsCast / maxTurns:F2}/turn) | Peak mana: {results.AveragePeakMana:F1}");
        Body($"Opening hand: {results.AverageOpeningHandLands:F2} avg lands | Brick (0-land): {results.BrickHandPercent:F1}% | Flood (5+): {results.FloodHandPercent:F1}% | Mulligans: {results.AverageMulligans:F2}");
        Body($"Mana efficiency: {results.AverageManaEfficiency:F1}% | Early actions by T3: {results.AverageEarlyTurnActions:F2} | Stranded 5+ drops: {results.AverageStrandedHighCostCards:F2}");

        var manaCurve = deck.GetManaCurve();
        Header("Mana Curve");
        if (manaCurve.Any())
        {
            int barWidth = 22;
            var displayBuckets = Enumerable.Range(0, 7)
                .Select(cmc => (Label: $"CMC{cmc}", Count: manaCurve.GetValueOrDefault(cmc, 0)))
                .Where(bucket => bucket.Count > 0)
                .ToList();
            int highCount = manaCurve.Where(kvp => kvp.Key >= 7).Sum(kvp => kvp.Value);
            if (highCount > 0)
                displayBuckets.Add(("CMC7+", highCount));

            int maxCount = displayBuckets.Any() ? displayBuckets.Max(bucket => bucket.Count) : 1;
            foreach (var (label, count) in displayBuckets)
            {
                int bars = maxCount > 0 ? (int)Math.Round((double)count * barWidth / maxCount) : 0;
                bars = Math.Max(0, Math.Min(barWidth, bars));
                string filled = new string('█', bars);
                string empty = new string('░', barWidth - bars);
                Color barColor = (label == "CMC0" || label == "CMC1" || label == "CMC2")
                    ? Color.FromArgb(22, 163, 74)
                    : (label == "CMC3" || label == "CMC4") ? Color.FromArgb(37, 99, 235) : Color.FromArgb(220, 38, 38);

                resultsTextBox.SelectionFont = new Font("Cascadia Mono", 9, FontStyle.Regular);
                resultsTextBox.SelectionColor = Color.FromArgb(55, 65, 81);
                resultsTextBox.AppendText($"  {label,-5} [{count,3}] ");
                resultsTextBox.SelectionColor = barColor;
                resultsTextBox.AppendText(filled);
                resultsTextBox.SelectionColor = Color.FromArgb(180, 180, 180);
                resultsTextBox.AppendText(empty + "\n");
            }

            var manaBrackets = deck.GetManaBrackets();
            string bracketLine = string.Join("  ", manaBrackets.OrderBy(kvp => kvp.Key).Select(kvp =>
            {
                int pct = nonLandCount > 0 ? kvp.Value * 100 / nonLandCount : 0;
                return $"{kvp.Key}: {kvp.Value} ({pct}%)";
            }));
            Body($"  {bracketLine}");
        }
        else
        {
            Body("No non-land cards found.");
        }

        Header("Card Type Breakdown");
        var coreTypes = DeckAnalysis.GetCoreTypeCounts(deck);
        if (coreTypes.Any())
        {
            string typeLine = string.Join(", ", coreTypes
                .OrderByDescending(kvp => kvp.Value)
                .Take(7)
                .Select(kvp =>
                {
                    int percentage = nonLandCount > 0 ? kvp.Value * 100 / nonLandCount : 0;
                    return $"{kvp.Key}:{kvp.Value} ({percentage}%)";
                }));
            Body(typeLine);
        }
        else
        {
            Body("No spells detected.");
        }

        int removalCount = DeckAnalysis.CountRoleCards(deck, "Removal");
        int drawCount = DeckAnalysis.CountRoleCards(deck, "Card Draw");
        int rampCount = DeckAnalysis.CountRoleCards(deck, "Ramp");
        int wipeCount = DeckAnalysis.CountRoleCards(deck, "Board Wipe");
        Header("Functional Tags");
        Body($"Removal: {removalCount} | Card Draw: {drawCount} | Ramp: {rampCount} | Board Wipes: {wipeCount}");
        Body("Tag Legend: Removal=targeted kill/exile/fight/sacrifice; Card Draw=draw/investigate; Ramp=mana/treasure; Board Wipe=mass removal; Protection=hexproof/indestructible/protection/ward.");

        if (DeckAnalysis.TryGetPrimaryTribe(deck, out string tribe, out int tribeCount, out int creatureCount))
        {
            double tribePct = creatureCount > 0 ? tribeCount * 100.0 / creatureCount : 0;
            Body($"Primary tribe: {tribe} ({tribeCount}/{creatureCount} creatures, {tribePct:F1}%)");
        }

        Body(string.Empty);

        var actionable = results.Recommendations
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .Where(text => !text.StartsWith("===") && !text.StartsWith("\n===") && text != "-")
            .Where(text => text.StartsWith("🚨") || text.StartsWith("⚠️") || text.StartsWith("💡") || text.StartsWith("✓") || text.Contains("Add ") || text.Contains("trim", StringComparison.OrdinalIgnoreCase) || text.Contains("consider", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var suggestedAdds = actionable.Where(text =>
            text.Contains("add", StringComparison.OrdinalIgnoreCase)
            || text.Contains("increase", StringComparison.OrdinalIgnoreCase)
            || text.Contains("more", StringComparison.OrdinalIgnoreCase)
            || text.Contains("include", StringComparison.OrdinalIgnoreCase)
            || text.Contains("consider", StringComparison.OrdinalIgnoreCase)).ToList();

        var suggestedCuts = actionable.Where(text =>
            text.Contains("trim", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cut", StringComparison.OrdinalIgnoreCase)
            || text.Contains("reduce", StringComparison.OrdinalIgnoreCase)
            || text.Contains("too many", StringComparison.OrdinalIgnoreCase)
            || text.Contains("over", StringComparison.OrdinalIgnoreCase)).ToList();

        var namedAdds = suggestedAdds.Where(text => text.Contains("cards:", StringComparison.OrdinalIgnoreCase) || text.Contains("examples:", StringComparison.OrdinalIgnoreCase)).ToList();
        var namedCuts = suggestedCuts.Where(text => text.Contains("candidates:", StringComparison.OrdinalIgnoreCase) || text.Contains("cards:", StringComparison.OrdinalIgnoreCase) || text.Contains("examples:", StringComparison.OrdinalIgnoreCase)).ToList();

        var addSection = namedAdds.Concat(suggestedAdds.Where(text => !namedAdds.Contains(text, StringComparer.OrdinalIgnoreCase))).Take(4).ToList();
        var cutSection = namedCuts.Concat(suggestedCuts.Where(text => !namedCuts.Contains(text, StringComparer.OrdinalIgnoreCase))).Take(4).ToList();

        if (addSection.Any())
        {
            Header("Suggested Adds");
            foreach (var recommendation in addSection)
                Body($"- {recommendation}");
        }

        if (cutSection.Any())
        {
            Header("Suggested Cuts");
            foreach (var recommendation in cutSection)
                Body($"- {recommendation}");
        }

        var namedRecommendations = actionable.Where(text => text.Contains("cards:", StringComparison.OrdinalIgnoreCase) || text.Contains("candidates:", StringComparison.OrdinalIgnoreCase) || text.Contains("examples:", StringComparison.OrdinalIgnoreCase)).Take(4).ToList();
        var shortRecommendations = namedRecommendations.Concat(actionable.Where(text => !namedRecommendations.Contains(text, StringComparer.OrdinalIgnoreCase))).Take(4).ToList();
        if (shortRecommendations.Any())
        {
            Header("Top Recommendations");
            foreach (var recommendation in shortRecommendations)
                Body($"- {recommendation}");
        }

        var comboPieces = DeckAnalysis.DetectComboPieces(deck);
        if (comboPieces.Any())
        {
            Header("Combo / Win-Con Pieces");
            foreach (var (label, cards) in comboPieces)
                Body($"  {label}: {string.Join(", ", cards.Take(5))}");
        }

        if (!string.IsNullOrWhiteSpace(commanderTextBox.Text))
            _ = AppendEdhrecSuggestionsAsync(deck);
    }

    private async Task AppendEdhrecSuggestionsAsync(Deck deck)
    {
        var suggestions = await GetEdhrecSuggestionsAsync(commanderTextBox.Text.Trim(), deck);
        if (!suggestions.Any())
            return;

        if (InvokeRequired)
        {
            Invoke((Action)(() => AppendEdhrecSection(suggestions)));
        }
        else
        {
            AppendEdhrecSection(suggestions);
        }
    }

    private void AppendEdhrecSection(List<string> suggestions)
    {
        resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
        resultsTextBox.SelectionColor = Color.FromArgb(30, 64, 175);
        resultsTextBox.AppendText("EDHREC Suggestions\n");
        resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
        resultsTextBox.SelectionColor = Color.FromArgb(31, 41, 55);
        foreach (var suggestion in suggestions)
            resultsTextBox.AppendText($"- {suggestion}\n");
    }

    private async Task<List<string>> GetEdhrecSuggestionsAsync(string commanderName, Deck deck, int maxSuggestions = 5)
    {
        if (string.IsNullOrWhiteSpace(commanderName))
            return new List<string>();

        string slug = Regex.Replace(commanderName.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
            return new List<string>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://json.edhrec.com/pages/commanders/{slug}.json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            string json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var inDeck = deck.Cards
                .Where(card => !string.IsNullOrWhiteSpace(card.Name))
                .Select(card => card.Name!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var found = new List<string>();
            CollectCardNames(document.RootElement, found, inDeck, commanderName.Trim());
            return found.Distinct(StringComparer.OrdinalIgnoreCase).Take(maxSuggestions).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void CollectCardNames(JsonElement element, List<string> names, HashSet<string> inDeck, string commanderName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String)
                {
                    string? name = nameProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(name)
                        && !inDeck.Contains(name)
                        && !name.Equals(commanderName, StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(name);
                    }
                }

                foreach (var property in element.EnumerateObject())
                    CollectCardNames(property.Value, names, inDeck, commanderName);
                break;

            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectCardNames(child, names, inDeck, commanderName);
                break;
        }
    }

    private async Task<Deck> ParseDeck(string deckText, string commanderText)
    {
        var deck = new Deck();
        string commanderName = string.Empty;
        Task<(int cost, List<string> colors, string type, string category, string oracleText)?>? commanderTask = null;
        if (!string.IsNullOrWhiteSpace(commanderText))
        {
            commanderName = commanderText.Trim();
            commanderTask = GetCardData(commanderName);
        }

        var lines = deckText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var cardNames = new List<string>();
        var quantities = new List<int>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            var parts = trimmed.Split(new[] { ' ' }, 2);
            if (parts.Length < 2 || !int.TryParse(parts[0], out int quantity))
                throw new FormatException($"Invalid line: {trimmed}");

            cardNames.Add(parts[1].Trim());
            quantities.Add(quantity);
        }

        var cardDataTasks = cardNames.Select(GetCardData).ToArray();
        var cardData = await Task.WhenAll(cardDataTasks);
        var commanderData = commanderTask != null ? await commanderTask : null;

        if (!string.IsNullOrWhiteSpace(commanderText))
        {
            int manaCost = 0;
            var colors = new List<string>();
            string type = string.Empty;
            string category = "Commander";
            string oracleText = string.Empty;
            bool isLand = false;

            if (commanderData.HasValue)
            {
                manaCost = commanderData.Value.cost;
                colors = commanderData.Value.colors;
                type = commanderData.Value.type;
                oracleText = commanderData.Value.oracleText;
                isLand = cardDatabase.TryGetValue(commanderName, out var commanderRecord)
                    ? commanderRecord.IsLand
                    : type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            var commanderCard = new Card
            {
                Name = commanderName,
                IsLand = isLand,
                ManaCost = manaCost,
                Colors = colors,
                Type = type,
                Category = category,
                OracleText = oracleText
            };
            ApplyOracleTextCategoryTags(commanderCard);
            deck.AddCard(commanderCard);
        }

        for (int index = 0; index < cardNames.Count; index++)
        {
            string cardName = cardNames[index];
            int quantity = quantities[index];
            var cardEntry = cardData[index];

            int manaCost = 0;
            var colors = new List<string>();
            string type = string.Empty;
            string category = string.Empty;
            string oracleText = string.Empty;
            bool isLand = false;

            if (cardEntry.HasValue)
            {
                manaCost = cardEntry.Value.cost;
                colors = cardEntry.Value.colors;
                type = cardEntry.Value.type;
                category = cardEntry.Value.category;
                oracleText = cardEntry.Value.oracleText;
                isLand = cardDatabase.TryGetValue(cardName.Trim(), out var record)
                    ? record.IsLand
                    : type.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else
            {
                manaCost = 3;
                category = "Unknown";
                isLand = cardName.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            for (int copy = 0; copy < quantity; copy++)
            {
                var card = new Card
                {
                    Name = cardName,
                    IsLand = isLand,
                    ManaCost = manaCost,
                    Colors = colors,
                    Type = type,
                    Category = category,
                    OracleText = oracleText
                };
                ApplyOracleTextCategoryTags(card);
                deck.AddCard(card);
            }
        }

        return deck;
    }

    private Task<(int cost, List<string> colors, string type, string category, string oracleText)?> GetCardData(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<(int, List<string>, string, string, string)?>((0, new List<string>(), string.Empty, "Unknown", string.Empty));

        if (cardDatabase.TryGetValue(name.Trim(), out var record))
            return Task.FromResult<(int, List<string>, string, string, string)?>((record.ManaCost, record.Colors, record.Type, record.Category, record.OracleText));

        var match = cardDatabase.Values.FirstOrDefault(card => string.Equals(card.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return Task.FromResult<(int, List<string>, string, string, string)?>((match.ManaCost, match.Colors, match.Type, match.Category, match.OracleText));

        return Task.FromResult<(int, List<string>, string, string, string)?>((0, new List<string>(), "Unknown", "Unknown", string.Empty));
    }
}
