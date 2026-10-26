using TSA.Core.Data;
using TSA.Core.Engine;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Core.Events;

public static class MidWarEvents
{
    internal static void Register(Dictionary<int, Func<EventContext, Task>> handlers)
    {
        handlers[CardIds.BrushWar] = BrushWar;
        handlers[CardIds.ArmsRace] = ArmsRace;
        handlers[CardIds.CubanMissileCrisis] = CubanMissileCrisis;
        handlers[CardIds.NuclearSubs] = ctx => EarlyWarEvents.Effect(ctx, "US battleground coups do not degrade DEFCON this turn.", thisTurnOnly: true);
        handlers[CardIds.Quagmire] = Quagmire;
        handlers[CardIds.SaltNegotiations] = SaltNegotiations;
        handlers[CardIds.BearTrap] = ctx => EarlyWarEvents.Effect(ctx, "The USSR is caught in Bear Trap.", side: Side.USSR);
        handlers[CardIds.Summit] = Summit;
        handlers[CardIds.HowILearned] = HowILearned;
        handlers[CardIds.Junta] = Junta;
        handlers[CardIds.KitchenDebates] = KitchenDebates;
        handlers[CardIds.MissileEnvy] = MissileEnvy;
        handlers[CardIds.WeWillBuryYou] = WeWillBuryYou;
        handlers[CardIds.BrezhnevDoctrine] = ctx => EarlyWarEvents.Effect(ctx, "USSR Operations cards get +1 this turn (max 4).", thisTurnOnly: true);
        handlers[CardIds.PortugueseEmpire] = PortugueseEmpire;
        handlers[CardIds.SouthAfricanUnrest] = SouthAfricanUnrest;
        handlers[CardIds.Allende] = ctx => Add(ctx, "chile", Side.USSR, 2);
        handlers[CardIds.WillyBrandt] = WillyBrandt;
        handlers[CardIds.MuslimRevolution] = MuslimRevolution;
        handlers[CardIds.AbmTreaty] = AbmTreaty;
        handlers[CardIds.CulturalRevolution] = CulturalRevolution;
        handlers[CardIds.FlowerPower] = ctx => EarlyWarEvents.Effect(ctx, "Flower Power: USSR gains 2 VP whenever the US plays a War card.");
        handlers[CardIds.U2Incident] = U2Incident;
        handlers[CardIds.Opec] = Opec;
        handlers[CardIds.LoneGunman] = LoneGunman;
        handlers[CardIds.ColonialRearGuards] = ctx => ctx.AddInfluence(
            "Colonial Rear Guards: add 1 US Influence in each of four African and/or Southeast Asian countries",
            Side.US, ctx.Countries(c => c.Region == Region.Africa || c.IsIn(SubRegion.SoutheastAsia)), 4, 1);
        handlers[CardIds.PanamaCanal] = PanamaCanal;
        handlers[CardIds.CampDavid] = CampDavid;
        handlers[CardIds.PuppetGovernments] = ctx => ctx.AddInfluence(
            "Puppet Governments: add 1 US Influence in three countries with no Influence",
            Side.US, ctx.Countries(c => c.UsInfluence == 0 && c.UssrInfluence == 0), 3, 1);
        handlers[CardIds.GrainSales] = GrainSales;
        handlers[CardIds.JohnPaul] = JohnPaul;
        handlers[CardIds.DeathSquads] = ctx => EarlyWarEvents.Effect(ctx,
            $"{ctx.Player.Label()} coups in Central/South America are +1, {ctx.Opponent.Label()} coups there are -1 this turn.", thisTurnOnly: true);
        handlers[CardIds.OasFounded] = ctx => ctx.AddInfluence("OAS Founded: add 2 US Influence in Central and/or South America",
            Side.US, ctx.Countries(c => c.Region is Region.CentralAmerica or Region.SouthAmerica), 2);
        handlers[CardIds.NixonChina] = NixonChina;
        handlers[CardIds.SadatExpels] = SadatExpels;
        handlers[CardIds.ShuttleDiplomacy] = ctx => EarlyWarEvents.Effect(ctx, "Shuttle Diplomacy waits for the next Middle East or Asia scoring.");
        handlers[CardIds.VoiceOfAmerica] = ctx => ctx.RemoveInfluence(
            "Voice of America: remove 4 USSR Influence from non-European countries (max 2 per country)",
            Side.USSR, ctx.Countries(c => c.Region != Region.Europe), 4, 2);
        handlers[CardIds.LiberationTheology] = ctx => ctx.AddInfluence(
            "Liberation Theology: add 3 USSR Influence in Central America (max 2 per country)",
            Side.USSR, ctx.Countries(c => c.Region == Region.CentralAmerica), 3, 2);
        handlers[CardIds.UssuriRiver] = UssuriRiver;
        handlers[CardIds.AskNot] = AskNot;
        handlers[CardIds.AllianceForProgress] = AllianceForProgress;
        handlers[CardIds.OneSmallStep] = OneSmallStep;
        handlers[CardIds.Che] = Che;
        handlers[CardIds.OurManInTehran] = OurManInTehran;
    }

