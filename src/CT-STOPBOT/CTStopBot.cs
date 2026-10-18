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
        private RelativeStrengthIndex _tpRsi;
        private readonly Dictionary<int, DateTime> _lastAttemptAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _lastNoticeAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, int> _failureCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, double> _suggestedTpPrices = new Dictionary<int, double>();
        private readonly Dictionary<int, DateTime> _suggestedTpUpdatedAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, string> _suggestedTpContexts = new Dictionary<int, string>();

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

            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);
            _tpFastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 9);
            _tpSlowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
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

            // Protect eligible positions that were already open when the cBot started.
            foreach (var position in Positions)
                ManagePosition(position);

            // Timer retries failed/missing initial protection even during quiet ticks.
            Timer.Start(1);
        }

        protected override void OnTick()
        {
            ManageAllPositions();
        }

        protected override void OnTimer()
        {
            ManageAllPositions();
        }

        protected override void OnStop()
        {
            Positions.Opened -= OnPositionOpened;
            Positions.Closed -= OnPositionClosed;

            foreach (var positionId in new List<int>(_suggestedTpPrices.Keys))
                RemoveSuggestedTpObjects(positionId);
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            if (IsManaged(args.Position))
                ManagePosition(args.Position);
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            Print("Position {0} closed. Reason={1}.", args.Position.Id, args.Reason);
            _lastAttemptAt.Remove(args.Position.Id);
            _lastNoticeAt.Remove(args.Position.Id);
            _failureCounts.Remove(args.Position.Id);
            RemoveSuggestedTpObjects(args.Position.Id);
        }

        private void ManageAllPositions()
        {
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
                        if (!_suggestedTpContexts.TryGetValue(position.Id, out previousContext) ||
                            !string.Equals(previousContext, context, StringComparison.Ordinal))
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
            var nearestLevel = position.TradeType == TradeType.Buy ? double.MaxValue : double.MinValue;
            var foundLevel = false;
            for (var i = firstIndex; i <= lastClosedIndex; i++)
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
            var startTime = Bars.OpenTimes[Bars.Count - 1];
            var endTime = startTime.AddMinutes(15.0);
            var color = position.TradeType == TradeType.Buy ? Color.Green : Color.Red;
            var lineName = GetSuggestedTpLineName(position.Id);
            var textName = GetSuggestedTpTextName(position.Id);
            var line = Chart.DrawTrendLine(lineName, startTime, targetPrice, endTime, targetPrice, color, 1, LineStyle.Dots);
            line.ExtendToInfinity = false;
            var label = Chart.DrawText(textName,
                string.Format("TP IDEA #{0} {1} [{2}]", position.Id, targetPrice.ToString("F" + Symbol.Digits), context),
                endTime, targetPrice, color);
            label.FontSize = 8;
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
        }

        private double ClampValue(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
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
