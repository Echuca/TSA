using TSA.Core.Data;
using TSA.Core.Engine;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Core.Events;

public static class LateWarEvents
{
    internal static void Register(Dictionary<int, Func<EventContext, Task>> handlers)
    {
        handlers[CardIds.IranianHostageCrisis] = IranianHostageCrisis;
        handlers[CardIds.IronLady] = IronLady;
        handlers[CardIds.ReaganBombsLibya] = ReaganBombsLibya;
        handlers[CardIds.StarWars] = StarWars;
        handlers[CardIds.NorthSeaOil] = ctx => EarlyWarEvents.Effect(ctx,
            "North Sea Oil: OPEC is no longer playable and the US may take 8 action rounds this turn.", data: ctx.State.Turn.ToString());
        handlers[CardIds.Reformer] = Reformer;
        handlers[CardIds.MarineBarracks] = MarineBarracks;
        handlers[CardIds.Kal007] = Kal007;
        handlers[CardIds.Glasnost] = Glasnost;
        handlers[CardIds.Ortega] = Ortega;
        handlers[CardIds.Terrorism] = Terrorism;
        handlers[CardIds.IranContra] = ctx => EarlyWarEvents.Effect(ctx, "US realignment rolls are -1 this turn.", thisTurnOnly: true);
        handlers[CardIds.Chernobyl] = Chernobyl;
        handlers[CardIds.LatinDebtCrisis] = LatinDebtCrisis;
        handlers[CardIds.TearDownThisWall] = TearDownThisWall;
        handlers[CardIds.EvilEmpire] = EvilEmpire;
        handlers[CardIds.AldrichAmes] = AldrichAmes;
        handlers[CardIds.PershingII] = PershingII;
        handlers[CardIds.Wargames] = Wargames;
        handlers[CardIds.Solidarity] = ctx => MidWarEvents.Add(ctx, "poland", Side.US, 3);
        handlers[CardIds.IranIraqWar] = ctx => EarlyWarEvents.ChooseWar(ctx, "iran", "iraq");
        handlers[CardIds.SamanthaAndYuri] = ctx => EarlyWarEvents.Effect(ctx, "USSR gains 1 VP for each US coup this turn.", thisTurnOnly: true);
        handlers[CardIds.Awacs] = Awacs;
    }

    private static async Task IranianHostageCrisis(EventContext ctx)
    {
        ctx.Country("iran").SetInfluence(Side.US, 0);
        await MidWarEvents.Add(ctx, "iran", Side.USSR, 2);
        await EarlyWarEvents.Effect(ctx, "All US Influence removed from Iran. Terrorism is doubled against the US.");
    }

    private static async Task IronLady(EventContext ctx)
    {
        ctx.State.AddVp(Side.US, 1);
        await MidWarEvents.Add(ctx, "argentina", Side.USSR, 1);
        ctx.Country("uk").SetInfluence(Side.USSR, 0);
        await EarlyWarEvents.Effect(ctx, "All USSR Influence removed from the UK. Socialist Governments is no longer playable.");
    }

    private static Task ReaganBombsLibya(EventContext ctx)
    {
        ctx.State.AddVp(Side.US, ctx.Country("libya").UssrInfluence / 2);
        return Task.CompletedTask;
    }

    private static async Task StarWars(EventContext ctx)
    {
        var chosen = await ctx.ChooseCard(Side.US, "Star Wars: choose a non-scoring card from the discard pile; its event occurs",
            ctx.Runner.DiscardedCards(), "No card");

        if (chosen is not { } id)
        {
            return;
        }

        var card = GameData.Instance.Card(id);
        var implementer = card.Side == Side.Neutral ? Side.US : card.Side;

        if (CardEvents.WhyNot(ctx.State, id, implementer) is { } why)
        {
            ctx.Log($"{card.Name}: event cannot occur ({why}).");
            return;
        }

        var inner = await ctx.Runner.ResolveEventAsync(id, implementer, ctx.Phasing, ctx.IsHeadline);
        ctx.Runner.Dispose(id, eventOccurred: true, inner.Destination, Side.US);
    }

