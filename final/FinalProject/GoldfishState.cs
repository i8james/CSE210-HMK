using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal enum GoldfishPhase
{
    Main,
    End
}

internal enum GoldfishActionType
{
    PlayLand,
    CastSpell,
    CastCommander,
    ActivateAbility,
    PassPhase
}

internal sealed class GoldfishAction
{
    public GoldfishActionType Type { get; init; }
    public Card? Card { get; init; }
    public int ManaCost { get; init; }
    public string Description { get; init; } = string.Empty;
}

internal sealed class GoldfishGameState
{
    public List<Card> Library { get; }
    public List<Card> Hand { get; }
    public List<Card> Battlefield { get; }
    public List<Card> Graveyard { get; }
    public List<Card> Exile { get; }
    public Card? Commander { get; }
    public bool CommanderAvailable { get; set; }
    public int CommanderTax { get; set; }
    public int Turn { get; set; }
    public GoldfishPhase Phase { get; set; }
    public int LandsInPlay { get; set; }
    public bool LandPlayedThisTurn { get; set; }
    public int PermanentRamp { get; set; }
    public int ManaAvailable { get; set; }
    public int ManaSpent { get; set; }
    public int CardsDrawn { get; set; }
    public int SpellsCast { get; set; }
    public int ActivatedAbilitiesUsed { get; set; }
    public int TriggeredAbilitiesResolved { get; set; }
    public bool InfiniteComboAchieved { get; set; }
    public string InfiniteComboLine { get; set; } = string.Empty;
    public List<string> ActionLog { get; }
    public HashSet<Card> ActivatedThisTurn { get; } = new HashSet<Card>(ReferenceEqualityComparer.Instance);
    public List<SpellbookComboVariant> KnownCombos { get; }
    public HashSet<string> AssembledSpellbookComboIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public List<string> SpellbookComboAssemblies { get; } = new List<string>();

    public GoldfishGameState(List<Card> library, List<Card> hand, List<Card> battlefield, Card? commander, bool commanderAvailable, int turn, int landsInPlay, int permanentRamp, IEnumerable<SpellbookComboVariant>? knownCombos = null)
    {
        Library = new List<Card>(library);
        Hand = new List<Card>(hand);
        Battlefield = new List<Card>(battlefield);
        Graveyard = new List<Card>();
        Exile = new List<Card>();
        Commander = commander;
        CommanderAvailable = commanderAvailable;
        Turn = turn;
        Phase = GoldfishPhase.Main;
        LandsInPlay = landsInPlay;
        PermanentRamp = permanentRamp;
        ManaAvailable = landsInPlay + permanentRamp;
        ActionLog = new List<string>();
        KnownCombos = knownCombos?.ToList() ?? new List<SpellbookComboVariant>();
    }

    private GoldfishGameState(GoldfishGameState other)
    {
        Library = new List<Card>(other.Library);
        Hand = new List<Card>(other.Hand);
        Battlefield = new List<Card>(other.Battlefield);
        Graveyard = new List<Card>(other.Graveyard);
        Exile = new List<Card>(other.Exile);
        Commander = other.Commander;
        CommanderAvailable = other.CommanderAvailable;
        CommanderTax = other.CommanderTax;
        Turn = other.Turn;
        Phase = other.Phase;
        LandsInPlay = other.LandsInPlay;
        LandPlayedThisTurn = other.LandPlayedThisTurn;
        PermanentRamp = other.PermanentRamp;
        ManaAvailable = other.ManaAvailable;
        ManaSpent = other.ManaSpent;
        CardsDrawn = other.CardsDrawn;
        SpellsCast = other.SpellsCast;
        ActivatedAbilitiesUsed = other.ActivatedAbilitiesUsed;
        TriggeredAbilitiesResolved = other.TriggeredAbilitiesResolved;
        InfiniteComboAchieved = other.InfiniteComboAchieved;
        InfiniteComboLine = other.InfiniteComboLine;
        ActionLog = new List<string>(other.ActionLog);
        KnownCombos = new List<SpellbookComboVariant>(other.KnownCombos);
        SpellbookComboAssemblies = new List<string>(other.SpellbookComboAssemblies);
        foreach (var comboId in other.AssembledSpellbookComboIds)
            AssembledSpellbookComboIds.Add(comboId);
        foreach (var card in other.ActivatedThisTurn)
            ActivatedThisTurn.Add(card);
    }

