# Demo validation plan

Do not skip these tests before live use. Record the broker, account type, symbol name, server time, symbol digits/tick size/pip size, observed spread, commission and the final result for each test.

## Build and configuration

- [ ] Build in the installed cTrader Automate version; resolve every compile warning/error.
- [ ] Start on XAUUSD M5; confirm the cBot logs its start.
- [ ] Start on M1 and H1; confirm it refuses to run.
- [ ] Start on a non-Gold symbol; confirm it stops.
- [ ] Confirm the broker's symbol alias and configure the symbol filter if required.
- [ ] Confirm whether all Gold positions should be managed or enable label restriction.
- [ ] Verify startup diagnostics show correct pip/tick sizes, current spread, minimum stop distance, and distance units.
- [ ] Verify initial-stop, break-even and structure-trailing switches can be tested independently.
- [ ] Open a position with cTrader server-side trailing enabled; confirm the bot leaves it untouched by default.
- [ ] Open a position with cTrader native server-side break-even configured; confirm the bot logs that it is excluded and makes no SL modifications.

## Initial protection

- [ ] Open a small demo Buy position manually with no stop; confirm a stop is installed or the exact failure is logged.
- [ ] Repeat for Sell.
- [ ] Open a position with an existing stop; confirm the initial-stop logic does not replace it.
- [ ] Move price close to an existing stop; confirm the bot warns when that stop is inside the current/broker gap and never widens it.
- [ ] Open a position while the market is moving quickly; confirm rejected/unsafe candidates are logged and the bot retries.
- [ ] Start the cBot while an eligible position is already open without a stop; confirm it attempts protection.
- [ ] Confirm the candidate is beyond the selected closed-candle swing and rounded to tick size.
- [ ] With insufficient chart history, confirm the bot waits for both full swing lookback and ATR warm-up instead of using partial history.
- [ ] Force repeated stop modification rejections and confirm retry delays start at the configured cooldown and then increase up to the configured maximum.
- [ ] Confirm stop placement respects the broker-reported minimum stop-loss distance when distance units are pips.
- [ ] If the broker reports percentage-based minimum distance, confirm the diagnostic log displays the units and candidate stops respect the percentage-derived gap.
- [ ] Confirm dynamic freeze rules can still reject an update and that the rejection is logged rather than treated as successful protection.
- [ ] Force/observe a case where the initial candle-based level is too close to or through the current quote; confirm the fallback warning is visible and a broker rejection is never reported as success.
- [ ] Force/observe an already-protected position whose trailing structure is invalidated; confirm that the trailing update is skipped instead of snapping the stop to the current market.

## Break-even and trailing

- [ ] Confirm no break-even move occurs before its configured estimated-cost threshold.
- [ ] Confirm the stop moves only when the candidate is more protective.
- [ ] Confirm commission multiplier, minimum round-trip commission reserve, spread, slippage and additional cost reserve change the estimated break-even offset as expected.
- [ ] Confirm already-accrued negative swap increases the required break-even offset; positive swap does not reduce the conservative cost estimate.
- [ ] Confirm stop modification preserves existing take-profit, stop trigger method and server-side trailing setting.
- [ ] Confirm positions with a native server-side break-even rule are excluded from management by this package version.
- [ ] Verify Buy break-even targets round up to tick size and Sell targets round down, then remain outside the current/broker minimum gap.
- [ ] Test both Buy and Sell in a widening spread; observe whether stop candidates are skipped safely.
- [ ] Verify realised net P/L after stop closure, not merely the displayed stop price.
- [ ] Let price make higher lows/lower highs; confirm trailing follows closed-candle structure and never loosens the stop.
- [ ] Confirm trailing respects the configured minimum improvement step even when a break-even candidate is also eligible.
- [ ] Confirm the bot does not modify other symbols or excluded labels.

## Resilience

- [ ] Check log output on broker rejection and ensure it never claims a failed stop was set.
- [ ] Confirm repeated rejections use exponential backoff up to the configured maximum retry interval.
- [ ] Observe one forced/induced management exception if feasible; confirm the bot logs it and continues servicing other positions.
- [ ] Check that setting `Manage positions with server-side trailing` off prevents conflicts with native trailing positions.
- [ ] Restart the cBot with a protected position; confirm it keeps and only tightens the existing stop.
- [ ] Restart the cBot with an unprotected position; confirm it retries an initial stop.
- [ ] Observe behaviour through a temporary connection interruption and during high-spread/rollover periods.
- [ ] Compare costs and realised net P/L against the broker's deal history.
- [ ] Run enough demo samples across Buy/Sell and varying volatility before reviewing live deployment.

## Acceptance criteria

A test is not passed solely because the cBot builds. All failed protection modifications must be visible in logs, existing stops must never be loosened, both directions must behave correctly, and demo results must match the configured commission/spread-cost assumptions to a reasonable degree. A successful test does not remove execution or slippage risk.