    private static async Task Reformer(EventContext ctx)
    {
        var amount = ctx.State.VictoryPoints < 0 ? 6 : 4;

        await ctx.AddInfluence($"The Reformer: add {amount} USSR Influence in Europe (max 2 per country)",
            Side.USSR, ctx.Countries(c => c.Region == Region.Europe), amount, 2);

        await EarlyWarEvents.Effect(ctx, "The USSR may no longer coup in Europe.");
    }

    private static async Task MarineBarracks(EventContext ctx)
    {
        ctx.Country("lebanon").SetInfluence(Side.US, 0);
        ctx.Log("All US Influence removed from Lebanon.");

        await ctx.RemoveInfluence("Marine Barracks Bombing: remove 2 more US Influence in the Middle East",
            Side.US, ctx.Countries(c => c.Region == Region.MiddleEast), 2);
    }

    private static async Task Kal007(EventContext ctx)
    {
        Defcon.Degrade(ctx.State, 1, ctx.Phasing);
        ctx.State.AddVp(Side.US, 2);

        if (ctx.Country("south_korea").IsControlledByUS)
        {
            await ctx.Runner.ConductOperationsAsync(Side.US, OpsSource.Virtual(4) with { AllowCoup = false });
        }
    }

    private static async Task Glasnost(EventContext ctx)
    {
        ctx.State.AddVp(Side.USSR, 2);
        Defcon.Improve(ctx.State, 1);

        if (ctx.State.HasEffect(CardIds.Reformer))
        {
            await ctx.Runner.ConductOperationsAsync(Side.USSR, OpsSource.Virtual(4) with { AllowCoup = false });
        }
    }

    private static async Task Ortega(EventContext ctx)
    {
        var nicaragua = ctx.Country("nicaragua");
        nicaragua.SetInfluence(Side.US, 0);
        ctx.Log("All US Influence removed from Nicaragua.");

        var neighbours = nicaragua.AdjacentCountries.ToHashSet();

        if (await ctx.Ask.YesNo(Side.USSR, "Ortega: make a free coup in a country adjacent to Nicaragua?", "Coup", "No"))
        {
            await ctx.Runner.ConductOperationsAsync(Side.USSR, new OpsSource
            {
                CardId = CardIds.Ortega,
                AllowInfluence = false,
                AllowRealign = false,
                Free = true,
                Filter = c => neighbours.Contains(c.Id)
            });
        }
    }

    private static async Task Terrorism(EventContext ctx)
    {
        var victim = ctx.Opponent;
        var count = ctx.Player == Side.USSR && ctx.State.HasEffect(CardIds.IranianHostageCrisis) ? 2 : 1;

        for (var i = 0; i < count; i++)
        {
            var discard = await CardEvents.AskDiscard(ctx, victim,
                $"Terrorism: which card did the {victim.Label()} randomly discard? ({i + 1} of {count})",
                c => !c.IsChinaCard, "No cards in hand");

            if (discard is not { } id)
            {
                return;
            }

            CardEvents.Discard(ctx, id, victim);
        }
    }

    private static async Task Chernobyl(EventContext ctx)
    {
        var regions = Enum.GetValues<Region>();
        var choice = await ctx.Ask.Option(Side.US, "Chernobyl: the USSR may not place Influence with Operations in which region this turn?",
            regions.Select(r => r.Label()).ToArray());

        await EarlyWarEvents.Effect(ctx, $"Chernobyl: no USSR Influence placement in {regions[choice].Label()} this turn.",
            thisTurnOnly: true, data: regions[choice].ToString());
    }

