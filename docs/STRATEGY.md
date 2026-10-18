# Strategy and implementation notes

## Scope

The bot manages existing positions; it does not create entries or change position volume. It is deliberately restricted to an M5 chart and a symbol containing the configured string (default XAUUSD). A broker may call Gold GOLD, XAUUSD., or another alias; adjust the symbol filter only after confirming the correct instrument.

By default it considers all position labels on the chart symbol, including manual positions; other cBots using the same symbol can also be affected. Enable the label restriction only after verifying the exact label used by the positions you want to manage. Use the feature switches to isolate initial-stop, break-even and structure-trailing behaviour during demo tests. Positions already using cTrader's server-side trailing stop are skipped by default to avoid competing stop managers; enabling management for these positions requires a dedicated test. The current stop-modification path uses the legacy absolute-protection overload, so positions with a native server-side break-even rule are skipped until the newer typed overload is verified against the pinned package and tested.

## Initial stop

1. Wait until there are enough bars for both the configured swing lookback and ATR warm-up, then read the last Swing lookback **closed M5 candles**; the still-forming candle is excluded. Do not silently calculate from incomplete startup history.
2. For Buy, find the lowest low; for Sell, find the highest high.
3. Place the candidate beyond that swing by the largest of:
   - ATR × configured ATR multiplier,
   - current spread × spread-buffer multiplier,
   - the minimum wick buffer in pips.
4. Round away from the market using the symbol tick size.
5. Enforce the larger of the configured market gap and the broker-reported minimum stop-loss distance. cTrader may express the broker distance in pips or as a percentage of price.
6. If the initial structural level is too close to or already through the current close-side quote, log a warning and calculate a fallback beyond the current quote. This may signal a broken swing; inspect the position immediately.
7. If a *trailing* structural candidate is too close to or through the current quote, skip that trailing update rather than snapping an already-protected stop close to market.

An existing stop is preserved; the cBot does not widen it. The broker-reported minimum distance reduces avoidable rejections, but it may not capture every dynamic freeze rule or execution constraint, so broker rejections still need to be tested.

## Cost-aware break-even estimate

The cBot estimates a stop price beyond entry using:

- current spread converted to pips × Break-even spread multiplier,
- absolute one-way Position.Commissions × Round-trip commission multiplier,
- commission and already-accrued **negative swap** converted into pips using the estimated pip value for the position volume,
- Slippage reserve (pips),
- Additional cost reserve (account currency),
- Minimum net lock (pips).
- Minimum round-trip commission reserve (account currency), used as a floor when the observed opening commission is zero or below the broker's expected minimum.

Break-even only becomes eligible after Position.Pips reaches this estimated offset plus Break-even activation extra. The candidate is accepted only when it improves the existing stop and is far enough from the current quote.

This is intentionally conservative and approximate. cTrader documents Position.Commissions as the one-way commission on the opening request, so the multiplier estimates the round trip. Position.Swap contributes already-accrued negative swap; future swap is not predictable and is not reserved. Commission schedules may have minimums, spreads change, a stop can slip, and conversion fees may accrue. Symbol.PipValue is fixed when the cBot starts and is not updated in real time; conversion into a different account currency may drift. Use the extra reserve to account for broker-specific costs, then validate actual net P/L on demo history. Do not interpret “break-even” as a guarantee of non-negative realised P/L.

## Trailing

The bot uses the same closed-candle swing method with a separate ATR multiplier. A structure-trailing candidate must tighten the existing stop by at least Trailing step (pips). A break-even-only improvement may move by one tick. If a structural candidate supersedes a break-even candidate, the trailing-step threshold still applies. The long-side stop can only rise; the short-side stop can only fall. The bot does not intentionally remove or loosen an existing stop.

By default, positions whose `HasTrailingStop` flag is already true are left untouched. This prevents the bot from competing with cTrader's server-side trailing engine. The modification call also preserves an eligible position's existing trailing flag and stop trigger method.

One stop modification is attempted per position at a time. Failed changes are logged; retries are throttled. The cBot does not guarantee continuous server-side protection while a change is pending or rejected.

## Suggested TP visualization (advisory only)

The TP line is separate from stop management and does not open trades or modify `Position.TakeProfit`. Each eligible open position receives its own chart object named by position ID. The target distance is based on persisted original R where recoverable, the current direction-sensitive ATR/trend/momentum estimate, and the nearest opposing closed-candle structure. Cost estimates include spread/slippage and, for live positions, estimated round-trip commission and accrued negative swap when pip-value metadata is available. A nearby structure is never crossed to force the preferred R:R: an insufficient ratio remains visible as `TP!`, with the ratio or an explicit unknown-cost/risk warning. The line and label are removed when that position closes or the cBot stops.

The recommendation uses the last **closed** M5 candle for EMA 9/21 trend, close-to-close momentum over three bars, and RSI 14. A directional score changes an ATR-based distance. The initial candidate is anchored to the position entry, so it does not chase every tick. A nearby historical high (Buy resistance) or low (Sell support) can cap the target after a small buffer. If the market reaches/passes the target, a new forward objective is calculated. These historical extrema are only simple reference levels, not confirmed multi-timeframe structure.

