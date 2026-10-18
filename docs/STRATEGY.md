# Strategy and implementation notes

## Scope

The bot manages existing positions; it does not create entries or change position volume. It is deliberately restricted to an M5 chart and a symbol containing the configured string (default XAUUSD). A broker may call Gold GOLD, XAUUSD., or another alias; adjust the symbol filter only after confirming the correct instrument.

By default it manages all position labels on that symbol, including manual positions. Enable the label restriction when only a particular strategy's positions should be touched.

## Initial stop

1. Read the last Swing lookback **closed M5 candles**; the still-forming candle is excluded.
2. For Buy, find the lowest low; for Sell, find the highest high.
3. Place the candidate beyond that swing by the largest of:
   - ATR × configured ATR multiplier,
   - current spread × spread-buffer multiplier,
   - the minimum wick buffer in pips.
4. Round away from the market using the symbol tick size.
5. Do not submit the level if it is already too close to or beyond the current close-side quote. If the broker rejects an update, log the error and retry after a cooldown.

An existing stop is preserved; the cBot does not widen it. If the structural candidate is already too close to or through the current quote, the bot logs a warning and calculates a fallback beyond the current quote. This can indicate that the structural level has already been invalidated; inspect the position promptly and verify any fallback/rejection in the logs. This is a simple structural baseline and needs demo observation to tune to the broker's Gold feed.

## Cost-aware break-even estimate

The cBot estimates a stop price beyond entry using:

- current spread converted to pips × Break-even spread multiplier,
- absolute observed Position.Commissions × Round-trip commission multiplier,
- commission converted into pips using an estimated pip value for the position volume,
- Slippage reserve (pips),
- Additional cost reserve (account currency),
- Minimum net lock (pips).

Break-even only becomes eligible after Position.Pips reaches this estimated offset plus Break-even activation extra. The candidate is accepted only when it improves the existing stop and is far enough from the current quote.

This is intentionally conservative and approximate. Commission schedules may have minimums, commissions may be charged in different assets, spreads change, a stop can slip, and overnight swap or conversion fees may accrue. Use the extra reserve to account for broker-specific costs, then validate actual net P/L on demo history. Do not interpret “break-even” as a guarantee of non-negative realised P/L.

## Trailing

The bot uses the same closed-candle swing method with a separate ATR multiplier. A trailing candidate must tighten the existing stop by at least Trailing step (pips), except that a break-even improvement may move by one tick. The long-side stop can only rise; the short-side stop can only fall. The bot does not intentionally remove or loosen an existing stop.

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
