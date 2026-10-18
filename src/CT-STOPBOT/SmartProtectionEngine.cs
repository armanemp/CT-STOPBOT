using System;

namespace CTStopBot
{
    /// <summary>
    /// Deterministic helpers for the M5 smart-stop enhancements. Keeping these
    /// calculations independent of cTrader API objects makes their edge cases testable.
    /// </summary>
    internal static class SmartProtectionEngine
    {
        public static double GetEffectiveSpread(double currentSpread, double tickSize, double floorPrice)
        {
            if (!IsFinite(currentSpread) || currentSpread < 0.0)
                currentSpread = 0.0;
            if (!IsFinite(tickSize) || tickSize < 0.0)
                tickSize = 0.0;
            if (!IsFinite(floorPrice) || floorPrice < 0.0)
                floorPrice = 0.0;

            return Math.Max(tickSize, Math.Max(currentSpread, floorPrice));
        }

        public static bool IsSpreadSpike(
            double currentSpread,
            double averageSpread,
            double floorPrice,
            double spikeMultiple)
        {
            if (!IsFinite(currentSpread) || currentSpread <= 0.0 ||
                !IsFinite(spikeMultiple) || spikeMultiple <= 0.0)
                return false;

            var baseline = Math.Max(
                IsFinite(averageSpread) && averageSpread > 0.0 ? averageSpread : 0.0,
                IsFinite(floorPrice) && floorPrice > 0.0 ? floorPrice : 0.0);

            return baseline > 0.0 && currentSpread > baseline * spikeMultiple;
        }

        /// <summary>
        /// Clamps fast/slow ATR ratio to the configured volatility scale. Invalid or
        /// unavailable ATR values return neutral scale 1.0 rather than an aggressive value.
        /// </summary>
        public static double GetVolatilityScale(
            double fastAtr,
            double slowAtr,
            double minimumScale,
            double maximumScale)
        {
            if (!IsFinite(fastAtr) || !IsFinite(slowAtr) || fastAtr <= 0.0 || slowAtr <= 0.0)
                return 1.0;

            if (!IsFinite(minimumScale) || minimumScale <= 0.0)
                minimumScale = 1.0;
            if (!IsFinite(maximumScale) || maximumScale <= 0.0)
                maximumScale = 1.0;
            if (maximumScale < minimumScale)
                maximumScale = minimumScale;

            var ratio = fastAtr / slowAtr;
            if (!IsFinite(ratio) || ratio <= 0.0)
                return 1.0;

            return Math.Max(minimumScale, Math.Min(maximumScale, ratio));
        }

        public static double GetAdaptiveTrailingAtrMultiplier(
            double baseMultiplier,
            double fastAtr,
            double slowAtr,
            double minimumScale,
            double maximumScale,
            double favorableMove,
            double initialRisk,
            double tightenAfterR,
            double tightenFactor,
            bool smartEnhancementsEnabled)
        {
            if (!IsFinite(baseMultiplier) || baseMultiplier < 0.0)
                return 0.0;
            if (!smartEnhancementsEnabled)
                return baseMultiplier;

            var scale = GetVolatilityScale(fastAtr, slowAtr, minimumScale, maximumScale);
            if (IsFinite(tightenAfterR) && tightenAfterR > 0.0 &&
                IsFinite(initialRisk) && initialRisk > 0.0 &&
                IsFinite(favorableMove) && favorableMove >= initialRisk * tightenAfterR)
            {
                var factor = IsFinite(tightenFactor) ? Math.Max(0.3, Math.Min(1.0, tightenFactor)) : 1.0;
                scale *= factor;
            }

            var result = baseMultiplier * scale;
            return IsFinite(result) && result >= 0.0 ? result : baseMultiplier;
        }

        /// <summary>
        /// Applies optional initial-SL distance bounds from the executable quote. BUY
        /// stops must be below referencePrice; SELL stops must be above it. A floor
        /// widens a stop; a cap may move it inside the original wick, so the caller logs
        /// the adjustment and performs entry-side/broker-gap validation afterwards.
        /// </summary>
        public static bool TryApplyInitialStopAtrBounds(
            double referencePrice,
            double candidateStop,
            double atrPrice,
            double minimumAtrMultiple,
            double maximumAtrMultiple,
            bool isBuy,
            out double boundedStop,
            out string adjustment)
        {
            boundedStop = candidateStop;
            adjustment = string.Empty;

            if (!IsFinite(referencePrice) || !IsFinite(candidateStop) ||
                !IsFinite(atrPrice) || !IsFinite(minimumAtrMultiple) ||
                !IsFinite(maximumAtrMultiple) || referencePrice <= 0.0 ||
                atrPrice <= 0.0 || minimumAtrMultiple < 0.0 || maximumAtrMultiple < 0.0)
                return false;

            var direction = isBuy ? 1.0 : -1.0;
            var distance = direction * (referencePrice - candidateStop);
            if (!IsFinite(distance) || distance <= 0.0)
                return false;

            var minimumDistance = minimumAtrMultiple > 0.0 ? atrPrice * minimumAtrMultiple : 0.0;
            var maximumDistance = maximumAtrMultiple > 0.0 ? atrPrice * maximumAtrMultiple : double.MaxValue;
            if (maximumDistance < minimumDistance)
                maximumDistance = minimumDistance;

            var boundedDistance = distance;
            if (minimumDistance > 0.0 && distance < minimumDistance)
            {
                boundedDistance = minimumDistance;
                adjustment = "ATR_FLOOR";
            }
            else if (maximumAtrMultiple > 0.0 && distance > maximumDistance)
            {
                boundedDistance = maximumDistance;
                adjustment = "ATR_CAP";
            }

            boundedStop = referencePrice - direction * boundedDistance;
            return IsFinite(boundedStop) &&
                (isBuy ? boundedStop < referencePrice : boundedStop > referencePrice);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