To avoid visual jitter, a price update requires at least the larger of the configured ATR fraction and pip threshold, and it is throttled by the update cooldown. Crossing the old target forces a refresh. The line is an informational scenario only: it is not an actual TP order, does not account for every future spread/commission/execution change, and does not guarantee that a market path will reach it.

## Pre-trade guidance and handover to live positions

Pre-entry ENTRY/SL/TP guides are drawn when enough closed M5 history and valid structure/quotes are available, even if quality filters do not pass. Failed candidates are dotted and explicitly marked provisional; invalid SL/TP levels carry `!`, and the status overlay explains the failing floor. ENTRY is a conditional breakout trigger beyond a relevant closed-candle swing, buffered by spread/ATR—not an active order. The alignment score remains a heuristic, not a statistically calibrated probability.

The pre-entry and installed initial SL both use closed-candle structure with a wick-cluster detector: when enough small-body, long-wick rejection candles cluster in the lookback, the relevant full-window extreme is preferred; otherwise the recent three-bar extreme/pivot candidate is used. The SL is buffered with ATR, spread and minimum-wick floors, rounded outward to the symbol tick, then validated against executable-side quotes and the broker's minimum stop distance. A fallback is used only when initial protection is missing and the preferred structure is not safely placeable; it is warned explicitly because it may no longer represent the preferred swing structure.

The TP idea projects from the suggested ENTRY level, scales ATR distance by trend/momentum alignment, and prefers the next confirmed swing high/low as a target barrier. If no confirmed pivot exists, the model falls back to the nearest qualifying historical high/low. SL and TP pip distances are measured from the displayed ENTRY level; ENTRY distance is measured from the current Ask/Bid. These are price-distance estimates, not expected profit.

A setup is marked qualified only if all hard quality floors pass: the displayed stop is safe and on the loss side of ENTRY; risk distance is 0.25–2.25 ATR; displayed cost-adjusted R:R is at least 1.8; spread/ATR is at most 0.16; directional alignment is at least 0.65; momentum is not extended; and closed-candle trend and momentum support the side. The calculation uses the exact prices predicted by the guide smoothing/cooldown logic so the status is evaluated against the geometry expected to be drawn. Pre-entry spread/slippage reserve is shared between target projection and ratio calculation; a separately configurable commission reserve in pips can account for known broker fees without pretending future volume/commission is known. Failing candidates remain visible as provisional guides, never as passed signals. These are engineering screens, not a validated trading edge.

Each line spans 20 M5 candles left and 20 M5 candles right of the active candle, uses time-based coordinates rather than a future bar index, and has thickness 1. No viewport auto-scroll is used. Labels contain their short name plus pip distance and are anchored at their text edge away from the horizontal line with a tick/pip/ATR/spread-aware price gap. Qualified ENTRY is gold with a star; provisional candidate levels are dotted and explicitly flagged. Small moves are smoothed and a cooldown reduces chart-object churn.

On a matching position's open event, the cBot attempts to install its initial stop if the position lacks one. The generic pre-entry SL line is hidden for that direction only when all matching open positions are observed with an absolute SL, native server-side break-even rule, or server-side trailing protection. If any matching position is unprotected, the panel warns and the line remains explicitly labelled as a warning—not as an active order. The generic pre-entry TP line is replaced by per-position TP suggestions, which continue to update independently until each position closes. No entry is opened and Position.TakeProfit is not changed.

The idea levels depend on the active M5 chart, broker symbol metadata and loaded history. There is no validated statistical expectancy behind the score or target projection yet; evaluate it on replay and demo before relying on it.

## Broker-specific constraints to confirm

- Gold symbol name, digits, tick size, pip size and spread regime.
- Stop-loss minimum distance, freeze level and supported stop trigger method.
- Commission amount and whether a displayed commission is one-way or round-trip.
- Swap schedule, account currency and conversion fees.
- Maximum order modification rate and behaviour during rollover/news.

## Safety defaults

- No new orders are opened.
- No position volume is changed.
- No existing stop is deliberately widened.
- No stop is submitted if the candidate is unsafe relative to the current quote.
- Failed modifications are logged, not represented as successful protection.
- Do not connect to a live account until compile, demo, rejection and restart cases are tested.


## Implemented defensive controls (current development branch)

- Separate parameter switches allow initial stop placement, break-even, and structure trailing to be tested independently. Disabling initial stop placement can leave a position with no stop; it is a test control, not a safe live default.
- Position management is wrapped in exception handling so one unexpected per-position exception is logged instead of terminating the entire per-tick loop.
- Position modifications preserve take-profit, the server-side trailing flag and stop trigger method. A position with native server-side break-even settings is skipped entirely. Structural stops round outward; break-even targets round toward the intended profit offset (Buy up, Sell down), then are revalidated against current/broker distance. If an existing stop becomes too close to the market/broker gap or appears crossed, the bot warns but will not widen it.
- The final selected trailing candidate must still meet the trailing-step threshold; a break-even candidate does not accidentally waive that threshold if structural trailing supersedes it.
- Minimum round-trip commission reserve can account for broker minimum commissions when the observed opening commission is zero or unusually small.


## Price-data guards and close events

The cBot refuses unsafe candidate prices when they are NaN/infinite, or when Bid/Ask/PipSize/TickSize are invalid. It logs the position close reason and removes its per-position retry/notice state when the position closes.