    internal static Task Add(EventContext ctx, string countryId, Side side, int amount)
    {
        var country = ctx.Country(countryId);
        country.AddInfluence(side, amount);
        ctx.Log($"+{amount} {side.Label()} in {country.Name}.");
        return Task.CompletedTask;
    }

    private static async Task BrushWar(EventContext ctx)
    {
        var targets = ctx.Countries(c =>
            c.Stability <= 2 &&
            !(ctx.Player == Side.USSR && Protections.NatoProtects(ctx.State, c)));

        var target = await ctx.ChooseCountry("Brush War: choose a country with stability 1 or 2 to attack", targets);
        await CardEvents.WarAsync(ctx, ctx.Country(target!), 0, 3, 1, 3);
    }

    private static Task ArmsRace(EventContext ctx)
    {
        var mine = ctx.State.Player(ctx.Player).MilitaryOps;
        var theirs = ctx.State.Player(ctx.Opponent).MilitaryOps;

        if (mine > theirs)
        {
            ctx.State.AddVp(ctx.Player, mine >= ctx.State.Defcon ? 3 : 1);
        }
        else
        {
            ctx.Log("Arms Race: not ahead in Military Ops — no VP.");
        }

        return Task.CompletedTask;
    }

    private static Task CubanMissileCrisis(EventContext ctx)
    {
        Defcon.Set(ctx.State, 2, ctx.Phasing);
        return EarlyWarEvents.Effect(ctx, $"Cuban Missile Crisis: any {ctx.Opponent.Label()} coup this turn causes nuclear war.",
            thisTurnOnly: true, side: ctx.Opponent);
    }

    private static Task Quagmire(EventContext ctx)
    {
        if (ctx.State.HasEffect(CardIds.Norad))
        {
            ctx.State.RemoveEffect(CardIds.Norad);
            ctx.Log("Quagmire cancels NORAD.");
        }

        return EarlyWarEvents.Effect(ctx, "The US is caught in Quagmire.", side: Side.US);
    }

    private static async Task SaltNegotiations(EventContext ctx)
    {
        Defcon.Improve(ctx.State, 2);
        await EarlyWarEvents.Effect(ctx, "Coups are -1 for both players this turn.", thisTurnOnly: true);

        var reclaimed = await ctx.ChooseCard(ctx.Player, "SALT Negotiations: reclaim a non-scoring card from the discard pile?",
            ctx.Runner.DiscardedCards(), "Don't reclaim a card");

        if (reclaimed is { } id)
        {
            ctx.State.MoveCard(id, CardLocation.Available);
            ctx.Log($"{ctx.Player.Label()} reclaims {GameData.Instance.Card(id).Name} into their hand.");
        }
    }

    private static async Task Summit(EventContext ctx)
    {
        var usBonus = Scoring.RegionsDominatedOrControlled(ctx.State, Side.US);
        var ussrBonus = Scoring.RegionsDominatedOrControlled(ctx.State, Side.USSR);

        var us = await ctx.Roll($"Summit: US roll (+{usBonus})", roller: Side.US) + usBonus;
        var ussr = await ctx.Roll($"Summit: USSR roll (+{ussrBonus})", roller: Side.USSR) + ussrBonus;

        if (us == ussr)
        {
            ctx.Log($"Summit: tie at {us}. No effect.");
            return;
        }

        var winner = us > ussr ? Side.US : Side.USSR;
        ctx.Log($"Summit: US {us} vs USSR {ussr}.");
        ctx.State.AddVp(winner, 2);

        var choice = await ctx.Ask.Option(winner, $"Summit: {winner.Label()} may move DEFCON one level",
            [
                new DecisionOption("Leave DEFCON"),
                new DecisionOption("Improve DEFCON by 1", ctx.State.Defcon >= 5 ? "Already at 5" : null),
                new DecisionOption("Degrade DEFCON by 1")
            ]);

        if (choice == 1)
        {
            Defcon.Improve(ctx.State, 1);
        }
        else if (choice == 2)
        {
            Defcon.Degrade(ctx.State, 1, ctx.Phasing);
        }
    }

