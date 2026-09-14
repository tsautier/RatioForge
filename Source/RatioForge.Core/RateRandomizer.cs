namespace RatioForge;

/// <summary>Generates whole-number transfer rates within configured inclusive bounds.</summary>
public static class RateRandomizer
{
    public static decimal NextInteger(decimal minimum, decimal maximum)
    {
        int lowerBound = Decimal.ToInt32(decimal.Ceiling(minimum));
        int upperBound = Decimal.ToInt32(decimal.Floor(maximum));
        if (upperBound < lowerBound)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum rate must be at least the minimum rate.");
        }

        return upperBound == lowerBound
            ? lowerBound
            : Random.Shared.Next(lowerBound, checked(upperBound + 1));
    }
}
