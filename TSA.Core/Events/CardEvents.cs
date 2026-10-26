using TSA.Core.Data;
using TSA.Core.Engine;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Core.Events;

/// <summary>
/// Registry of card events. Each event is a static method in
/// <see cref="EarlyWarEvents"/>, <see cref="MidWarEvents"/> or <see cref="LateWarEvents"/>.
/// </summary>
public static class CardEvents
{
    private static readonly Dictionary<int, Func<EventContext, Task>> Handlers = new();

    static CardEvents()
    {
        EarlyWarEvents.Register(Handlers);
        MidWarEvents.Register(Handlers);
        LateWarEvents.Register(Handlers);
    }

    public static bool IsImplemented(int cardId) => Handlers.ContainsKey(cardId);

    public static async Task ResolveAsync(EventContext context)
    {
        if (Handlers.TryGetValue(context.CardId, out var handler))
        {
            await handler(context);
        }
        else
        {
            context.Log($"{context.Card.Name}: no automatic resolution. Use Corrections to apply it by hand.");
        }
    }

    /// <summary>
    /// Returns why the event cannot be played as an event right now, or null if it can.
    /// <paramref name="player"/> is the side carrying out the event.
    /// </summary>
    public static string? WhyNot(GameState state, int cardId, Side player)
    {
        bool Has(int id) => state.HasEffect(id);

        return cardId switch
        {
            CardIds.SocialistGovernments when Has(CardIds.IronLady) => "The Iron Lady is in effect",
            CardIds.ArabIsraeliWar when Has(CardIds.CampDavid) => "Camp David Accords is in effect",
            CardIds.Nato when !Has(CardIds.MarshallPlan) && !Has(CardIds.WarsawPact) => "Needs Marshall Plan or Warsaw Pact first",
            CardIds.WillyBrandt when Has(CardIds.TearDownThisWall) => "Tear Down This Wall is in effect",
            CardIds.FlowerPower when Has(CardIds.EvilEmpire) => "An Evil Empire is in effect",
            CardIds.MuslimRevolution when Has(CardIds.Awacs) => "AWACS Sale to Saudis is in effect",
            CardIds.Opec when Has(CardIds.NorthSeaOil) => "North Sea Oil is in effect",
            CardIds.Solidarity when !Has(CardIds.JohnPaul) => "Needs John Paul II Elected Pope",
            CardIds.StarWars when state.US.SpaceRacePosition <= state.USSR.SpaceRacePosition => "US is not ahead in the Space Race",
            CardIds.OneSmallStep when state.Player(player).SpaceRacePosition >= state.Player(player.Opponent()).SpaceRacePosition => "Not behind in the Space Race",
            CardIds.Wargames when state.Defcon != 2 => "DEFCON is not 2",
            CardIds.CambridgeFive when state.Era == CardEra.LateWar => "Not playable in the Late War",
            _ => null
        };
    }

    /// <summary>
    /// Shared resolution for Korean War, Arab-Israeli War, Indo-Pakistani War, Iran-Iraq War and Brush War.
    /// Enemy superpower spaces count as adjacent enemy-controlled countries (rule 2.1.5).
    /// </summary>
    internal static async Task WarAsync(EventContext ctx, Country target, int penalty, int successFrom, int vp, int militaryOps)
    {
        var attacker = ctx.Player;
        var defender = attacker.Opponent();

        penalty += target.AdjacentCountries.Count(id => ctx.Country(id).IsControlledBy(defender));

        if (target.AdjacentSuperpower == defender)
        {
            penalty++;
        }

        var roll = await ctx.Roll(
            $"{ctx.Card.Name}: {attacker.Label()} attacks {target.Name} (modifier -{penalty})",
            $"Success on modified {successFrom}-6");

        MilitaryOps.Add(ctx.State, attacker, militaryOps);

        if (roll - penalty >= successFrom)
        {
            ctx.Log($"{ctx.Card.Name}: rolled {roll} - {penalty} = {roll - penalty}. Victory in {target.Name}!");
            target.AddInfluence(attacker, target.Influence(defender));
            target.SetInfluence(defender, 0);
            ctx.State.AddVp(attacker, vp);
        }
        else
        {
            ctx.Log($"{ctx.Card.Name}: rolled {roll} - {penalty} = {roll - penalty}. The attack fails.");
        }
    }

    /// <summary>
    /// Asks which card a player discarded, among cards that could be in their hand.
    /// </summary>
    internal static Task<int?> AskDiscard(EventContext ctx, Side whose, string prompt, Func<CardDefinition, bool>? filter = null, string noneLabel = "They have no such card")
    {
        var eligible = ctx.Runner.PlayableCards()
            .Where(id => filter?.Invoke(GameData.Instance.Card(id)) ?? true);

        return ctx.ChooseCard(whose, prompt, eligible, noneLabel);
    }

    internal static void Discard(EventContext ctx, int cardId, Side whose)
    {
        ctx.State.MoveCard(cardId, CardLocation.Discard);
        ctx.State.AddLog(whose, $"{whose.Label()} discards {GameData.Instance.Card(cardId).Name}.");
    }
}
