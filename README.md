# CT-STOPBOT-M1-Manual

**Manual XAUUSD scalp protection for cTrader Automate (C# cBot).**

This branch is the manual-trading robot only. It does not open, close, resize, or reverse trades. Attach it to the **Gold/XAUUSD M1 chart**; when you open a manual position without a Stop Loss, it attempts to install protective stop loss and then manages cost-aware break-even and structural trailing.

## M1 stop logic

- The initial Buy SL is placed below the **most recently closed M1 candle's Low**; the initial Sell SL is above that candle's High.
- It inspects up to three recently closed candles. A neighbouring candle may replace the previous candle's wick only when its relevant shadow is genuinely longer and its extreme extends by no more than the configured nearby-wick allowance (default 8 pips). It will not jump to a much more distant swing just because it exists.
- The stop adds an ATR/spread-aware buffer, rounds outward to the symbol tick size, and checks cTrader's configured minimum stop distance. If market/broker constraints make the preferred level unplaceable, it logs the fallback clearly.
- Existing manual SLs are not overwritten by the initial-stop routine. The manager tightens a stop only when break-even or trailing rules create a more protective level.
- Every position on the current chart symbol is eligible by default, independent of label. Enable the label filter if another bot also trades the same symbol and should not be managed.

## Commission, spread and risk

- Configured one-way commission is **5 account-currency units per lot per side** by default, so the assumed round trip is 10 per lot. Actual broker-reported commission is used when it is larger. The account-currency setting must match your account (for example, 5 means USD 5 only on a USD account).
- The effective spread floor is **0.25 XAU price units**. On a broker where `PipSize = 0.01`, that corresponds to 25 pips; this is a conservative assumption above the approximately 22-pip spread supplied for configuration. Verify `PipSize`, current spread and broker stop-distance diagnostics in the startup log.
- Break-even covers the estimated round-trip commission, slippage reserve and a small net-profit lock. The opening spread is already reflected in live executable Pips measured from the entry fill, while the current spread floor and broker minimum distance are checked separately before an SL change. It waits for a live favourable move and a fully closed post-entry M1 candle; default activation is around 0.9R, but the cost-coverage requirement can delay it.
- Structural trailing activates after a favourable move around 1.2R and closed-bar confirmation, then only tightens the SL. Trailing steps and ATR/spread buffers suppress small noisy updates.
- The planned stop's estimated cash risk is logged. The default 50-unit risk threshold is **warning-only**, not a guaranteed maximum loss. A manual manager cannot reliably cap a fixed 1-lot position's loss when the valid candle-wick SL is farther away; it will not silently move the stop inside the wick, change volume, or close your trade. Reduce the position or skip the setup when the estimated risk is too high.
- A stop is not a guaranteed exit price. Broker rejection, gaps, latency and slippage can increase realised losses. Commission, pip value, spread and minimum stop distance differ between brokers.

## Build on Windows

Use the separate branch and run these commands from the repository root:

```powershell
git fetch origin
git switch feature/m1-manual-scalp
git pull --ff-only origin feature/m1-manual-scalp

dotnet run --project .\tests\CT-STOPBOT.Logic.Tests\CT-STOPBOT.Logic.Tests.csproj -c Release
.\scripts\verify-cbot-source.ps1
dotnet build .\src\CT-STOPBOT\CT-STOPBOT.csproj -c Release -p:AlgoPublish=true -p:IncludeSource=false
```

The cBot artifact name is **CT-STOPBOT-M1-Manual**. After a successful build, confirm the matching `.algo` exists under `%USERPROFILE%\Documents\cAlgo\Sources\Robots\` and appears in cTrader Desktop → Algo → cBots. The branch source has to be built with your installed cTrader Automate package; this repository update itself is not a locally verified compiled artifact.

## Demo-only validation before live use

1. Attach it to the broker's actual Gold symbol on M1 and inspect the pip/tick size, spread and minimum stop-distance startup diagnostics.
2. On demo, open one small manual Buy without an SL, then one Sell without an SL. Verify each initial stop is below the previous closed candle's Low / above its High, with the nearby-wick rule and buffer visible in the log.
3. Verify a manually supplied SL is preserved, a stop is never loosened, TP stays intact, and separate positions are independently managed.
4. Let a trade reach the cost-aware break-even condition and observe the SL; compare the bot's cost estimate with the broker's commission and the closed trade's net result.
5. Observe trailing on several M1 candles and test spread spikes, market close, rejected stop modification and bot restart. If no SL is visible on the actual position, treat it as unprotected.
6. Do not run live until the M1 build, both trade directions, broker constraints and the cost model have been verified on demo.

The setup-quality panel and suggested TP are decision-support visuals only, not an entry signal, forecast or real TP order. No trading edge or profit is guaranteed.
