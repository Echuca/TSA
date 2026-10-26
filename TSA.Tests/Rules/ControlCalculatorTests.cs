using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Tests.Rules;

public class ControlCalculatorTests
{
    [Fact]
    public void Israel_Is_US_Controlled()
    {
        var country = new Country { Stability = 4, UsInfluence = 4, UssrInfluence = 0 };

        Assert.Equal(Side.US, ControlCalculator.GetController(country));
    }

    [Fact]
    public void Country_Is_Neutral_When_Not_Enough_Margin()
    {
        var country = new Country { Stability = 3, UsInfluence = 4, UssrInfluence = 2 };

        Assert.Equal(Side.Neutral, ControlCalculator.GetController(country));
    }

    [Fact]
    public void GainControl_Adds_Just_Enough()
    {
        var country = new Country { Stability = 3, UsInfluence = 2, UssrInfluence = 0 };

        country.GainControl(Side.USSR);

        Assert.Equal(5, country.UssrInfluence);
        Assert.True(country.IsControlledByUSSR);
    }
}
