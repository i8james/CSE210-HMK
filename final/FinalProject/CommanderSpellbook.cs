using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

internal sealed class SpellbookComboUse
{
    public string CardName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public List<string> ZoneLocations { get; init; } = new List<string>();
}

internal sealed class SpellbookComboVariant
{
    public string Id { get; init; } = string.Empty;
    public string Identity { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ManaNeeded { get; init; } = string.Empty;
    public string EasyPrerequisites { get; init; } = string.Empty;
    public string NotablePrerequisites { get; init; } = string.Empty;
    public string BracketTag { get; init; } = string.Empty;
    public List<SpellbookComboUse> Uses { get; init; } = new List<SpellbookComboUse>();
    public List<string> Produces { get; init; } = new List<string>();

    public string Summary
    {
        get
        {
            string uses = string.Join(" + ", Uses.Select(use => use.Quantity > 1 ? $"{use.Quantity}x {use.CardName}" : use.CardName));
            string payoff = Produces.FirstOrDefault() ?? "combo payoff";
            return uses.Length > 0 ? $"{uses} -> {payoff}" : payoff;
        }
    }

    public bool IsInfiniteLike()
    {
        if (Produces.Any(feature => feature.Contains("Infinite", StringComparison.OrdinalIgnoreCase)))
            return true;

        return Description.Contains("repeat", StringComparison.OrdinalIgnoreCase)
            || Description.Contains("infinite", StringComparison.OrdinalIgnoreCase)
            || Produces.Any(feature => feature.Contains("win", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class SpellbookComboReport
{
    public string Identity { get; init; } = string.Empty;
    public List<SpellbookComboVariant> Included { get; init; } = new List<SpellbookComboVariant>();
    public List<SpellbookComboVariant> AlmostIncluded { get; init; } = new List<SpellbookComboVariant>();
}

internal static class CommanderSpellbookService
{
    private static readonly HttpClient Http = new HttpClient();
    private static readonly Dictionary<string, SpellbookComboReport> Cache = new Dictionary<string, SpellbookComboReport>(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<SpellbookComboReport> GetDeckReportAsync(Deck deck, CancellationToken cancellationToken = default)
    {
        string key = ComputeDeckKey(deck);
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var payload = new
        {
            main = deck.Cards
                .Where(card => !card.IsCommander && !string.IsNullOrWhiteSpace(card.Name))
                .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key)
                .Select(group => new { card = group.Key, quantity = group.Count() })
                .ToList(),
            commanders = deck.Commander != null && !string.IsNullOrWhiteSpace(deck.Commander.Name)
                ? new[] { new { card = deck.Commander.Name!.Trim(), quantity = 1 } }
                : Array.Empty<object>()
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync("https://backend.commanderspellbook.com/find-my-combos", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var report = ParseReport(document.RootElement);
        Cache[key] = report;
        return report;
    }

    private static SpellbookComboReport ParseReport(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results))
            return new SpellbookComboReport();

        return new SpellbookComboReport
        {
            Identity = results.TryGetProperty("identity", out var identityProp) ? identityProp.GetString() ?? string.Empty : string.Empty,
            Included = ParseVariants(results, "included"),
            AlmostIncluded = ParseVariants(results, "almostIncluded")
        };
    }

    private static List<SpellbookComboVariant> ParseVariants(JsonElement results, string propertyName)
    {
        if (!results.TryGetProperty(propertyName, out var variantsElement) || variantsElement.ValueKind != JsonValueKind.Array)
            return new List<SpellbookComboVariant>();

        var variants = new List<SpellbookComboVariant>();
        foreach (var variant in variantsElement.EnumerateArray())
        {
            var uses = new List<SpellbookComboUse>();
            if (variant.TryGetProperty("uses", out var usesElement) && usesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var use in usesElement.EnumerateArray())
                {
                    string cardName = string.Empty;
                    if (use.TryGetProperty("card", out var cardElement)
                        && cardElement.ValueKind == JsonValueKind.Object
                        && cardElement.TryGetProperty("name", out var nameElement))
                    {
                        cardName = nameElement.GetString() ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(cardName))
                        continue;

                    var zones = new List<string>();
                    if (use.TryGetProperty("zoneLocations", out var zonesElement) && zonesElement.ValueKind == JsonValueKind.Array)
                    {
                        zones.AddRange(zonesElement.EnumerateArray()
                            .Select(zone => zone.GetString() ?? string.Empty)
                            .Where(zone => zone.Length > 0));
                    }

                    uses.Add(new SpellbookComboUse
                    {
                        CardName = cardName,
                        Quantity = use.TryGetProperty("quantity", out var quantityElement) && quantityElement.ValueKind == JsonValueKind.Number
                            ? Math.Max(1, quantityElement.GetInt32())
                            : 1,
                        ZoneLocations = zones
                    });
                }
            }

            var produces = new List<string>();
            if (variant.TryGetProperty("produces", out var producesElement) && producesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var produced in producesElement.EnumerateArray())
                {
                    if (produced.TryGetProperty("feature", out var featureElement)
                        && featureElement.ValueKind == JsonValueKind.Object
                        && featureElement.TryGetProperty("name", out var featureName))
                    {
                        string? name = featureName.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            produces.Add(name);
                    }
                }
            }

            variants.Add(new SpellbookComboVariant
            {
                Id = variant.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty,
                Identity = variant.TryGetProperty("identity", out var identityElement) ? identityElement.GetString() ?? string.Empty : string.Empty,
                Description = variant.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() ?? string.Empty : string.Empty,
                ManaNeeded = variant.TryGetProperty("manaNeeded", out var manaElement) ? manaElement.GetString() ?? string.Empty : string.Empty,
                EasyPrerequisites = variant.TryGetProperty("easyPrerequisites", out var easyElement) ? easyElement.GetString() ?? string.Empty : string.Empty,
                NotablePrerequisites = variant.TryGetProperty("notablePrerequisites", out var notableElement) ? notableElement.GetString() ?? string.Empty : string.Empty,
                BracketTag = variant.TryGetProperty("bracketTag", out var bracketElement) ? bracketElement.GetString() ?? string.Empty : string.Empty,
                Uses = uses,
                Produces = produces
            });
        }

        return variants;
    }

    private static string ComputeDeckKey(Deck deck)
    {
        string commander = deck.Commander?.Name?.Trim() ?? "no-commander";
        string cards = string.Join("|", deck.Cards
            .Where(card => !card.IsCommander && !string.IsNullOrWhiteSpace(card.Name))
            .GroupBy(card => card.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}"));
        return commander + "||" + cards;
    }
}

internal static class SpellbookComboTracker
{
    public static void Track(GoldfishGameState state)
    {
        foreach (var combo in state.KnownCombos)
        {
            if (state.AssembledSpellbookComboIds.Contains(combo.Id))
                continue;

            if (!IsSatisfied(combo, state))
                continue;

            state.AssembledSpellbookComboIds.Add(combo.Id);
            state.SpellbookComboAssemblies.Add(combo.Summary);
            state.ActionLog.Add($"T{state.Turn}: assembled Spellbook combo - {combo.Summary}");

            if (!state.InfiniteComboAchieved && combo.IsInfiniteLike())
            {
                state.InfiniteComboAchieved = true;
                state.InfiniteComboLine = combo.Summary;
            }
        }
    }

    private static bool IsSatisfied(SpellbookComboVariant combo, GoldfishGameState state)
    {
        return combo.Uses.All(use => CountAvailableCopies(use.CardName, use.ZoneLocations, state) >= use.Quantity);
    }

    private static int CountAvailableCopies(string cardName, IReadOnlyCollection<string> zones, GoldfishGameState state)
    {
        int total = 0;
        foreach (string zone in zones.DefaultIfEmpty("B"))
        {
            total += zone switch
            {
                "B" => state.Battlefield.Count(card => card.Name != null && card.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase)),
                "H" => state.Hand.Count(card => card.Name != null && card.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase)),
                "G" => state.Graveyard.Count(card => card.Name != null && card.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase)),
                "E" => state.Exile.Count(card => card.Name != null && card.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase)),
                "L" => state.Library.Count(card => card.Name != null && card.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase)),
                "C" => state.CommanderAvailable && state.Commander?.Name != null && state.Commander.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                _ => 0
            };
        }

        return total;
    }
}