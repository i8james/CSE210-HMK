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

    public GoldfishGameState(List<Card> library, List<Card> hand, List<Card> battlefield, Card? commander, bool commanderAvailable, int turn, int landsInPlay, int permanentRamp)
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