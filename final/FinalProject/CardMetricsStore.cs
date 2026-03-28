using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

#nullable enable

/// <summary>
/// Tracks card usage patterns across submitted decks to identify good cards.
/// Learns which cards appear in successful decks, what archetypes they fit into,
/// and uses frequency + context to score card quality.
/// </summary>
internal class CardMetricsStore
{
    private const string MetricsFileName = "card_metrics.json";
    private readonly string _metricsPath;

    public Dictionary<string, CardMetrics> Metrics { get; } = new Dictionary<string, CardMetrics>(StringComparer.OrdinalIgnoreCase);

    public CardMetricsStore(string storageDirectory)
    {
        _metricsPath = Path.Combine(storageDirectory, MetricsFileName);
        LoadMetrics();
    }

    /// <summary>Records a card's usage in a deck during evaluation.</summary>
    public void RecordCardInDeck(Card card, Deck deck, DeckArchetype archetype)
    {
        if (card == null || card.IsCommander || card.IsLand)
            return;

        string name = card.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (!Metrics.ContainsKey(name))
            Metrics[name] = new CardMetrics { Name = name };

        var metrics = Metrics[name];
        metrics.TimesSeenCount++;

        // Record what archetype this card appears in
        if (!metrics.ArchetypeAppearances.ContainsKey(archetype))
            metrics.ArchetypeAppearances[archetype] = 0;
        metrics.ArchetypeAppearances[archetype]++;

        // Record color identity compatibility
        var commanderIdentity = deck.GetCommanderColorIdentity();
        if (card.ColorIdentity.Count > 0)
        {
            var cardIdentity = card.ColorIdentity;
            bool isLegal = cardIdentity.All(c => commanderIdentity.Contains(c));
            if (isLegal)
                metrics.TimesInLegalColorIdentity++;
        }

        // Record card types it appears with
        var tags = DeckAnalysis.GetCategoryTags(card);
        foreach (var tag in tags)
        {
            if (!metrics.RoleTagAppearances.ContainsKey(tag))
                metrics.RoleTagAppearances[tag] = 0;
            metrics.RoleTagAppearances[tag]++;
        }

        metrics.LastSeenDeckDateTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates a quality score for a card based on:
    /// - Frequency of appearance
    /// - Fit within specific archetypes
    /// - Color identity compatibility
    /// Higher score = better card for inclusion in decks
    /// </summary>
    public double GetCardQualityScore(string cardName, DeckArchetype targetArchetype)
    {
        if (!Metrics.TryGetValue(cardName, out var metrics))
            return 0.0; // Unknown card

        if (metrics.TimesSeenCount == 0)
            return 0.0;

        double score = 0.0;

        // Base frequency score (appears in more decks = better)
        // Scale: 0-1 appearances, 1-5 appearances, 5+ appearances
        score += Math.Min(metrics.TimesSeenCount / 20.0, 1.0) * 30.0;

        // Archetype fit (bonus if this card appears in the target archetype)
        if (metrics.ArchetypeAppearances.TryGetValue(targetArchetype, out int archetypeCount))
        {
            double archetypeFit = archetypeCount / Math.Max(1.0, metrics.TimesSeenCount);
            score += archetypeFit * 40.0; // 0-40 point archetype bonus
        }

        // Color identity legality ratio (consistently in valid decks)
        if (metrics.TimesSeenCount > 0)
        {
            double legalRatio = metrics.TimesInLegalColorIdentity / Math.Max(1.0, metrics.TimesSeenCount);
            score += legalRatio * 20.0; // 0-20 point legality bonus
        }

        // Role diversity (appears in many different roles = flexible)
        double roleVariety = Math.Min(metrics.RoleTagAppearances.Count / 5.0, 1.0);
        score += roleVariety * 10.0; // 0-10 point diversity bonus

        return score;
    }

    /// <summary>Gets the quality score for a CardDbRecord in a given archetype.</summary>
    public double GetCardQualityScore(CardDbRecord record, DeckArchetype targetArchetype)
    {
        return GetCardQualityScore(record.Name, targetArchetype);
    }

    /// <summary>Returns the top cards for a given archetype based on learned metrics.</summary>
    public List<(string CardName, double Score)> GetTopCardsForArchetype(DeckArchetype archetype, int count = 10)
    {
        return Metrics
            .Select(kvp => (CardName: kvp.Key, Score: GetCardQualityScore(kvp.Key, archetype)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .Take(count)
            .ToList();
    }

    /// <summary>Saves learned metrics to persistent storage.</summary>
    public void SaveMetrics()
    {
        try
        {
            var json = JsonSerializer.Serialize(Metrics.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metricsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save card metrics: {ex.Message}");
        }
    }

    /// <summary>Loads learned metrics from persistent storage.</summary>
    private void LoadMetrics()
    {
        try
        {
            if (!File.Exists(_metricsPath))
                return;

            string json = File.ReadAllText(_metricsPath);
            var loaded = JsonSerializer.Deserialize<List<CardMetrics>>(json) ?? new List<CardMetrics>();
            
            Metrics.Clear();
            foreach (var metric in loaded)
            {
                if (metric != null && !string.IsNullOrEmpty(metric.Name))
                {
                    Metrics[metric.Name] = metric;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load card metrics: {ex.Message}");
        }
    }

    /// <summary>Clears all learned metrics (for testing/reset).</summary>
    public void ClearMetrics()
    {
        Metrics.Clear();
        try
        {
            if (File.Exists(_metricsPath))
                File.Delete(_metricsPath);
        }
        catch { }
    }
}

/// <summary>Aggregated metrics for a single card across all decks it's appeared in.</summary>
internal class CardMetrics
{
    public string Name { get; set; } = string.Empty;
    public int TimesSeenCount { get; set; } = 0;
    public int TimesInLegalColorIdentity { get; set; } = 0;
    public DateTime LastSeenDeckDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>How many times this card appeared in each archetype.</summary>
    public Dictionary<DeckArchetype, int> ArchetypeAppearances { get; set; } = new Dictionary<DeckArchetype, int>();

    /// <summary>How many times this card appeared with each role/category tag.</summary>
    public Dictionary<string, int> RoleTagAppearances { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