    private static async Task HowILearned(EventContext ctx)
    {
        var choice = await ctx.Ask.Option(ctx.Player, "How I Learned to Stop Worrying: set DEFCON to",
            "5", "4", "3", "2", "1 (nuclear war — you lose)");

        Defcon.Set(ctx.State, 5 - choice, ctx.Phasing);
        MilitaryOps.Set(ctx.State, ctx.Player, 5);
        ctx.Log($"{ctx.Player.Label()} Military Ops set to 5.");
    }

    private static async Task Junta(EventContext ctx)
    {
        bool Latin(Country c) => c.Region is Region.CentralAmerica or Region.SouthAmerica;

        await ctx.AddInfluence("Junta: place 2 Influence in one Central or South American country",
            ctx.Player, ctx.Countries(Latin), 2, 2);

        var choice = await ctx.Ask.Option(ctx.Player, "Junta: make a free Coup or Realignment in Central/South America?",
            "Free Coup or Realignment", "No");

        if (choice == 0)
        {
            await ctx.Runner.ConductOperationsAsync(ctx.Player, new OpsSource
            {
                CardId = CardIds.Junta,
                AllowInfluence = false,
                Free = true,
                Filter = Latin
            });
        }
    }

    private static Task KitchenDebates(EventContext ctx)
    {
        var us = ctx.State.Countries.Count(c => c.Battleground && c.IsControlledByUS);
        var ussr = ctx.State.Countries.Count(c => c.Battleground && c.IsControlledByUSSR);

        if (us > ussr)
        {
            ctx.Log("Kitchen Debates: the US pokes the USSR in the chest!");
            ctx.State.AddVp(Side.US, 2);
        }
        else
        {
            ctx.Log($"Kitchen Debates: US controls {us} battlegrounds vs USSR {ussr}. No effect.");
        }

        return Task.CompletedTask;
    }

    private static async Task MissileEnvy(EventContext ctx)
    {
        var player = ctx.Player;
        var opponent = ctx.Opponent;

        var taken = await CardEvents.AskDiscard(ctx, opponent,
            $"Missile Envy: which card does the {opponent.Label()} hand over (their highest Ops; they choose between ties)?",
            c => !c.IsChinaCard, "They have no other cards");

        ctx.Destination = CardLocation.Available;

        if (taken is not { } id)
        {
            return;
        }

        var card = GameData.Instance.Card(id);
        ctx.State.AddEffect(CardIds.MissileEnvy, opponent);
        ctx.Log($"Missile Envy: {player.Label()} takes {card.Name}; {opponent.Label()} must play Missile Envy for Ops next action round.");

        if (card.Side != opponent && !card.Scoring)
        {
            if (CardEvents.WhyNot(ctx.State, id, player) is { } why)
            {
                ctx.Log($"{card.Name}: event cannot occur ({why}).");
                ctx.State.MoveCard(id, CardLocation.Discard);
                return;
            }

            var inner = await ctx.Runner.ResolveEventAsync(id, card.Side == Side.Neutral ? player : card.Side, ctx.Phasing, ctx.IsHeadline);
            ctx.Runner.Dispose(id, eventOccurred: true, inner.Destination, player);
        }
        else if (!card.Scoring)
        {
            await ctx.Runner.ConductOperationsAsync(player, OpsSource.FromCard(id));
            ctx.State.MoveCard(id, CardLocation.Discard);
        }
        else
        {
            ctx.State.MoveCard(id, CardLocation.Discard);
        }
    }

    private static Task WeWillBuryYou(EventContext ctx)
    {
        Defcon.Degrade(ctx.State, 1, ctx.Phasing);
        return EarlyWarEvents.Effect(ctx, "We Will Bury You: USSR gains 3 VP unless the US plays UN Intervention next action round.");
    }

    private static async Task PortugueseEmpire(EventContext ctx)
    {
        await Add(ctx, "se_african_states", Side.USSR, 2);
        await Add(ctx, "angola", Side.USSR, 2);
    }

