using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Rules;

public static class ControlCalculator
{
    public static Side GetController(Country country)
    {
        if (IsControlledByUS(country))
        {
            return Side.US;
        }

        if (IsControlledByUSSR(country))
        {
            return Side.USSR;
        }

        return Side.Neutral;
    }

    public static bool IsControlledByUS(
        Country country)
    {
        return country.UsInfluence >= country.Stability
            && (country.UsInfluence - country.UssrInfluence)
                >= country.Stability;
    }

    public static bool IsControlledByUSSR(
        Country country)
    {
        return country.UssrInfluence >= country.Stability
            && (country.UssrInfluence - country.UsInfluence)
                >= country.Stability;
    }

    public static bool IsControlledBy(
        Country country,
        Side side)
    {
        return side switch
        {
            Side.US => IsControlledByUS(country),
            Side.USSR => IsControlledByUSSR(country),
            _ => false
        };
    }
}