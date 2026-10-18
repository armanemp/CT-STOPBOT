# CT-STOPBOT

**Adaptive stop management for cTrader Automate (C# cBot)**

Initial target: XAUUSD / Gold on the 5-minute chart. The first iteration focuses on managing positions you open manually or through another strategy; it does not open trades.

## Current capabilities

- Detects newly opened positions and checks existing positions when the cBot starts.
- Requires an M5 chart and a symbol name containing the configured string (default: XAUUSD; change the filter for a broker-specific name such as GOLD).
- Places an initial protective stop beyond recent closed-candle swing structure, with an ATR/spread-aware buffer.
- Preserves an existing stop instead of replacing it with a potentially worse initial stop.
- Estimates a cost-aware break-even price using observed position commission, a round-trip commission multiplier, current spread, and configurable slippage/cost reserves.
- Tightens an existing stop using recent closed-candle structure; never intentionally moves it away from the market.
- Logs rejected/failed modifications and retries after a cooldown.
- Includes a label filter for users who do not want to manage every position on the selected symbol.

## Important limitations

This is an early engineering baseline, not a guarantee of profit or loss-free execution. A cBot cannot guarantee that a stop is installed before the next price movement, and a broker can reject a stop that violates minimum-distance or market rules. Stop execution can slip in fast markets. Commission, swaps, conversion fees and minimum stop distances vary by broker and account; verify them with your broker and configure the reserves accordingly.

The break-even calculation is a conservative estimate, not a broker-certified net P/L calculation. The initial stop uses recent candle structure, not a promise that a candle wick is the objectively best stop location. The cBot does not calculate position size or guarantee a maximum cash risk.

## Import / run

1. Open **cTrader Desktop → Algo**.
2. Create a new **cBot** and copy **src/CT-STOPBOT/CTStopBot.cs** into it (or add the file to your cTrader cBot project).
3. Build it in cTrader so it compiles against the API version installed on your platform.
4. Run it on the broker's Gold/XAUUSD **M5** chart.
5. Inspect the parameters and logs. First use a **demo account** and test manual positions, Buy/Sell positions, spread changes, and order rejections.

This repository intentionally does not claim a successful compile: compilation and demo execution must be completed in cTrader Automate, where the matching cAlgo.API SDK is provided.

## Suggested repository layout

- **src/CT-STOPBOT/CTStopBot.cs** — cBot implementation
- **docs/STRATEGY.md** — decisions, cost model, and limitations
- **docs/TEST_PLAN.md** — demo-account test checklist

## Development stages

- [x] Repository baseline and first stop-management implementation
- [ ] Compile against the target cTrader version
- [ ] Demo validation on the user's broker/symbol specification
- [ ] Improve commission/swap and minimum-stop-distance handling from broker observations
- [ ] Add repeatable backtest and regression coverage
- [ ] Review edge cases before any live-account use
