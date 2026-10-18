using System;

namespace CTStopBot
{
    /// <summary>
    /// Pure price-distance calculations shared by pre-trade guidance and open-position TP guidance.
    /// Keeps risk/reward arithmetic independent of cTrader chart and trading API objects.
    /// </summary>
    internal static class RiskRewardEngine
    {
        public static bool TryGetRequiredRewardDistance(
            double initialRiskDistance,
            double costReserve,
            double minimumNetRewardRisk,
            out double requiredRewardDistance)
        {
            requiredRewardDistance = 0.0;
            if (!IsFinite(initialRiskDistance) || !IsFinite(costReserve) ||
                !IsFinite(minimumNetRewardRisk) ||
                initialRiskDistance <= 0.0 || costReserve < 0.0 || minimumNetRewardRisk <= 0.0)
                return false;

            var result = minimumNetRewardRisk * (initialRiskDistance + costReserve) + costReserve;
            if (!IsFinite(result) || result <= costReserve)
                return false;

            requiredRewardDistance = result;
            return true;
        }

        public static bool TryCalculateNetRewardRisk(
            double riskDistance,
            double rewardDistance,
            double costReserve,
            out double netRewardRisk)
        {
            netRewardRisk = 0.0;
            if (!IsFinite(riskDistance) || !IsFinite(rewardDistance) ||
                !IsFinite(costReserve) || riskDistance <= 0.0 ||
                rewardDistance <= costReserve || costReserve < 0.0)
                return false;

            var denominator = riskDistance + costReserve;
            if (!IsFinite(denominator) || denominator <= 0.0)
                return false;

            var result = (rewardDistance - costReserve) / denominator;
            if (!IsFinite(result) || result <= 0.0)
                return false;

            netRewardRisk = result;
            return true;
        }

        /// <summary>
        /// Caps a projected target at the nearest opposing structure, leaving the desired
        /// price buffer where possible. If structure is unusually close, the buffer is
        /// reduced only as much as needed to keep the target on the profitable side of the
        /// reference and before the barrier. The caller must still reject low-R:R setups.
        /// Direction is +1 for Buy and -1 for Sell.
        /// </summary>
        public static bool TryCapTargetBeforeBarrier(
            double referencePrice,
            double projectedTarget,
            double barrierPrice,
            double desiredBuffer,
            double minimumForwardDistance,
            int direction,
            out double cappedTarget)
        {
            cappedTarget = projectedTarget;
            if (!IsFinite(referencePrice) || !IsFinite(projectedTarget) ||
                !IsFinite(barrierPrice) || !IsFinite(desiredBuffer) ||
                !IsFinite(minimumForwardDistance) ||
                desiredBuffer < 0.0 || minimumForwardDistance <= 0.0 ||
                (direction != 1 && direction != -1))
                return false;

            var barrierDistance = direction * (barrierPrice - referencePrice);
            var projectedDistance = direction * (projectedTarget - referencePrice);
            if (barrierDistance <= minimumForwardDistance ||
                projectedDistance <= barrierDistance)
                return false;

            // Respect structure even if the requested buffer would move the target
            // behind the reference. A close barrier should yield a low-R:R/no-trade
            // setup, not a target silently projected through the barrier.
            var availableBuffer = Math.Max(0.0, barrierDistance - minimumForwardDistance);
            var effectiveBuffer = Math.Min(desiredBuffer, availableBuffer);
            var result = barrierPrice - direction * effectiveBuffer;
            var resultDistance = direction * (result - referencePrice);
            if (!IsFinite(result) || resultDistance <= 0.0 ||
                resultDistance >= barrierDistance)
                return false;

            cappedTarget = result;
            return true;
        }

        public static bool MeetsMinimumNetRewardRisk(double rewardRisk, double minimumNetRewardRisk)
        {
            return IsFinite(rewardRisk) && IsFinite(minimumNetRewardRisk) &&
                minimumNetRewardRisk > 0.0 && rewardRisk >= minimumNetRewardRisk;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