    private static async Task SouthAfricanUnrest(EventContext ctx)
    {
        var choice = await ctx.Ask.Option(Side.USSR, "South African Unrest: choose one",
            "Add 2 USSR Influence in South Africa",
            "Add 1 in South Africa and 2 in countries adjacent to South Africa");

        if (choice == 0)
        {
            await Add(ctx, "south_africa", Side.USSR, 2);
            return;
        }

        await Add(ctx, "south_africa", Side.USSR, 1);
        await ctx.AddInfluence("Add 2 USSR Influence in countries adjacent to South Africa",
            Side.USSR, ctx.Country("south_africa").AdjacentCountries.Select(ctx.Country), 2);
    }

    private static async Task WillyBrandt(EventContext ctx)
    {
        ctx.State.AddVp(Side.USSR, 1);
        await Add(ctx, "west_germany", Side.USSR, 1);
        await EarlyWarEvents.Effect(ctx, "NATO no longer protects West Germany.");
    }

    private static async Task MuslimRevolution(EventContext ctx)
    {
        string[] ids = ["sudan", "iran", "iraq", "egypt", "libya", "saudi_arabia", "syria", "jordan"];

        var chosen = await ctx.ChooseCountries("Muslim Revolution: remove all US Influence from two countries",
            ids.Select(ctx.Country).Where(c => c.UsInfluence > 0), 2, 2);

        foreach (var id in chosen)
        {
            ctx.Country(id).SetInfluence(Side.US, 0);
            ctx.Log($"All US Influence removed from {ctx.Country(id).Name}.");
        }
    }

    private static async Task AbmTreaty(EventContext ctx)
    {
        Defcon.Improve(ctx.State, 1);
        await ctx.Runner.ConductOperationsAsync(ctx.Player, OpsSource.Virtual(4));
    }

    private static Task CulturalRevolution(EventContext ctx)
    {
        if (ctx.State.ChinaCardOwner == Side.US)
        {
            ctx.State.ChinaCardOwner = Side.USSR;
            ctx.State.ChinaCardFaceUp = true;
            ctx.Log("The USSR claims The China Card face up.");
        }
        else
        {
            ctx.State.AddVp(Side.USSR, 1);
        }

        return Task.CompletedTask;
    }

    private static Task U2Incident(EventContext ctx)
    {
        ctx.State.AddVp(Side.USSR, 1);
        return EarlyWarEvents.Effect(ctx, "U2 Incident: +1 more VP if UN Intervention is played this turn.", thisTurnOnly: true);
    }

    private static Task Opec(EventContext ctx)
    {
        string[] ids = ["egypt", "iran", "libya", "saudi_arabia", "iraq", "gulf_states", "venezuela"];
        var vp = ids.Count(id => ctx.Country(id).IsControlledByUSSR);
        ctx.Log($"OPEC: USSR controls {vp} oil producers.");
        ctx.State.AddVp(Side.USSR, vp);
        return Task.CompletedTask;
    }

    private static async Task LoneGunman(EventContext ctx)
    {
        ctx.Log("The US reveals their hand to the USSR.");
        await ctx.Runner.ConductOperationsAsync(Side.USSR, OpsSource.Virtual(1));
    }

    private static async Task PanamaCanal(EventContext ctx)
    {
        foreach (var id in new[] { "panama", "costa_rica", "venezuela" })
        {
            await Add(ctx, id, Side.US, 1);
        }
    }

    private static async Task CampDavid(EventContext ctx)
    {
        ctx.State.AddVp(Side.US, 1);

        foreach (var id in new[] { "israel", "jordan", "egypt" })
        {
            await Add(ctx, id, Side.US, 1);
        }

        await EarlyWarEvents.Effect(ctx, "Arab-Israeli War may no longer be played as an event.");
    }

    private static async Task GrainSales(EventContext ctx)
    {
        var taken = await CardEvents.AskDiscard(ctx, Side.USSR,
            "Grain Sales: which card did the US randomly take from the USSR hand?",
            c => !c.IsChinaCard, "The USSR has no cards");

        if (taken is { } id &&
            await ctx.Ask.YesNo(Side.US, $"Grain Sales: does the US play {GameData.Instance.Card(id).Name}?", "Play it", "Return it"))
        {
            await ctx.Runner.PlayCardAsync(Side.US, id);
            return;
        }

        if (taken is not null)
        {
            ctx.Log("The card is returned to the USSR.");
        }

        await ctx.Runner.ConductOperationsAsync(Side.US, OpsSource.FromCard(CardIds.GrainSales));
    }

    private static async Task JohnPaul(EventContext ctx)
    {
        var poland = ctx.Country("poland");
        poland.RemoveInfluence(Side.USSR, 2);
        poland.AddInfluence(Side.US, 1);
        await EarlyWarEvents.Effect(ctx, "-2 USSR, +1 US in Poland. Solidarity may now be played.");
    }

