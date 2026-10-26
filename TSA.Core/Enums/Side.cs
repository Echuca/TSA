namespace TSA.Core.Enums;

public enum Side
{
    US,
    USSR,

    /// <summary>
    /// Used for cards whose event belongs to both players and for uncontrolled countries.
    /// </summary>
    Neutral
}

public static class SideExtensions
{
    public static Side Opponent(this Side side) => side switch
    {
        Side.US => Side.USSR,
        Side.USSR => Side.US,
        _ => throw new ArgumentOutOfRangeException(nameof(side), "Neutral has no opponent.")
    };

    public static string Label(this Side side) => side switch
    {
        Side.US => "US",
        Side.USSR => "USSR",
        _ => "Both"
    };
}
