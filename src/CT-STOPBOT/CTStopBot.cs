using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace CTStopBot
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CTStopBot : Robot
    {
        [Parameter("Symbol name contains", DefaultValue = "XAUUSD")]
        public string SymbolNameFilter { get; set; }

        [Parameter("Restrict to label", DefaultValue = false)]
        public bool RestrictToLabel { get; set; }

        [Parameter("Managed position label", DefaultValue = "CT-STOPBOT")]
        public string ManagedLabel { get; set; }

        [Parameter("Enable initial stop placement", DefaultValue = true)]
        public bool EnableInitialStopPlacement { get; set; }

        [Parameter("Enable cost-aware break-even", DefaultValue = true)]
        public bool EnableBreakEven { get; set; }

        [Parameter("Enable structure trailing", DefaultValue = true)]
        public bool EnableStructureTrailing { get; set; }

        [Parameter("Manage positions with server-side trailing", DefaultValue = false)]
        public bool ManagePositionsWithServerTrailing { get; set; }

        [Parameter("Show suggested TP line (visual only)", DefaultValue = true)]
        public bool ShowSuggestedTpLine { get; set; }

        [Parameter("TP structure lookback (closed M5 candles)", DefaultValue = 30, MinValue = 5, MaxValue = 200)]
        public int TpStructureLookback { get; set; }

        [Parameter("TP base ATR multiplier", DefaultValue = 1.4, MinValue = 0.5, MaxValue = 5.0)]
        public double TpBaseAtrMultiplier { get; set; }

        [Parameter("TP trend extension ATR", DefaultValue = 0.8, MinValue = 0.0, MaxValue = 3.0)]
        public double TpTrendExtensionAtr { get; set; }

        [Parameter("TP minimum update move (ATR)", DefaultValue = 0.15, MinValue = 0.01, MaxValue = 2.0)]
        public double TpMinimumUpdateAtr { get; set; }

        [Parameter("TP minimum update move (pips)", DefaultValue = 3.0, MinValue = 0.1, MaxValue = 100.0)]
        public double TpMinimumUpdatePips { get; set; }

        [Parameter("TP update cooldown (seconds)", DefaultValue = 2, MinValue = 0, MaxValue = 30)]
        public int TpUpdateCooldownSeconds { get; set; }

        [Parameter("Show pre-trade SL/TP guidance and market conditions", DefaultValue = true)]
        public bool ShowPreTradeGuidance { get; set; }

        [Parameter("Maximum spread / ATR ratio for favorable setup", DefaultValue = 0.16, MinValue = 0.01, MaxValue = 1.0)]
        public double MaxSpreadToAtrRatio { get; set; }

        [Parameter("Pre-trade guide minimum update move (ATR)", DefaultValue = 0.12, MinValue = 0.01, MaxValue = 2.0)]
        public double GuideMinimumUpdateAtr { get; set; }

        [Parameter("Pre-trade guide minimum update move (pips)", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 100.0)]
        public double GuideMinimumUpdatePips { get; set; }

        [Parameter("Pre-trade guide update cooldown (seconds)", DefaultValue = 3, MinValue = 0, MaxValue = 120)]
        public int GuideUpdateCooldownSeconds { get; set; }

        [Parameter("Minimum pre-trade reward/risk filter", DefaultValue = 1.8, MinValue = 0.5, MaxValue = 5.0)]
        public double MinimumPreTradeRewardRisk { get; set; }

        [Parameter("Maximum pre-trade SL distance (ATR)", DefaultValue = 2.25, MinValue = 0.5, MaxValue = 10.0)]
        public double MaximumPreTradeStopDistanceAtr { get; set; }

        [Parameter("Confirmed pivot strength (bars each side)", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int ConfirmedPivotStrength { get; set; }

        [Parameter("Swing lookback (closed M5 candles)", DefaultValue = 5, MinValue = 2, MaxValue = 50)]
        public int SwingLookback { get; set; }

        [Parameter("ATR period", DefaultValue = 14, MinValue = 2, MaxValue = 100)]
        public int AtrPeriod { get; set; }

        [Parameter("Initial buffer: ATR multiplier", DefaultValue = 0.25, MinValue = 0.0, MaxValue = 3.0)]
        public double InitialAtrBuffer { get; set; }

        [Parameter("Trailing buffer: ATR multiplier", DefaultValue = 0.20, MinValue = 0.0, MaxValue = 3.0)]
        public double TrailingAtrBuffer { get; set; }

        [Parameter("Minimum wick buffer (pips)", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 100.0)]
        public double MinimumWickBufferPips { get; set; }

        [Parameter("Spread buffer multiplier", DefaultValue = 1.5, MinValue = 0.0, MaxValue = 10.0)]
        public double SpreadBufferMultiplier { get; set; }

        [Parameter("Break-even activation extra (pips)", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 100.0)]
        public double BreakEvenTriggerExtraPips { get; set; }

        [Parameter("Break-even spread multiplier", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 5.0)]
        public double BreakEvenSpreadMultiplier { get; set; }

        [Parameter("Round-trip commission multiplier", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 5.0)]
        public double RoundTripCommissionMultiplier { get; set; }

        [Parameter("Minimum round-trip commission reserve (account currency)", DefaultValue = 0.0, MinValue = 0.0)]
        public double MinimumRoundTripCommissionReserve { get; set; }

        [Parameter("Additional cost reserve (account currency)", DefaultValue = 0.0, MinValue = 0.0)]
        public double AdditionalCostReserve { get; set; }

        [Parameter("Slippage reserve (pips)", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 100.0)]
        public double SlippageReservePips { get; set; }

        [Parameter("Minimum net lock (pips)", DefaultValue = 0.5, MinValue = 0.0, MaxValue = 100.0)]
        public double MinimumNetLockPips { get; set; }

        [Parameter("Trailing step (pips)", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 100.0)]
        public double TrailingStepPips { get; set; }

        [Parameter("Minimum stop gap from market (pips)", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 500.0)]
        public double MinimumMarketGapPips { get; set; }

        [Parameter("Initial retry cooldown (seconds)", DefaultValue = 2, MinValue = 1, MaxValue = 30)]
        public int RetryCooldownSeconds { get; set; }

        [Parameter("Maximum retry cooldown (seconds)", DefaultValue = 60, MinValue = 2, MaxValue = 600)]
        public int MaximumRetryCooldownSeconds { get; set; }

        private AverageTrueRange _atr;
        private ExponentialMovingAverage _tpFastEma;
        private ExponentialMovingAverage _tpSlowEma;
        private ExponentialMovingAverage _tpTrendEma;
        private RelativeStrengthIndex _tpRsi;
        private readonly Dictionary<int, DateTime> _lastAttemptAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _lastNoticeAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, int> _failureCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, double> _suggestedTpPrices = new Dictionary<int, double>();
        private readonly Dictionary<int, DateTime> _suggestedTpUpdatedAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, string> _suggestedTpContexts = new Dictionary<int, string>();
        private readonly Dictionary<int, DateTime> _suggestedTpStartTimes = new Dictionary<int, DateTime>();
        private readonly Dictionary<string, double> _marketGuidePrices = new Dictionary<string, double>();
        private readonly Dictionary<string, DateTime> _marketGuideUpdatedAt = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _marketGuideStartTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, string> _marketGuideContexts = new Dictionary<string, string>();
        private const int GuideLineBarsLeft = 20;
        private const int GuideLineBarsRight = 20;
        private const double MinimumQualityRewardRisk = 1.8;
        private const double MaximumQualitySpreadToAtr = 0.16;
        private const double MaximumQualityStopDistanceAtr = 2.25;
        private const double MinimumQualityStopDistanceAtr = 0.25;
        private const double MinimumDirectionalAlignment = 0.65;
        protected override void OnStart()
        {
            if (TimeFrame != cAlgo.API.TimeFrame.Minute5)
            {
                Print("CT-STOPBOT stopped: attach it to an M5 chart. Current timeframe: {0}", TimeFrame);
                Stop();
                return;
            }

            if (string.IsNullOrWhiteSpace(SymbolNameFilter) ||
                SymbolName.IndexOf(SymbolNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Print("CT-STOPBOT stopped: symbol '{0}' does not contain configured filter '{1}'. Change the filter only if this is your broker's Gold symbol.",
                    SymbolName, SymbolNameFilter);
                Stop();
                return;
            }

            if (Symbol.PipSize <= 0 || Symbol.TickSize <= 0)
            {
                Print("CT-STOPBOT stopped: invalid symbol metadata. PipSize={0}, TickSize={1}.", Symbol.PipSize, Symbol.TickSize);
                Stop();
                return;
            }

            if (Symbol.PipValue <= 0)
                Print("WARNING: Symbol.PipValue is not positive; cost-aware break-even will be unavailable until the symbol/account metadata provides a valid value.");

            Chart.RemoveObject("CTSTOPBOT_MARKET_STATUS");

            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);
            _tpFastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 9);
            _tpSlowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
            _tpTrendEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 50);
            _tpRsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;

            var minDistanceUnit = Symbol.MinDistanceType == SymbolMinDistanceType.Pips ? "pips" : "percent";
            var spreadPips = Symbol.PipSize > 0 ? Symbol.Spread / Symbol.PipSize : 0.0;
            Print("CT-STOPBOT started on {0} {1}. Positions matching this symbol will be managed{2}.",
                SymbolName, TimeFrame, RestrictToLabel ? " only when label matches '" + ManagedLabel + "'" : " regardless of label");
            Print("Symbol diagnostics: Bid={0}, Ask={1}, spread={2:F2} pips, PipSize={3}, TickSize={4}, PipValue={5}; broker min SL distance={6} {7}.",
                Symbol.Bid, Symbol.Ask, spreadPips, Symbol.PipSize, Symbol.TickSize, Symbol.PipValue, Symbol.MinStopLossDistance, minDistanceUnit);
            Print("Management switches: initial stop={0}, break-even={1}, structure trailing={2}, manage native trailing positions={3}, label filter={4}.",
                EnableInitialStopPlacement, EnableBreakEven, EnableStructureTrailing, ManagePositionsWithServerTrailing, RestrictToLabel);
            Print("Suggested TP line: enabled={0} (visual only; the cBot never creates or changes a take-profit order).",
                ShowSuggestedTpLine);
            Print("Pre-trade guidance: enabled={0}; uses closed-candle EMA 9/21/50, RSI, momentum, ATR, spread, and recent market structure. Alignment score is heuristic, not a probability or entry order.",
                ShowPreTradeGuidance);

            // Protect eligible positions that were already open when the cBot started.
            foreach (var position in Positions)
                ManagePosition(position);

            UpdatePreTradeGuidance();

            // Timer retries failed/missing initial protection even during quiet ticks.
            Timer.Start(1);
        }

        protected override void OnTick()
        {
            ManageAllPositions();
        }

        protected override void OnTimer()
        {
            // Refresh guidance and manage stops without altering the user's chart viewport.
            ManageAllPositions();
        }

        protected override void OnStop()
        {
            Positions.Opened -= OnPositionOpened;
            Positions.Closed -= OnPositionClosed;

            foreach (var positionId in new List<int>(_suggestedTpPrices.Keys))
                RemoveSuggestedTpObjects(positionId);

            RemoveAllPreTradeGuideObjects();
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            if (IsManaged(args.Position))
            {
                ManagePosition(args.Position);
                UpdatePreTradeGuidance();
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            Print("Position {0} closed. Reason={1}.", args.Position.Id, args.Reason);
            _lastAttemptAt.Remove(args.Position.Id);
            _lastNoticeAt.Remove(args.Position.Id);
            _failureCounts.Remove(args.Position.Id);
            RemoveSuggestedTpObjects(args.Position.Id);
            UpdatePreTradeGuidance();
        }

        private void ManageAllPositions()
        {
            // Keep the pre-entry map fresh, then manage every matching open position
            // independently. The chart-only calculation cannot block SL management.
            UpdatePreTradeGuidance();

            foreach (var position in Positions)
                ManagePosition(position);
        }

        private bool IsManaged(Position position)
        {
            if (position == null)
                return false;

            if (!string.Equals(position.SymbolName, SymbolName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (RestrictToLabel &&
                !string.Equals(position.Label ?? string.Empty, ManagedLabel ?? string.Empty, StringComparison.Ordinal))
                return false;

            return true;
        }

        private void ManagePosition(Position position)
        {
            try
            {
                ManagePositionCore(position);
            }
            catch (Exception ex)
            {
                if (position != null)
                    NotifyThrottled(position.Id, "Unexpected exception during position management: " + ex);
                else
                    Print("CT-STOPBOT warning: unexpected exception while managing a null position: {0}", ex.Message);
            }
        }

        private void ManagePositionCore(Position position)
        {
            if (!IsManaged(position))
                return;

            // This chart-only recommendation is independent of stop management and
            // is evaluated even when a position uses a native server-side stop rule.
            UpdateSuggestedTakeProfit(position);

            if (position.StopLossBreakEven != null)
            {
                // This code path currently uses the legacy absolute-protection overload.
                // Newer cTrader docs list a typed overload with native break-even support,
                // but that overload must be verified against the pinned package and tested
                // before this bot can safely modify positions carrying a native BE rule.
                // Until then, leave these positions untouched.
                NotifyThrottled(position.Id,
                    "Position has a native server-side break-even rule. The current modification path has not been verified to preserve it, so CT-STOPBOT is leaving the position untouched.");
                return;
            }

            if (position.HasTrailingStop && !ManagePositionsWithServerTrailing)
            {
                NotifyThrottled(position.Id,
                    "Position has a server-side trailing stop. CT-STOPBOT is leaving it untouched; enable the corresponding parameter only after testing how both trailing mechanisms interact.");
                return;
            }

            var minimumBarsRequired = Math.Max(SwingLookback + 1, AtrPeriod + 2);
            if (Bars.Count < minimumBarsRequired)
            {
                NotifyThrottled(position.Id,
                    string.Format("Not enough M5 history: {0} bars available, at least {1} required for the configured swing/ATR settings. Stop calculation is waiting.",
                        Bars.Count, minimumBarsRequired));
                return;
            }

            // Never overwrite an existing stop with the initial structural stop.
            if (!position.StopLoss.HasValue)
            {
                if (!EnableInitialStopPlacement)
                {
                    NotifyThrottled(position.Id,
                        "Initial stop placement is disabled. This position remains unprotected unless another mechanism has installed a stop.");
                    return;
                }

                double initialStop;
                if (!TryCalculateStructuralStop(position, InitialAtrBuffer, true, out initialStop))
                {
                    NotifyThrottled(position.Id,
                        "Initial stop was not submitted: no safe closed-candle swing price is currently available. The position may remain unprotected; check logs and broker rules.");
                    return;
                }

                TryModifyStop(position, initialStop, true, 0.0, "initial wick/ATR stop");
                return;
            }

            var currentStop = position.StopLoss.Value;
            if (!IsSafeRelativeToMarket(currentStop, position.TradeType))
            {
                NotifyThrottled(position.Id,
                    "Existing stop is currently inside the configured/broker market gap or has been crossed. CT-STOPBOT will not widen it; inspect the live position and broker rules.");
            }

            var bestStop = currentStop;
            var hasBetterStop = false;
            var breakEvenUpgrade = false;
            var reason = "structure trailing";

            // Cost-aware break-even estimate. Do not guess if pip value is unavailable.
            double requiredOffsetPips;
            if (EnableBreakEven &&
                TryGetRequiredBreakEvenOffsetPips(position, out requiredOffsetPips) &&
                position.Pips >= requiredOffsetPips + BreakEvenTriggerExtraPips)
            {
                var breakEvenPrice = position.TradeType == TradeType.Buy
                    ? position.EntryPrice + requiredOffsetPips * Symbol.PipSize
                    : position.EntryPrice - requiredOffsetPips * Symbol.PipSize;

                breakEvenPrice = NormalizeBreakEvenPrice(breakEvenPrice, position.TradeType);

                if (IsSafeRelativeToMarket(breakEvenPrice, position.TradeType) &&
                    IsMoreProtective(breakEvenPrice, bestStop, position.TradeType))
                {
                    bestStop = breakEvenPrice;
                    hasBetterStop = true;
                    breakEvenUpgrade = true;
                    reason = "cost-aware break-even";
                }
            }

            // Trail behind recent CLOSED candle structure. Use max for Buy / min
            // for Sell so trailing cannot loosen a stop or a break-even level.
            double trailingStop;
            if (EnableStructureTrailing &&
                TryCalculateStructuralStop(position, TrailingAtrBuffer, false, out trailingStop) &&
                IsSafeRelativeToMarket(trailingStop, position.TradeType) &&
                IsMoreProtective(trailingStop, bestStop, position.TradeType))
            {
                bestStop = trailingStop;
                hasBetterStop = true;
                // If the structural level supersedes the break-even candidate,
                // it must still satisfy the configured trailing step.
                breakEvenUpgrade = false;
                reason = "wick trailing";
            }

            if (!hasBetterStop)
                return;

            var improvementThreshold = breakEvenUpgrade
                ? Symbol.TickSize / Symbol.PipSize
                : TrailingStepPips;

            if (!ImprovesExistingStop(bestStop, currentStop, position.TradeType, improvementThreshold))
                return;

            TryModifyStop(position, bestStop, false, improvementThreshold, reason);
        }


        private void UpdateSuggestedTakeProfit(Position position)
        {
            try
            {
                if (!ShowSuggestedTpLine)
                {
                    RemoveSuggestedTpObjects(position.Id);
                    return;
                }

                double candidate;
                double atrPrice;
                string context;
                if (!TryCalculateSuggestedTakeProfit(position, out candidate, out atrPrice, out context))
                {
                    RemoveSuggestedTpObjects(position.Id);
                    NotifyThrottled(position.Id,
                        "Suggested TP line is waiting for valid M5 history/market data. No take-profit order was changed.");
                    return;
                }

                double liveRewardRisk;
                if (position.StopLoss.HasValue &&
                    IsStopStillOnLossSide(position) &&
                    !TryGetCostAdjustedRewardRisk(position.EntryPrice, position.StopLoss.Value, candidate,
                        position.TradeType, out liveRewardRisk))
                {
                    RemoveSuggestedTpObjects(position.Id);
                    NotifyThrottled(position.Id,
                        "Suggested TP hidden: target does not meet the minimum cost-adjusted reward/risk filter against the current SL. No TP order was changed.");
                    return;
                }

                double previousPrice;
                var hasPrevious = _suggestedTpPrices.TryGetValue(position.Id, out previousPrice);
                var targetWasReached = hasPrevious && IsSuggestedTargetReached(previousPrice, position.TradeType);

                if (hasPrevious && !targetWasReached)
                {
                    var minimumMove = Math.Max(TpMinimumUpdatePips * Symbol.PipSize, atrPrice * TpMinimumUpdateAtr);
                    var movement = Math.Abs(candidate - previousPrice);

                    // Keep the price stable through small quote/indicator noise. A change
                    // in the market regime may update the label without moving the line.
                    if (movement < minimumMove)
                    {
                        string previousContext;
                        var contextChanged = !_suggestedTpContexts.TryGetValue(position.Id, out previousContext) ||
                            !string.Equals(previousContext, context, StringComparison.Ordinal);
                        DateTime lastAnchor;
                        var newBar = !_suggestedTpStartTimes.TryGetValue(position.Id, out lastAnchor) ||
                            lastAnchor != Bars.OpenTimes[Bars.Count - 1];
                        if (contextChanged || newBar)
                        {
                            DrawSuggestedTpObjects(position, previousPrice, context);
                            _suggestedTpContexts[position.Id] = context;
                        }

                        return;
                    }

                    DateTime lastUpdate;
                    if (TpUpdateCooldownSeconds > 0 &&
                        _suggestedTpUpdatedAt.TryGetValue(position.Id, out lastUpdate) &&
                        (Server.Time - lastUpdate).TotalSeconds < TpUpdateCooldownSeconds)
                        return;
                }

                DrawSuggestedTpObjects(position, candidate, context);
                _suggestedTpPrices[position.Id] = candidate;
                _suggestedTpUpdatedAt[position.Id] = Server.Time;
                _suggestedTpContexts[position.Id] = context;
            }
            catch (Exception ex)
            {
                // A chart-drawing failure must never prevent the independent SL manager
                // from running for this position.
                NotifyThrottled(position.Id, "Suggested TP drawing failed; stop management continues. Details: " + ex.Message);
            }
        }

        private bool TryCalculateSuggestedTakeProfit(Position position, out double targetPrice, out double atrPrice, out string context)
        {
            targetPrice = 0.0;
            atrPrice = 0.0;
            context = "MIXED";

            if (Symbol.PipSize <= 0 || Symbol.TickSize <= 0 ||
                Symbol.Bid <= 0 || Symbol.Ask <= 0 || Symbol.Ask < Symbol.Bid)
                return false;

            var lastClosedIndex = Bars.Count - 2;
            var minimumBarsRequired = Math.Max(TpStructureLookback + 1, Math.Max(AtrPeriod + 2, 23));
            if (lastClosedIndex < 3 || Bars.Count < minimumBarsRequired)
                return false;

            atrPrice = _atr != null && lastClosedIndex < _atr.Result.Count
                ? _atr.Result[lastClosedIndex]
                : 0.0;
            if (double.IsNaN(atrPrice) || double.IsInfinity(atrPrice) || atrPrice <= 0.0)
                return false;

            var fastEma = _tpFastEma.Result[lastClosedIndex];
            var slowEma = _tpSlowEma.Result[lastClosedIndex];
            var rsi = _tpRsi.Result[lastClosedIndex];
            if (double.IsNaN(fastEma) || double.IsInfinity(fastEma) ||
                double.IsNaN(slowEma) || double.IsInfinity(slowEma) ||
                double.IsNaN(rsi) || double.IsInfinity(rsi))
                return false;

            // The TP distance is anchored to entry so the line does not chase every tick.
            // Only after price reaches/passes that level do we propose a fresh forward target.
            var direction = position.TradeType == TradeType.Buy ? 1.0 : -1.0;
            var referencePrice = position.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;

            // Combine trend (EMA 9/21), three-closed-bar momentum and RSI. All inputs use
            // closed candles only, preventing the signal itself from repainting every tick.
            var momentumIndex = Math.Max(0, lastClosedIndex - 3);
            var trendScore = ClampValue(((fastEma - slowEma) / atrPrice) / 0.45, -1.0, 1.0);
            var momentumScore = ClampValue(((Bars.ClosePrices[lastClosedIndex] - Bars.ClosePrices[momentumIndex]) / atrPrice) / 0.8, -1.0, 1.0);
            var rsiScore = ClampValue((rsi - 50.0) / 20.0, -1.0, 1.0);
            var directionalScore = ClampValue(direction * (0.45 * trendScore + 0.35 * momentumScore + 0.20 * rsiScore), -1.0, 1.0);

            var atrMultiplier = ClampValue(TpBaseAtrMultiplier + directionalScore * TpTrendExtensionAtr, 0.6, 4.0);
            var minimumTargetDistance = Math.Max(Symbol.TickSize * 3.0,
                Math.Max(Symbol.Spread * 1.5, atrPrice * 0.35));
            var projectedDistance = Math.Max(atrPrice * atrMultiplier, minimumTargetDistance);
            var target = position.EntryPrice + direction * projectedDistance;
            var extendedAfterTarget = false;

            // If the entry-based target has already been reached, suggest the next
            // forward objective instead of leaving a stale line behind market price.
            if (direction * (target - referencePrice) <= minimumTargetDistance)
            {
                target = referencePrice + direction * projectedDistance;
                extendedAfterTarget = true;
            }

            // Cap a farther ATR projection at the nearest relevant recent resistance/support,
            // with a small buffer before that level. Ignore levels that are too close to price.
            var firstIndex = lastClosedIndex - TpStructureLookback + 1;
            double nearestLevel;
            var foundLevel = TryFindNearestTargetPivot(
                position.TradeType, lastClosedIndex, TpStructureLookback,
                referencePrice, minimumTargetDistance, out nearestLevel);
            var usedConfirmedPivot = foundLevel;
            if (!foundLevel)
                nearestLevel = position.TradeType == TradeType.Buy ? double.MaxValue : double.MinValue;
            for (var i = firstIndex; !usedConfirmedPivot && i <= lastClosedIndex; i++)
            {
                if (position.TradeType == TradeType.Buy)
                {
                    var high = Bars.HighPrices[i];
                    if (high > referencePrice + minimumTargetDistance && high < nearestLevel)
                    {
                        nearestLevel = high;
                        foundLevel = true;
                    }
                }
                else
                {
                    var low = Bars.LowPrices[i];
                    if (low < referencePrice - minimumTargetDistance && low > nearestLevel)
                    {
                        nearestLevel = low;
                        foundLevel = true;
                    }
                }
            }

            var usedStructure = false;
            if (foundLevel)
            {
                var structureBuffer = Math.Max(Symbol.TickSize * 2.0, Math.Max(Symbol.Spread, atrPrice * 0.05));
                var structureTarget = nearestLevel - direction * structureBuffer;
                var structureDistance = direction * (structureTarget - referencePrice);
                var projectedDistanceFromMarket = direction * (target - referencePrice);

                if (structureDistance >= minimumTargetDistance &&
                    structureDistance < projectedDistanceFromMarket)
                {
                    target = structureTarget;
                    usedStructure = true;
                }
            }

            targetPrice = NormalizeSuggestedTpPrice(target, position.TradeType);

            // A final direction check guards against rounding or a rapidly moving quote.
            if (direction * (targetPrice - referencePrice) <= 0.0)
                return false;

            var alignment = directionalScore >= 0.35 ? "ALIGNED" :
                directionalScore <= -0.35 ? "COUNTER" : "MIXED";
            context = alignment + (usedStructure ? "/STRUCTURE" : "/ATR") +
                (extendedAfterTarget ? "/NEXT" : string.Empty);
            return !double.IsNaN(targetPrice) && !double.IsInfinity(targetPrice);
        }

        private double NormalizeTargetBeforeStructureBarrier(double price, TradeType tradeType)
        {
            if (Symbol.TickSize <= 0)
                return Math.Round(price, Symbol.Digits, MidpointRounding.AwayFromZero);

            // Keep a structure-capped Buy target below resistance and a Sell target
            // above support. Do not round a capped target through the barrier.
            var ticks = price / Symbol.TickSize;
            var normalized = tradeType == TradeType.Buy
                ? Math.Floor(ticks + 1e-10) * Symbol.TickSize
                : Math.Ceiling(ticks - 1e-10) * Symbol.TickSize;
            return Math.Round(normalized, Symbol.Digits, MidpointRounding.AwayFromZero);
        }

        private double NormalizeSuggestedTpPrice(double price, TradeType tradeType)
        {
            if (Symbol.TickSize <= 0)
                return Math.Round(price, Symbol.Digits, MidpointRounding.AwayFromZero);

            // Suggested take profit is rounded outward in the trade's profit direction.
            var ticks = price / Symbol.TickSize;
            var normalized = tradeType == TradeType.Buy
                ? Math.Ceiling(ticks - 1e-10) * Symbol.TickSize
                : Math.Floor(ticks + 1e-10) * Symbol.TickSize;
            return Math.Round(normalized, Symbol.Digits, MidpointRounding.AwayFromZero);
        }

        private void DrawSuggestedTpObjects(Position position, double targetPrice, string context)
        {
            var currentBarIndex = Bars.Count - 1;
            var startBarIndex = Math.Max(0, currentBarIndex - GuideLineBarsLeft);
            var endBarIndex = currentBarIndex + GuideLineBarsRight;
            var color = Color.FromHex("#438D7F");
            var lineName = GetSuggestedTpLineName(position.Id);
            var textName = GetSuggestedTpTextName(position.Id);
            var line = Chart.DrawTrendLine(lineName, startBarIndex, targetPrice, endBarIndex, targetPrice,
                color, 1, LineStyle.Solid);
            line.ExtendToInfinity = false;

            var distancePips = Symbol.PipSize > 0 ? Math.Abs(targetPrice - position.EntryPrice) / Symbol.PipSize : 0.0;
            var labelOffset = GetGuideLabelOffsetPrice(GetCurrentAtrPrice());
            var labelPrice = position.TradeType == TradeType.Buy
                ? targetPrice + labelOffset
                : targetPrice - labelOffset;
            var labelAbove = position.TradeType == TradeType.Buy;
            var label = Chart.DrawText(textName,
                string.Format("TP | {0:F1} pips", distancePips),
                endBarIndex, labelPrice, color);
            label.FontSize = 14;
            label.FontFamily = "Segoe UI Light";
            label.IsBold = false;
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.VerticalAlignment = labelAbove ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            _suggestedTpStartTimes[position.Id] = Bars.OpenTimes[Bars.Count - 1];
        }

        private bool IsSuggestedTargetReached(double targetPrice, TradeType tradeType)
        {
            return tradeType == TradeType.Buy
                ? Symbol.Bid >= targetPrice
                : Symbol.Ask <= targetPrice;
        }

        private string GetSuggestedTpLineName(int positionId)
        {
            return "CTSTOPBOT_TP_LINE_" + positionId;
        }

        private string GetSuggestedTpTextName(int positionId)
        {
            return "CTSTOPBOT_TP_TEXT_" + positionId;
        }

        private void RemoveSuggestedTpObjects(int positionId)
        {
            Chart.RemoveObject(GetSuggestedTpLineName(positionId));
            Chart.RemoveObject(GetSuggestedTpTextName(positionId));
            _suggestedTpPrices.Remove(positionId);
            _suggestedTpUpdatedAt.Remove(positionId);
            _suggestedTpContexts.Remove(positionId);
            _suggestedTpStartTimes.Remove(positionId);
        }

        private double ClampValue(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }


        private void UpdatePreTradeGuidance()
        {
            try
            {
                if (!ShowPreTradeGuidance)
                {
                    RemoveAllPreTradeGuideObjects();
                    return;
                }

                double signalScore;
                double atrPrice;
                double rsi;
                double spreadToAtr;
                double momentumScore;
                string trendText;
                string momentumText;
                string volatilityText;
                bool overextended;

                if (!TryGetMarketSignal(out signalScore, out atrPrice, out rsi, out spreadToAtr,
                    out momentumScore, out trendText, out momentumText, out volatilityText, out overextended))
                {
                    RemoveMarketGuideObject("BUY_ENTRY");
                    RemoveMarketGuideObject("BUY_SL");
                    RemoveMarketGuideObject("BUY_TP");
                    RemoveMarketGuideObject("SELL_ENTRY");
                    RemoveMarketGuideObject("SELL_SL");
                    RemoveMarketGuideObject("SELL_TP");
                    return;
                }

                string buyStatus;
                string sellStatus;
                UpdateOneSideGuidance(TradeType.Buy, signalScore, atrPrice, rsi, spreadToAtr,
                    trendText, momentumText, overextended, out buyStatus);
                UpdateOneSideGuidance(TradeType.Sell, signalScore, atrPrice, rsi, spreadToAtr,
                    trendText, momentumText, overextended, out sellStatus);

            }
            catch (Exception ex)
            {
                // Never allow a drawing/indicator exception to stop the protection loop.
                Print("WARNING: pre-trade guidance refresh failed; open-position stop management continues. {0}", ex.Message);
            }
        }

        private bool TryGetMarketSignal(
            out double signalScore,
            out double atrPrice,
            out double rsi,
            out double spreadToAtr,
            out double momentumScore,
            out string trendText,
            out string momentumText,
            out string volatilityText,
            out bool overextended)
        {
            signalScore = 0.0;
            atrPrice = 0.0;
            rsi = 50.0;
            spreadToAtr = 0.0;
            momentumScore = 0.0;
            trendText = "UNKNOWN";
            momentumText = "UNKNOWN";
            volatilityText = "UNKNOWN";
            overextended = false;

            if (Symbol.PipSize <= 0 || Symbol.TickSize <= 0 ||
                Symbol.Bid <= 0 || Symbol.Ask <= 0 || Symbol.Ask < Symbol.Bid)
                return false;

            var lastClosedIndex = Bars.Count - 2;
            var minimumBarsRequired = Math.Max(52, Math.Max(TpStructureLookback + 1, AtrPeriod + 2));
            if (lastClosedIndex < 50 || Bars.Count < minimumBarsRequired)
                return false;

            atrPrice = _atr != null && lastClosedIndex < _atr.Result.Count
                ? _atr.Result[lastClosedIndex]
                : 0.0;
            rsi = _tpRsi != null && lastClosedIndex < _tpRsi.Result.Count
                ? _tpRsi.Result[lastClosedIndex]
                : 50.0;

            if (!IsFinitePositive(atrPrice) || double.IsNaN(rsi) || double.IsInfinity(rsi))
                return false;

            var fastEma = _tpFastEma.Result[lastClosedIndex];
            var slowEma = _tpSlowEma.Result[lastClosedIndex];
            var trendEma = _tpTrendEma.Result[lastClosedIndex];
            if (!IsFiniteNumber(fastEma) || !IsFiniteNumber(slowEma) || !IsFiniteNumber(trendEma))
                return false;

            var shortTrendScore = ClampValue(((fastEma - slowEma) / atrPrice) / 0.45, -1.0, 1.0);
            var longTrendScore = ClampValue(((slowEma - trendEma) / atrPrice) / 0.75, -1.0, 1.0);
            var trendScore = ClampValue(0.65 * shortTrendScore + 0.35 * longTrendScore, -1.0, 1.0);

            var momentumIndex = Math.Max(0, lastClosedIndex - 3);
            momentumScore = ClampValue(
                ((Bars.ClosePrices[lastClosedIndex] - Bars.ClosePrices[momentumIndex]) / atrPrice) / 0.8,
                -1.0, 1.0);

            var rsiScore = ClampValue((rsi - 50.0) / 20.0, -1.0, 1.0);
            var candleScore = ClampValue(
                ((Bars.ClosePrices[lastClosedIndex] - Bars.OpenPrices[lastClosedIndex]) / atrPrice) / 0.5,
                -1.0, 1.0);

            signalScore = ClampValue(
                0.40 * trendScore + 0.30 * momentumScore + 0.20 * rsiScore + 0.10 * candleScore,
                -1.0, 1.0);

            // Penalize very stretched momentum instead of automatically treating an
            // extreme RSI reading as stronger trend confirmation.
            if (rsi > 75.0 && signalScore > 0.0)
            {
                signalScore = ClampValue(signalScore - Math.Min(0.20, (rsi - 75.0) / 100.0), -1.0, 1.0);
                overextended = true;
            }
            else if (rsi < 25.0 && signalScore < 0.0)
            {
                signalScore = ClampValue(signalScore + Math.Min(0.20, (25.0 - rsi) / 100.0), -1.0, 1.0);
                overextended = true;
            }

            spreadToAtr = Symbol.Spread / atrPrice;
            if (!IsFiniteNumber(spreadToAtr) || spreadToAtr < 0.0)
                return false;

            trendText = trendScore >= 0.25 ? "BULLISH" :
                trendScore <= -0.25 ? "BEARISH" : "MIXED";
            momentumText = momentumScore >= 0.20 ? "UP" :
                momentumScore <= -0.20 ? "DOWN" : "FLAT";
            
            var atrSum = 0.0;
            var atrCount = 0;
            var firstAtrIndex = Math.Max(0, lastClosedIndex - 19);
            for (var i = firstAtrIndex; i <= lastClosedIndex; i++)
            {
                var value = _atr.Result[i];
                if (IsFinitePositive(value))
                {
                    atrSum += value;
                    atrCount++;
                }
            }

            var averageAtr = atrCount > 0 ? atrSum / atrCount : atrPrice;
            var atrRatio = averageAtr > 0.0 ? atrPrice / averageAtr : 1.0;
            volatilityText = atrRatio >= 1.25 ? "HIGH" :
                atrRatio <= 0.75 ? "LOW" : "NORMAL";

            return true;
        }

        private void UpdateOneSideGuidance(
            TradeType tradeType,
            double signalScore,
            double atrPrice,
            double rsi,
            double spreadToAtr,
            string trendText,
            string momentumText,
            bool overextended,
            out string sideStatus)
        {
            var sideKey = tradeType == TradeType.Buy ? "BUY" : "SELL";
            var totalPositions = 0;
            var unprotectedPositions = 0;
            GetManagedPositionProtectionCounts(tradeType, out totalPositions, out unprotectedPositions);

            double entryPrice;
            double stopPrice;
            double targetPrice;
            double rewardRisk;
            double stopDistanceAtr;
            bool stopIsSafe;
            bool stopUsesPivot;
            string setupContext;

            var calculated = TryCalculateMarketSideGuidance(tradeType, signalScore, atrPrice, overextended,
                trendText, momentumText,
                out entryPrice, out stopPrice, out targetPrice, out rewardRisk,
                out stopDistanceAtr, out stopIsSafe, out stopUsesPivot, out setupContext);

            var setupQuality = GetSetupQuality(tradeType, signalScore, spreadToAtr, overextended);
            var sideAlignment = tradeType == TradeType.Buy ? signalScore : -signalScore;
            var trendSupportsSide = tradeType == TradeType.Buy ? trendText == "BULLISH" : trendText == "BEARISH";
            var momentumSupportsSide = tradeType == TradeType.Buy ? momentumText == "UP" : momentumText == "DOWN";
            var direction = tradeType == TradeType.Buy ? 1.0 : -1.0;
            var minimumStructuralRisk = Math.Max(atrPrice * 0.25,
                Math.Max(Symbol.Spread * 2.0, Symbol.TickSize * 4.0));
            var directionalRisk = direction * (entryPrice - stopPrice);
            var directionalReward = direction * (targetPrice - entryPrice);
            var stopOnCorrectSide = calculated && directionalRisk >= minimumStructuralRisk;
            var targetOnCorrectSide = calculated &&
                directionalReward > Math.Max(Symbol.TickSize * 2.0, Symbol.Spread * 1.5);
            var requiredMinimumRewardRisk = Math.Max(MinimumQualityRewardRisk, MinimumPreTradeRewardRisk);
            var maximumAllowedSpreadToAtr = Math.Min(MaximumQualitySpreadToAtr, MaxSpreadToAtrRatio);
            var maximumAllowedStopDistanceAtr = Math.Min(MaximumQualityStopDistanceAtr, MaximumPreTradeStopDistanceAtr);

            var highConfidence = calculated && stopIsSafe && stopOnCorrectSide && targetOnCorrectSide &&
                !overextended && spreadToAtr <= maximumAllowedSpreadToAtr &&
                stopDistanceAtr >= MinimumQualityStopDistanceAtr && stopDistanceAtr <= maximumAllowedStopDistanceAtr &&
                rewardRisk >= requiredMinimumRewardRisk &&
                sideAlignment >= MinimumDirectionalAlignment && trendSupportsSide && momentumSupportsSide;

            if (!calculated)
                setupQuality = "WAIT — no valid structural setup";
            else if (!stopIsSafe)
                setupQuality = "WAIT — SL invalid at current quote/broker gap";
            else if (!stopOnCorrectSide)
                setupQuality = "WAIT — SL is too close or on the wrong side of ENTRY";
            else if (!targetOnCorrectSide)
                setupQuality = "WAIT — TP is too close or on the wrong side of ENTRY";
            else if (stopDistanceAtr < MinimumQualityStopDistanceAtr)
                setupQuality = "WAIT — structural SL is too tight/noise-sensitive";
            else if (stopDistanceAtr > Math.Min(MaximumQualityStopDistanceAtr, MaximumPreTradeStopDistanceAtr))
                setupQuality = "WAIT — structural SL too wide";
            else if (rewardRisk < Math.Max(MinimumQualityRewardRisk, MinimumPreTradeRewardRisk))
                setupQuality = "WAIT — cost-adjusted reward/risk below filter";
            else if (spreadToAtr > Math.Min(MaximumQualitySpreadToAtr, MaxSpreadToAtrRatio))
                setupQuality = "WAIT — spread/ATR too high";
            else if (sideAlignment < MinimumDirectionalAlignment)
                setupQuality = "WAIT — directional alignment is not strong enough";
            else if (overextended)
                setupQuality = "WAIT — RSI/momentum stretched";
            else if (!trendSupportsSide)
                setupQuality = "WAIT — trend structure does not confirm this side";
            else if (!momentumSupportsSide)
                setupQuality = "WAIT — closed-candle momentum is not confirmed";
            else
                setupQuality = "QUALIFIED — structure, trend, momentum and R:R passed";

            if (totalPositions == 0)
            {
                if (!calculated)
                {
                    RemoveMarketGuideObject(sideKey + "_ENTRY");
                    RemoveMarketGuideObject(sideKey + "_SL");
                    RemoveMarketGuideObject(sideKey + "_TP");
                    sideStatus = "waiting for valid data";
                    return;
                }

                if (!highConfidence)
                {
                    RemoveMarketGuideObject(sideKey + "_ENTRY");
                    RemoveMarketGuideObject(sideKey + "_SL");
                    RemoveMarketGuideObject(sideKey + "_TP");
                    sideStatus = setupQuality;
                    return;
                }

                var marketReferencePrice = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
                var entryColor = highConfidence
                    ? Color.FromHex("#E8C45B")
                    : tradeType == TradeType.Buy ? Color.FromHex("#477C89") : Color.FromHex("#936174");
                var stopColor = highConfidence ? Color.FromHex("#D8757B") : Color.FromHex("#A45F68");
                var targetColor = highConfidence ? Color.FromHex("#55B79E") : Color.FromHex("#438D7F");

                DrawOrUpdateMarketGuide(sideKey + "_ENTRY", tradeType, entryPrice,
                    marketReferencePrice, atrPrice, sideKey + " ENTRY", entryColor,
                    false, true, highConfidence);

                double displayedEntryPrice;
                if (!_marketGuidePrices.TryGetValue(sideKey + "_ENTRY", out displayedEntryPrice))
                    displayedEntryPrice = entryPrice;

                // Recalculate risk/reward from the exact ENTRY price currently displayed.
                // The entry line is smoothed to avoid jitter; never let an old displayed
                // entry coexist with SL/TP levels whose geometry now fails the hard filters.
                double displayedRewardRisk;
                var displayedStopDistanceAtr = atrPrice > 0.0
                    ? Math.Abs(displayedEntryPrice - stopPrice) / atrPrice
                    : double.MaxValue;
                if (!TryGetCostAdjustedRewardRisk(displayedEntryPrice, stopPrice, targetPrice,
                        tradeType, out displayedRewardRisk) ||
                    displayedStopDistanceAtr < MinimumQualityStopDistanceAtr ||
                    displayedStopDistanceAtr > Math.Min(MaximumQualityStopDistanceAtr, MaximumPreTradeStopDistanceAtr))
                {
                    RemoveMarketGuideObject(sideKey + "_ENTRY");
                    RemoveMarketGuideObject(sideKey + "_SL");
                    RemoveMarketGuideObject(sideKey + "_TP");
                    sideStatus = "WAIT — displayed ENTRY/SL/TP geometry failed the cost-adjusted R:R filter";
                    return;
                }

                // From this point the reported R:R/risk distance matches the displayed
                // ENTRY/SL/TP prices, rather than only the unsmoothed candidate values.
                rewardRisk = displayedRewardRisk;
                stopDistanceAtr = displayedStopDistanceAtr;

                if (stopIsSafe)
                {
                    DrawOrUpdateMarketGuide(sideKey + "_SL", tradeType, stopPrice,
                        displayedEntryPrice, atrPrice, sideKey + " SL", stopColor,
                        false, false, highConfidence);
                }
                else
                {
                    RemoveMarketGuideObject(sideKey + "_SL");
                }

                if (ShowSuggestedTpLine)
                {
                    DrawOrUpdateMarketGuide(sideKey + "_TP", tradeType, targetPrice,
                        displayedEntryPrice, atrPrice, sideKey + " TP", targetColor,
                        true, false, highConfidence);
                }
                else
                {
                    RemoveMarketGuideObject(sideKey + "_TP");
                }

                // Final acceptance uses the prices actually retained by the chart objects.
                // Independent smoothing of ENTRY, SL and TP must never leave a displayed
                // ratio that differs from the ratio accepted by the setup calculator.
                double renderedStopPrice;
                double renderedTargetPrice = targetPrice;
                double renderedRewardRisk;
                var hasRenderedStop = _marketGuidePrices.TryGetValue(
                    sideKey + "_SL", out renderedStopPrice);
                var hasRenderedTarget = !ShowSuggestedTpLine ||
                    _marketGuidePrices.TryGetValue(sideKey + "_TP", out renderedTargetPrice);
                var renderedStopDistanceAtr = atrPrice > 0.0
                    ? Math.Abs(displayedEntryPrice - renderedStopPrice) / atrPrice
                    : double.MaxValue;
                var renderedDirectionalRisk = direction * (displayedEntryPrice - renderedStopPrice);
                var renderedDirectionalReward = direction * (renderedTargetPrice - displayedEntryPrice);
                if (!hasRenderedStop || !hasRenderedTarget ||
                    renderedDirectionalRisk < minimumStructuralRisk ||
                    renderedDirectionalReward <= Math.Max(Symbol.TickSize * 2.0, Symbol.Spread * 1.5) ||
                    renderedStopDistanceAtr < MinimumQualityStopDistanceAtr ||
                    renderedStopDistanceAtr > Math.Min(MaximumQualityStopDistanceAtr, MaximumPreTradeStopDistanceAtr) ||
                    !TryGetCostAdjustedRewardRisk(displayedEntryPrice, renderedStopPrice,
                        renderedTargetPrice, tradeType, out renderedRewardRisk))
                {
                    RemoveMarketGuideObject(sideKey + "_ENTRY");
                    RemoveMarketGuideObject(sideKey + "_SL");
                    RemoveMarketGuideObject(sideKey + "_TP");
                    sideStatus = "WAIT — displayed ENTRY/SL/TP levels fail risk/reward geometry";
                    return;
                }

                rewardRisk = renderedRewardRisk;
                stopDistanceAtr = renderedStopDistanceAtr;
                sideStatus = string.Format("QUALIFIED | net R:R {0:F2} | risk {1:F2} ATR | displayed prices validated",
                    rewardRisk, stopDistanceAtr);
                return;
            }

            // Do not display a new pre-entry trigger for a side that already has positions.
            RemoveMarketGuideObject(sideKey + "_ENTRY");

            // The generic idea is not the open position's exact stop. Hide it after
            // every matching position has real/native protection; when one is still
            // unprotected, retain the level only with an explicit warning label.
            RemoveMarketGuideObject(sideKey + "_TP");

            if (unprotectedPositions == 0)
            {
                RemoveMarketGuideObject(sideKey + "_SL");
                sideStatus = string.Format("{0} position(s); protection present; generic SL hidden; {1}",
                    totalPositions,
                    ShowSuggestedTpLine ? "per-position TP continues" : "TP display disabled");
            }
            else
            {
                if (calculated && stopIsSafe)
                {
                    DrawOrUpdateMarketGuide(sideKey + "_SL", tradeType, stopPrice,
                        tradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask,
                        atrPrice, sideKey + " SL!", Color.FromHex("#D8757B"),
                        false, false, false);
                }
                else
                {
                    RemoveMarketGuideObject(sideKey + "_SL");
                }

                sideStatus = string.Format("WARNING: {0}/{1} position(s) lack SL/native trailing protection; inspect immediately",
                    unprotectedPositions, totalPositions);
            }
        }

        private bool TryCalculateMarketSideGuidance(
            TradeType tradeType,
            double signalScore,
            double atrPrice,
            bool overextended,
            string trendText,
            string momentumText,
            out double entryPrice,
            out double stopPrice,
            out double targetPrice,
            out double rewardRisk,
            out double stopDistanceAtr,
            out bool stopIsSafe,
            out bool stopUsesPivot,
            out string setupContext)
        {
            entryPrice = 0.0;
            stopPrice = 0.0;
            targetPrice = 0.0;
            rewardRisk = 0.0;
            stopDistanceAtr = 0.0;
            stopIsSafe = false;
            stopUsesPivot = false;
            setupContext = "WAIT";

            var lastClosedIndex = Bars.Count - 2;
            if (lastClosedIndex < 0 || lastClosedIndex + 1 < SwingLookback ||
                lastClosedIndex + 1 < TpStructureLookback)
                return false;

            var direction = tradeType == TradeType.Buy ? 1.0 : -1.0;
            var marketReferencePrice = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            entryPrice = marketReferencePrice;

            var swingLow = double.MaxValue;
            var swingHigh = double.MinValue;
            var firstSwingIndex = lastClosedIndex - SwingLookback + 1;
            for (var i = firstSwingIndex; i <= lastClosedIndex; i++)
            {
                swingLow = Math.Min(swingLow, Bars.LowPrices[i]);
                swingHigh = Math.Max(swingHigh, Bars.HighPrices[i]);
            }

            var atrBuffer = atrPrice * InitialAtrBuffer;
            var spreadBuffer = Math.Max(Symbol.Spread, Symbol.TickSize) * SpreadBufferMultiplier;
            var minimumBuffer = MinimumWickBufferPips * Symbol.PipSize;
            var stopBuffer = Math.Max(minimumBuffer, Math.Max(atrBuffer, spreadBuffer));
            var pivotLookback = Math.Max(SwingLookback, ConfirmedPivotStrength * 2 + 3);
            double pivotAnchor;
            var stopBoundary = tradeType == TradeType.Buy
                ? Symbol.Bid - GetRequiredMarketGap(tradeType) + stopBuffer
                : Symbol.Ask + GetRequiredMarketGap(tradeType) - stopBuffer;
            stopUsesPivot = TryFindLatestConfirmedPivot(
                tradeType == TradeType.Buy, lastClosedIndex, pivotLookback,
                stopBoundary, tradeType == TradeType.Buy, out pivotAnchor);
            if (stopUsesPivot)
            {
                if (tradeType == TradeType.Buy)
                    swingLow = pivotAnchor;
                else
                    swingHigh = pivotAnchor;
            }

            var rawStop = tradeType == TradeType.Buy
                ? swingLow - stopBuffer
                : swingHigh + stopBuffer;
            stopPrice = NormalizeStopPrice(rawStop, tradeType);
            stopIsSafe = IsSafeRelativeToMarket(stopPrice, tradeType);

            // A suggested entry is a conditional breakout trigger at the nearest
            // unbroken confirmed swing high (Buy) / swing low (Sell), with a volatility
            // buffer. If none qualifies, use a small trigger beyond the current quote.
            var entryBuffer = Math.Max(Symbol.TickSize * 2.0,
                Math.Max(Symbol.Spread * 0.5, atrPrice * 0.08));
            double entryBarrier;
            if (TryFindNearestTargetPivot(tradeType, lastClosedIndex, TpStructureLookback,
                marketReferencePrice, entryBuffer, out entryBarrier))
                entryPrice = entryBarrier + direction * entryBuffer;
            else
                entryPrice = marketReferencePrice + direction * entryBuffer;

            entryPrice = NormalizeSuggestedTpPrice(entryPrice, tradeType);
            if (direction * (entryPrice - marketReferencePrice) <= 0.0)
                entryPrice = NormalizeSuggestedTpPrice(
                    marketReferencePrice + direction * Math.Max(entryBuffer, Symbol.TickSize * 2.0),
                    tradeType);

            stopDistanceAtr = atrPrice > 0.0 ? Math.Abs(entryPrice - stopPrice) / atrPrice : double.MaxValue;

            // TP projection combines volatility with the trend/momentum alignment for
            // this side; a nearby historical barrier can shorten an overly ambitious target.
            var sideAlignment = ClampValue(direction * signalScore, -1.0, 1.0);
            var atrMultiplier = ClampValue(TpBaseAtrMultiplier + sideAlignment * TpTrendExtensionAtr, 0.6, 4.0);
            var minimumTargetDistance = Math.Max(Symbol.TickSize * 3.0,
                Math.Max(Symbol.Spread * 1.5, atrPrice * 0.35));
            var projectedDistance = Math.Max(atrPrice * atrMultiplier, minimumTargetDistance);

            // Build TP distance from the actual ENTRY-to-SL risk, not only an ATR estimate.
            // A nearby structure may cap this target; the final R:R check then rejects it
            // rather than allowing the TP to ignore a resistance/support barrier.
            var directionalRiskBeforeCosts = direction * (entryPrice - stopPrice);
            var requiredMinimumRewardRisk = Math.Max(MinimumQualityRewardRisk, MinimumPreTradeRewardRisk);
            var executionCostReserve = Math.Max(Symbol.Spread, Symbol.TickSize)
                + SlippageReservePips * Symbol.PipSize;
            if (directionalRiskBeforeCosts > Symbol.TickSize)
            {
                var minimumRewardDistanceForRisk = requiredMinimumRewardRisk *
                    (directionalRiskBeforeCosts + executionCostReserve) + executionCostReserve;
                projectedDistance = Math.Max(projectedDistance, minimumRewardDistanceForRisk);
            }

            var target = entryPrice + direction * projectedDistance;

            double nearestLevel;
            var foundLevel = TryFindNearestTargetPivot(
                tradeType, lastClosedIndex, TpStructureLookback,
                entryPrice, minimumTargetDistance, out nearestLevel);
            var usedConfirmedPivot = foundLevel;
            if (!foundLevel)
                nearestLevel = tradeType == TradeType.Buy ? double.MaxValue : double.MinValue;

            var firstStructureIndex = lastClosedIndex - TpStructureLookback + 1;
            for (var i = firstStructureIndex; !usedConfirmedPivot && i <= lastClosedIndex; i++)
            {
                if (tradeType == TradeType.Buy)
                {
                    var high = Bars.HighPrices[i];
                    if (high > entryPrice + minimumTargetDistance && high < nearestLevel)
                    {
                        nearestLevel = high;
                        foundLevel = true;
                    }
                }
                else
                {
                    var low = Bars.LowPrices[i];
                    if (low < entryPrice - minimumTargetDistance && low > nearestLevel)
                    {
                        nearestLevel = low;
                        foundLevel = true;
                    }
                }
            }

            var usedStructure = false;
            if (foundLevel)
            {
                var structureBuffer = Math.Max(Symbol.TickSize * 2.0, Math.Max(Symbol.Spread, atrPrice * 0.05));
                var structureTarget = nearestLevel - direction * structureBuffer;
                var structureDistance = direction * (structureTarget - entryPrice);
                var projectedDistanceFromEntry = direction * (target - entryPrice);
                if (structureDistance >= minimumTargetDistance &&
                    structureDistance < projectedDistanceFromEntry)
                {
                    target = structureTarget;
                    usedStructure = true;
                }
            }

            targetPrice = usedStructure
                ? NormalizeTargetBeforeStructureBarrier(target, tradeType)
                : NormalizeSuggestedTpPrice(target, tradeType);
            if (!IsFinitePositive(entryPrice) ||
                direction * (targetPrice - entryPrice) <= 0.0)
                return false;

            var directionalRiskDistance = direction * (entryPrice - stopPrice);
            var directionalRewardDistance = direction * (targetPrice - entryPrice);
            var executionCostReserve = Math.Max(Symbol.Spread, Symbol.TickSize)
                + SlippageReservePips * Symbol.PipSize;

            // Reject the geometry later if the structural stop is on the wrong side.
            // R:R is deliberately conservative: reserve spread and configured slippage
            // from reward, and add the same reserve to risk. Commission cannot be
            // precisely included here because no prospective position volume is known.
            rewardRisk = directionalRiskDistance > Symbol.TickSize &&
                directionalRewardDistance > executionCostReserve
                ? (directionalRewardDistance - executionCostReserve) /
                    (directionalRiskDistance + executionCostReserve)
                : 0.0;

            var alignmentIndex = (int)Math.Round(50.0 + 50.0 * sideAlignment);
            var quality = GetSetupQuality(tradeType, signalScore, Symbol.Spread / atrPrice,
                overextended);
            if (!stopIsSafe)
                quality = "WAIT — SL invalid at current quote/broker gap";
            else if (stopDistanceAtr > Math.Min(MaximumQualityStopDistanceAtr, MaximumPreTradeStopDistanceAtr))
                quality = "WAIT — structural SL too wide";
            else if (rewardRisk < Math.Max(MinimumQualityRewardRisk, MinimumPreTradeRewardRisk))
                quality = "WAIT — cost-adjusted reward/risk below filter";
            else if ((tradeType == TradeType.Buy && trendText == "BEARISH" && momentumText == "DOWN") ||
                     (tradeType == TradeType.Sell && trendText == "BULLISH" && momentumText == "UP"))
                quality = "WAIT — trend and momentum oppose this side";
            setupContext = string.Format("align {0}/100 | {1} | {2} | R:R {3:F2} | {4}",
                alignmentIndex,
                sideAlignment >= 0.35 ? "trend aligned" : sideAlignment <= -0.35 ? "countertrend" : "mixed",
                usedStructure ? (usedConfirmedPivot ? "confirmed pivot cap" : "range cap") : "ATR target",
                rewardRisk,
                quality);
            return true;
        }

        private string GetSetupQuality(TradeType tradeType, double signalScore, double spreadToAtr, bool overextended)
        {
            var alignment = tradeType == TradeType.Buy ? signalScore : -signalScore;
            if (spreadToAtr > MaxSpreadToAtrRatio)
                return "WAIT — spread/ATR too high";
            if (overextended)
                return "WAIT — RSI/momentum stretched";
            if (alignment >= 0.35)
                return "FAVORABLE ALIGNMENT (not an entry signal)";
            if (alignment <= -0.35)
                return "COUNTER-TREND / WAIT";
            return "MIXED / WAIT";
        }

        private void GetManagedPositionProtectionCounts(TradeType tradeType, out int total, out int unprotected)
        {
            total = 0;
            unprotected = 0;

            foreach (var position in Positions)
            {
                if (!IsManaged(position) || position.TradeType != tradeType)
                    continue;

                total++;
                var hasProtection = position.StopLoss.HasValue ||
                    position.StopLossBreakEven != null || position.HasTrailingStop;
                if (!hasProtection)
                    unprotected++;
            }
        }

        private void DrawOrUpdateMarketGuide(
            string key,
            TradeType tradeType,
            double candidatePrice,
            double distanceReferencePrice,
            double atrPrice,
            string title,
            Color color,
            bool isTakeProfit,
            bool isEntry,
            bool highConfidence)
        {
            double previousPrice;
            var hasPrevious = _marketGuidePrices.TryGetValue(key, out previousPrice);
            var levelReached = hasPrevious && (isTakeProfit || isEntry) &&
                IsSuggestedTargetReached(previousPrice, tradeType);
            var displayPrice = candidatePrice;
            var priceChanged = true;

            if (hasPrevious && !levelReached)
            {
                var threshold = Math.Max(GuideMinimumUpdatePips * Symbol.PipSize, atrPrice * GuideMinimumUpdateAtr);
                if (Math.Abs(candidatePrice - previousPrice) < threshold)
                {
                    displayPrice = previousPrice;
                    priceChanged = false;
                }

                DateTime lastUpdate;
                var cooldownActive = priceChanged && GuideUpdateCooldownSeconds > 0 &&
                    _marketGuideUpdatedAt.TryGetValue(key, out lastUpdate) &&
                    (Server.Time - lastUpdate).TotalSeconds < GuideUpdateCooldownSeconds;
                if (cooldownActive)
                {
                    displayPrice = previousPrice;
                    priceChanged = false;
                }
            }

            var styleKey = (isEntry ? "ENTRY" : "LEVEL") + (highConfidence ? "|HIGH" : "|NORMAL");
            string previousStyle;
            var styleChanged = !_marketGuideContexts.TryGetValue(key, out previousStyle) ||
                !string.Equals(previousStyle, styleKey, StringComparison.Ordinal);

            var currentBarTime = Bars.OpenTimes[Bars.Count - 1];
            var currentBarIndex = Bars.Count - 1;
            var startBarIndex = Math.Max(0, currentBarIndex - GuideLineBarsLeft);
            var endBarIndex = currentBarIndex + GuideLineBarsRight;
            DateTime lastStartTime;
            var newBar = !_marketGuideStartTimes.TryGetValue(key, out lastStartTime) || lastStartTime != currentBarTime;
            var lineName = GetMarketGuideLineName(key);
            var textName = GetMarketGuideTextName(key);

            if (!hasPrevious || priceChanged || styleChanged || levelReached || newBar)
            {
                var style = isEntry && !highConfidence ? LineStyle.Dots : LineStyle.Solid;
                var line = Chart.DrawTrendLine(lineName, startBarIndex, displayPrice, endBarIndex, displayPrice,
                    color, 1, style);
                line.ExtendToInfinity = false;
                _marketGuidePrices[key] = displayPrice;
                _marketGuideUpdatedAt[key] = Server.Time;
                _marketGuideStartTimes[key] = currentBarTime;
            }

            var distancePips = Symbol.PipSize > 0
                ? Math.Abs(displayPrice - distanceReferencePrice) / Symbol.PipSize
                : 0.0;
            var labelText = isEntry && highConfidence ? "★ " : string.Empty;
            labelText += string.Format("{0} | {1:F1} pips", title, distancePips);
            var labelOffset = GetGuideLabelOffsetPrice(atrPrice);
            // ENTRY and TP sit on different sides from SL for each trade direction.
            // The label's nearest text edge is anchored away from its guide line.
            var labelAbove = (isEntry || isTakeProfit)
                ? tradeType == TradeType.Buy
                : tradeType == TradeType.Sell;
            if (isTakeProfit)
                labelOffset *= 1.25;
            var labelPrice = displayPrice + (labelAbove ? labelOffset : -labelOffset);
            var label = Chart.DrawText(textName, labelText, endBarIndex, labelPrice, color);
            label.FontSize = 14;
            label.FontFamily = "Segoe UI Light";
            label.IsBold = false;
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.VerticalAlignment = labelAbove ? VerticalAlignment.Bottom : VerticalAlignment.Top;

            _marketGuideContexts[key] = styleKey;
        }

        private bool IsStopStillOnLossSide(Position position)
        {
            var direction = position.TradeType == TradeType.Buy ? 1.0 : -1.0;
            return direction * (position.EntryPrice - position.StopLoss.Value) > Symbol.TickSize;
        }

        private bool TryGetCostAdjustedRewardRisk(
            double entryPrice,
            double stopPrice,
            double targetPrice,
            TradeType tradeType,
            out double rewardRisk)
        {
            rewardRisk = 0.0;
            if (Symbol.PipSize <= 0 || Symbol.TickSize <= 0 ||
                !IsFiniteNumber(entryPrice) || !IsFiniteNumber(stopPrice) || !IsFiniteNumber(targetPrice))
                return false;

            var direction = tradeType == TradeType.Buy ? 1.0 : -1.0;
            var riskDistance = direction * (entryPrice - stopPrice);
            var rewardDistance = direction * (targetPrice - entryPrice);
            var costReserve = Math.Max(Symbol.Spread, Symbol.TickSize)
                + SlippageReservePips * Symbol.PipSize;

            if (riskDistance <= Symbol.TickSize || rewardDistance <= costReserve)
                return false;

            rewardRisk = (rewardDistance - costReserve) / (riskDistance + costReserve);
            return IsFiniteNumber(rewardRisk) &&
                rewardRisk >= Math.Max(MinimumQualityRewardRisk, MinimumPreTradeRewardRisk);
        }

        private double GetCurrentAtrPrice()
        {
            var index = Bars.Count - 2;
            if (_atr == null || index < 0 || index >= _atr.Result.Count)
                return 0.0;

            var atrPrice = _atr.Result[index];
            return IsFinitePositive(atrPrice) ? atrPrice : 0.0;
        }

        private double GetGuideLabelOffsetPrice(double atrPrice)
        {
            // The text is edge-aligned away from the line, so this is a true visible gap
            // instead of an anchor displacement partly consumed by half the text height.
            // The ATR floor scales with chart volatility; tick/pip/spread floors protect
            // symbols whose ATR or reported spread is unusually small.
            var offset = Math.Max(Symbol.TickSize * 8.0, Symbol.PipSize * 5.0);
            if (IsFinitePositive(atrPrice))
                offset = Math.Max(offset, atrPrice * 0.08);
            if (IsFinitePositive(Symbol.Spread))
                offset = Math.Max(offset, Symbol.Spread * 2.0);

            return offset;
        }

        private string GetMarketGuideLineName(string key)
        {
            return "CTSTOPBOT_GUIDE_LINE_" + key;
        }

        private string GetMarketGuideTextName(string key)
        {
            return "CTSTOPBOT_GUIDE_TEXT_" + key;
        }

        private void RemoveMarketGuideObject(string key)
        {
            Chart.RemoveObject(GetMarketGuideLineName(key));
            Chart.RemoveObject(GetMarketGuideTextName(key));
            _marketGuidePrices.Remove(key);
            _marketGuideUpdatedAt.Remove(key);
            _marketGuideContexts.Remove(key);
            _marketGuideStartTimes.Remove(key);
        }

        private void RemoveAllPreTradeGuideObjects()
        {
            RemoveMarketGuideObject("BUY_ENTRY");
            RemoveMarketGuideObject("BUY_SL");
            RemoveMarketGuideObject("BUY_TP");
            RemoveMarketGuideObject("SELL_ENTRY");
            RemoveMarketGuideObject("SELL_SL");
            RemoveMarketGuideObject("SELL_TP");
            Chart.RemoveObject("CTSTOPBOT_MARKET_STATUS");
        }

        private bool IsFiniteNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private bool IsFinitePositive(double value)
        {
            return IsFiniteNumber(value) && value > 0.0;
        }

        private bool TryFindLatestConfirmedPivot(bool seekLow, int lastClosedIndex, int lookback, double boundary, bool below, out double level)
        {
            level = 0.0;
            var strength = Math.Max(1, ConfirmedPivotStrength);
            var first = Math.Max(strength, lastClosedIndex - lookback + 1);
            var last = lastClosedIndex - strength;
            for (var i = last; i >= first; i--)
            {
                var price = seekLow ? Bars.LowPrices[i] : Bars.HighPrices[i];
                var pivot = true;
                for (var n = 1; n <= strength; n++)
                {
                    if (seekLow ? price >= Bars.LowPrices[i - n] || price >= Bars.LowPrices[i + n]
                                : price <= Bars.HighPrices[i - n] || price <= Bars.HighPrices[i + n])
                    {
                        pivot = false;
                        break;
                    }
                }
                if (!pivot || (below ? price >= boundary : price <= boundary))
                    continue;
                level = price;
                return true;
            }
            return false;
        }

        private bool TryFindNearestTargetPivot(TradeType tradeType, int lastClosedIndex, int lookback, double reference, double minDistance, out double level)
        {
            level = 0.0;
            var strength = Math.Max(1, ConfirmedPivotStrength);
            var first = Math.Max(strength, lastClosedIndex - lookback + 1);
            var last = lastClosedIndex - strength;
            var findHigh = tradeType == TradeType.Buy;
            var found = false;
            var nearest = findHigh ? double.MaxValue : double.MinValue;
            for (var i = first; i <= last; i++)
            {
                var price = findHigh ? Bars.HighPrices[i] : Bars.LowPrices[i];
                var pivot = true;
                for (var n = 1; n <= strength; n++)
                {
                    if (findHigh ? price <= Bars.HighPrices[i - n] || price <= Bars.HighPrices[i + n]
                                 : price >= Bars.LowPrices[i - n] || price >= Bars.LowPrices[i + n])
                    {
                        pivot = false;
                        break;
                    }
                }
                if (!pivot)
                    continue;
                if (findHigh)
                {
                    if (price > reference + minDistance && price < nearest)
                    {
                        nearest = price;
                        found = true;
                    }
                }
                else if (price < reference - minDistance && price > nearest)
                {
                    nearest = price;
                    found = true;
                }
            }
            if (found)
                level = nearest;
            return found;
        }

        private bool TryCalculateStructuralStop(Position position, double atrMultiplier, bool allowFallback, out double stopPrice)
        {
            stopPrice = 0.0;

            var lastClosedIndex = Bars.Count - 2;
            if (lastClosedIndex < 0 || lastClosedIndex + 1 < SwingLookback)
                return false;

            // Use exactly the configured number of closed candles. Do not silently
            // reduce the lookback when chart history has not loaded yet.
            var firstIndex = lastClosedIndex - SwingLookback + 1;
            var swingLow = double.MaxValue;
            var swingHigh = double.MinValue;

            for (var i = firstIndex; i <= lastClosedIndex; i++)
            {
                swingLow = Math.Min(swingLow, Bars.LowPrices[i]);
                swingHigh = Math.Max(swingHigh, Bars.HighPrices[i]);
            }

            var atrPrice = _atr != null && lastClosedIndex < _atr.Result.Count
                ? _atr.Result[lastClosedIndex]
                : 0.0;

            if (double.IsNaN(atrPrice) || double.IsInfinity(atrPrice) || atrPrice < 0)
                atrPrice = 0.0;

            var spreadBuffer = Math.Max(Symbol.Spread, Symbol.TickSize) * SpreadBufferMultiplier;
            var atrBuffer = atrPrice * atrMultiplier;
            var minimumBuffer = MinimumWickBufferPips * Symbol.PipSize;
            var buffer = Math.Max(minimumBuffer, Math.Max(spreadBuffer, atrBuffer));

            if (allowFallback)
            {
                // Use the same confirmed-pivot rule as the pre-entry SL suggestion,
                // so the initial stop does not silently switch to a different structure.
                var pivotLookback = Math.Max(SwingLookback, ConfirmedPivotStrength * 2 + 3);
                double pivotAnchor;
                var stopBoundary = position.TradeType == TradeType.Buy
                    ? Symbol.Bid - GetRequiredMarketGap(position.TradeType) + buffer
                    : Symbol.Ask + GetRequiredMarketGap(position.TradeType) - buffer;
                if (TryFindLatestConfirmedPivot(
                    position.TradeType == TradeType.Buy,
                    lastClosedIndex,
                    pivotLookback,
                    stopBoundary,
                    position.TradeType == TradeType.Buy,
                    out pivotAnchor))
                {
                    if (position.TradeType == TradeType.Buy)
                        swingLow = pivotAnchor;
                    else
                        swingHigh = pivotAnchor;
                }
            }

            var rawPrice = position.TradeType == TradeType.Buy
                ? swingLow - buffer
                : swingHigh + buffer;

            stopPrice = NormalizeStopPrice(rawPrice, position.TradeType);

            if (!IsSafeRelativeToMarket(stopPrice, position.TradeType))
            {
                if (!allowFallback)
                {
                    NotifyThrottled(position.Id,
                        "Structural trailing candidate is no longer safely beyond the current quote; skipping this trailing update rather than snapping the stop close to market.");
                    return false;
                }

                // For a missing initial stop only, try an explicitly warned fallback
                // beyond the current quote. A broken structure needs immediate review.
                NotifyThrottled(position.Id,
                    "Initial structural stop is at/beyond the safe market gap; trying a fallback stop beyond the current quote. Review immediately because the original swing may have been invalidated.");

                var fallbackGap = Math.Max(GetRequiredMarketGap(position.TradeType), buffer);
                rawPrice = position.TradeType == TradeType.Buy
                    ? Math.Min(rawPrice, Symbol.Bid - fallbackGap)
                    : Math.Max(rawPrice, Symbol.Ask + fallbackGap);

                stopPrice = NormalizeStopPrice(rawPrice, position.TradeType);
            }

            return IsSafeRelativeToMarket(stopPrice, position.TradeType);
        }

        private bool TryGetRequiredBreakEvenOffsetPips(Position position, out double offsetPips)
        {
            offsetPips = 0.0;

            if (Symbol.PipSize <= 0 || Symbol.LotSize <= 0 || Symbol.PipValue <= 0)
                return false;

            // cTrader's Symbol.PipValue is used with volume in units in its risk examples.
            // It is fixed when the cBot starts, so account-currency conversion can drift over time.
            var pipValueForPosition = Symbol.PipValue * position.VolumeInUnits;
            if (pipValueForPosition <= 0 || double.IsNaN(pipValueForPosition) ||
                double.IsInfinity(pipValueForPosition))
                return false;

            // Position.Commissions is the one-way commission for opening the position.
            // The multiplier estimates the round trip. Count already-accrued negative
            // swap as a cost; do not rely on future swap being predictable.
            var accruedNegativeSwapCash = Math.Max(0.0, -position.Swap);
            var estimatedRoundTripCommissionCash =
                Math.Max(Math.Abs(position.Commissions) * RoundTripCommissionMultiplier,
                         MinimumRoundTripCommissionReserve);

            var estimatedCommissionCash =
                estimatedRoundTripCommissionCash
                + accruedNegativeSwapCash
                + AdditionalCostReserve;

            var commissionPips = estimatedCommissionCash / pipValueForPosition;
            var spreadPips = (Symbol.Spread / Symbol.PipSize) * BreakEvenSpreadMultiplier;

            offsetPips = spreadPips + commissionPips + SlippageReservePips + MinimumNetLockPips;
            return !double.IsNaN(offsetPips) && !double.IsInfinity(offsetPips) && offsetPips >= 0.0;
        }

        private double NormalizeStopPrice(double price, TradeType tradeType)
        {
            if (Symbol.TickSize <= 0)
                return Math.Round(price, Symbol.Digits);

            // Round outward, never inward towards the current price.
            var ticks = price / Symbol.TickSize;
            var normalized = tradeType == TradeType.Buy
                ? Math.Floor(ticks + 1e-10) * Symbol.TickSize
                : Math.Ceiling(ticks - 1e-10) * Symbol.TickSize;

            return Math.Round(normalized, Symbol.Digits, MidpointRounding.AwayFromZero);
        }

        private double GetRequiredMarketGap(TradeType tradeType)
        {
            var minimumGap = Math.Max(Symbol.TickSize, MinimumMarketGapPips * Symbol.PipSize);
            var brokerDistance = Symbol.MinStopLossDistance;

            if (double.IsNaN(brokerDistance) || double.IsInfinity(brokerDistance) || brokerDistance <= 0)
                return minimumGap;

            if (Symbol.MinDistanceType == SymbolMinDistanceType.Pips)
            {
                brokerDistance *= Symbol.PipSize;
            }
            else
            {
                // cTrader expresses percentage-based minimum distances as a percentage
                // of the current market price. Use the close-side quote for this trade.
                var referencePrice = tradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
                brokerDistance = referencePrice * brokerDistance / 100.0;
            }

            if (!double.IsNaN(brokerDistance) && !double.IsInfinity(brokerDistance) && brokerDistance > 0)
                minimumGap = Math.Max(minimumGap, brokerDistance);

            return minimumGap;
        }

        private double NormalizeBreakEvenPrice(double price, TradeType tradeType)
        {
            if (Symbol.TickSize <= 0)
                return Math.Round(price, Symbol.Digits, MidpointRounding.AwayFromZero);

            // A break-even target must not be rounded below its intended profit offset:
            // round Buy levels up and Sell levels down, then re-check broker/market distance.
            var ticks = price / Symbol.TickSize;
            var normalized = tradeType == TradeType.Buy
                ? Math.Ceiling(ticks - 1e-10) * Symbol.TickSize
                : Math.Floor(ticks + 1e-10) * Symbol.TickSize;

            return Math.Round(normalized, Symbol.Digits, MidpointRounding.AwayFromZero);
        }

        private bool IsSafeRelativeToMarket(double stopPrice, TradeType tradeType)
        {
            if (double.IsNaN(stopPrice) || double.IsInfinity(stopPrice) ||
                Symbol.PipSize <= 0 || Symbol.TickSize <= 0 ||
                Symbol.Bid <= 0 || Symbol.Ask <= 0 || Symbol.Ask < Symbol.Bid ||
                double.IsNaN(Symbol.Bid) || double.IsNaN(Symbol.Ask) ||
                double.IsInfinity(Symbol.Bid) || double.IsInfinity(Symbol.Ask))
                return false;

            var minimumGap = GetRequiredMarketGap(tradeType);

            if (tradeType == TradeType.Buy)
                return stopPrice < Symbol.Bid - minimumGap;

            return stopPrice > Symbol.Ask + minimumGap;
        }

        private bool IsMoreProtective(double candidate, double reference, TradeType tradeType)
        {
            return tradeType == TradeType.Buy
                ? candidate > reference + Symbol.TickSize * 0.25
                : candidate < reference - Symbol.TickSize * 0.25;
        }

        private bool ImprovesExistingStop(double candidate, double current, TradeType tradeType, double minimumPips)
        {
            var threshold = Math.Max(Symbol.TickSize * 0.5, minimumPips * Symbol.PipSize);
            return tradeType == TradeType.Buy
                ? candidate >= current + threshold
                : candidate <= current - threshold;
        }

        private void TryModifyStop(Position position, double stopPrice, bool isInitial, double minimumImprovementPips, string reason)
        {
            if (!IsSafeRelativeToMarket(stopPrice, position.TradeType))
            {
                NotifyThrottled(position.Id, "Stop update skipped because the candidate is too close to, or beyond, the current market. Candidate: " + stopPrice);
                return;
            }

            if (position.StopLoss.HasValue)
            {
                if (!ImprovesExistingStop(stopPrice, position.StopLoss.Value, position.TradeType, minimumImprovementPips))
                    return;
            }
            else if (!isInitial)
            {
                // Initial protection must be established before any trailing/break-even update.
                return;
            }

            DateTime lastAttempt;
            var retryDelay = RetryCooldownSeconds;
            int failures;
            if (_failureCounts.TryGetValue(position.Id, out failures) && failures > 0)
            {
                var exponent = Math.Min(Math.Max(0, failures - 1), 8);
                var backoff = RetryCooldownSeconds * Math.Pow(2.0, exponent);
                var maximumDelay = Math.Max(RetryCooldownSeconds, MaximumRetryCooldownSeconds);
                retryDelay = (int)Math.Min(maximumDelay, backoff);
            }

            if (_lastAttemptAt.TryGetValue(position.Id, out lastAttempt) &&
                (Server.Time - lastAttempt).TotalSeconds < retryDelay)
                return;

            _lastAttemptAt[position.Id] = Server.Time;

            // Preserve the position's existing server-side trailing-stop flag and
            // stop trigger method while changing only the absolute stop price.
            // Native server-side break-even positions are skipped above until a typed
            // overload is verified against the pinned package and demo-tested.
            TradeResult result;
            try
            {
                result = ModifyPosition(position, stopPrice, position.TakeProfit,
                    ProtectionType.Absolute, position.HasTrailingStop, position.StopLossTriggerMethod);
            }
            catch (Exception ex)
            {
                int exceptionFailureCount;
                var retryInSeconds = RegisterModificationFailure(position.Id, out exceptionFailureCount);
                Print("WARNING position {0}: ModifyPosition threw an exception: {1}. Candidate={2}; failures={3}; retry delay={4}s.",
                    position.Id, ex, stopPrice, exceptionFailureCount, retryInSeconds);
                return;
            }

            if (result.IsSuccessful)
            {
                _failureCounts.Remove(position.Id);
                Print("Position {0}: {1} accepted at {2}; current Pips={3:F2}, NetProfit={4:F2}, volume={5}.",
                    position.Id, reason, stopPrice, position.Pips, position.NetProfit, position.VolumeInUnits);
            }
            else
            {
                int failureCount;
                var retryInSeconds = RegisterModificationFailure(position.Id, out failureCount);
                Print("WARNING position {0}: ModifyPosition failed ({1}). Candidate stop={2}; current stop={3}; Bid={4}; Ask={5}; required market gap={6}. Failure count={7}; retry delay={8}s. Verify broker distance/freeze rules and execution conditions.",
                    position.Id, result.Error, stopPrice,
                    position.StopLoss.HasValue ? position.StopLoss.Value.ToString() : "none",
                    Symbol.Bid, Symbol.Ask, GetRequiredMarketGap(position.TradeType),
                    failureCount, retryInSeconds);
            }
        }

        private int RegisterModificationFailure(int positionId, out int failureCount)
        {
            int previousFailures;
            _failureCounts.TryGetValue(positionId, out previousFailures);
            failureCount = previousFailures + 1;
            _failureCounts[positionId] = failureCount;

            var exponent = Math.Min(Math.Max(0, failureCount - 1), 8);
            var backoff = RetryCooldownSeconds * Math.Pow(2.0, exponent);
            var maximumDelay = Math.Max(RetryCooldownSeconds, MaximumRetryCooldownSeconds);
            return (int)Math.Min(maximumDelay, backoff);
        }

        private void NotifyThrottled(int positionId, string message)
        {
            DateTime lastNotice;
            if (_lastNoticeAt.TryGetValue(positionId, out lastNotice) &&
                (Server.Time - lastNotice).TotalSeconds < 10)
                return;

            _lastNoticeAt[positionId] = Server.Time;
            Print("WARNING position {0}: {1}", positionId, message);
        }
    }
}
