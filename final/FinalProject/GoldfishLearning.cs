using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#nullable enable

internal sealed class GoldfishLearningProfile
{
    public string DeckKey { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public double CommanderBias { get; set; }
    public Dictionary<string, double> TagBiases { get; set; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
}

internal static class GoldfishLearningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "goldfish_learning.json");
    private static readonly Dictionary<string, GoldfishLearningProfile> Cache = LoadAll();

    public static GoldfishLearningProfile LoadProfile(Deck deck)
    {
        string deckKey = ComputeDeckKey(deck);
        if (!Cache.TryGetValue(deckKey, out var profile))
        {
            profile = new GoldfishLearningProfile { DeckKey = deckKey };
            Cache[deckKey] = profile;
        }

        return profile;
    }

    public static void SaveProfile(GoldfishLearningProfile profile)
    {
        Cache[profile.DeckKey] = profile;
        string json = JsonSerializer.Serialize(Cache.Values.OrderBy(value => value.DeckKey).ToList(), JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    private static Dictionary<string, GoldfishLearningProfile> LoadAll()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new Dictionary<string, GoldfishLearningProfile>(StringComparer.OrdinalIgnoreCase);

            string json = File.ReadAllText(FilePath);
            var profiles = JsonSerializer.Deserialize<List<GoldfishLearningProfile>>(json, JsonOptions) ?? new List<GoldfishLearningProfile>();
            return profiles.ToDictionary(profile => profile.DeckKey, profile => profile, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, GoldfishLearningProfile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string ComputeDeckKey(Deck deck)
    {
        string commander = deck.Commander?.Name?.Trim() ?? "no-commander";
        string cardSignature = string.Join("|", deck.Cards
            .Where(card => !card.IsCommander)
            .GroupBy(card => card.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}"));

        string raw = commander + "||" + cardSignature;
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}