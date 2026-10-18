# CT-STOPBOT

**Adaptive stop management for cTrader Automate (C# cBot)**

Initial target: XAUUSD / Gold on the 5-minute chart. The first iteration focuses on managing positions you open manually or through another strategy; it does not open trades.

## Current capabilities

- Detects newly opened positions and checks existing positions when the cBot starts.
- Requires an M5 chart and a symbol name containing the configured string (default: XAUUSD; change the filter for a broker-specific name such as GOLD).
- Places an initial protective stop beyond recent closed-candle swing structure, with an ATR/spread-aware buffer.
- Preserves an existing stop instead of replacing it with a potentially worse initial stop.
- Estimates a cost-aware break-even price using one-way opening commission, already-accrued negative swap, a round-trip commission multiplier, current spread, and configurable slippage/cost reserves.
- Uses the broker-reported minimum stop-loss distance where available (in pips or percentage units), alongside the configured minimum gap.
- Prints diagnostic symbol data at startup, including spread, pip/tick sizes, pip value, and broker minimum stop distance.
- Tightens an existing stop using recent closed-candle structure; never intentionally moves it away from the market.
- Logs rejected/failed modifications and retries with exponential backoff.
- Uses emergency fallback only for missing initial protection; an invalidated trailing candidate is skipped rather than snapped to market.
- Includes a label filter for users who do not want to manage every position on the selected symbol.
- Supports independent switches for initial stop, break-even and structural trailing; position-level exceptions are logged instead of silently aborting the management loop.
- Keeps stop-management state keyed by position ID, so multiple matching-symbol positions are processed independently. By default, all labels on the chart symbol are eligible, including manual positions; other cBots on that symbol may also be affected. Native server-side trailing positions are left alone by default. Positions carrying native server-side break-even are skipped until the typed API is verified against the pinned package and demo-tested.
- Supports a minimum commission-cost reserve for broker schedules where the observed opening commission is zero or below the true round-trip minimum.
- Draws a short, dotted, per-position suggested TP line (green for Buy, red for Sell) based on closed-candle EMA 9/21, 3-bar momentum, RSI 14, ATR distance, and a nearby historical support/resistance level. A movement threshold and cooldown suppress small fluctuations; if price reaches the current suggestion, the line is recalculated promptly. The line is informational only and never creates or changes an actual TP order.
- Shows pre-entry BUY and SELL scenarios before a trade: candidate SL/TP levels, reward-to-risk estimate, trend (EMA 9/21/50), three-bar momentum, RSI, ATR regime, spread-to-ATR condition, and a heuristic alignment index. SL uses the latest confirmed swing pivot where available, with a labelled range-extreme fallback; after entry, the bot's initial SL calculation uses the same pivot preference. SL scenarios are hidden for a direction once all matching open positions have protection; if any matching position is unprotected, the panel warns explicitly. The live TP line remains independently attached to each open position after entry.
- Pre-entry quality labels apply configurable spread/ATR, maximum SL-distance-in-ATR, and minimum reward/risk filters, and warn when both trend and momentum oppose a side. Defaults are 2.5 ATR maximum SL distance and 1.5 minimum R:R. These are screening thresholds, not proof of positive expectancy. Prices use closed-candle structure plus current ATR/spread and broker distance checks. Lines are smoothed by minimum-move thresholds/cooldown and TP suggestions refresh promptly after their target is reached. The alignment index is not a probability, the display does not generate entry orders, and a visible candidate is never an active stop/TP order.

## Repository workflow (Windows)

The repository can live anywhere; it does not need to be inside cTrader's folders. The project file is `src/CT-STOPBOT/CT-STOPBOT.csproj`.

1. Install Git for Windows and the .NET 6 SDK (the current project targets net6.0).
2. Clone the development branch into your own workspace.
3. Run the following from the repository root in PowerShell:

```powershell
dotnet build .\src\CT-STOPBOT\CT-STOPBOT.csproj -c Release -p:AlgoPublish=true -p:IncludeSource=false
```

The cTrader.Automate build targets the `.algo` artifact and publishes it to the standard Windows cTrader Robots source folder, `%USERPROFILE%\Documents\cAlgo\Sources\Robots\`. Source code remains in this repository; the published `.algo` is built without embedded source. Intermediate build files remain in the repository's ignored `bin` and `obj` folders.

## Important limitations

This is an early engineering baseline, not a guarantee of profit or loss-free execution. A cBot cannot guarantee that a stop is installed before the next price movement, and a broker can reject a stop that violates minimum-distance or market rules. Stop execution can slip in fast markets. Commission, swaps, conversion fees and minimum stop distances vary by broker and account; verify them with your broker and configure the reserves accordingly.

The break-even calculation is a conservative estimate, not a broker-certified net P/L calculation. The initial stop uses recent candle structure, not a promise that a candle wick is the objectively best stop location. The cBot does not calculate position size or guarantee a maximum cash risk. Pre-entry SL/TP lines and the market-condition panel are decision support, not entry signals or active orders. Their alignment index is heuristic and has not been statistically calibrated. The TP suggestion is not a forecast guarantee; the cBot never submits or edits an actual take-profit order.

## Test before live use

After a successful build, open cTrader Desktop → Algo → cBots and confirm the cBot appears. Run it on a demo account on the broker's Gold/XAUUSD M5 chart. Test manual Buy/Sell positions, existing/no stop, spread changes, cost assumptions, broker rejection and restart behaviour. Do not use a live account until the installed cTrader API version builds cleanly and the broker-specific demo checklist passes.

## Project layout

- `src/CT-STOPBOT/CTStopBot.cs` — cBot implementation
- `src/CT-STOPBOT/CT-STOPBOT.csproj` — build configuration
- `docs/STRATEGY.md` — decisions, cost model, and limitations
- `docs/TEST_PLAN.md` — demo-account test checklist
- `docs/REPORT_FA.md` — detailed Persian audit, behaviour guide, known limitations and demo plan