    public GoldfishGameState Clone()
    {
        return new GoldfishGameState(this);
    }
}

internal static class GoldfishLegalActionGenerator
{
    public static List<GoldfishAction> GetLegalActions(GoldfishGameState state)
    {
        var actions = new List<GoldfishAction>();

        if (state.Phase != GoldfishPhase.Main)
        {
            actions.Add(new GoldfishAction { Type = GoldfishActionType.PassPhase, Description = "Pass" });
            return actions;
        }

        if (!state.LandPlayedThisTurn)
        {
            foreach (var land in state.Hand.Where(card => card.IsLand).DistinctBy(card => card.Name))
            {
                actions.Add(new GoldfishAction
                {
                    Type = GoldfishActionType.PlayLand,
                    Card = land,
                    Description = $"Play {land.Name}"
                });
            }
        }

        foreach (var card in state.Hand.Where(card => !card.IsLand && card.ManaCost <= state.ManaAvailable))
        {
            actions.Add(new GoldfishAction
            {
                Type = GoldfishActionType.CastSpell,
                Card = card,
                ManaCost = Math.Max(0, card.ManaCost),
                Description = $"Cast {card.Name}"
            });
        }

        foreach (var permanent in state.Battlefield.Where(card => GoldfishAbilityHeuristics.CanActivate(card, state)))
        {
                if (state.ActivatedThisTurn.Contains(permanent))
                    continue;

            int activationCost = GoldfishAbilityHeuristics.GetActivationManaCost(permanent);
            if (activationCost > state.ManaAvailable)
                continue;

            actions.Add(new GoldfishAction
            {
                Type = GoldfishActionType.ActivateAbility,
                Card = permanent,
                ManaCost = activationCost,
                Description = $"Activate {permanent.Name}"
            });
        }

        if (state.CommanderAvailable && state.Commander != null)
        {
            int commanderCost = Math.Max(0, state.Commander.ManaCost) + state.CommanderTax;
            if (commanderCost <= state.ManaAvailable)
            {
                actions.Add(new GoldfishAction
                {
                    Type = GoldfishActionType.CastCommander,
                    Card = state.Commander,
                    ManaCost = commanderCost,
                    Description = $"Cast commander {state.Commander.Name}"
                });
            }
        }

        actions.Add(new GoldfishAction { Type = GoldfishActionType.PassPhase, Description = "Pass" });
        return actions;
    }
}

internal static class GoldfishAbilityHeuristics
{
    public static bool CanActivate(Card card, GoldfishGameState state)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.OracleText))
            return false;

        string oracle = card.OracleText.ToLowerInvariant();
        if (!oracle.Contains(":") && !oracle.Contains("whenever") && !oracle.Contains("at the beginning"))
            return false;

            // Note: {t}: add { is intentionally excluded — land/mana-rock mana is already
            // pre-computed in ManaAvailable = landsInPlay + permanentRamp each turn.
            // Including it here would cause every mana source to be "re-tapped" for free.
            if (oracle.Contains("{t}: draw") || oracle.Contains("{t}: create") || oracle.Contains("{t}: proliferate") || oracle.Contains("{t}: untap"))
            return true;

        if (oracle.Contains("pay ") && (oracle.Contains(": draw") || oracle.Contains(": create") || oracle.Contains(": proliferate") || oracle.Contains(": untap")))
            return true;

        // Detect search/tutor activated abilities
        if (oracle.Contains("{t}") && (oracle.Contains("search") || oracle.Contains("fetch")))
            return true;

        if (oracle.Contains("pay ") && (oracle.Contains("search") || oracle.Contains("fetch")))
            return true;

        return false;
    }

    public static int GetActivationManaCost(Card card)
    {
        string oracle = (card.OracleText ?? string.Empty).ToLowerInvariant();
        if (oracle.Contains("{3}")) return 3;
        if (oracle.Contains("{2}")) return 2;
        if (oracle.Contains("{1}")) return 1;
        return 0;
    }
}