    private static async Task LatinDebtCrisis(EventContext ctx)
    {
        var discard = await CardEvents.AskDiscard(ctx, Side.US,
            "Latin American Debt Crisis: does the US discard a card worth 3+ Ops? Choose it, or choose none.",
            c => !c.IsChinaCard && !c.Scoring && OpsCalculator.Value(ctx.State, Side.US, c.Id) >= 3,
            "No discard: USSR doubles Influence in two South American countries");

        if (discard is { } id)
        {
            CardEvents.Discard(ctx, id, Side.US);
            return;
        }

        var chosen = await ctx.ChooseCountries("Latin American Debt Crisis: double USSR Influence in two South American countries",
            ctx.Countries(c => c.Region == Region.SouthAmerica && c.UssrInfluence > 0), 2, 2);

        foreach (var countryId in chosen)
        {
            var country = ctx.Country(countryId);
            country.AddInfluence(Side.USSR, country.UssrInfluence);
            ctx.Log($"USSR Influence in {country.Name} doubled to {country.UssrInfluence}.");
        }
    }

    private static async Task TearDownThisWall(EventContext ctx)
    {
        if (ctx.State.HasEffect(CardIds.WillyBrandt))
        {
            ctx.State.RemoveEffect(CardIds.WillyBrandt);
            ctx.Log("Willy Brandt is cancelled.");
        }

        await MidWarEvents.Add(ctx, "east_germany", Side.US, 3);
        await EarlyWarEvents.Effect(ctx, "Willy Brandt may no longer be played.");

        if (await ctx.Ask.YesNo(Side.US, "Tear Down This Wall: make a free Coup or Realignments in Europe?", "Yes", "No"))
        {
            await ctx.Runner.ConductOperationsAsync(Side.US, new OpsSource
            {
                CardId = CardIds.TearDownThisWall,
                AllowInfluence = false,
                Free = true,
                Filter = c => c.Region == Region.Europe
            });
        }
    }

    private static async Task EvilEmpire(EventContext ctx)
    {
        if (ctx.State.HasEffect(CardIds.FlowerPower))
        {
            ctx.State.RemoveEffect(CardIds.FlowerPower);
            ctx.Log("Flower Power is cancelled.");
        }

        ctx.State.AddVp(Side.US, 1);
        await EarlyWarEvents.Effect(ctx, "Flower Power may no longer be played.");
    }

    private static async Task AldrichAmes(EventContext ctx)
    {
        ctx.Log("The US exposes their hand for the rest of the turn.");

        var discard = await CardEvents.AskDiscard(ctx, Side.USSR,
            "Aldrich Ames: which card from the US hand does the USSR discard?", c => !c.IsChinaCard, "The US has no cards");

        if (discard is { } id)
        {
            CardEvents.Discard(ctx, id, Side.US);
        }
    }

    private static async Task PershingII(EventContext ctx)
    {
        ctx.State.AddVp(Side.USSR, 1);

        var chosen = await ctx.ChooseCountries("Pershing II: remove 1 US Influence from up to three Western European countries",
            ctx.Countries(c => c.IsIn(SubRegion.WesternEurope) && c.UsInfluence > 0), 0, 3);

        foreach (var id in chosen)
        {
            ctx.Country(id).RemoveInfluence(Side.US, 1);
            ctx.Log($"-1 US in {ctx.Country(id).Name}.");
        }
    }

    private static async Task Wargames(EventContext ctx)
    {
        if (!await ctx.Ask.YesNo(ctx.Player, $"Wargames: end the game now? The {ctx.Opponent.Label()} gets 6 VP first, then the higher VP wins.",
                "End the game", "Keep playing"))
        {
            ctx.Log("Wargames: the game continues.");
            return;
        }

        ctx.State.AddVp(ctx.Opponent, 6);
        Victory.EndOnPoints(ctx.State, "Wargames ended the game.");
    }

    private static async Task Awacs(EventContext ctx)
    {
        await MidWarEvents.Add(ctx, "saudi_arabia", Side.US, 2);
        await EarlyWarEvents.Effect(ctx, "Muslim Revolution may no longer be played as an event.");
    }
}
