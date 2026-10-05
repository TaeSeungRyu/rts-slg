namespace SanguoSLG.Core.Simulation.RenewalMovement;

internal static class IntegerMath
{
    public static long SquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var root = (long)Math.Sqrt(value);
        while (root < 3_037_000_499 && checked((root + 1) * (root + 1)) <= value)
        {
            root++;
        }
        while (root * root > value)
        {
            root--;
        }
        return root;
    }
}