    private static Task NixonChina(EventContext ctx)
    {
        if (ctx.State.ChinaCardOwner == Side.US)
        {
            ctx.State.AddVp(Side.US, 2);
        }
        else
        {
            ctx.State.ChinaCardOwner = Side.US;
            ctx.State.ChinaCardFaceUp = false;
            ctx.Log("The US receives The China Card face down.");
        }

        return Task.CompletedTask;
    }

    private static Task SadatExpels(EventContext ctx)
    {
        var egypt = ctx.Country("egypt");
        egypt.SetInfluence(Side.USSR, 0);
        egypt.AddInfluence(Side.US, 1);
        ctx.Log("All USSR Influence removed from Egypt; +1 US.");
        return Task.CompletedTask;
    }

    private static async Task UssuriRiver(EventContext ctx)
    {
        if (ctx.State.ChinaCardOwner == Side.USSR)
        {
            ctx.State.ChinaCardOwner = Side.US;
            ctx.State.ChinaCardFaceUp = true;
            ctx.Log("The US claims The China Card face up.");
            return;
        }

        await ctx.AddInfluence("Ussuri River Skirmish: add 4 US Influence in Asia (max 2 per country)",
            Side.US, ctx.Countries(c => c.Region == Region.Asia), 4, 2);
    }

    private static async Task AskNot(EventContext ctx)
    {
        ctx.Log("The US discards cards and draws replacements. Record each discarded card.");

        while (true)
        {
            var discard = await CardEvents.AskDiscard(ctx, Side.US, "Ask Not: choose a card the US discarded",
                c => !c.IsChinaCard, "Done");

            if (discard is not { } id)
            {
                return;
            }

            CardEvents.Discard(ctx, id, Side.US);
        }
    }

    private static Task AllianceForProgress(EventContext ctx)
    {
        var vp = ctx.State.Countries.Count(c =>
            c.Battleground && c.IsControlledByUS && c.Region is Region.CentralAmerica or Region.SouthAmerica);

        ctx.State.AddVp(Side.US, vp);
        return Task.CompletedTask;
    }

    private static Task OneSmallStep(EventContext ctx)
    {
        SpaceRace.Advance(ctx.State, ctx.Player, 2, vpForLastBoxOnly: true);
        return Task.CompletedTask;
    }

    private static async Task Che(EventContext ctx)
    {
        bool Target(Country c) => !c.Battleground && c.Region is Region.CentralAmerica or Region.SouthAmerica or Region.Africa;

        var first = ctx.State.Countries.Where(c => Target(c) && CoupRules.WhyNot(ctx.State, Side.USSR, c, free: false) is null).ToList();

        var targetId = await ctx.ChooseCountry("Che: coup a non-battleground in Central America, South America or Africa", first, "No coup");

        if (targetId is null)
        {
            return;
        }

        var result = await CheCoup(ctx, ctx.Country(targetId));

        if (result.Removed == 0)
        {
            return;
        }

        var second = first.Where(c => c.Id != targetId && c.UsInfluence > 0).ToList();
        var secondId = await ctx.ChooseCountry("Che: second coup against a different target", second, "No second coup");

        if (secondId is not null)
        {
            await CheCoup(ctx, ctx.Country(secondId));
        }
    }

    private static async Task<CoupResult> CheCoup(EventContext ctx, Country country)
    {
        var ops = OpsCalculator.Value(ctx.State, Side.USSR, CardIds.Che, OpsScope.For(country));
        var roll = await ctx.Roll($"Che: coup in {country.Name} with {ops} Ops", roller: Side.USSR);
        return CoupRules.Resolve(ctx.State, Side.USSR, country, ops, roll, free: false);
    }

    private static async Task OurManInTehran(EventContext ctx)
    {
        if (!ctx.State.Countries.Any(c => c.Region == Region.MiddleEast && c.IsControlledByUS))
        {
            ctx.Log("The US controls no Middle East country: no effect.");
            return;
        }

        ctx.Log("The US draws the top 5 cards. Record each card discarded (shown to the USSR); return the rest and reshuffle.");

        for (var i = 0; i < 5; i++)
        {
            var discard = await CardEvents.AskDiscard(ctx, Side.US, $"Our Man in Tehran: discarded card {i + 1} of up to 5",
                c => !c.IsChinaCard, "Done");

            if (discard is not { } id)
            {
                return;
            }

            CardEvents.Discard(ctx, id, Side.US);
        }
    }
}
