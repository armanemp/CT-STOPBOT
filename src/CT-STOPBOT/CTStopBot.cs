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

        [Parameter("Retry cooldown (seconds)", DefaultValue = 2, MinValue = 1, MaxValue = 30)]
        public int RetryCooldownSeconds { get; set; }

        private AverageTrueRange _atr;
        private readonly Dictionary<int, DateTime> _lastAttemptAt = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _lastNoticeAt = new Dictionary<int, DateTime>();

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

            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);

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
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            if (IsManaged(args.Position))
                ManagePosition(args.Position);
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            _lastAttemptAt.Remove(args.Position.Id);
            _lastNoticeAt.Remove(args.Position.Id);
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
                    NotifyThrottled(position.Id, "Unexpected exception during position management: " + ex.Message);
                else
                    Print("CT-STOPBOT warning: unexpected exception while managing a null position: {0}", ex.Message);
            }
        }

        private void ManagePositionCore(Position position)
        {
            if (!IsManaged(position))
                return;

            if (position.HasTrailingStop && !ManagePositionsWithServerTrailing)
            {
                NotifyThrottled(position.Id,
                    "Position has a server-side trailing stop. CT-STOPBOT is leaving it untouched; enable the corresponding parameter only after testing how both trailing mechanisms interact.");
                return;
            }

            if (Bars.Count < 3)
            {
                NotifyThrottled(position.Id, "Not enough M5 candle history yet; stop calculation is waiting for more bars.");
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
                if (!TryCalculateStructuralStop(position, InitialAtrBuffer, out initialStop))
                {
                    NotifyThrottled(position.Id,
                        "Initial stop was not submitted: no safe closed-candle swing price is currently available. The position may remain unprotected; check logs and broker rules.");
                    return;
                }

                TryModifyStop(position, initialStop, true, 0.0, "initial wick/ATR stop");
                return;
            }

            var currentStop = position.StopLoss.Value;
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

                breakEvenPrice = NormalizeStopPrice(breakEvenPrice, position.TradeType);

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
                TryCalculateStructuralStop(position, TrailingAtrBuffer, out trailingStop) &&
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

        private bool TryCalculateStructuralStop(Position position, double atrMultiplier, out double stopPrice)
        {
            stopPrice = 0.0;

            var lastClosedIndex = Bars.Count - 2;
            if (lastClosedIndex < 1)
                return false;

            var firstIndex = Math.Max(0, lastClosedIndex - SwingLookback + 1);
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
                // The original swing stop may already have been crossed or may be
                // too close to market to submit. Use an explicitly warned fallback
                // beyond the current quote so the position is not silently ignored.
                NotifyThrottled(position.Id,
                    "Structural stop is already at/beyond the safe market gap; trying a fallback stop beyond the current quote. Review the position immediately because its structural level may have been invalidated.");

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

        private bool IsSafeRelativeToMarket(double stopPrice, TradeType tradeType)
        {
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
            if (_lastAttemptAt.TryGetValue(position.Id, out lastAttempt) &&
                (Server.Time - lastAttempt).TotalSeconds < RetryCooldownSeconds)
                return;

            _lastAttemptAt[position.Id] = Server.Time;

            // Preserve the position's existing server-side trailing-stop flag and
            // stop trigger method while changing only the absolute stop price.
            var result = ModifyPosition(position, stopPrice, position.TakeProfit,
                ProtectionType.Absolute, position.HasTrailingStop, position.StopLossTriggerMethod);

            if (result.IsSuccessful)
            {
                Print("Position {0}: {1} accepted at {2}; current Pips={3:F2}, NetProfit={4:F2}, volume={5}.",
                    position.Id, reason, stopPrice, position.Pips, position.NetProfit, position.VolumeInUnits);
            }
            else
            {
                Print("WARNING position {0}: ModifyPosition failed ({1}). Candidate stop={2}; current stop={3}; Bid={4}; Ask={5}; required market gap={6}. Verify broker distance/freeze rules and execution conditions.",
                    position.Id, result.Error, stopPrice,
                    position.StopLoss.HasValue ? position.StopLoss.Value.ToString() : "none",
                    Symbol.Bid, Symbol.Ask, GetRequiredMarketGap(position.TradeType));
            }
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
