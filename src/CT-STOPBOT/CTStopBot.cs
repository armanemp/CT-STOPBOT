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

            Print("CT-STOPBOT started on {0} {1}. Positions matching this symbol will be managed{2}.",
                SymbolName, TimeFrame, RestrictToLabel ? " only when label matches '" + ManagedLabel + "'" : " regardless of label");

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
            if (!IsManaged(position))
                return;

            if (Bars.Count < 3)
            {
                NotifyThrottled(position.Id, "Not enough M5 candle history yet; stop calculation is waiting for more bars.");
                return;
            }

            // Never overwrite an existing stop with the initial structural stop.
            if (!position.StopLoss.HasValue)
            {
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
            if (TryGetRequiredBreakEvenOffsetPips(position, out requiredOffsetPips) &&
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
            if (TryCalculateStructuralStop(position, TrailingAtrBuffer, out trailingStop) &&
                IsSafeRelativeToMarket(trailingStop, position.TradeType) &&
                IsMoreProtective(trailingStop, bestStop, position.TradeType))
            {
                bestStop = trailingStop;
                hasBetterStop = true;
                reason = breakEvenUpgrade ? "break-even + wick trailing" : "wick trailing";
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
            return IsSafeRelativeToMarket(stopPrice, position.TradeType);
        }

        private bool TryGetRequiredBreakEvenOffsetPips(Position position, out double offsetPips)
        {
            offsetPips = 0.0;

            if (Symbol.PipSize <= 0 || Symbol.LotSize <= 0 || Symbol.PipValue <= 0)
                return false;

            // Position.Commissions is documented as commission associated with the
            // trade request (one way). Estimate round trip and permit an extra reserve.
            var pipValueForPosition = Symbol.PipValue * (position.VolumeInUnits / Symbol.LotSize);
            if (pipValueForPosition <= 0 || double.IsNaN(pipValueForPosition) ||
                double.IsInfinity(pipValueForPosition))
                return false;

            var estimatedCommissionCash =
                Math.Abs(position.Commissions) * RoundTripCommissionMultiplier + AdditionalCostReserve;

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

        private bool IsSafeRelativeToMarket(double stopPrice, TradeType tradeType)
        {
            var minimumGap = Math.Max(Symbol.TickSize, MinimumMarketGapPips * Symbol.PipSize);

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

            var result = ModifyPosition(position, stopPrice, position.TakeProfit, ProtectionType.Absolute);
            if (result.IsSuccessful)
            {
                Print("Position {0}: {1} set/updated to {2}.", position.Id, reason, stopPrice);
            }
            else
            {
                Print("WARNING position {0}: ModifyPosition failed ({1}). Candidate stop={2}. Verify broker minimum distance, market state, and account permissions.",
                    position.Id, result.Error, stopPrice);
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
