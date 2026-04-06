using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private static readonly Color SurfaceBackground = Color.FromArgb(11, 18, 32);
    private static readonly Color SurfacePanel = Color.FromArgb(18, 30, 52);
    private static readonly Color SurfaceInput = Color.FromArgb(8, 14, 27);
    private static readonly Color TextPrimary = Color.FromArgb(229, 239, 255);
    private static readonly Color TextMuted = Color.FromArgb(155, 176, 205);
    private static readonly Color AccentPrimary = Color.FromArgb(56, 189, 248);
    private static readonly Color AccentHighlight = Color.FromArgb(250, 204, 21);

    private static readonly HttpClient Http = new HttpClient();
    private static readonly HashSet<string> NonLandCoreTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Creature",
        "Artifact",
        "Enchantment",
        "Instant",
        "Sorcery",
        "Planeswalker",
        "Battle"
    };
    private static readonly HashSet<string> RoleTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Removal",
        "Card Draw",
        "Board Wipe",
        "Ramp",
        "Tutor",
        "Counterspell",
        "Token Generation",
        "Recursion",
        "Protection",
        "Life Gain",
        "Discard",
        "Stax"
    };

    private readonly Dictionary<string, CardDbRecord> cardDatabase = new Dictionary<string, CardDbRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CardDbRecord> normalizedCardIndex = new Dictionary<string, CardDbRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> commanderLegalityCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> persistedCardNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> missingCardNames = new List<string>();
    private readonly string cardDatabaseFilePath;
    private CardMetricsStore? _cardMetrics;

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
    private Button trainButton = null!;
    private Button cancelButton = null!;
    private ComboBox archetypeComboBox = null!;
    private ComboBox opponentProfileComboBox = null!;
    private ComboBox gamesComboBox = null!;
    private ComboBox edhrecThemeComboBox = null!;
    private CheckBox cedhCheckBox = null!;
    private CheckBox themeModeCheckBox = null!;
    private FlowLayoutPanel actionBarPanel = null!;
    private TabControl outputTabs = null!;
    private RichTextBox trainLogTextBox = null!;
    private ListBox historyListBox = null!;
    private RichTextBox historyDetailsTextBox = null!;
    private readonly ToolTip cardPreviewToolTip = new ToolTip();
    private string _lastHoveredCard = string.Empty;
    private readonly List<HistoryEntry> reportHistory = new List<HistoryEntry>();
    private bool _isLightTheme;
    private System.Windows.Forms.Timer? _wizardTimer;
    private int _wizardFrame;
    private readonly string[] WizardFrames = BuildWizardFrames();
    private CancellationTokenSource? _activeRunCancellation;
    private string _pendingEdhrecThemeSelection = "Auto Theme";

    private sealed class HistoryEntry
    {
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    public DeckEvaluatorForm()
    {
        cardDatabaseFilePath = ResolveCardDatabaseFilePath();
        Text = "Fizban Deck Studio";
        Size = new Size(900, 740);
        MinimumSize = new Size(860, 700);
        BackColor = SurfaceBackground;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;

        var titleLabel = new Label
        {
            Text = "Fizban Deck Studio",
            Font = new Font("Segoe UI Semibold", 19, FontStyle.Bold),
            ForeColor = AccentHighlight,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Location = new Point(20, 10),
            Size = new Size(860, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(titleLabel);

        var subtitleLabel = new Label
        {
            Text = "Analyze, tune, and train your commander decks in one place.",
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Location = new Point(20, 44),
            Size = new Size(860, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(subtitleLabel);

        var commanderLabel = new Label
        {
            Text = "Commander:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(20, 68),
            Size = new Size(110, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(commanderLabel);

        commanderTextBox = new TextBox
        {
            Font = new Font("Segoe UI", 10),
            BackColor = SurfaceInput,
            ForeColor = TextPrimary,
            Location = new Point(130, 66),
            Size = new Size(730, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(commanderTextBox);

        var moxfieldLabel = new Label
        {
            Text = "Moxfield Import:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(20, 102),
            Size = new Size(110, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(moxfieldLabel);

        moxfieldLinkTextBox = new TextBox
        {
            Font = new Font("Segoe UI", 10),
            BackColor = SurfaceInput,
            ForeColor = TextMuted,
            Location = new Point(130, 100),
            Size = new Size(570, 26),
            PlaceholderText = "Moxfield import is temporarily unavailable. Paste deck list below.",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        moxfieldLinkTextBox.ReadOnly = true;
        moxfieldLinkTextBox.TabStop = false;
        Controls.Add(moxfieldLinkTextBox);

        importMoxfieldButton = new Button
        {
            Text = "Import Unavailable",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(56, 66, 87),
            ForeColor = TextMuted,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(710, 98),
            Size = new Size(150, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        importMoxfieldButton.FlatAppearance.BorderSize = 0;
        importMoxfieldButton.Enabled = false;
        Controls.Add(importMoxfieldButton);

        var deckLabel = new Label
        {
            Text = "Deck List (99 cards):",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(20, 136),
            Size = new Size(150, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(deckLabel);

        deckCountLabel = new Label
        {
            Text = "Cards Entered: 0",
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(180, 138),
            Size = new Size(260, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(deckCountLabel);

        deckInputTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Cascadia Mono", 10),
            BackColor = SurfaceInput,
            ForeColor = TextPrimary,
            Location = new Point(20, 162),
            Size = new Size(412, 402),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
        };
        deckInputTextBox.TextChanged += DeckInputTextBox_TextChanged;
        Controls.Add(deckInputTextBox);

        actionBarPanel = new FlowLayoutPanel
        {
            Location = new Point(20, 570),
            Size = new Size(840, 38),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            Margin = new Padding(0)
        };
        Controls.Add(actionBarPanel);

        evaluateButton = new Button
        {
            Text = "Ask Fizban",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = AccentPrimary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(110, 36),
            Margin = new Padding(0, 0, 8, 2)
        };
        evaluateButton.FlatAppearance.BorderSize = 0;
        evaluateButton.Click += EvaluateButton_Click;
        actionBarPanel.Controls.Add(evaluateButton);

        quitButton = new Button
        {
            Text = "Quit",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(185, 55, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(85, 36),
            Margin = new Padding(0, 0, 8, 2)
        };
        quitButton.FlatAppearance.BorderSize = 0;
        quitButton.Click += (_, _) => Close();
        actionBarPanel.Controls.Add(quitButton);

        onDrawCheckBox = new CheckBox
        {
            Text = "On the draw",
            Font = new Font("Segoe UI", 9),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Size = new Size(110, 24),
            Margin = new Padding(0, 7, 10, 2)
        };
        actionBarPanel.Controls.Add(onDrawCheckBox);

        saveReportButton = new Button
        {
            Text = "Save Report",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(95, 36),
            Margin = new Padding(0, 0, 8, 2)
        };
        saveReportButton.FlatAppearance.BorderSize = 0;
        saveReportButton.Click += SaveReportButton_Click;
        actionBarPanel.Controls.Add(saveReportButton);

        trainButton = new Button
        {
            Text = "Train Fizban",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(14, 116, 144),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(105, 36),
            Margin = new Padding(0, 0, 10, 2)
        };
        trainButton.FlatAppearance.BorderSize = 0;
        trainButton.Click += TrainButton_Click;
        actionBarPanel.Controls.Add(trainButton);

        cancelButton = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(180, 83, 9),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(85, 36),
            Margin = new Padding(0, 0, 10, 2),
            Enabled = false
        };
        cancelButton.FlatAppearance.BorderSize = 0;
        cancelButton.Click += CancelButton_Click;
        actionBarPanel.Controls.Add(cancelButton);

        var archetypeLabel = new Label
        {
            Text = "Archetype:",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Size = new Size(75, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 7, 4, 2)
        };
        actionBarPanel.Controls.Add(archetypeLabel);

        archetypeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            BackColor = SurfacePanel,
            ForeColor = TextPrimary,
            Size = new Size(130, 26),
            Margin = new Padding(0, 5, 10, 2)
        };
        archetypeComboBox.Items.Add("Auto Detect");
        foreach (DeckArchetype archetype in Enum.GetValues(typeof(DeckArchetype)))
            archetypeComboBox.Items.Add(archetype);
        archetypeComboBox.SelectedIndex = 0;
        actionBarPanel.Controls.Add(archetypeComboBox);

        var gamesLabel = new Label
        {
            Text = "Games:",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Size = new Size(50, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 7, 4, 2)
        };
        actionBarPanel.Controls.Add(gamesLabel);

        var profileLabel = new Label
        {
            Text = "Pod:",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Size = new Size(36, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 7, 4, 2)
        };
        actionBarPanel.Controls.Add(profileLabel);

        opponentProfileComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            BackColor = SurfacePanel,
            ForeColor = TextPrimary,
            Size = new Size(100, 26),
            Margin = new Padding(0, 5, 10, 2)
        };
        foreach (OpponentPodProfile profile in Enum.GetValues(typeof(OpponentPodProfile)))
            opponentProfileComboBox.Items.Add(profile);
        opponentProfileComboBox.SelectedItem = OpponentPodProfile.Focused;
        actionBarPanel.Controls.Add(opponentProfileComboBox);

        gamesComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            BackColor = SurfacePanel,
            ForeColor = TextPrimary,
            Size = new Size(70, 26),
            Margin = new Padding(0, 5, 10, 2)
        };
        gamesComboBox.Items.Add("10k");
        gamesComboBox.Items.Add("50k");
        gamesComboBox.Items.Add("100k");
        gamesComboBox.Items.Add("200k");
        gamesComboBox.Items.Add("500k");
        gamesComboBox.SelectedIndex = 2; // Default to 100k
        actionBarPanel.Controls.Add(gamesComboBox);

        var edhrecThemeLabel = new Label
        {
            Text = "Theme:",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            Size = new Size(48, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 7, 4, 2)
        };
        actionBarPanel.Controls.Add(edhrecThemeLabel);

        edhrecThemeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            BackColor = SurfacePanel,
            ForeColor = TextPrimary,
            Size = new Size(150, 26),
            Margin = new Padding(0, 5, 10, 2)
        };
        edhrecThemeComboBox.Items.Add("Auto Theme");
        edhrecThemeComboBox.SelectedIndex = 0;
        actionBarPanel.Controls.Add(edhrecThemeComboBox);

        cedhCheckBox = new CheckBox
        {
            Text = "\u2694 cEDH",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = AccentHighlight,
            BackColor = Color.Transparent,
            Size = new Size(65, 24),
            Margin = new Padding(0, 7, 0, 2)
        };
        actionBarPanel.Controls.Add(cedhCheckBox);

        themeModeCheckBox = new CheckBox
        {
            Text = "Light mode",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            Size = new Size(90, 24),
            Margin = new Padding(10, 7, 0, 2)
        };
        themeModeCheckBox.CheckedChanged += ThemeModeCheckBox_CheckedChanged;
        actionBarPanel.Controls.Add(themeModeCheckBox);

        progressBar = new ProgressBar
        {
            Location = new Point(20, 624),
            Size = new Size(840, 18),
            Minimum = 0,
            Maximum = 1000,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(progressBar);

        progressLabel = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 9),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(20, 646),
            Size = new Size(740, 20),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        Controls.Add(progressLabel);

        var resultsLabel = new Label
        {
            Text = "Fizban Output:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = AccentHighlight,
            BackColor = Color.Transparent,
            Location = new Point(450, 136),
            Size = new Size(160, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(resultsLabel);

        outputTabs = new TabControl
        {
            Location = new Point(450, 162),
            Size = new Size(430, 402),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        var analyzeTab = new TabPage("Analyze");
        var trainTab = new TabPage("Train");
        var historyTab = new TabPage("History");

        resultsTextBox = new RichTextBox
        {
            ReadOnly = true,
            Font = new Font("Segoe UI", 10),
            BackColor = SurfaceInput,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.None,
            Location = new Point(8, 8),
            Size = new Size(406, 358),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        resultsTextBox.MouseMove += ResultsTextBox_MouseMove;
        analyzeTab.Controls.Add(resultsTextBox);

        trainLogTextBox = new RichTextBox
        {
            ReadOnly = true,
            Font = new Font("Segoe UI", 10),
            BackColor = SurfaceInput,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.None,
            Location = new Point(8, 8),
            Size = new Size(406, 358),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        trainTab.Controls.Add(trainLogTextBox);

        historyListBox = new ListBox
        {
            Font = new Font("Segoe UI", 9),
            BackColor = SurfacePanel,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.None,
            Location = new Point(8, 8),
            Size = new Size(406, 118),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        historyListBox.SelectedIndexChanged += HistoryListBox_SelectedIndexChanged;

        historyDetailsTextBox = new RichTextBox
        {
            ReadOnly = true,
            Font = new Font("Segoe UI", 9),
            BackColor = SurfaceInput,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.None,
            Location = new Point(8, 132),
            Size = new Size(406, 234),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        historyTab.Controls.Add(historyListBox);
        historyTab.Controls.Add(historyDetailsTextBox);

        outputTabs.TabPages.Add(analyzeTab);
        outputTabs.TabPages.Add(trainTab);
        outputTabs.TabPages.Add(historyTab);
        Controls.Add(outputTabs);

        Resize += (_, _) => ReflowBottomControls();
        commanderTextBox.Leave += async (_, _) => await RefreshEdhrecThemesAsync();
        FormClosing += DeckEvaluatorForm_FormClosing;

        UpdateDeckCountLabel();
        ReflowBottomControls();
        StyleDesktopChrome();
        LoadCardDatabase();
        string storageDir = Path.GetDirectoryName(cardDatabaseFilePath) ?? Environment.CurrentDirectory;
        _cardMetrics = new CardMetricsStore(storageDir);
        LoadUiState();
        ApplyTheme();
        Shown += async (_, _) => await RestoreThemeSelectionAsync();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Color top = _isLightTheme ? Color.FromArgb(241, 245, 249) : Color.FromArgb(6, 11, 20);
        Color bottom = _isLightTheme ? Color.FromArgb(219, 234, 254) : Color.FromArgb(15, 30, 56);
        using var brush = new LinearGradientBrush(ClientRectangle, top, bottom, 120f);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using var glow = new SolidBrush(_isLightTheme
            ? Color.FromArgb(20, 59, 130, 246)
            : Color.FromArgb(22, 56, 189, 248));
        e.Graphics.FillEllipse(glow, new Rectangle(-120, -110, 360, 240));

        using var glow2 = new SolidBrush(_isLightTheme
            ? Color.FromArgb(16, 30, 41, 59)
            : Color.FromArgb(24, 250, 204, 21));
        e.Graphics.FillEllipse(glow2, new Rectangle(ClientSize.Width - 220, -80, 320, 200));
    }

    private void ReflowBottomControls()
    {
        int margin = 20;
        int availableWidth = ClientSize.Width - (margin * 2);
        if (availableWidth < 200)
            availableWidth = 200;

        actionBarPanel.Location = new Point(margin, ClientSize.Height - 140);
        actionBarPanel.Size = new Size(availableWidth, 78);

        progressBar.Location = new Point(margin, ClientSize.Height - 56);
        progressBar.Size = new Size(availableWidth, progressBar.Height);

        progressLabel.Location = new Point(margin, ClientSize.Height - 34);
        progressLabel.Size = new Size(Math.Max(220, availableWidth - 120), progressLabel.Height);
    }

    private void StyleDesktopChrome()
    {
        actionBarPanel.Padding = new Padding(0, 2, 0, 0);

        StyleButton(evaluateButton, AccentPrimary, Color.FromArgb(14, 165, 233));
        StyleButton(trainButton, Color.FromArgb(16, 185, 129), Color.FromArgb(5, 150, 105));
        StyleButton(saveReportButton, Color.FromArgb(59, 130, 246), Color.FromArgb(37, 99, 235));
        StyleButton(cancelButton, Color.FromArgb(217, 119, 6), Color.FromArgb(180, 83, 9));
        StyleButton(quitButton, Color.FromArgb(225, 29, 72), Color.FromArgb(190, 24, 93));

        StyleInput(commanderTextBox);
        StyleInput(moxfieldLinkTextBox);
        StyleInput(deckInputTextBox);
        StyleInput(resultsTextBox);

        StyleCombo(archetypeComboBox);
        StyleCombo(opponentProfileComboBox);
        StyleCombo(gamesComboBox);
        StyleCombo(edhrecThemeComboBox);
    }

    private void StyleButton(Button button, Color baseColor, Color hoverColor)
    {
        button.BackColor = baseColor;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(baseColor);
        button.FlatAppearance.MouseOverBackColor = hoverColor;

        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = baseColor;
        button.EnabledChanged += (_, _) =>
        {
            button.BackColor = button.Enabled ? baseColor : Color.FromArgb(82, 95, 118);
            button.ForeColor = button.Enabled ? Color.White : Color.FromArgb(190, 201, 220);
        };

        ApplyRoundedCorners(button, 10);
    }

    private static void StyleInput(Control control)
    {
        if (control is TextBoxBase box)
        {
            box.BorderStyle = BorderStyle.FixedSingle;
        }
    }

    private void StyleCombo(ComboBox combo)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = SurfacePanel;
        combo.ForeColor = TextPrimary;
        ApplyRoundedCorners(combo, 8);
    }

    private static void ApplyRoundedCorners(Control control, int radius)
    {
        void UpdateRegion()
        {
            try
            {
                if (control.IsDisposed || control.Width <= 0 || control.Height <= 0)
                    return;

                using var path = new GraphicsPath();
                int diameter = Math.Max(2, radius * 2);
                var rect = new Rectangle(0, 0, control.Width - 1, control.Height - 1);

                path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
                path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();

                control.Region = new Region(path);
            }
            catch
            {
                // Keep default region if GDI operations fail unexpectedly.
            }
        }

        control.SizeChanged += (_, _) => UpdateRegion();
        UpdateRegion();
    }

    private static string ResolveCardDatabaseFilePath()
    {
        string basePath = FizbanStorage.GetPath("cards.csv");
        var candidates = new List<string>
        {
            basePath,
            Path.Combine(Directory.GetCurrentDirectory(), "cards.csv")
        };

        string current = FizbanStorage.BaseDirectory;
        for (int depth = 0; depth < 8; depth++)
        {
            var parent = Directory.GetParent(current);
            if (parent == null)
                break;

            candidates.Add(Path.Combine(parent.FullName, "cards.csv"));
            candidates.Add(Path.Combine(parent.FullName, "final", "FinalProject", "cards.csv"));
            current = parent.FullName;
        }

        var existing = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.Length)
            .ThenByDescending(info => info.LastWriteTimeUtc)
            .ToList();

        return existing.FirstOrDefault()?.FullName ?? basePath;
    }

    private void LoadUiState()
    {
        var settings = FizbanSettingsStore.Load();
        commanderTextBox.Text = settings.CommanderText;
        deckInputTextBox.Text = settings.DeckText;
        onDrawCheckBox.Checked = settings.OnDraw;
        cedhCheckBox.Checked = settings.Cedh;
        SelectComboBoxItem(gamesComboBox, settings.GamesSelection, fallbackIndex: 2);
        SelectComboBoxItem(archetypeComboBox, settings.ArchetypeSelection, fallbackIndex: 0);
        SelectComboBoxItem(opponentProfileComboBox, settings.OpponentProfileSelection, fallbackIndex: 1);
        _pendingEdhrecThemeSelection = string.IsNullOrWhiteSpace(settings.EdhrecThemeSelection) ? "Auto Theme" : settings.EdhrecThemeSelection;
        _isLightTheme = settings.LightMode;
        themeModeCheckBox.Checked = _isLightTheme;
        UpdateDeckCountLabel();
    }

    private async Task RestoreThemeSelectionAsync()
    {
        if (!string.IsNullOrWhiteSpace(commanderTextBox.Text))
            await RefreshEdhrecThemesAsync();

        SelectComboBoxItem(edhrecThemeComboBox, _pendingEdhrecThemeSelection, fallbackIndex: 0);
    }

    private void SaveUiState()
    {
        FizbanSettingsStore.Save(new FizbanAppSettings
        {
            CommanderText = commanderTextBox.Text,
            DeckText = deckInputTextBox.Text,
            GamesSelection = gamesComboBox.SelectedItem?.ToString() ?? "100k",
            ArchetypeSelection = archetypeComboBox.SelectedItem?.ToString() ?? "Auto Detect",
            OpponentProfileSelection = opponentProfileComboBox.SelectedItem?.ToString() ?? OpponentPodProfile.Focused.ToString(),
            EdhrecThemeSelection = edhrecThemeComboBox.SelectedItem?.ToString() ?? "Auto Theme",
            OnDraw = onDrawCheckBox.Checked,
            Cedh = cedhCheckBox.Checked,
            LightMode = themeModeCheckBox.Checked
        });
    }

    private static void SelectComboBoxItem(ComboBox comboBox, string? value, int fallbackIndex)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                if (string.Equals(comboBox.Items[index]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }
        }

        if (comboBox.Items.Count == 0)
            return;

        comboBox.SelectedIndex = Math.Max(0, Math.Min(comboBox.Items.Count - 1, fallbackIndex));
    }

    private void DeckEvaluatorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        SaveUiState();
        _activeRunCancellation?.Cancel();
        _activeRunCancellation?.Dispose();
        _activeRunCancellation = null;
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        if (_activeRunCancellation == null)
            return;

        cancelButton.Enabled = false;
        progressLabel.Text = "Cancelling current run...";
        _activeRunCancellation.Cancel();
    }

    private void ThemeModeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _isLightTheme = themeModeCheckBox.Checked;
        ApplyTheme();
        SaveUiState();
    }

    private void ApplyTheme()
    {
        Color baseBackground = _isLightTheme ? Color.FromArgb(239, 246, 255) : SurfaceBackground;
        Color basePanel = _isLightTheme ? Color.FromArgb(219, 234, 254) : SurfacePanel;
        Color baseInput = _isLightTheme ? Color.FromArgb(248, 250, 252) : SurfaceInput;
        Color baseText = _isLightTheme ? Color.FromArgb(15, 23, 42) : TextPrimary;
        Color mutedText = _isLightTheme ? Color.FromArgb(71, 85, 105) : TextMuted;

        BackColor = baseBackground;
        commanderTextBox.BackColor = baseInput;
        commanderTextBox.ForeColor = baseText;
        moxfieldLinkTextBox.BackColor = baseInput;
        moxfieldLinkTextBox.ForeColor = mutedText;
        deckInputTextBox.BackColor = baseInput;
        deckInputTextBox.ForeColor = baseText;

        resultsTextBox.BackColor = baseInput;
        resultsTextBox.ForeColor = baseText;
        trainLogTextBox.BackColor = baseInput;
        trainLogTextBox.ForeColor = baseText;
        historyListBox.BackColor = basePanel;
        historyListBox.ForeColor = baseText;
        historyDetailsTextBox.BackColor = baseInput;
        historyDetailsTextBox.ForeColor = baseText;

        archetypeComboBox.BackColor = basePanel;
        archetypeComboBox.ForeColor = baseText;
        opponentProfileComboBox.BackColor = basePanel;
        opponentProfileComboBox.ForeColor = baseText;
        gamesComboBox.BackColor = basePanel;
        gamesComboBox.ForeColor = baseText;
        edhrecThemeComboBox.BackColor = basePanel;
        edhrecThemeComboBox.ForeColor = baseText;

        themeModeCheckBox.ForeColor = mutedText;
        deckCountLabel.ForeColor = mutedText;
        progressLabel.ForeColor = mutedText;
        Invalidate();
    }

    private void AppendTrainLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        trainLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        outputTabs.SelectedIndex = 1;
    }

    private void AddReportToHistory(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var entry = new HistoryEntry
        {
            Title = title,
            Content = content
        };

        reportHistory.Insert(0, entry);
        if (reportHistory.Count > 24)
            reportHistory.RemoveAt(reportHistory.Count - 1);

        historyListBox.Items.Clear();
        foreach (var item in reportHistory)
            historyListBox.Items.Add(item.Title);

        historyListBox.SelectedIndex = 0;
    }

    private void HistoryListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (historyListBox.SelectedIndex < 0 || historyListBox.SelectedIndex >= reportHistory.Count)
            return;

        historyDetailsTextBox.Text = reportHistory[historyListBox.SelectedIndex].Content;
    }

    private void ResultsTextBox_MouseMove(object? sender, MouseEventArgs e)
    {
        try
        {
            if (resultsTextBox.IsDisposed || !resultsTextBox.Visible || resultsTextBox.TextLength == 0)
                return;

            int charIndex = resultsTextBox.GetCharIndexFromPosition(e.Location);
            if (charIndex < 0 || charIndex >= resultsTextBox.TextLength)
                return;

            int lineIndex = resultsTextBox.GetLineFromCharIndex(charIndex);
            if (lineIndex < 0 || lineIndex >= resultsTextBox.Lines.Length)
                return;

            string line = resultsTextBox.Lines[lineIndex].Trim();
            if (!TryExtractCardNameFromLine(line, out string cardName))
            {
                if (_lastHoveredCard.Length > 0)
                {
                    cardPreviewToolTip.Hide(resultsTextBox);
                    _lastHoveredCard = string.Empty;
                }
                return;
            }

            if (cardName.Equals(_lastHoveredCard, StringComparison.OrdinalIgnoreCase))
                return;

            _lastHoveredCard = cardName;
            string preview = BuildCardPreview(cardName);
            cardPreviewToolTip.Show(preview, resultsTextBox, e.Location.X + 18, e.Location.Y + 18, 3500);
        }
        catch
        {
            // Avoid UI crash from tooltip/hover parsing edge cases.
        }
    }

    private bool TryExtractCardNameFromLine(string line, out string cardName)
    {
        cardName = string.Empty;
        if (line.Length < 4)
            return false;

        if (line.StartsWith("+") || line.StartsWith("-"))
            line = line.Substring(1).Trim();
        else if (line.StartsWith("+") || line.StartsWith("-"))
            line = line.TrimStart('+', '-').Trim();

        if (line.StartsWith("+") || line.StartsWith("-"))
            line = line.Substring(1).Trim();

        if (line.StartsWith("Swap in:") || line.StartsWith("Swap out:") || line.StartsWith("Source:") || line.StartsWith("Confidence:"))
            return false;

        int reasonIndex = line.IndexOf(" — ", StringComparison.Ordinal);
        if (reasonIndex > 0)
            line = line.Substring(0, reasonIndex).Trim();

        line = line.TrimStart('+', '-', '•').Trim();
        var record = FindCardRecord(line);
        if (record == null)
            return false;

        cardName = record.Name;
        return true;
    }

    private string BuildCardPreview(string cardName)
    {
        var record = FindCardRecord(cardName);
        if (record == null)
            return cardName;

        string tags = string.IsNullOrWhiteSpace(record.Category) ? "No tags" : record.Category;
        string typeLine = string.IsNullOrWhiteSpace(record.Type) ? "Unknown type" : record.Type;
        return $"{record.Name}\nMV: {record.ManaCost} | {typeLine}\nTags: {tags}";
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
        static void AddBoardByName(JsonElement boardsObject, string boardName, List<string> output)
        {
            if (boardsObject.ValueKind != JsonValueKind.Object)
                return;

            foreach (var boardProperty in boardsObject.EnumerateObject())
            {
                if (!boardProperty.Name.Equals(boardName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (boardProperty.Value.ValueKind == JsonValueKind.Object)
                    AppendCardsFromBoard(boardProperty.Value, output);
            }
        }

        if (root.TryGetProperty("boards", out var boards)
            && boards.ValueKind == JsonValueKind.Object)
        {
            AddBoardByName(boards, "mainboard", lines);

            if (!lines.Any())
            {
                foreach (var board in boards.EnumerateObject())
                {
                    if (board.Name.Equals("commanders", StringComparison.OrdinalIgnoreCase)
                        || board.Name.Equals("commander", StringComparison.OrdinalIgnoreCase)
                        || board.Name.Equals("sideboard", StringComparison.OrdinalIgnoreCase)
                        || board.Name.Equals("maybeboard", StringComparison.OrdinalIgnoreCase)
                        || board.Name.Equals("tokens", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (board.Value.ValueKind == JsonValueKind.Object)
                        AppendCardsFromBoard(board.Value, lines);
                }
            }
        }

        if (!lines.Any() && root.TryGetProperty("mainboard", out var rootMainboard) && rootMainboard.ValueKind == JsonValueKind.Object)
            AppendCardsFromBoard(rootMainboard, lines);

        if (!lines.Any() && root.TryGetProperty("cards", out var cards) && cards.ValueKind == JsonValueKind.Object)
            AppendCardsFromBoard(cards, lines);

        if (!lines.Any() && root.TryGetProperty("main", out var main) && main.ValueKind == JsonValueKind.Object)
            AppendCardsFromBoard(main, lines);

        if (!lines.Any() && root.TryGetProperty("deck", out var deck) && deck.ValueKind == JsonValueKind.Object)
        {
            if (deck.TryGetProperty("mainboard", out var deckMain) && deckMain.ValueKind == JsonValueKind.Object)
                AppendCardsFromBoard(deckMain, lines);
            if (!lines.Any() && deck.TryGetProperty("cards", out var deckCards) && deckCards.ValueKind == JsonValueKind.Object)
                AppendCardsFromBoard(deckCards, lines);
        }

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
        int deckListIndex = normalized.IndexOf("## Deck List", StringComparison.OrdinalIgnoreCase);
        if (deckListIndex >= 0)
            normalized = normalized.Substring(deckListIndex + "## Deck List".Length);

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

        if (!entries.Any())
        {
            foreach (Match match in Regex.Matches(normalized, @"(?m)^\s*(?<qty>\d+)\s+\[(?<name>[^\]]+)\]\("))
            {
                string name = WebUtility.HtmlDecode(match.Groups["name"].Value).Trim();
                if (name.EndsWith(" Transform", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - " Transform".Length).TrimEnd();

                if (name.Length == 0)
                    continue;

                if (!int.TryParse(match.Groups["qty"].Value, out int quantity) || quantity <= 0)
                    continue;

                entries.Add((name, quantity));
            }
        }

        if (!entries.Any())
        {
            bool inDeckList = false;
            foreach (string rawLine in normalized.Split('\n'))
            {
                string line = WebUtility.HtmlDecode(rawLine).Trim();
                if (line.Length == 0)
                    continue;

                if (!inDeckList)
                {
                    if (line.StartsWith("## Deck List", StringComparison.OrdinalIgnoreCase))
                        inDeckList = true;

                    continue;
                }

                if (line.StartsWith("## ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Tokens (", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Considering (", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (line.StartsWith("["))
                    continue;

                if (line.StartsWith("Playtest", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Need some sleeves?", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Support us on Patreon", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Add Tokens to Wish List", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var plainEntryMatch = Regex.Match(line, @"^(?<qty>\d+)\s+(?<name>.+?)\s*(?:\[\]\(#\))?$", RegexOptions.Singleline);
                if (!plainEntryMatch.Success)
                    continue;

                if (!int.TryParse(plainEntryMatch.Groups["qty"].Value, out int quantity) || quantity <= 0)
                    continue;

                string name = plainEntryMatch.Groups["name"].Value.Trim();
                name = Regex.Replace(name, @"\s+\[.*$", string.Empty).Trim();
                name = Regex.Replace(name, @"\s+(Game Changers|Transform)$", string.Empty, RegexOptions.IgnoreCase).Trim();
                if (name.Length == 0)
                    continue;

                entries.Add((name, quantity));
            }
        }

        return entries;
    }

    private static (string Commander, string DeckList)? TryParseMoxfieldMirrorDeck(string markdown, string commanderHint)
    {
        var entries = ParseMoxfieldMirrorEntries(markdown);
        if (!entries.Any())
            entries = ParsePlainDeckQuantityLines(markdown);

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

    private static List<(string Name, int Quantity)> ParsePlainDeckQuantityLines(string text)
    {
        var entries = new List<(string Name, int Quantity)>();
        if (string.IsNullOrWhiteSpace(text))
            return entries;

        string normalized = WebUtility.HtmlDecode(text.Replace("\r\n", "\n"));
        bool inDeckList = false;

        foreach (string raw in normalized.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (!inDeckList)
            {
                if (line.IndexOf("Deck List", StringComparison.OrdinalIgnoreCase) >= 0)
                    inDeckList = true;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Tokens (", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Considering (", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Markdown Content:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("## Cost Analysis", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("## Color Analysis", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("## Sample Hand", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("## Comments", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("## Similar Decks", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("## Additional Links", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (line.StartsWith("[")
                || line.StartsWith("Playtest", StringComparison.OrdinalIgnoreCase)
                || line.IndexOf("Support us on Patreon", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("Buy @", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            var match = Regex.Match(line, @"^(?<qty>\d+)\s+(?<name>.+)$");
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups["qty"].Value, out int qty) || qty <= 0)
                continue;

            string name = match.Groups["name"].Value.Trim();
            name = Regex.Replace(name, @"\s*\[\]\(#\)\s*$", string.Empty).Trim();
            name = Regex.Replace(name, @"\s+(Game Changers|Transform)\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
            if (name.Length == 0)
                continue;

            entries.Add((name, qty));
        }

        return entries;
    }

    private void UpdateDeckCountLabel()
    {
        int total = 0;
        var lines = deckInputTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (TryParseDeckEntry(line, out _, out int quantity))
                total += quantity;
            else if (ShouldIgnoreDeckLine(line))
                continue;
            else
                total += 1;
        }

        deckCountLabel.Text = $"Cards Entered: {total}";
        deckCountLabel.ForeColor = total == 99 ? Color.FromArgb(74, 222, 128) : Color.FromArgb(148, 163, 184);
    }

    private void DeckInputTextBox_TextChanged(object? sender, EventArgs e)
    {
        UpdateDeckCountLabel();

        if (!string.IsNullOrWhiteSpace(commanderTextBox.Text))
            return;

        string detectedCommander = TryDetectCommanderFromDeckInput(deckInputTextBox.Text);
        if (!string.IsNullOrWhiteSpace(detectedCommander))
            commanderTextBox.Text = detectedCommander;
    }

    private static string TryDetectCommanderFromDeckInput(string deckText)
    {
        if (string.IsNullOrWhiteSpace(deckText))
            return string.Empty;

        string normalized = deckText.Replace("\r\n", "\n");
        string[] lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        bool commanderSection = false;
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (Regex.IsMatch(line, @"^(commander|command zone)\s*:?$", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, @"^\[\s*commander\s*\]$", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, @"^//\s*commander\s*$", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, @"^#\s*commander\s*$", RegexOptions.IgnoreCase))
            {
                commanderSection = true;
                continue;
            }

            if (commanderSection && (line.StartsWith("[", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Sideboard", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Maybeboard", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Deck", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Mainboard", StringComparison.OrdinalIgnoreCase)))
            {
                commanderSection = false;
            }

            if (commanderSection && TryParseDeckEntry(line, out string cardName, out int quantity) && quantity >= 1)
                return cardName;

            var inlineCommander = Regex.Match(line, @"^commander\s*:\s*(?<name>.+)$", RegexOptions.IgnoreCase);
            if (inlineCommander.Success)
                return inlineCommander.Groups["name"].Value.Trim();
        }

        if (lines.Length >= 100)
        {
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (TryParseDeckEntry(line, out string cardName, out int quantity) && quantity == 1)
                    return cardName;
            }
        }

        return string.Empty;
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

        if (root.TryGetProperty("commander", out var commanderElement))
        {
            if (commanderElement.ValueKind == JsonValueKind.String)
                return commanderElement.GetString();

            if (commanderElement.ValueKind == JsonValueKind.Object)
            {
                if (commanderElement.TryGetProperty("name", out var commanderName)
                    && commanderName.ValueKind == JsonValueKind.String)
                {
                    return commanderName.GetString();
                }

                if (commanderElement.TryGetProperty("card", out var commanderCard)
                    && commanderCard.ValueKind == JsonValueKind.Object
                    && commanderCard.TryGetProperty("name", out var cardName)
                    && cardName.ValueKind == JsonValueKind.String)
                {
                    return cardName.GetString();
                }
            }
        }

        if (root.TryGetProperty("deck", out var deck)
            && deck.ValueKind == JsonValueKind.Object
            && deck.TryGetProperty("commander", out var deckCommander))
        {
            if (deckCommander.ValueKind == JsonValueKind.String)
                return deckCommander.GetString();

            if (deckCommander.ValueKind == JsonValueKind.Object
                && deckCommander.TryGetProperty("name", out var deckCommanderName)
                && deckCommanderName.ValueKind == JsonValueKind.String)
            {
                return deckCommanderName.GetString();
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
        string bareDeckUrl = $"https://moxfield.com/decks/{deckId}";

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
        foreach (var pageUrl in new[] { canonicalDeckUrl, bareDeckUrl })
        {
            string? pageHtml = await TryDownloadTextAsync(pageUrl, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            if (!string.IsNullOrWhiteSpace(pageHtml))
            {
                commanderHint = ExtractCommanderFromMoxfieldHtml(pageHtml);
                var parsedHtmlDeck = TryParseMoxfieldMirrorDeck(pageHtml, commanderHint);
                if (parsedHtmlDeck.HasValue)
                    return parsedHtmlDeck.Value;
            }

            foreach (var mirrorUrl in new[]
            {
                $"https://r.jina.ai/http://{pageUrl}",
                $"https://r.jina.ai/http://{pageUrl}/download",
                $"https://r.jina.ai/http://{pageUrl}?view=spoiler"
            })
            {
                string? mirroredMarkdown = await TryDownloadTextAsync(mirrorUrl, "text/plain, text/markdown, */*");
                if (string.IsNullOrWhiteSpace(mirroredMarkdown))
                    continue;

                var parsedMirrorDeck = TryParseMoxfieldMirrorDeck(mirroredMarkdown, commanderHint);
                if (parsedMirrorDeck.HasValue)
                    return parsedMirrorDeck.Value;
            }
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
            resultsTextBox.SelectionColor = Color.FromArgb(255, 255, 255);
            resultsTextBox.AppendText("Moxfield Import\n");

            void Header(string text)
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                resultsTextBox.SelectionColor = Color.FromArgb(250, 204, 21);
                resultsTextBox.AppendText(text + "\n");
            }

            void Body(string text)
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                resultsTextBox.SelectionColor = Color.FromArgb(220, 214, 254);
                resultsTextBox.AppendText(text + "\n");
            }

            Body(new string('-', 50));
            if (!string.IsNullOrWhiteSpace(imported.Commander))
                Body($"Commander: {imported.Commander}");

            var importedLines = imported.DeckList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int totalImported = 0;
            foreach (var line in importedLines)
            {
                if (TryParseDeckEntry(line, out _, out int quantity))
                    totalImported += quantity;
                else if (!ShouldIgnoreDeckLine(line))
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
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => NormalizeDeckCardName(name))
            .Where(name => name.Length > 0 && FindCardRecord(name) == null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!missing.Any())
            return;

        progressLabel.Text = $"Fetching {missing.Count} unknown card(s) from Scryfall...";
        var unresolved = await FetchMissingCardsFromScryfallAsync(missing);
        foreach (var name in unresolved)
        {
            bool resolved = await TryFetchFromScryfallAsync(name);
            if (!resolved)
                missingCardNames.Add(name);
        }
    }

    private async Task<List<string>> FetchMissingCardsFromScryfallAsync(List<string> names)
    {
        var unresolved = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        const int maxIdentifiersPerRequest = 75;

        for (int index = 0; index < names.Count; index += maxIdentifiersPerRequest)
        {
            var chunk = names.Skip(index).Take(maxIdentifiersPerRequest).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!chunk.Any())
                continue;

            try
            {
                using var payload = new StringContent(JsonSerializer.Serialize(new
                {
                    identifiers = chunk.Select(name => new { name }).ToList()
                }), System.Text.Encoding.UTF8, "application/json");

                using var response = await Http.PostAsync("https://api.scryfall.com/cards/collection", payload);
                if (!response.IsSuccessStatusCode)
                    continue;

                string json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cardElement in dataElement.EnumerateArray())
                    {
                        var record = BuildCardRecordFromScryfall(cardElement, string.Empty);
                        if (record == null)
                            continue;

                        IndexCardRecord(record, record.Name);
                        PersistCardRecord(record);
                        unresolved.Remove(record.Name);
                    }
                }

                if (root.TryGetProperty("not_found", out var notFoundElement) && notFoundElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var missing in notFoundElement.EnumerateArray())
                    {
                        if (missing.TryGetProperty("name", out var missingNameElement) && missingNameElement.ValueKind == JsonValueKind.String)
                        {
                            var missingName = NormalizeDeckCardName(missingNameElement.GetString());
                            if (missingName.Length > 0)
                                unresolved.Add(missingName);
                        }
                    }
                }
            }
            catch
            {
                // Fall back to one-by-one fetches for this chunk.
            }
        }

        return unresolved.ToList();
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
                var record = BuildCardRecordFromScryfall(root, name);
                if (record == null)
                    continue;

                IndexCardRecord(record, name);
                PersistCardRecord(record);
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
        const string ExpectedHeader = "Name,ManaCost,Colors,ColorIdentity,Type,Category,CardType,IsLand,OracleText";
        const string LegacyHeader = "Name,ManaCost,Colors,Type,Category,CardType,IsLand,OracleText";

        if (!File.Exists(cardDatabaseFilePath))
        {
            File.WriteAllText(cardDatabaseFilePath,
                "Name,ManaCost,Colors,ColorIdentity,Type,Category,CardType,IsLand,OracleText\n" +
                "Island,0,,,Land,Land,Land,true,\"\"\n" +
                "Forest,0,,,Land,Land,Land,true,\"\"\n" +
                "Swamp,0,,,Land,Land,Land,true,\"\"\n" +
                "Mountain,0,,,Land,Land,Land,true,\"\"\n" +
                "Plains,0,,,Land,Land,Land,true,\"\"\n" +
                "Watery Grave,0,U;B,U;B,Land — Island Swamp,Land,Land,true,\"\"\n" +
                "Sol Ring,1,,,Artifact,Ramp,Artifact,false,\"\"\n" +
                "Swords to Plowshares,1,,,Instant,Removal,Instant,false,\"Remove target creature or planeswalker.\"\n" +
                "Rhystic Study,3,U,U,Enchantment,Card Draw,Enchantment,false,\"Whenever an opponent casts a spell, you may draw a card unless that player pays {1}.\"\n" +
                "Nicol Bolas Dragon God,6,U;B;R,U;B;R,Planeswalker,Planeswalker,Planeswalker,false,\"\"\n");
        }

        try
        {
            var lines = File.ReadAllLines(cardDatabaseFilePath);
            if (lines.Length == 0)
            {
                MessageBox.Show("cards.csv exists but is empty. A fallback starter database will be written.", "Deck Evaluator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                File.WriteAllText(cardDatabaseFilePath, ExpectedHeader + "\n");
                lines = File.ReadAllLines(cardDatabaseFilePath);
            }

            string header = lines[0].Trim();
            if (header.Equals(LegacyHeader, StringComparison.Ordinal))
            {
                MigrateLegacyCardDatabase(lines);
                lines = File.ReadAllLines(cardDatabaseFilePath);
                header = lines[0].Trim();
            }

            if (!header.Equals(ExpectedHeader, StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "cards.csv header does not match expected schema. Parsing will continue, but some fields may load incorrectly.\n\n"
                    + $"Expected: {ExpectedHeader}\n"
                    + $"Found:    {header}",
                    "Deck Evaluator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            int malformedRows = 0;
            int nonLandRows = 0;
            int roleTaggedRows = 0;
            foreach (var row in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row))
                    continue;

                var fields = ParseCsvRow(row);
                if (fields.Length < 5)
                {
                    malformedRows++;
                    continue;
                }

                string name = fields[0].Trim();
                if (name.Length == 0)
                    continue;

                persistedCardNames.Add(name);

                int manaCost = 0;
                if (double.TryParse(fields[1], out double parsedManaCost))
                    manaCost = NormalizeManaValue(parsedManaCost);

                var colors = fields.Length >= 3
                    ? fields[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(color => color.Trim()).Where(color => color.Length > 0).ToList()
                    : new List<string>();
                bool hasColorIdentityColumn = fields.Length >= 9;
                var colorIdentity = hasColorIdentityColumn
                    ? fields[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(color => color.Trim()).Where(color => color.Length > 0).ToList()
                    : colors.ToList();
                string type = fields.Length >= (hasColorIdentityColumn ? 5 : 4) ? fields[hasColorIdentityColumn ? 4 : 3].Trim() : string.Empty;
                string category = fields.Length >= (hasColorIdentityColumn ? 6 : 5) ? fields[hasColorIdentityColumn ? 5 : 4].Trim() : "Unknown";
                bool isLand = false;
                string oracleText = string.Empty;

                if (fields.Length >= 9)
                {
                    isLand = bool.TryParse(fields[7], out bool parsedIsLand) && parsedIsLand;
                    oracleText = fields[8].Trim().Trim('"');
                }
                else if (fields.Length >= 8)
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

                if (IsLandByTypeLine(type))
                    isLand = true;

                var record = new CardDbRecord
                {
                    Name = name,
                    ManaCost = manaCost,
                    Colors = colors,
                    ColorIdentity = colorIdentity,
                    Type = type,
                    Category = category,
                    IsLand = isLand,
                    OracleText = oracleText
                };
                IndexCardRecord(record, name);

                if (!isLand)
                {
                    nonLandRows++;
                    bool hasRoleTag = category
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(tag => tag.Trim())
                        .Any(tag => RoleTags.Contains(tag));
                    if (hasRoleTag)
                        roleTaggedRows++;
                }
            }

            if (malformedRows > 0)
            {
                MessageBox.Show(
                    $"Loaded cards.csv with {malformedRows} malformed row(s) skipped. Consider regenerating the database.",
                    "Deck Evaluator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            if (nonLandRows > 0)
            {
                double tagCoverage = roleTaggedRows * 100.0 / nonLandRows;
                if (tagCoverage < 15)
                {
                    MessageBox.Show(
                        $"cards.csv loaded, but only {tagCoverage:F1}% of non-land cards have role tags (Removal, Card Draw, Ramp, etc.). "
                        + "Consider regenerating cards.csv so evaluator recommendations can use role tags reliably.",
                        "Deck Evaluator",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load card database: " + ex.Message, "Deck Evaluator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MigrateLegacyCardDatabase(string[] legacyLines)
    {
        var migrated = new List<string>
        {
            "Name,ManaCost,Colors,ColorIdentity,Type,Category,CardType,IsLand,OracleText"
        };

        foreach (var row in legacyLines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(row))
                continue;

            var fields = ParseCsvRow(row);
            if (fields.Length < 8)
            {
                migrated.Add(row);
                continue;
            }

            string colors = fields[2];
            migrated.Add(string.Join(",",
                CsvEscape(fields[0]),
                fields[1],
                CsvEscape(fields[2]),
                CsvEscape(colors),
                CsvEscape(fields[3]),
                CsvEscape(fields[4]),
                CsvEscape(fields[5]),
                fields[6],
                CsvEscape(fields[7])));
        }

        File.WriteAllLines(cardDatabaseFilePath, migrated);
    }

    private static string InferCardType(string typeLine, bool isLand)
    {
        if (isLand || typeLine.IndexOf("Land", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Land";

        foreach (var type in new[] { "Creature", "Instant", "Sorcery", "Artifact", "Enchantment", "Planeswalker", "Battle" })
        {
            if (typeLine.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0)
                return type;
        }

        return "Other";
    }

    private static string InferCategoryFromCardData(string typeLine, string oracleText, bool isLand)
    {
        if (isLand)
            return "Land";

        var card = new Card
        {
            Name = string.Empty,
            IsLand = isLand,
            ManaCost = 0,
            Type = typeLine,
            Category = string.Empty,
            OracleText = oracleText
        };

        var tags = new List<string>();
        if (typeLine.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Creature");
        if (typeLine.IndexOf("Instant", StringComparison.OrdinalIgnoreCase) >= 0 || typeLine.IndexOf("Sorcery", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Instant/Sorcery");
        if (typeLine.IndexOf("Artifact", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Artifact");
        if (typeLine.IndexOf("Enchantment", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Enchantment");
        if (typeLine.IndexOf("Planeswalker", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("Planeswalker");

        string oracleLower = oracleText.ToLowerInvariant();
        if ((oracleLower.Contains("draw") && oracleLower.Contains("card")) || oracleLower.Contains("investigate")) tags.Add("Card Draw");
        if ((oracleLower.Contains("destroy") && oracleLower.Contains("target"))
            || (oracleLower.Contains("exile") && oracleLower.Contains("target"))
            || (oracleLower.Contains("fight") && oracleLower.Contains("target"))
            || (oracleLower.Contains("damage") && oracleLower.Contains("target"))) tags.Add("Removal");
        if ((oracleLower.Contains("add {") || oracleLower.Contains("create a treasure") || oracleLower.Contains("create treasure"))
            && !isLand) tags.Add("Ramp");
        if (oracleLower.Contains("search your library")) tags.Add("Tutor");

        return tags.Any() ? string.Join(", ", tags.Distinct(StringComparer.OrdinalIgnoreCase)) : InferCardType(typeLine, isLand);
    }

    private static int NormalizeManaValue(double rawManaValue)
    {
        if (double.IsNaN(rawManaValue) || double.IsInfinity(rawManaValue))
            return 0;

        return Math.Max(0, (int)Math.Floor(rawManaValue + 1e-9));
    }

    private static bool IsLandByTypeLine(string? typeLine)
    {
        if (string.IsNullOrWhiteSpace(typeLine))
            return false;

        string normalized = typeLine.Replace("â€”", "—");
        var faces = normalized.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
        bool sawLandFace = false;
        bool sawNonLandFace = false;

        foreach (var face in faces)
        {
            string front = face;
            int dashIndex = front.IndexOf('—');
            if (dashIndex >= 0)
                front = front.Substring(0, dashIndex);

            var tokens = front
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .ToList();
            if (!tokens.Any())
                continue;

            bool hasLand = tokens.Contains("Land", StringComparer.OrdinalIgnoreCase);
            bool hasNonLandCore = tokens.Any(token => NonLandCoreTypes.Contains(token));

            if (hasLand)
                sawLandFace = true;
            if (hasNonLandCore)
                sawNonLandFace = true;
        }

        return sawLandFace && !sawNonLandFace;
    }

    private static string CsvEscape(string value)
    {
        string normalized = value.Replace("\r", " ").Replace("\n", " ");
        if (normalized.Contains('"'))
            normalized = normalized.Replace("\"", "\"\"");

        return normalized.IndexOfAny(new[] { ',', '"' }) >= 0 ? $"\"{normalized}\"" : normalized;
    }

    private void PersistCardRecord(CardDbRecord record)
    {
        if (persistedCardNames.Contains(record.Name))
            return;

        string cardType = InferCardType(record.Type, record.IsLand);
        string csvLine = string.Join(",",
            CsvEscape(record.Name),
            record.ManaCost.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            CsvEscape(string.Join(";", record.Colors)),
            CsvEscape(string.Join(";", record.ColorIdentity.Count > 0 ? record.ColorIdentity : record.Colors)),
            CsvEscape(record.Type),
            CsvEscape(record.Category),
            CsvEscape(cardType),
            record.IsLand ? "true" : "false",
            CsvEscape(record.OracleText));

        File.AppendAllText(cardDatabaseFilePath, csvLine + Environment.NewLine);
        persistedCardNames.Add(record.Name);
    }

    private static string NormalizeDeckCardName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        string normalized = name.Trim();
        normalized = Regex.Replace(normalized, @"^\d+\s*x?\s*", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\sx\d+$", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+\([^\)]*\)$", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+\[[^\]]*\]$", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+\d+[A-Za-z]?$", string.Empty);
        normalized = normalized.Replace("â€”", "—").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static string BuildLookupKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        string lowered = NormalizeDeckCardName(name).ToLowerInvariant();
        lowered = Regex.Replace(lowered, @"[^a-z0-9\s'\-,]", string.Empty);
        return Regex.Replace(lowered, @"\s+", " ").Trim();
    }

    private void IndexCardRecord(CardDbRecord record, params string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(record.Name))
            return;

        cardDatabase[record.Name] = record;
        string primaryKey = BuildLookupKey(record.Name);
        if (primaryKey.Length > 0)
            normalizedCardIndex[primaryKey] = record;

        foreach (var face in record.Name.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string faceKey = BuildLookupKey(face);
            if (faceKey.Length > 0)
                normalizedCardIndex[faceKey] = record;
        }

        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
                continue;

            string cleanedAlias = NormalizeDeckCardName(alias);
            if (cleanedAlias.Length > 0)
                cardDatabase[cleanedAlias] = record;

            string aliasKey = BuildLookupKey(cleanedAlias);
            if (aliasKey.Length > 0)
                normalizedCardIndex[aliasKey] = record;
        }
    }

    private CardDbRecord? FindCardRecord(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string cleaned = NormalizeDeckCardName(name);
        if (cleaned.Length == 0)
            return null;

        if (cardDatabase.TryGetValue(cleaned, out var direct))
            return direct;

        string key = BuildLookupKey(cleaned);
        if (key.Length > 0 && normalizedCardIndex.TryGetValue(key, out var indexed))
            return indexed;

        return cardDatabase.Values.FirstOrDefault(card => string.Equals(card.Name, cleaned, StringComparison.OrdinalIgnoreCase));
    }

    private CardDbRecord? BuildCardRecordFromScryfall(JsonElement root, string requestedName)
    {
        string cardName = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? requestedName : requestedName;
        if (string.IsNullOrWhiteSpace(cardName))
            return null;

        int cmc = root.TryGetProperty("cmc", out var cmcProperty) && cmcProperty.ValueKind == JsonValueKind.Number
            ? NormalizeManaValue(cmcProperty.GetDouble())
            : 3;

        string typeLine = root.TryGetProperty("type_line", out var typeProperty)
            ? typeProperty.GetString() ?? string.Empty
            : string.Empty;

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

        // Always read color_identity separately — it includes activated ability symbols
        var colorIdentity = new List<string>();
        if (root.TryGetProperty("color_identity", out var identityProperty) && identityProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var color in identityProperty.EnumerateArray())
            {
                if (color.GetString() is string colorCode && colorCode.Length > 0)
                    colorIdentity.Add(colorCode);
            }
        }

        // Fallback: if colors array was empty, use color_identity for colors too
        if (!colors.Any())
            colors.AddRange(colorIdentity);

        bool isLand = IsLandByTypeLine(typeLine);
        return new CardDbRecord
        {
            Name = cardName,
            ManaCost = isLand ? 0 : cmc,
            Colors = colors.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ColorIdentity = colorIdentity.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Type = typeLine,
            Category = InferCategoryFromCardData(typeLine, oracleText, isLand),
            IsLand = isLand,
            OracleText = oracleText
        };
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
            if (IsLandByTypeLine(card.Type)) tags.Add("Land");

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
        outputTabs.SelectedIndex = 0;
        SaveUiState();
        _activeRunCancellation?.Dispose();
        _activeRunCancellation = new CancellationTokenSource();
        var cancellationToken = _activeRunCancellation.Token;

        try
        {
            SetActionButtonsEnabled(false);
            cancelButton.Enabled = true;
            StartWizardAnimation();
            var rawInputLines = deckInputTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var prefetchNames = rawInputLines
                .Select(line => TryParseDeckEntry(line, out string parsedName, out _) ? parsedName : string.Empty)
                .Where(name => name.Length > 0)
                .ToList();
            if (!string.IsNullOrWhiteSpace(commanderTextBox.Text))
                prefetchNames.Add(NormalizeDeckCardName(commanderTextBox.Text));
            await PrefetchMissingCardsAsync(prefetchNames);
            cancellationToken.ThrowIfCancellationRequested();

            Deck deck = await ParseDeck(deckInputTextBox.Text, commanderTextBox.Text);
            if (deck.Cards.Count == 0)
            {
                resultsTextBox.Clear();
                resultsTextBox.SelectionColor = Color.Red;
                resultsTextBox.AppendText("No cards found in deck list.");
                return;
            }
            var legalityErrors = await ValidateCommanderDeckLegalityAsync(deck, cancellationToken);
            if (legalityErrors.Count > 0)
            {
                StopWizardAnimation();
                resultsTextBox.Clear();
                resultsTextBox.SelectionColor = Color.FromArgb(248, 113, 113);
                resultsTextBox.SelectionFont = new Font("Segoe UI", 11, FontStyle.Bold);
                resultsTextBox.AppendText("Deck Legality Check Failed\n\n");
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                foreach (string error in legalityErrors)
                    resultsTextBox.AppendText($"- {error}\n");

                progressLabel.Text = "Fix deck legality issues and try again.";
                return;
            }

            progressLabel.Text = "Checking Commander Spellbook combos...";
            await AttachSpellbookReportAsync(deck, cancellationToken);

            int numSimulations = gamesComboBox.SelectedItem switch
            {
                "10k" => 10000,
                "50k" => 50000,
                "200k" => 200000,
                "500k" => 500000,
                _ => 100000, // Default to 100k
            };
            int maxTurns = 10;
            progressBar.Value = 0;
            progressLabel.Text = "Fizban the Fabulous is running simulations...";
            AppendTrainLog($"Analyze run started: {numSimulations:N0} games, {maxTurns} turns.");

            bool onDraw = onDrawCheckBox.Checked;
            var profile = GetSelectedOpponentProfile();
            var evaluator = new DeckEvaluator(deck, GetSelectedArchetypeOverride(), cedhCheckBox.Checked, profile);
            var results = await Task.Run(() => evaluator.RunSimulations(numSimulations, maxTurns, onDraw, progress =>
            {
                if (!IsHandleCreated || IsDisposed)
                    return;

                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed)
                        return;

                    int scaled = (int)Math.Round(progress * (double)progressBar.Maximum / numSimulations);
                    scaled = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, scaled));
                    progressBar.Value = scaled;
                    progressLabel.Text = $"Simulating game {progress}/{numSimulations} ({scaled}/{progressBar.Maximum})";
                }));
            }, cancellationToken), cancellationToken);

            progressLabel.Text = "Gathering commander data...";
            var enrichedSuggestions = await BuildSuggestionsWithCommanderDataAsync(deck, results);

            progressLabel.Text = "Done!";
            StopWizardAnimation();
            RenderResults(deck, results, numSimulations, maxTurns, onDraw, enrichedSuggestions);
            AddReportToHistory($"Analyze | {DateTime.Now:MM/dd HH:mm} | {results.EstimatedBracket}", resultsTextBox.Text);
            AppendTrainLog($"Analyze run completed. Bracket: {results.EstimatedBracket}, Power: {results.EstimatedPowerLevel:F1}/10.");
        }
        catch (OperationCanceledException)
        {
            StopWizardAnimation();
            resultsTextBox.Clear();
            resultsTextBox.SelectionColor = Color.FromArgb(251, 191, 36);
            resultsTextBox.AppendText("Evaluation cancelled.");
            progressLabel.Text = "Evaluation cancelled.";
        }
        catch (Exception ex)
        {
            StopWizardAnimation();
            resultsTextBox.Clear();
            resultsTextBox.SelectionColor = Color.FromArgb(248, 113, 113);
            resultsTextBox.AppendText($"Error: {ex.Message}");
        }
        finally
        {
            cancelButton.Enabled = false;
            SetActionButtonsEnabled(true);
            _activeRunCancellation?.Dispose();
            _activeRunCancellation = null;
        }
    }

    private async void TrainButton_Click(object? sender, EventArgs e)
    {
        const int batches = 6;
        const int simulationsPerBatch = 100000;
        const int maxTurns = 10;

        SaveUiState();
        _activeRunCancellation?.Dispose();
        _activeRunCancellation = new CancellationTokenSource();
        var cancellationToken = _activeRunCancellation.Token;

        try
        {
            outputTabs.SelectedIndex = 1;
            SetActionButtonsEnabled(false);
            cancelButton.Enabled = true;
            StartWizardAnimation();
            progressBar.Minimum = 0;
            progressBar.Maximum = batches;
            progressBar.Value = 0;
            progressLabel.Text = $"Fizban the Fabulous is training! ({batches} batches × {simulationsPerBatch:N0} sims)";
            AppendTrainLog($"Training started: {batches} batches, {simulationsPerBatch:N0} sims per batch.");

            bool onDraw = onDrawCheckBox.Checked;
            EvaluationResults results = await RunHeadlessTrainingAsync(
                deckInputTextBox.Text,
                commanderTextBox.Text,
                simulationsPerBatch,
                maxTurns,
                batches,
                onDraw,
                message =>
                {
                    if (!IsHandleCreated)
                        return;

                    BeginInvoke((Action)(() =>
                    {
                        progressLabel.Text = message;
                        AppendTrainLog(message);
                        var batchMatch = Regex.Match(message, @"Batch\s+(?<index>\d+)/(?<total>\d+)", RegexOptions.IgnoreCase);
                        if (batchMatch.Success
                            && int.TryParse(batchMatch.Groups["index"].Value, out int batchIndex)
                            && batchIndex >= progressBar.Minimum
                            && batchIndex <= progressBar.Maximum)
                        {
                            progressBar.Value = batchIndex;
                        }
                    }));
                },
                cedhCheckBox.Checked,
                GetSelectedOpponentProfile(),
                cancellationToken);

            Deck deck = await ParseDeck(deckInputTextBox.Text, commanderTextBox.Text);
            await AttachSpellbookReportAsync(deck, cancellationToken);
            progressLabel.Text = "Gathering commander data...";
            var enrichedSuggestions = await BuildSuggestionsWithCommanderDataAsync(deck, results);
            StopWizardAnimation();
            RenderResults(deck, results, simulationsPerBatch, maxTurns, onDraw, enrichedSuggestions);
            AddReportToHistory($"Train | {DateTime.Now:MM/dd HH:mm} | {results.EstimatedBracket}", resultsTextBox.Text);
            progressLabel.Text = $"Fizban the Fabulous finished training! Learned games: {results.LearningGamesSeen}";
            AppendTrainLog($"Training complete. Learned games: {results.LearningGamesSeen}. Power: {results.EstimatedPowerLevel:F1}/10.");
        }
        catch (OperationCanceledException)
        {
            StopWizardAnimation();
            resultsTextBox.Clear();
            resultsTextBox.SelectionColor = Color.FromArgb(251, 191, 36);
            resultsTextBox.AppendText("Training cancelled.");
            progressLabel.Text = "Training cancelled.";
        }
        catch (Exception ex)
        {
            StopWizardAnimation();
            resultsTextBox.Clear();
            resultsTextBox.SelectionColor = Color.FromArgb(248, 113, 113);
            resultsTextBox.AppendText($"Training error: {ex.Message}");
            progressLabel.Text = "Training failed.";
        }
        finally
        {
            progressBar.Maximum = 1000;
            progressBar.Value = 0;
            cancelButton.Enabled = false;
            SetActionButtonsEnabled(true);
            _activeRunCancellation?.Dispose();
            _activeRunCancellation = null;
        }
    }

    private static string[] BuildWizardFrames()
    {
        return new[]
        {
            " *                 .    \n" +
            "          .             \n" +
            "           /_\\          \n" +
            "        .-\\_//-.        \n" +
            "        |  o o  |  .    \n" +
            "        |   ^   |       \n" +
            "       /|\\___//|\\      \n" +
            "      /_|_===_|_\\      \n" +
            "        /  | |  \\      \n" +
            "       /___| |___\\     \n" +
            "\n  Consulting the arcane database...",

            "    .           *       \n" +
            "       .                \n" +
            "           /_\\          \n" +
            "        .-\\_//-.        \n" +
            "   .    |  o o  |       \n" +
            "        |   ~   |  *    \n" +
            "       /|\\___//|\\      \n" +
            "      /_|_===_|_\\      \n" +
            "        /  | |  \\      \n" +
            "       /___| |___\\     \n" +
            "\n  Running goldfish simulations...",

            "            *      .    \n" +
            "   .                    \n" +
            "           /_\\          \n" +
            "        .-\\_//-.   .    \n" +
            "        |  o o  |       \n" +
            "     *  |   ^   |       \n" +
            "       /|\\___//|\\      \n" +
            "      /_|_===_|_\\      \n" +
            "        /  | |  \\      \n" +
            "       /___| |___\\     \n" +
            "\n  Evaluating mana curve...",

            "   .              *     \n" +
            "          .             \n" +
            "           /_\\          \n" +
            "        .-\\_//-.        \n" +
            "        |  o o  |       \n" +
            "        |   -   |  .    \n" +
            "       /|\\___//|\\  *   \n" +
            "      /_|_===_|_\\      \n" +
            "        /  | |  \\      \n" +
            "       /___| |___\\     \n" +
            "\n  Checking synergies...",

            "       *      .         \n" +
            "                   .    \n" +
            "           /_\\          \n" +
            "        .-\\_//-.        \n" +
            "     .  |  o o  |       \n" +
            "        |   ^   |       \n" +
            "       /|\\___//|\\  *   \n" +
            "      /_|_===_|_\\      \n" +
            "        /  | |  \\      \n" +
            "       /___| |___\\     \n" +
            "\n  Analyzing card interactions...",

            "          .        *    \n" +
            "    .                   \n" +
            "           /_\\          \n" +
            "        .-\\_//-.        \n" +
            "        |  o o  |   .   \n" +
            "        |   ~   |       \n" +
            "       /|\\___//|\\      \n" +
            "      /_|_===_|_\\  *   \n" +
            "        /  | |  \\      \n" +
            "       /___| |___\\     \n" +
            "\n  Tallying power level..."
        };
    }

    private void StartWizardAnimation()
    {
        _wizardFrame = 0;
        _wizardTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _wizardTimer.Tick += WizardTimer_Tick;
        WizardTimer_Tick(null, EventArgs.Empty);
        _wizardTimer.Start();
    }

    private void StopWizardAnimation()
    {
        if (_wizardTimer != null)
        {
            _wizardTimer.Stop();
            _wizardTimer.Dispose();
            _wizardTimer = null;
        }
    }

    private void WizardTimer_Tick(object? sender, EventArgs e)
    {
        resultsTextBox.Clear();
        resultsTextBox.SelectionFont = new Font("Cascadia Mono", 10, FontStyle.Regular);
        resultsTextBox.SelectionColor = Color.FromArgb(180, 150, 255);
        resultsTextBox.AppendText(WizardFrames[_wizardFrame % WizardFrames.Length]);
        _wizardFrame++;
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        evaluateButton.Enabled = enabled;
        trainButton.Enabled = enabled;
        importMoxfieldButton.Enabled = enabled;
        saveReportButton.Enabled = enabled;
        cancelButton.Enabled = !enabled && _activeRunCancellation != null && !_activeRunCancellation.IsCancellationRequested;
    }

    private async Task AttachSpellbookReportAsync(Deck deck, CancellationToken cancellationToken)
    {
        try
        {
            deck.SpellbookReport = await CommanderSpellbookService.GetDeckReportAsync(deck, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            deck.SpellbookReport = null;
        }
    }

    private void RenderResults(Deck deck, EvaluationResults results, int numSimulations, int maxTurns, bool onDraw, IReadOnlyList<DeckSuggestion> enrichedSuggestions)
    {
        // Record all cards in the deck to learn what cards are good
        if (_cardMetrics != null && deck.Commander != null)
        {
            var deckArchetype = DeckAnalysis.DetectPrimaryArchetype(deck);
            foreach (var card in deck.Cards.Where(c => !c.IsCommander))
            {
                _cardMetrics.RecordCardInDeck(card, deck, deckArchetype);
            }
            _cardMetrics.SaveMetrics();
        }

        resultsTextBox.Clear();

        void Header(string text)
        {
            resultsTextBox.AppendText("\n");
            resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.FromArgb(250, 204, 21);
            resultsTextBox.AppendText($"[{text}]\n");
        }

        void Body(string text)
        {
            resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
            resultsTextBox.SelectionColor = Color.FromArgb(220, 214, 254);
            resultsTextBox.AppendText(text + "\n");
        }

        void ConfidenceMeter(double confidence)
        {
            int slots = 18;
            int filled = Math.Max(0, Math.Min(slots, (int)Math.Round(confidence * slots)));
            resultsTextBox.SelectionFont = new Font("Cascadia Mono", 9, FontStyle.Bold);
            resultsTextBox.SelectionColor = Color.FromArgb(74, 222, 128);
            resultsTextBox.AppendText(new string('█', filled));
            resultsTextBox.SelectionColor = Color.FromArgb(71, 85, 105);
            resultsTextBox.AppendText(new string('░', slots - filled));
            resultsTextBox.SelectionColor = Color.FromArgb(250, 204, 21);
            resultsTextBox.AppendText($" {confidence * 100:F0}%\n");
        }

        resultsTextBox.SelectionFont = new Font("Segoe UI", 13, FontStyle.Bold);
        resultsTextBox.SelectionColor = Color.FromArgb(255, 255, 255);
        resultsTextBox.AppendText(results.IsCedh ? "Fizban's cEDH Analysis Report\n" : "Fizban's Analysis Report\n");
        Body($"{numSimulations:N0} simulations across {maxTurns} turns");
        Body(new string('-', 72));

        if (missingCardNames.Any())
        {
            resultsTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Italic);
            resultsTextBox.SelectionColor = Color.FromArgb(251, 191, 36);
            resultsTextBox.AppendText($"⚠ {missingCardNames.Count} card(s) not in local DB — resolved via Scryfall or defaults: {string.Join(", ", missingCardNames.Take(6))}{(missingCardNames.Count > 6 ? "…" : string.Empty)}\n");
        }

        Body($"Simulation: Fizban the Fabulous ✦ mulligans, tutor targets, draw sequencing, reactive spell restraint, turn-sequence planning | On the draw: {(onDraw ? "Yes" : "No")}");
        Body($"Archetype model: {results.SelectedArchetype}");
        if (results.DetectedArchetype != results.SelectedArchetype)
            Body($"Auto-detected archetype: {results.DetectedArchetype}");

        if (deck.Commander != null)
            Body($"Commander: {deck.Commander.Name}");
        else if (!string.IsNullOrWhiteSpace(commanderTextBox.Text))
            Body($"Commander: {commanderTextBox.Text.Trim()}");

        int landCount = deck.LandCount;
        int nonLandCount = deck.Cards.Count(card => !card.IsLand && !card.IsCommander);

        Header("Summary");
        Body($"Cards: {deck.Cards.Count} | Lands: {landCount} ({(landCount * 100.0 / deck.Cards.Count):F1}%) | Spells: {nonLandCount} ({(nonLandCount * 100.0 / deck.Cards.Count):F1}%)");
        Body($"Land hit rate: {(100 - (results.AverageMissedLands / maxTurns * 100)):F1}% | Lands by T{maxTurns}: {results.AverageLandsPlayed:F2} | Idle turns: {results.AverageIdleTurns:F2}");
        Body($"Avg spells cast/game: {results.AverageSpellsCast:F1} ({results.AverageSpellsCast / maxTurns:F2}/turn) | Peak mana: {results.AveragePeakMana:F1}");
        Body($"Opening hand: {results.AverageOpeningHandLands:F2} avg lands | Brick (0-land): {results.BrickHandPercent:F1}% | Flood (5+): {results.FloodHandPercent:F1}% | Mulligans: {results.AverageMulligans:F2}");
        Body($"Mana efficiency: {results.AverageManaEfficiency:F1}% | Early actions by T3: {results.AverageEarlyTurnActions:F2} | Stranded 5+ drops: {results.AverageStrandedHighCostCards:F2}");
        Body($"Activated abilities used/game: {results.AverageActivatedAbilitiesUsed:F2} | Triggered abilities resolved/game: {results.AverageTriggeredAbilitiesResolved:F2} | Infinite combo wins: {results.InfiniteComboWinRate:F1}%");
        if (deck.Commander != null)
            Body($"Commander cast rate: {results.CommanderCastRate:F1}% | Average commander cast turn: {(results.AverageCommanderCastTurn > 0 ? $"T{results.AverageCommanderCastTurn:F2}" : "not cast")}");

        Header("Power Review");
        Body($"Estimated power level: {results.EstimatedPowerLevel:F1}/10");
        Body($"Estimated commander bracket: {results.EstimatedBracket}");
        Body(results.PowerSummary);
        foreach (var signal in results.PowerSignals.Take(5))
            Body($"- {signal}");

        Header("Pod Modeling");
        Body($"Opponent profile: {results.OpponentProfile}");
        if (!string.IsNullOrWhiteSpace(results.OpponentProfileSummary))
            Body(results.OpponentProfileSummary);

        Header("Performance Decomposition");
        Body($"Tempo score: {results.TempoScore:F1}/10 - {results.TempoSummary}");
        Body($"Card advantage score: {results.CardAdvantageScore:F1}/10 - {results.CardAdvantageSummary}");
        Body($"Interaction score: {results.InteractionScore:F1}/10 - {results.InteractionSummary}");

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
                resultsTextBox.SelectionColor = Color.FromArgb(196, 181, 253);
                resultsTextBox.AppendText($"  {label,-5} [{count,3}] ");
                resultsTextBox.SelectionColor = barColor;
                resultsTextBox.AppendText(filled);
                resultsTextBox.SelectionColor = Color.FromArgb(60, 50, 90);
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
        var topSuggestions = enrichedSuggestions
            .Where(suggestion => !suggestion.Source.Equals("EDHREC", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(suggestion => suggestion.Confidence)
            .ThenBy(suggestion => suggestion.Title)
            .Take(5)
            .ToList();

        if (topSuggestions.Any())
        {
            Header("Suggested Swaps");
            foreach (var suggestion in topSuggestions)
            {
                string kindLabel = suggestion.Kind switch
                {
                    DeckSuggestionKind.Add => "Add",
                    DeckSuggestionKind.Cut => "Cut",
                    DeckSuggestionKind.Swap => "Swap",
                    _ => "Note"
                };

                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                resultsTextBox.SelectionColor = Color.FromArgb(255, 255, 255);
                resultsTextBox.AppendText($"{kindLabel}: {suggestion.Title}\n");

                if (!string.IsNullOrWhiteSpace(suggestion.Source))
                {
                    resultsTextBox.SelectionFont = new Font("Segoe UI", 8, FontStyle.Bold);
                    resultsTextBox.SelectionColor = Color.FromArgb(148, 163, 184);
                    resultsTextBox.AppendText($"Source: {suggestion.Source}\n");
                }

                resultsTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Bold);
                resultsTextBox.SelectionColor = Color.FromArgb(196, 181, 253);
                resultsTextBox.AppendText("Confidence: ");
                ConfidenceMeter(suggestion.Confidence);

                Body(suggestion.Details);

                if (suggestion.SuggestedCuts.Any())
                {
                    Body("Swap out:");
                    foreach (var cut in suggestion.SuggestedCuts)
                    {
                        string reason = suggestion.CutReasons.TryGetValue(cut, out var cutReason) ? $" — {cutReason}" : string.Empty;
                        Body($"  - {cut}{reason}");
                    }
                }

                if (suggestion.SuggestedAdds.Any())
                {
                    Body("Swap in:");
                    foreach (var add in suggestion.SuggestedAdds)
                    {
                        string reason = suggestion.AddReasons.TryGetValue(add, out var addReason) ? $" — {addReason}" : string.Empty;
                        Body($"  + {add}{reason}");
                    }
                }

                Body(string.Empty);
            }
        }

        // --- EDHREC Picks (dedicated clean section — card names only) ---
        var edhrecSugg = enrichedSuggestions.FirstOrDefault(s => s.Source.Equals("EDHREC", StringComparison.OrdinalIgnoreCase));
        if (edhrecSugg != null && edhrecSugg.SuggestedAdds.Any())
        {
            string edhrecTitle = deck.Commander != null
                ? $"EDHREC Picks — {deck.Commander.Name}"
                : "EDHREC Picks";
            Header(edhrecTitle);
            resultsTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Italic);
            resultsTextBox.SelectionColor = Color.FromArgb(148, 163, 184);
            resultsTextBox.AppendText("Cards frequently paired with this commander that aren't in your list:\n");
            foreach (var cardName in edhrecSugg.SuggestedAdds)
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                resultsTextBox.SelectionColor = Color.FromArgb(74, 222, 128);
                string reason = edhrecSugg.AddReasons.TryGetValue(cardName, out var addReason) ? $" — {addReason}" : string.Empty;
                resultsTextBox.AppendText($"  + {cardName}{reason}\n");
            }
            if (edhrecSugg.SuggestedCuts.Any())
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Italic);
                resultsTextBox.SelectionColor = Color.FromArgb(148, 163, 184);
                resultsTextBox.AppendText("\nPossible cuts to make room:\n");
                foreach (var cut in edhrecSugg.SuggestedCuts)
                {
                    resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                    resultsTextBox.SelectionColor = Color.FromArgb(248, 113, 113);
                    string reason = edhrecSugg.CutReasons.TryGetValue(cut, out var cutReason) ? $" — {cutReason}" : string.Empty;
                    resultsTextBox.AppendText($"  - {cut}{reason}\n");
                }
            }
            Body(string.Empty);
        }

        // --- Deck Improvements (local analysis — specific card names from DB) ---
        var localSuggestions = enrichedSuggestions
            .Where(s => !s.Source.Equals("EDHREC", StringComparison.OrdinalIgnoreCase) && s.SuggestedAdds.Any())
            .OrderByDescending(s => s.Confidence)
            .Take(3)
            .ToList();
        if (localSuggestions.Any())
        {
            Header("Deck Improvements");
            foreach (var sugg in localSuggestions)
            {
                resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                resultsTextBox.SelectionColor = Color.FromArgb(250, 204, 21);
                resultsTextBox.AppendText($"{sugg.Title}\n");
                resultsTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Italic);
                resultsTextBox.SelectionColor = Color.FromArgb(148, 163, 184);
                resultsTextBox.AppendText($"{sugg.Details}\n");
                foreach (var add in sugg.SuggestedAdds.Take(4))
                {
                    resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                    resultsTextBox.SelectionColor = Color.FromArgb(74, 222, 128);
                    string reason = sugg.AddReasons.TryGetValue(add, out var addReason) ? $" — {addReason}" : string.Empty;
                    resultsTextBox.AppendText($"  + {add}{reason}\n");
                }
                foreach (var cut in sugg.SuggestedCuts.Take(2))
                {
                    resultsTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                    resultsTextBox.SelectionColor = Color.FromArgb(248, 113, 113);
                    string reason = sugg.CutReasons.TryGetValue(cut, out var cutReason) ? $" — {cutReason}" : string.Empty;
                    resultsTextBox.AppendText($"  - {cut}{reason}\n");
                }
                Body(string.Empty);
            }
        }

        if (results.SpellbookKnownCombos.Any())
        {
            Header("Commander Spellbook Combos");
            Body("Combos Commander Spellbook identifies as already present in the submitted list:");
            foreach (var combo in results.SpellbookKnownCombos.Take(6))
                Body($"  - {combo}");
        }

        if (results.SpellbookAlmostCombos.Any())
        {
            Header("Near Miss Combos");
            Body("Spellbook lines that are close but still missing one or more required pieces:");
            foreach (var combo in results.SpellbookAlmostCombos.Take(5))
                Body($"  - {combo}");
        }

        // --- Quick Notes (concise flagged observations only) ---
        var quickNotes = actionable
            .Where(text => text.StartsWith("🚨") || text.StartsWith("⚠️") || text.StartsWith("💡"))
            .Take(5)
            .ToList();
        if (quickNotes.Any())
        {
            Header("Quick Notes");
            foreach (var note in quickNotes)
                Body($"→ {note}");
        }

        var comboPieces = DeckAnalysis.DetectComboPieces(deck);
        if (comboPieces.Any())
        {
            Header("Combo / Win-Con Pieces");
            foreach (var (label, cards) in comboPieces)
                Body($"  {label}: {string.Join(", ", cards.Take(5))}");
        }

        if (results.ComboLines.Any())
        {
            Header("Detected Combo Wins");
            foreach (var comboLine in results.ComboLines)
                Body($"- {comboLine}");
        }

        if (results.SpellbookComboAssemblies.Any())
        {
            Header("Goldfish Combo Assemblies");
            Body($"Commander Spellbook combo assembly rate: {results.SpellbookComboAssemblyRate:F1}%");
            foreach (var comboLine in results.SpellbookComboAssemblies.Take(6))
                Body($"- {comboLine}");
        }

        if (results.SamplePlayPatterns.Any())
        {
            Header("Sample Goldfish Lines");
            foreach (var logLine in results.SamplePlayPatterns.Take(6))
                Body($"- {logLine}");
        }

    }

    private DeckArchetype? GetSelectedArchetypeOverride()
    {
        return archetypeComboBox.SelectedItem is DeckArchetype archetype ? archetype : null;
    }

    private OpponentPodProfile GetSelectedOpponentProfile()
    {
        return opponentProfileComboBox.SelectedItem is OpponentPodProfile profile
            ? profile
            : OpponentPodProfile.Focused;
    }

    private async Task<List<string>> GetEdhrecSuggestionsAsync(string commanderName, Deck deck, int maxSuggestions = 5)
    {
        return await GetEdhrecSuggestionsAsync(commanderName, deck, GetSelectedEdhrecTheme(), maxSuggestions);
    }

    private async Task<List<string>> GetEdhrecSuggestionsAsync(string commanderName, Deck deck, string selectedTheme, int maxSuggestions = 5)
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
            return ExtractEdhrecCommanderCards(document.RootElement, deck, commanderName.Trim(), maxSuggestions, selectedTheme);
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task<List<DeckSuggestion>> BuildSuggestionsWithCommanderDataAsync(Deck deck, EvaluationResults results)
    {
        return await BuildSuggestionsWithCommanderDataAsync(deck, results, commanderTextBox.Text.Trim(), GetSelectedEdhrecTheme());
    }

    private async Task<List<DeckSuggestion>> BuildSuggestionsWithCommanderDataAsync(Deck deck, EvaluationResults results, string commanderName, string selectedTheme)
    {
        var enriched = results.Suggestions
            .Select(suggestion => EnrichSuggestionFromDatabase(deck, suggestion))
            .ToList();

        if (commanderName.Length == 0)
            return SanitizeCommanderLegalSuggestions(deck, enriched);

        List<string> edhrecSuggestions = await GetEdhrecSuggestionsAsync(commanderName, deck, selectedTheme, 6);
        if (edhrecSuggestions.Any())
            await PrefetchMissingCardsAsync(edhrecSuggestions);

        edhrecSuggestions = FilterCommanderLegalCardNames(deck, edhrecSuggestions, 5);

        if (edhrecSuggestions.Any())
        {
            // Build a ranked list of cut candidates:
            //   - prioritize cards with no recognised functional role (not removal/draw/ramp/wipe/protection)
            //   - then high CMC, then alpha ordering as a stable tie-break
            var roleTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Removal", "Card Draw", "Ramp", "Board Wipe", "Protection", "Tutor", "Counter"
            };
            var cardsToCut = deck.Cards
                .Where(card => !card.IsLand && !card.IsCommander && !string.IsNullOrWhiteSpace(card.Name))
                .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(card =>
                {
                    bool hasRole = !string.IsNullOrWhiteSpace(card.Category)
                        && roleTags.Any(role => card.Category.Contains(role, StringComparison.OrdinalIgnoreCase));
                    bool hasOracleRole = roleTags.Any(role =>
                        card.OracleText?.Contains(role, StringComparison.OrdinalIgnoreCase) == true);
                    int roleScore = (hasRole || hasOracleRole) ? 1 : 0;
                    return (Card: card, RoleScore: roleScore);
                })
                .OrderBy(entry => entry.RoleScore)
                .ThenByDescending(entry => entry.Card.ManaCost)
                .ThenBy(entry => entry.Card.Name)
                .Select(entry => entry.Card.Name!.Trim())
                .Take(Math.Min(4, edhrecSuggestions.Count))
                .ToList();

            enriched.Add(new DeckSuggestion
            {
                Kind = DeckSuggestionKind.Swap,
                Title = $"Commander-tuned upgrades for {commanderName}",
                Details = "These are cards not already in the list that EDHREC associates with the chosen commander. They are useful as upgrade candidates when you want suggestions informed by common commander builds.",
                RoleTag = string.Empty,
                Source = "EDHREC",
                Confidence = 0.78,
                SuggestedAdds = edhrecSuggestions.Take(5).ToList(),
                SuggestedCuts = cardsToCut,
                AddReasons = edhrecSuggestions.Take(5).ToDictionary(
                    name => name,
                    _ => "High-synergy inclusion from EDHREC for this commander and theme.",
                    StringComparer.OrdinalIgnoreCase),
                CutReasons = cardsToCut.ToDictionary(
                    name => name,
                    _ => "Lower-priority slot identified to make room for a stronger commander-specific include.",
                    StringComparer.OrdinalIgnoreCase)
            });
        }

        return SanitizeCommanderLegalSuggestions(deck, enriched);
    }

    private List<string> ExtractEdhrecCommanderCards(JsonElement root, Deck deck, string commanderName, int maxSuggestions, string selectedTheme)
    {
        var inDeck = deck.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.Name))
            .Select(card => card.Name!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var collected = new List<(string Name, double Score)>();
        string preferredTag = NormalizeThemeTag(selectedTheme);

        if (TryGetEdhrecCardLists(root, out var cardLists))
        {
            foreach (var list in cardLists)
            {
                string header = list.Header ?? string.Empty;
                double sectionWeight = GetEdhrecSectionWeight(header, list.Tag, preferredTag);
                foreach (var card in list.Cards)
                {
                    if (string.IsNullOrWhiteSpace(card.Name)
                        || inDeck.Contains(card.Name)
                        || card.Name.Equals(commanderName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Skip cards outside the commander's color identity if we have DB data for them
                    var cardRecord = FindCardRecord(card.Name);
                    if (cardRecord != null && !deck.IsInColorIdentity(cardRecord))
                        continue;

                    double score = sectionWeight + card.Synergy + (card.Inclusion / 100000.0);
                    collected.Add((card.Name, score));
                }
            }
        }

        if (!collected.Any())
        {
            var fallback = new List<string>();
            CollectCardNamesFallback(root, fallback, inDeck, commanderName, deck);
            return fallback.Distinct(StringComparer.OrdinalIgnoreCase).Take(maxSuggestions).ToList();
        }

        return collected
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(item => item.Score)
            .Select(item => item.Name)
            .Take(maxSuggestions)
            .ToList();
    }

    private void CollectCardNamesFallback(JsonElement element, List<string> names, HashSet<string> inDeck, string commanderName, Deck deck)
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
                        var record = FindCardRecord(name);
                        if (record != null && deck.IsInColorIdentity(record))
                            names.Add(name);
                    }
                }

                foreach (var property in element.EnumerateObject())
                    CollectCardNamesFallback(property.Value, names, inDeck, commanderName, deck);
                break;

            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectCardNamesFallback(child, names, inDeck, commanderName, deck);
                break;
        }
    }

    public List<string> FindRecommendedCardsForRole(Deck deck, string roleTag, int maxResults = 5, int maxManaCost = 4)
    {
        return FindRecommendedCardsForEffect(deck, roleTag, maxResults, maxManaCost);
    }

    public List<string> FindRecommendedCardsForEffect(Deck deck, string roleTag, int maxResults = 5, int maxManaCost = 4)
    {
        var deckNames = deck.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.Name))
            .Select(card => card.Name!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return cardDatabase.Values
            .Where(record => !record.IsLand)
            .Where(record => !deckNames.Contains(record.Name))
            .Select(record => new
            {
                Record = record,
                SignatureScore = ScoreCardRecordForDeck(record, roleTag, deck)
            })
            .Where(entry => entry.SignatureScore > 0)
            .Where(entry => entry.Record.ManaCost <= maxManaCost)
            .Where(entry => deck.IsInColorIdentity(entry.Record))
            .OrderByDescending(entry => entry.SignatureScore)
            .ThenBy(entry => entry.Record.ManaCost)
            .ThenBy(entry => entry.Record.Name)
            .Select(entry => entry.Record.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    private int ScoreCardRecordForDeck(CardDbRecord record, string roleTag, Deck deck)
    {
        int score = ScoreCardRecordForEffect(record, roleTag);
        var tags = (record.Category ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var archetype = GetSelectedArchetypeOverride() ?? DeckAnalysis.DetectPrimaryArchetype(deck);

        if (tags.Contains("Other"))
            score -= 2;
        if (string.IsNullOrWhiteSpace(record.OracleText))
            score -= 1;

        // Boost score based on learned card quality from submitted decks
        if (_cardMetrics != null)
        {
            double qualityScore = _cardMetrics.GetCardQualityScore(record, archetype);
            // Scale quality score (0-100) into a modest bonus (0-10 points)
            int qualityBonus = (int)Math.Round(qualityScore / 10.0);
            score += qualityBonus;
        }

        // Smart ramp recommendations: prioritize early ramp when needed
        if (roleTag.Equals("Ramp", StringComparison.OrdinalIgnoreCase) && tags.Contains("Ramp"))
        {
            var earlyRamp = DeckAnalysis.GetEarlyRampCards(deck);
            int earlyRampCount = earlyRamp.Count;
            int targetEarlyRamp = Math.Max(2, DeckAnalysis.CountRealRamp(deck) - 2);
            
            // If we need more early ramp, heavily boost 0-2 CMC ramp cards
            if (earlyRampCount < targetEarlyRamp)
            {
                if (record.ManaCost <= 2)
                    score += 6; // High priority for actual early ramp
                else if (record.ManaCost <= 3)
                    score += 2;
            }
            
            // Check ramp type breakdown to recommend variety
            var rampBreakdown = DeckAnalysis.GetRampTypeBreakdown(deck);
            
            // If we're heavy on artifacts, boost creature ramp
            if (rampBreakdown[DeckAnalysis.RampType.ArtifactRamp] > 3 && 
                rampBreakdown[DeckAnalysis.RampType.CreatureRamp] < 2)
            {
                if (record.Type.Contains("Creature", StringComparison.OrdinalIgnoreCase))
                    score += 3;
            }
            
            // If we're heavy on creatures, boost artifact ramp for redundancy
            if (rampBreakdown[DeckAnalysis.RampType.CreatureRamp] > 3 && 
                rampBreakdown[DeckAnalysis.RampType.ArtifactRamp] < 2)
            {
                if (record.Type.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
                    score += 3;
            }
        }

        switch (archetype)
        {
            case DeckArchetype.Spellslinger:
                if (record.Type.Contains("Instant", StringComparison.OrdinalIgnoreCase) || record.Type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
                    score += 3;
                if (record.Type.Contains("Creature", StringComparison.OrdinalIgnoreCase) && !tags.Contains("Token Generation") && !tags.Contains("Protection"))
                    score -= 3;
                break;

            case DeckArchetype.Combo:
                if (tags.Contains("Tutor") || tags.Contains("Card Draw") || tags.Contains("Counterspell"))
                    score += 2;
                if (record.ManaCost >= 5 && !tags.Contains("Tutor") && !tags.Contains("Card Draw"))
                    score -= 2;
                break;

            case DeckArchetype.Tokens:
                if (tags.Contains("Token Generation") || tags.Contains("Protection"))
                    score += 3;
                break;

            case DeckArchetype.Tribal:
                if (record.Type.Contains("Creature", StringComparison.OrdinalIgnoreCase))
                    score += 2;
                break;

            case DeckArchetype.Ramp:
                if (record.ManaCost >= 4 && !roleTag.Equals("Ramp", StringComparison.OrdinalIgnoreCase))
                    score += 1;
                break;
        }

        return score;
    }

    private static int ScoreCardRecordForRole(CardDbRecord record, string roleTag)
    {
        int score = 0;
        var tags = (record.Category ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (tags.Contains(roleTag))
            score += 5;
        if (tags.Contains("Ramp") && roleTag == "Ramp" && record.ManaCost <= 2)
            score += 3;
        if (tags.Contains("Card Draw") && roleTag == "Card Draw" && record.ManaCost <= 4)
            score += 3;
        if (tags.Contains("Removal") && roleTag == "Removal" && record.ManaCost <= 3)
            score += 3;
        if (!string.IsNullOrWhiteSpace(record.OracleText))
            score += 1;

        return score;
    }

    private static int ScoreCardRecordForEffect(CardDbRecord record, string roleTag)
    {
        string oracle = (record.OracleText ?? string.Empty).ToLowerInvariant();
        string type = (record.Type ?? string.Empty).ToLowerInvariant();
        int score = ScoreCardRecordForRole(record, roleTag);

        switch (roleTag.ToLowerInvariant())
        {
            case "ramp":
                if (record.ManaCost <= 2) score += 4;
                if (oracle.Contains("add {") || oracle.Contains("search your library for a basic land") || oracle.Contains("search your library for a forest")) score += 5;
                if (type.Contains("artifact")) score += 2;
                break;

            case "card draw":
                if (oracle.Contains("draw") && oracle.Contains("card")) score += 5;
                if (oracle.Contains("whenever") && oracle.Contains("draw")) score += 4;
                if (type.Contains("enchantment") || type.Contains("creature")) score += 2;
                break;

            case "removal":
                if (oracle.Contains("destroy target") || oracle.Contains("exile target") || oracle.Contains("target creature gets")) score += 6;
                if (record.ManaCost <= 3) score += 3;
                if (type.Contains("instant")) score += 2;
                break;

            case "board wipe":
                if (oracle.Contains("destroy all") || oracle.Contains("exile all") || oracle.Contains("each creature")) score += 7;
                break;

            case "counterspell":
                if (oracle.Contains("counter target spell")) score += 7;
                if (record.ManaCost <= 3) score += 2;
                break;

            case "token generation":
                if (oracle.Contains("create") && (oracle.Contains("token") || oracle.Contains("tokens"))) score += 6;
                if (oracle.Contains("whenever") && oracle.Contains("create")) score += 2;
                break;
        }

        return score;
    }

    private static double GetEdhrecSectionWeight(string header, string tag, string preferredTag)
    {
        string normalizedHeader = (header ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedTag = (tag ?? string.Empty).Trim().ToLowerInvariant();

        double bonus = 0;
        if (!string.IsNullOrWhiteSpace(preferredTag))
        {
            if (normalizedTag.Contains(preferredTag))
                bonus += 0.45;
            if (normalizedHeader.Contains(preferredTag.Replace("-", " ")))
                bonus += 0.25;
        }

        if (normalizedHeader.Contains("high synergy")) return 1.0 + bonus;
        if (normalizedHeader.Contains("game changer")) return 0.95 + bonus;
        if (normalizedHeader.Contains("top cards")) return 0.85 + bonus;
        if (normalizedTag.Contains("highsynergy")) return 1.0 + bonus;
        if (normalizedTag.Contains("topcards")) return 0.85 + bonus;
        if (normalizedTag.Contains("gamechangers")) return 0.95 + bonus;
        if (normalizedTag.Contains("instants") || normalizedTag.Contains("sorceries") || normalizedTag.Contains("enchantments") || normalizedTag.Contains("creatures") || normalizedTag.Contains("planeswalkers")) return 0.65 + bonus;
        return 0.5 + bonus;
    }

    private static bool TryGetEdhrecCardLists(JsonElement root, out List<(string Header, string Tag, List<(string Name, double Synergy, int Inclusion)> Cards)> cardLists)
    {
        cardLists = new List<(string Header, string Tag, List<(string Name, double Synergy, int Inclusion)> Cards)>();

        if (!root.TryGetProperty("container", out var container)
            || !container.TryGetProperty("json_dict", out var jsonDict)
            || !jsonDict.TryGetProperty("cardlists", out var lists)
            || lists.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var list in lists.EnumerateArray())
        {
            string header = list.TryGetProperty("header", out var headerProp) ? headerProp.GetString() ?? string.Empty : string.Empty;
            string tag = list.TryGetProperty("tag", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
            var cards = new List<(string Name, double Synergy, int Inclusion)>();

            if (list.TryGetProperty("cardviews", out var cardViews) && cardViews.ValueKind == JsonValueKind.Array)
            {
                foreach (var card in cardViews.EnumerateArray())
                {
                    string name = card.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
                    double synergy = card.TryGetProperty("synergy", out var synergyProp) && synergyProp.ValueKind == JsonValueKind.Number
                        ? synergyProp.GetDouble()
                        : 0;
                    int inclusion = card.TryGetProperty("inclusion", out var inclusionProp) && inclusionProp.ValueKind == JsonValueKind.Number
                        ? inclusionProp.GetInt32()
                        : 0;

                    if (!string.IsNullOrWhiteSpace(name))
                        cards.Add((name, synergy, inclusion));
                }
            }

            if (cards.Any())
                cardLists.Add((header, tag, cards));
        }

        return cardLists.Any();
    }

    private DeckSuggestion EnrichSuggestionFromDatabase(Deck deck, DeckSuggestion suggestion)
    {
        var enriched = new DeckSuggestion
        {
            Kind = suggestion.Kind,
            Title = suggestion.Title,
            Details = suggestion.Details,
            RoleTag = suggestion.RoleTag,
            Source = string.IsNullOrWhiteSpace(suggestion.Source) ? "Simulator" : suggestion.Source,
            Confidence = suggestion.Confidence,
            SuggestedCuts = suggestion.SuggestedCuts.ToList(),
            SuggestedAdds = suggestion.SuggestedAdds.ToList(),
            AddReasons = new Dictionary<string, string>(suggestion.AddReasons, StringComparer.OrdinalIgnoreCase),
            CutReasons = new Dictionary<string, string>(suggestion.CutReasons, StringComparer.OrdinalIgnoreCase)
        };

        if (!string.IsNullOrWhiteSpace(enriched.RoleTag))
        {
            if (enriched.RoleTag.Equals("Land", StringComparison.OrdinalIgnoreCase))
            {
                var landAdds = LandRecommendationEngine.RecommendAdds(deck, 5);
                foreach (var land in landAdds)
                {
                    if (!enriched.SuggestedAdds.Any(name => name.Equals(land.Name, StringComparison.OrdinalIgnoreCase)))
                        enriched.SuggestedAdds.Add(land.Name);
                    enriched.AddReasons[land.Name] = land.Reason;
                }
            }
            else
            {
                int manaCap = enriched.RoleTag.Equals("Ramp", StringComparison.OrdinalIgnoreCase) ? 3 : 4;
                var databaseAdds = FindRecommendedCardsForEffect(deck, enriched.RoleTag, 5, manaCap);
                foreach (var candidate in databaseAdds)
                {
                    if (!enriched.SuggestedAdds.Any(name => name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                        enriched.SuggestedAdds.Add(candidate);
                    if (!enriched.AddReasons.ContainsKey(candidate))
                        enriched.AddReasons[candidate] = $"Fits the {enriched.RoleTag} role at a mana value appropriate for this deck and stays inside your commander's colors.";
                }
            }

            enriched.SuggestedAdds = enriched.SuggestedAdds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
        }

        enriched.SuggestedAdds = FilterCommanderLegalCardNames(deck, enriched.SuggestedAdds, 5);
        enriched.AddReasons = enriched.AddReasons
            .Where(entry => enriched.SuggestedAdds.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var cut in enriched.SuggestedCuts)
        {
            if (!enriched.CutReasons.ContainsKey(cut))
                enriched.CutReasons[cut] = "Higher-cost or lower-priority slot that is easier to trim without weakening core deck functions.";
        }

        return enriched;
    }

    private List<string> FilterCommanderLegalCardNames(Deck deck, IEnumerable<string> names, int maxResults)
    {
        return names
            .Select(NormalizeDeckCardName)
            .Where(name => name.Length > 0)
            .Where(name =>
            {
                var record = FindCardRecord(name);
                return record != null && deck.IsInColorIdentity(record);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    private List<DeckSuggestion> SanitizeCommanderLegalSuggestions(Deck deck, IEnumerable<DeckSuggestion> suggestions)
    {
        return suggestions
            .Select(suggestion =>
            {
                suggestion.SuggestedAdds = FilterCommanderLegalCardNames(deck, suggestion.SuggestedAdds, 5);
                suggestion.AddReasons = suggestion.AddReasons
                    .Where(entry => suggestion.SuggestedAdds.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
                return suggestion;
            })
            .ToList();
    }

    private async Task RefreshEdhrecThemesAsync()
    {
        string commanderName = commanderTextBox.Text.Trim();
        edhrecThemeComboBox.Items.Clear();
        edhrecThemeComboBox.Items.Add("Auto Theme");

        if (commanderName.Length == 0)
        {
            edhrecThemeComboBox.SelectedIndex = 0;
            return;
        }

        var themes = await GetEdhrecThemesAsync(commanderName);
        foreach (var theme in themes)
            edhrecThemeComboBox.Items.Add(theme);

        edhrecThemeComboBox.SelectedIndex = 0;
    }

    private async Task<List<string>> GetEdhrecThemesAsync(string commanderName)
    {
        string slug = Regex.Replace(commanderName.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0)
            return new List<string>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://json.edhrec.com/pages/commanders/{slug}.json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            string json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            return ExtractEdhrecThemes(document.RootElement);
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> ExtractEdhrecThemes(JsonElement root)
    {
        if (!root.TryGetProperty("taglinks", out var tagLinks) || tagLinks.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return tagLinks.EnumerateArray()
            .Select(tag => new
            {
                Name = tag.TryGetProperty("value", out var valueProp) ? valueProp.GetString() ?? string.Empty : string.Empty,
                Count = tag.TryGetProperty("count", out var countProp) && countProp.ValueKind == JsonValueKind.Number ? countProp.GetInt32() : 0
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
            .OrderByDescending(tag => tag.Count)
            .Take(12)
            .Select(tag => tag.Name)
            .ToList();
    }

    private string GetSelectedEdhrecTheme()
    {
        if (edhrecThemeComboBox.SelectedItem == null)
            return string.Empty;

        string selected = edhrecThemeComboBox.SelectedItem.ToString() ?? string.Empty;
        return selected.Equals("Auto Theme", StringComparison.OrdinalIgnoreCase) ? string.Empty : selected;
    }

    private static string NormalizeThemeTag(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
            return string.Empty;

        string normalized = theme.Trim().ToLowerInvariant();
        normalized = normalized.Replace("+", "plus ");
        normalized = Regex.Replace(normalized, "[^a-z0-9]+", "-");
        return normalized.Trim('-');
    }

    private async Task<Deck> ParseDeck(string deckText, string commanderText)
    {
        var deck = new Deck();
        string commanderName = string.Empty;
        Task<(int cost, List<string> colors, List<string> colorIdentity, string type, string category, string oracleText)?>? commanderTask = null;
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
            if (ShouldIgnoreDeckLine(line))
                continue;

            if (!TryParseDeckEntry(line, out string parsedName, out int quantity))
                throw new FormatException($"Invalid line: {line.Trim()}");

            cardNames.Add(parsedName);
            quantities.Add(quantity);
        }

        var cardDataTasks = cardNames.Select(GetCardData).ToArray();
        var cardData = await Task.WhenAll(cardDataTasks);
        var commanderData = commanderTask != null ? await commanderTask : null;

        if (!string.IsNullOrWhiteSpace(commanderText))
        {
            int manaCost = 0;
            var colors = new List<string>();
            var colorIdentity = new List<string>();
            string type = string.Empty;
            string category = "Commander";
            string oracleText = string.Empty;
            bool isLand = false;

            if (commanderData.HasValue)
            {
                manaCost = commanderData.Value.cost;
                colors = commanderData.Value.colors;
                colorIdentity = commanderData.Value.colorIdentity;
                type = commanderData.Value.type;
                oracleText = commanderData.Value.oracleText;
                isLand = FindCardRecord(commanderName)?.IsLand ?? IsLandByTypeLine(type);
            }

            var commanderCard = new Card
            {
                Name = commanderName,
                IsLand = isLand,
                IsCommander = true,
                ManaCost = manaCost,
                Colors = colors,
                ColorIdentity = colorIdentity.Count > 0 ? colorIdentity : colors,
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
            var colorIdentity = new List<string>();
            string type = string.Empty;
            string category = string.Empty;
            string oracleText = string.Empty;
            bool isLand = false;

            if (cardEntry.HasValue)
            {
                manaCost = cardEntry.Value.cost;
                colors = cardEntry.Value.colors;
                colorIdentity = cardEntry.Value.colorIdentity;
                type = cardEntry.Value.type;
                category = cardEntry.Value.category;
                oracleText = cardEntry.Value.oracleText;
                isLand = FindCardRecord(cardName)?.IsLand ?? IsLandByTypeLine(type);
            }
            else
            {
                manaCost = 3;
                category = "Unknown";
                isLand = false;
            }

            for (int copy = 0; copy < quantity; copy++)
            {
                var card = new Card
                {
                    Name = cardName,
                    IsLand = isLand,
                    ManaCost = manaCost,
                    Colors = colors,
                    ColorIdentity = colorIdentity.Count > 0 ? colorIdentity : colors,
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

    private Task<(int cost, List<string> colors, List<string> colorIdentity, string type, string category, string oracleText)?> GetCardData(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<(int, List<string>, List<string>, string, string, string)?>(null);

        var record = FindCardRecord(name);
        if (record != null)
        {
            // Use stored ColorIdentity if available; fall back to Colors
            var identity = record.ColorIdentity.Count > 0 ? record.ColorIdentity : record.Colors;
            return Task.FromResult<(int, List<string>, List<string>, string, string, string)?>((record.ManaCost, record.Colors, identity, record.Type, record.Category, record.OracleText));
        }

        return Task.FromResult<(int, List<string>, List<string>, string, string, string)?>(null);
    }

    public async Task<EvaluationResults> RunHeadlessTrainingAsync(
        string deckText,
        string commanderText,
        int simulationsPerBatch,
        int maxTurns,
        int batches,
        bool onDraw,
        Action<string>? log = null,
        bool isCedh = false,
        OpponentPodProfile opponentProfile = OpponentPodProfile.Focused,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deckText))
            throw new InvalidOperationException("Deck text is required for training.");

        if (simulationsPerBatch <= 0)
            throw new InvalidOperationException("Simulations per batch must be greater than 0.");

        if (batches <= 0)
            throw new InvalidOperationException("Batches must be greater than 0.");

        var rawInputLines = deckText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var prefetchNames = rawInputLines
            .Select(line => TryParseDeckEntry(line, out string parsedName, out _) ? parsedName : string.Empty)
            .Where(name => name.Length > 0)
            .ToList();
        if (!string.IsNullOrWhiteSpace(commanderText))
            prefetchNames.Add(NormalizeDeckCardName(commanderText));

        log?.Invoke($"Prefetching data for {prefetchNames.Count} deck entries...");
        await PrefetchMissingCardsAsync(prefetchNames);
        cancellationToken.ThrowIfCancellationRequested();

        Deck deck = await ParseDeck(deckText, commanderText);
        if (deck.Cards.Count == 0)
            throw new InvalidOperationException("No cards found after parsing deck text.");
        var legalityErrors = await ValidateCommanderDeckLegalityAsync(deck, cancellationToken);
        if (legalityErrors.Count > 0)
            throw new InvalidOperationException("Deck legality check failed: " + string.Join(" | ", legalityErrors.Take(6)));

        log?.Invoke("Checking Commander Spellbook combos...");
        await AttachSpellbookReportAsync(deck, cancellationToken);

        EvaluationResults? latest = null;
        for (int batch = 1; batch <= batches; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evaluator = new DeckEvaluator(deck, GetSelectedArchetypeOverride(), isCedh, opponentProfile);
            latest = await Task.Run(() => evaluator.RunSimulations(simulationsPerBatch, maxTurns, onDraw, cancellationToken: cancellationToken), cancellationToken);

            log?.Invoke(
                $"Batch {batch}/{batches} complete | Idle {latest.AverageIdleTurns:F2} | ManaEff {latest.AverageManaEfficiency:F1}% | CmdrCast {latest.CommanderCastRate:F1}% | LearnedGames {latest.LearningGamesSeen}");
        }

        return latest ?? throw new InvalidOperationException("Training run did not produce results.");
    }

    private async Task<List<string>> ValidateCommanderDeckLegalityAsync(Deck deck, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (deck.Commander == null || string.IsNullOrWhiteSpace(deck.Commander.Name))
            errors.Add("Commander is required.");

        int nonCommanderCount = deck.Cards.Count(card => !card.IsCommander);
        if (nonCommanderCount != 99)
            errors.Add($"Commander format expects exactly 99 cards in the decklist (found {nonCommanderCount}).");

        var copyGroups = deck.Cards
            .Where(card => !card.IsCommander && !string.IsNullOrWhiteSpace(card.Name))
            .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        int duplicateIssues = 0;
        foreach (var group in copyGroups)
        {
            var exemplar = group.First();
            if (IsBasicLandCard(exemplar) || AllowsUnlimitedCopies(exemplar))
                continue;

            duplicateIssues++;
            if (duplicateIssues <= 6)
                errors.Add($"{group.Key} has {group.Count()} copies (Commander allows only 1 unless a card says otherwise).");
        }

        if (duplicateIssues > 6)
            errors.Add($"{duplicateIssues - 6} additional duplicate-card issues not shown.");

        if (deck.Commander != null)
        {
            var commanderIdentity = deck.GetCommanderColorIdentity();
            if (commanderIdentity.Count > 0)
            {
                var illegalGroups = deck.Cards
                    .Where(card => !card.IsCommander)
                    .Where(card => !IsCardWithinCommanderIdentity(card, commanderIdentity))
                    .GroupBy(card => card.Name ?? "Unknown Card", StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Key)
                    .OrderBy(name => name)
                    .ToList();

                foreach (string name in illegalGroups.Take(8))
                    errors.Add($"{name} is outside the commander's color identity.");

                if (illegalGroups.Count > 8)
                    errors.Add($"{illegalGroups.Count - 8} additional color identity violations not shown.");
            }
        }

        // Commander banlist / legality lookup via Scryfall (cached per app session).
        var namesToCheck = deck.Cards
            .Where(card => !card.IsCommander && !string.IsNullOrWhiteSpace(card.Name))
            .Select(card => card.Name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var illegalByFormat = await LookupCommanderIllegalCardsAsync(namesToCheck, cancellationToken);
        foreach (var issue in illegalByFormat.Take(8))
            errors.Add(issue);
        if (illegalByFormat.Count > 8)
            errors.Add($"{illegalByFormat.Count - 8} additional Commander format legality issues not shown.");

        return errors;
    }

    private async Task<List<string>> LookupCommanderIllegalCardsAsync(IEnumerable<string> cardNames, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var uniqueNames = cardNames
            .Select(NormalizeDeckCardName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unresolved = new List<string>();
        foreach (var name in uniqueNames)
        {
            string key = BuildLookupKey(name);
            if (key.Length > 0 && commanderLegalityCache.TryGetValue(key, out var cachedStatus))
            {
                cachedStatus ??= "unknown";
                if (!IsCommanderLegalStatus(cachedStatus))
                    issues.Add(BuildCommanderLegalityError(name, cachedStatus));
            }
            else
            {
                unresolved.Add(name);
            }
        }

        if (!unresolved.Any())
            return issues;

        const int maxIdentifiersPerRequest = 75;
        for (int index = 0; index < unresolved.Count; index += maxIdentifiersPerRequest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = unresolved.Skip(index).Take(maxIdentifiersPerRequest).ToList();
            var fetched = await FetchCommanderLegalityChunkAsync(chunk);

            foreach (var name in chunk)
            {
                string key = BuildLookupKey(name);
                string status = fetched.TryGetValue(name, out var foundStatus) ? (foundStatus ?? "unknown") : "unknown";
                if (key.Length > 0)
                    commanderLegalityCache[key] = status;

                if (!IsCommanderLegalStatus(status))
                    issues.Add(BuildCommanderLegalityError(name, status));
            }
        }

        return issues;
    }

    private async Task<Dictionary<string, string>> FetchCommanderLegalityChunkAsync(List<string> names)
    {
        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
            return statuses;

        try
        {
            using var payload = new StringContent(JsonSerializer.Serialize(new
            {
                identifiers = names.Select(name => new { name }).ToList()
            }), System.Text.Encoding.UTF8, "application/json");

            using var response = await Http.PostAsync("https://api.scryfall.com/cards/collection", payload);
            if (!response.IsSuccessStatusCode)
                return statuses;

            string json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
                return statuses;

            foreach (var cardElement in dataElement.EnumerateArray())
            {
                if (!cardElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                    continue;

                string fetchedName = NormalizeDeckCardName(nameElement.GetString());
                if (fetchedName.Length == 0)
                    continue;

                string status = ReadCommanderLegalityStatus(cardElement);
                statuses[fetchedName] = status;
            }
        }
        catch
        {
            // Leave statuses unresolved; callers will treat unknown conservatively.
        }

        return statuses;
    }

    private static string ReadCommanderLegalityStatus(JsonElement cardElement)
    {
        if (cardElement.TryGetProperty("legalities", out var legalities)
            && legalities.ValueKind == JsonValueKind.Object
            && legalities.TryGetProperty("commander", out var commanderStatus)
            && commanderStatus.ValueKind == JsonValueKind.String)
        {
            return commanderStatus.GetString()?.Trim().ToLowerInvariant() ?? "unknown";
        }

        return "unknown";
    }

    private static bool IsCommanderLegalStatus(string status)
    {
        return status.Equals("legal", StringComparison.OrdinalIgnoreCase)
            || status.Equals("unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommanderLegalityError(string cardName, string status)
    {
        return status switch
        {
            "banned" => $"{cardName} is banned in Commander.",
            "not_legal" => $"{cardName} is not legal in Commander.",
            "restricted" => $"{cardName} is restricted and not legal for normal Commander deck construction.",
            _ => $"{cardName} has an unknown Commander legality status."
        };
    }

    private static bool IsBasicLandCard(Card card)
    {
        if (!card.IsLand)
            return false;

        var typeTokens = DeckAnalysis.ExtractTypeTokens(card.Type);
        if (typeTokens.Contains("Basic", StringComparer.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(card.Name))
            return false;

        return card.Name.Equals("Plains", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Island", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Swamp", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Mountain", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Forest", StringComparison.OrdinalIgnoreCase)
            || card.Name.Equals("Wastes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsUnlimitedCopies(Card card)
    {
        if (string.IsNullOrWhiteSpace(card.OracleText))
            return false;

        string oracle = card.OracleText.ToLowerInvariant();
        return oracle.Contains("a deck can have any number of cards named")
            || oracle.Contains("a deck can have any number of cards with the same name as this card");
    }

    private static bool IsCardWithinCommanderIdentity(Card card, HashSet<string> commanderIdentity)
    {
        var cardIdentity = card.ColorIdentity.Count > 0 ? card.ColorIdentity : card.Colors;
        if (cardIdentity.Count == 0)
            return true;

        return cardIdentity.All(color => commanderIdentity.Contains(color));
    }

    private static bool ShouldIgnoreDeckLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        string trimmed = line.Trim();
        if (trimmed.Length == 0)
            return true;
        if (trimmed.StartsWith("#") || trimmed.StartsWith("//"))
            return true;

        string lowered = trimmed.ToLowerInvariant();
        return lowered is "commander" or "commander:" or "deck" or "deck:" or "mainboard" or "mainboard:" or "sideboard" or "sideboard:" or "maybeboard" or "maybeboard:" or "considering" or "considering:";
    }

    private static bool TryParseDeckEntry(string line, out string cardName, out int quantity)
    {
        cardName = string.Empty;
        quantity = 0;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string trimmed = line.Trim();
        if (ShouldIgnoreDeckLine(trimmed))
            return false;

        var leading = Regex.Match(trimmed, @"^(?<qty>\d+)\s*(x)?\s+(?<name>.+)$", RegexOptions.IgnoreCase);
        if (leading.Success)
        {
            quantity = Math.Max(1, int.Parse(leading.Groups["qty"].Value));
            cardName = NormalizeDeckCardName(leading.Groups["name"].Value);
            return cardName.Length > 0;
        }

        var trailing = Regex.Match(trimmed, @"^(?<name>.+?)\s+[xX](?<qty>\d+)$", RegexOptions.IgnoreCase);
        if (trailing.Success)
        {
            quantity = Math.Max(1, int.Parse(trailing.Groups["qty"].Value));
            cardName = NormalizeDeckCardName(trailing.Groups["name"].Value);
            return cardName.Length > 0;
        }

        string inferred = NormalizeDeckCardName(trimmed);
        if (inferred.Length == 0)
            return false;

        quantity = 1;
        cardName = inferred;
        return true;
    }
}
