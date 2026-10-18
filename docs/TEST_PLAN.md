# Demo validation plan

Do not skip these tests before live use. Record the broker, account type, symbol name, server time, symbol digits/tick size/pip size, observed spread, commission and the final result for each test.

## Build and configuration

- [ ] Build in the installed cTrader Automate version; resolve every compile warning/error.
- [ ] Start on XAUUSD M5; confirm the cBot logs its start.
- [ ] Start on M1 and H1; confirm it refuses to run.
- [ ] Start on a non-Gold symbol; confirm it stops.
- [ ] Confirm the broker's symbol alias and configure the symbol filter if required.
- [ ] Confirm whether all Gold positions should be managed or enable label restriction.

## Initial protection

- [ ] Open a small demo Buy position manually with no stop; confirm a stop is installed or the exact failure is logged.
- [ ] Repeat for Sell.
- [ ] Open a position with an existing stop; confirm the initial-stop logic does not replace it.
- [ ] Open a position while the market is moving quickly; confirm rejected/unsafe candidates are logged and the bot retries.
- [ ] Start the cBot while an eligible position is already open without a stop; confirm it attempts protection.
- [ ] Confirm the candidate is beyond the selected closed-candle swing and rounded to tick size.
- [ ] Confirm stop placement respects the broker's minimum distance/freeze rules.
- [ ] Force/observe a case where the candle-based level is too close to or through the current quote; confirm the fallback warning is visible and a broker rejection is never reported as success.

## Break-even and trailing

- [ ] Confirm no break-even move occurs before its configured estimated-cost threshold.
- [ ] Confirm the stop moves only when the candidate is more protective.
- [ ] Confirm commission multiplier, spread, slippage and additional cost reserve change the estimated break-even offset as expected.
- [ ] Test both Buy and Sell in a widening spread; observe whether stop candidates are skipped safely.
- [ ] Verify realised net P/L after stop closure, not merely the displayed stop price.
- [ ] Let price make higher lows/lower highs; confirm trailing follows closed-candle structure and never loosens the stop.
- [ ] Confirm trailing respects the configured minimum improvement step.
- [ ] Confirm the bot does not modify other symbols or excluded labels.

## Resilience

- [ ] Check log output on broker rejection and ensure it never claims a failed stop was set.
- [ ] Restart the cBot with a protected position; confirm it keeps and only tightens the existing stop.
- [ ] Restart the cBot with an unprotected position; confirm it retries an initial stop.
- [ ] Observe behaviour through a temporary connection interruption and during high-spread/rollover periods.
- [ ] Compare costs and realised net P/L against the broker's deal history.
- [ ] Run enough demo samples across Buy/Sell and varying volatility before reviewing live deployment.

## Acceptance criteria

A test is not passed solely because the cBot builds. All failed protection modifications must be visible in logs, existing stops must never be loosened, both directions must behave correctly, and demo results must match the configured commission/spread-cost assumptions to a reasonable degree. A successful test does not remove execution or slippage risk.
