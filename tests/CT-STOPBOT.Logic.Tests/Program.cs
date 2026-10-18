using System;
using CTStopBot;

internal static class Program
{
    private static int _assertions;

    private static int Main()
    {
        try
        {
            RequiredRewardDistanceIncludesExecutionCosts();
            NetRewardRiskCalculatesCorrectly();
            StructuralCapsAreSymmetricForBuyAndSell();
            OutwardRoundedTargetsAreRecappedBeforeStructure();
            NearStructureIsNeverSilentlyIgnored();
            StructureBeyondProjectedTargetDoesNotCap();
            InvalidInputsAreRejected();

            Console.WriteLine("PASS: {0} risk-reward assertions.", _assertions);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL after {0} assertions: {1}", _assertions, ex);
            return 1;
        }
    }

    private static void RequiredRewardDistanceIncludesExecutionCosts()
    {
        double required;
        Assert(RiskRewardEngine.TryGetRequiredRewardDistance(10.0, 0.5, 1.8, out required),
            "required reward distance should be calculable");
        Near(19.4, required, 1e-10, "1.8R target must include costs on risk and reward");
        double rr;
        Assert(RiskRewardEngine.TryCalculateNetRewardRisk(10.0, required, 0.5, out rr),
            "the cost-adjusted ratio should be calculable");
        Near(1.8, rr, 1e-10, "required target should meet the requested net R:R exactly");
        Assert(RiskRewardEngine.MeetsMinimumNetRewardRisk(rr, 1.8),
            "exact threshold should qualify");
        Assert(!RiskRewardEngine.MeetsMinimumNetRewardRisk(rr - 0.001, 1.8),
            "below-threshold setup must not qualify");
    }

    private static void NetRewardRiskCalculatesCorrectly()
    {
        double rr;
        Assert(RiskRewardEngine.TryCalculateNetRewardRisk(20.0, 40.5, 0.5, out rr),
            "valid ratio should calculate");
        Near(40.0 / 20.5, rr, 1e-10, "ratio formula must use net reward and cost-adjusted risk");
        Assert(!RiskRewardEngine.TryCalculateNetRewardRisk(20.0, 0.5, 0.5, out rr),
            "reward not beyond costs is invalid");
    }

    private static void StructuralCapsAreSymmetricForBuyAndSell()
    {
        double target;
        Assert(RiskRewardEngine.TryCapTargetBeforeBarrier(
            100.0, 130.0, 125.0, 1.0, 0.5, 1, out target),
            "Buy target should cap before resistance");
        Near(124.0, target, 1e-10, "Buy cap leaves buffer before resistance");

        Assert(RiskRewardEngine.TryCapTargetBeforeBarrier(
            100.0, 70.0, 75.0, 1.0, 0.5, -1, out target),
            "Sell target should cap before support");
        Near(76.0, target, 1e-10, "Sell cap leaves buffer before support");
    }

    private static void OutwardRoundedTargetsAreRecappedBeforeStructure()
    {
        double target;

        // These target prices represent an outward-rounded tick that has crossed the
        // structure even though the unrounded target was just before it.
        Assert(RiskRewardEngine.TryCapTargetBeforeBarrier(
            100.0, 100.01, 100.009, 0.001, 0.0001, 1, out target),
            "outward-rounded Buy TP crossing resistance must be recapped");
        Assert(target > 100.0 && target < 100.009,
            "recapped Buy target must remain between entry and resistance");

        Assert(RiskRewardEngine.TryCapTargetBeforeBarrier(
            100.0, 99.99, 99.991, 0.001, 0.0001, -1, out target),
            "outward-rounded Sell TP crossing support must be recapped");
        Assert(target < 100.0 && target > 99.991,
            "recapped Sell target must remain between support and entry");
    }

    private static void NearStructureIsNeverSilentlyIgnored()
    {
        double target;
        Assert(RiskRewardEngine.TryCapTargetBeforeBarrier(
            100.0, 120.0, 103.0, 5.0, 0.5, 1, out target),
            "near resistance must cap even if the preferred buffer does not fit");
        Assert(target > 100.0 && target < 103.0,
            "close-structure target stays between entry and resistance");

        double rr;
        Assert(!RiskRewardEngine.TryCalculateNetRewardRisk(10.0, target - 100.0, 0.5, out rr),
            "a target with no room beyond execution costs is invalid");
        Assert(!RiskRewardEngine.MeetsMinimumNetRewardRisk(0.0, 1.8),
            "tight structural target must remain a no-trade setup, not be extended through resistance");
    }

    private static void StructureBeyondProjectedTargetDoesNotCap()
    {
        double target;
        Assert(!RiskRewardEngine.TryCapTargetBeforeBarrier(
            100.0, 110.0, 115.0, 1.0, 0.5, 1, out target),
            "structure beyond target should not cap it");
        Near(110.0, target, 1e-10, "projected target remains unchanged without an intervening barrier");
    }

    private static void InvalidInputsAreRejected()
    {
        double value;
        Assert(!RiskRewardEngine.TryGetRequiredRewardDistance(0.0, 1.0, 1.8, out value),
            "zero risk should be rejected");
        Assert(!RiskRewardEngine.TryGetRequiredRewardDistance(10.0, double.NaN, 1.8, out value),
            "non-finite cost should be rejected");
        Assert(!RiskRewardEngine.TryCapTargetBeforeBarrier(100.0, 110.0, 105.0, 1.0, 0.5, 0, out value),
            "invalid direction should be rejected");
    }

    private static void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Near(double expected, double actual, double tolerance, string message)
    {
        Assert(Math.Abs(expected - actual) <= tolerance,
            message + " (expected " + expected + ", got " + actual + ")");
    }
}
