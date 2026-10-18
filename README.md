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
- Draws a per-position suggested TP line 20 M5 bars to the right of the active bar. It budgets distance from the persisted original risk R when recoverable, considers the nearest opposing closed-candle structure, and uses estimated spread/slippage/commission/swap costs when metadata permits. A target constrained by nearby structure remains visible as `TP!` with its R:R (or an explicit unknown-cost/risk note); it is not pushed through support/resistance or silently deleted to disguise a low R:R. This is visual guidance only and never creates or edits an actual TP order.
- Draws calculated pre-entry `ENTRY`, `SL`, and `TP` guides whenever valid structure/market data exists. A setup that fails a quality floor remains visible as dotted provisional lines with `SL!`/`TP!` and status text, rather than disappearing or pretending it is qualified. ENTRY is a conditional breakout trigger, SL uses closed-candle structure/wick clusters and broker-gap checks, and TP respects the nearest opposing structure. These are chart-only suggestions, never orders.
- Every guide spans 20 M5 candles to the left and 20 M5 candles to the right of the active candle, uses time-based chart coordinates and thickness 1, and never scrolls the viewport automatically. Labels use 14-point Segoe UI Light and an ATR/tick/pip/spread-aware price offset. Unqualified candidates are dotted and labelled as provisional; only a qualified ENTRY is gold with a star.
- Generic pre-entry ENTRY/TP lines are removed for a side when a matching position is open. The generic SL line is hidden only once all matching positions are protected; otherwise a short `SL!` warning label remains. Each open position keeps its own TP line and short label. Pre-entry lines are visual guidance only, not active orders; no entry is opened and no real TP is created or modified.
- Hard quality floors require cost-adjusted R:R of at least 1.8 after execution-cost reserves, SL distance from 0.25 to 2.25 ATR, spread/ATR at most 0.16, directional alignment at least 0.65, no extended momentum, and closed-candle trend and momentum confirming the side. The pre-entry commission reserve is separately configurable because a future position's volume and account commission schedule are not known yet. These filters are engineering screens, not evidence of a statistically validated trading edge.

## Repository workflow (Windows)

The repository can live anywhere; it does not need to be inside cTrader's folders. The project file is `src/CT-STOPBOT/CT-STOPBOT.csproj`.

1. Install Git for Windows and the .NET 6 SDK (the cBot target is net6.0).
2. Pull the `feature/stop-management-v0.1` branch.
3. Run the logic tests, source-invariant checks and release build from the repository root:

```powershell
dotnet run --project .\tests\CT-STOPBOT.Logic.Tests\CT-STOPBOT.Logic.Tests.csproj -c Release
.\scripts\verify-cbot-source.ps1
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
