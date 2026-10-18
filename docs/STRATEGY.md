# Strategy and implementation notes

## Scope

The bot manages existing positions; it does not create entries or change position volume. It is deliberately restricted to an M5 chart and a symbol containing the configured string (default XAUUSD). A broker may call Gold GOLD, XAUUSD., or another alias; adjust the symbol filter only after confirming the correct instrument.

By default it manages all position labels on that symbol, including manual positions. Enable the label restriction when only a particular strategy's positions should be touched. Use the feature switches to isolate initial-stop, break-even and structure-trailing behaviour during demo tests. Positions already using cTrader's server-side trailing stop are skipped by default to avoid competing stop managers; enable that option only after a dedicated test.

## Initial stop

1. Read the last Swing lookback **closed M5 candles**; the still-forming candle is excluded.
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
- Position modifications preserve take-profit, the server-side trailing flag, stop trigger method, and any existing server-side break-even settings. Structural stops round outward; break-even targets round toward the intended profit offset (Buy up, Sell down), then are revalidated against current/broker distance. If an existing stop becomes too close to the market/broker gap or appears crossed, the bot warns but will not widen it.
- The final selected trailing candidate must still meet the trailing-step threshold; a break-even candidate does not accidentally waive that threshold if structural trailing supersedes it.
- Minimum round-trip commission reserve can account for broker minimum commissions when the observed opening commission is zero or unusually small.


## Price-data guards and close events

The cBot refuses unsafe candidate prices when they are NaN/infinite, or when Bid/Ask/PipSize/TickSize are invalid. It logs the position close reason and removes its per-position retry/notice state when the position closes.
