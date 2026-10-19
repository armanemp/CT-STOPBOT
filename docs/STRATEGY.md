# CT-STOPBOT-M1-Manual — Strategy and implementation notes

## Scope and safe use

This cBot manages positions that already exist on the attached chart symbol. It never opens a trade, changes volume, or intentionally closes a position. The expected chart is **Gold/XAUUSD M1**; the symbol filter defaults to `XAUUSD`, so configure it only after verifying the broker's actual symbol alias (for example, `GOLD`).

By default, all positions on that symbol are eligible, including manually opened positions and potentially positions opened by another cBot. Enable the label restriction if needed. A position that already has an SL keeps it: the initial-stop routine only installs an SL when the position has none. Native server-side break-even positions are skipped because this source version does not use a verified typed modification overload that preserves that setting. Native server-side trailing positions are skipped by default to avoid competing managers.

## Initial M1 stop: previous candle wick

For an unprotected Buy, the main anchor is the Low of the latest **closed** M1 candle; for an unprotected Sell, it is the High of that candle. The currently forming candle is never used as the initial anchor.

The robot then inspects up to three closed candles (configurable). An older neighbouring candle is considered only when:
- its relevant wick points in the protective direction and has a meaningful shadow ratio;
- that shadow is longer than the primary candle's corresponding shadow; and
- its extreme extends beyond the primary candle by no more than the configured allowance (default **8 pips**).

This implements the "slightly longer nearby wick" rule without jumping to a much more distant swing. The selected wick is padded using the minimum wick buffer, ATR and an effective-spread floor. SL prices are rounded outward to the symbol tick size. cTrader's minimum stop distance and the live executable-side quote are also checked.

If the wick stop is no longer placeable relative to the live quote or broker rules, the robot may compute a separate emergency market-gap level and logs that this is a fallback, not the preferred wick structure. This can create a wider cash risk; inspect the position immediately. An SL is only confirmed after cTrader's refreshed position state shows the requested price.

## Commission and spread model

- `Minimum one-way commission per lot` defaults to 5.0 account-currency units. The conservative round-trip estimate is at least 10.0 per lot. Actual reported commission, multiplied by the configured round-trip factor, is used if that estimate is larger.
- The commission parameter is denominated in the account's deposit currency. If the deposit currency is not USD, convert the broker's USD 5-per-side commission to account currency before relying on the default.
- The effective spread floor defaults to **0.25 XAU price units**. If `PipSize = 0.01`, this is 25 pips—slightly above the approximately 22 pips described for the configured broker. The floor is deliberately price-unit based; confirm the actual startup diagnostics. If the broker's quote conventions differ, adjust this parameter.
- For the initial SL, the entry-to-stop price distance already incorporates the spread paid between entry-side and exit-side quotes. The cash-risk estimate therefore adds round-trip commission and slippage reserve but does not add the spread twice as a separate cash fee.
- The break-even offset includes round-trip commission and fees, accrued negative swap, slippage reserve and a small net-lock amount. The opening spread is already reflected in live executable Pips measured from the entry fill, so it is not added a second time to the offset. Effective spread and broker minimum distance still constrain whether the new SL can be placed. This should be checked against actual closed-trade net P/L.

## Break-even activation

Default activation is 0.90R (with a hard floor of 0.75R), and requires both a favourable live move and at least one fully closed post-entry M1 candle confirming the configured R threshold. It also waits for the estimated execution-cost offset plus an extra activation margin. That means it will not necessarily move at 0.90R if the commission/slippage cost threshold has not been met. The SL only changes if the candidate improves protection and is currently acceptable under market/broker-distance checks.

No stop algorithm can guarantee a net non-negative exit. Slippage, gaps, commission conversion, spread spikes, broker rules and the exact fill can change realised P/L.

## Structural trailing

Default trailing activation is 1.20R (with a hard floor of 1.0R) plus a closed-candle confirmation after entry. Once activated, the bot uses recent closed-candle structure, ATR and spread-aware buffers to propose a more protective stop. It never intentionally loosens an existing SL. In normal operation, the candidate must improve the current level by the configured trailing step; the BE-only one-tick path is handled separately. Adaptive trailing is tighter after larger favourable movement and adapts its buffer to recent/slow ATR.

A structure candidate that has already been crossed or is too close to the live quote is skipped rather than snapped inward. Native server trailing is left alone by default.

## Estimated cash-risk warning for fixed manual size

When it proposes the initial wick stop, the bot estimates initial adverse cash risk from the entry-to-stop price distance, volume-specific pip value, estimated round-trip commissions and slippage reserve. It logs the estimated lot amount and risk. The default threshold of 50 account-currency units is **warning-only**.

This is intentional: a manual management bot cannot both preserve a valid wick stop and guarantee a fixed maximum cash loss for a pre-existing 1-lot position when the wick is too far away. It must not pretend otherwise by moving the SL inside the candle's invalidation point, silently resizing the trade, or force-closing the user's position. If the estimate is too high, reduce size or skip the setup; if a broker-distance fallback widens the stop, review the new estimate and exposure.

The warning estimate is not a guaranteed maximum loss. A stop can be rejected, gap through, slip, or execute at a worse price. The actual money value of a pip is broker/account-dependent and should be confirmed using demo trade history.

## Visual decision support

This branch retains the optional pre-entry ENTRY/SL/TP guides, per-position suggested TP lines and status overlay. These are chart drawings only: they do not create entry orders or modify the real take-profit order. The TP model uses closed-candle indicators and opposing structure, and marks constrained/low-quality candidates rather than disguising poor reward/risk. Any alignment score is heuristic, not a measured probability.

Guides use time-based coordinates, 20 bars left/right, line thickness 1 and no chart auto-scroll. Chart updates are smoothed to reduce repeated object redraws. The guide stop is changed to the same previous-wick rule used by the initial stop so the displayed SL should be directionally consistent with the installed structural SL, although live quote and broker constraints can still force a fallback.

## Build and verification

From the repo root on Windows:

```powershell
dotnet run --project .\tests\CT-STOPBOT.Logic.Tests\CT-STOPBOT.Logic.Tests.csproj -c Release
.\scripts\verify-cbot-source.ps1
dotnet build .\src\CT-STOPBOT\CT-STOPBOT.csproj -c Release -p:AlgoPublish=true -p:IncludeSource=false
```

The configured artifact name is `CT-STOPBOT-M1-Manual`. A repository/source update is not evidence that the installed local SDK compiled successfully; build using the user's installed cTrader Automate package and verify the published artifact matches the latest source.

## Demo validation required

Test on demo before any live use: a small manual Buy and Sell without SL; a position with a pre-existing SL; several independent positions; wider-than-normal spread and spread spikes; rejected SL modifications; restarting the bot with a naked position; cost-aware break-even; M1 closed-bar trailing; and the bot's handling of native server-side stop settings. Verify the SL directly on the live position after every change, and compare the estimate with final net P/L.
