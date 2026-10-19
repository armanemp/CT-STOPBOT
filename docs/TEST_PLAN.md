# Demo test plan — CT-STOPBOT-M1-Manual

**Do not run live until each safety check below passes on the actual broker's Gold symbol.** This robot manages manual positions only; it does not open or resize trades.

## Build and symbol setup

- [ ] Run the logic test project and `scripts/verify-cbot-source.ps1`.
- [ ] Build the Release cBot using the installed cTrader Automate package.
- [ ] Confirm the resulting artifact is named `CT-STOPBOT-M1-Manual` and the file in cTrader's Robots folder matches the latest build.
- [ ] Start on the broker's XAUUSD/Gold **M1** chart. Confirm it refuses M5 and any unapproved symbol.
- [ ] Confirm the broker symbol alias, PipSize, TickSize, PipValue, current spread and minimum stop-loss distance in startup diagnostics.
- [ ] Confirm whether all same-symbol positions or only a specific label should be managed.
- [ ] For a USD account with the stated schedule, confirm one-way commission is USD 5 per lot and set the account-currency parameter accordingly. Confirm the effective spread floor in price units; 0.25 means 25 pips only if PipSize = 0.01.

## Initial stop / nearby wick

- [ ] On demo, open a small Buy without an SL. Verify the installed SL sits below the most recently closed M1 candle's Low, plus its protective buffer.
- [ ] Open a small Sell without an SL. Verify the installed SL sits above the most recently closed M1 candle's High, plus its protective buffer.
- [ ] Create a case where a neighbouring candle has a slightly more extreme and genuinely longer relevant wick, within the configured extension allowance. Verify it is selected.
- [ ] Create a case where the neighbouring wick extends beyond the allowance or does not have a longer relevant shadow. Verify it is ignored.
- [ ] Confirm the currently forming M1 candle is never the initial anchor.
- [ ] Confirm a pre-existing manual SL is not overwritten by the initial-stop routine.
- [ ] Inspect any fallback log when the preferred wick level is too close to the current quote or is rejected by the broker; verify the actual SL on the position, not only the log.
- [ ] Check initial risk estimate, volume in lots, commission reserve and warning threshold. Verify a risk above the threshold produces a warning and that it does not silently move the SL inside the wick.

## Break-even and trailing

- [ ] Confirm the BE candidate does not activate merely from unrealised profit before a fully closed post-entry M1 candle confirms the R threshold.
- [ ] Confirm BE waits until round-trip commission, negative-swap reserve, slippage and the minimum net-lock reserve are covered; the opening spread should not be counted twice in live Pips.
- [ ] Compare the required offset with actual broker commission and final net P/L for a closed trade. Tune only after recording the difference.
- [ ] Confirm BE only improves the stop and never loosens it.
- [ ] Confirm trailing starts after the configured R threshold and closed-candle confirmation, then updates using closed-candle structure.
- [ ] Confirm the trailing step suppresses tiny modifications and that a candidate crossed by price or inside broker minimum distance is skipped instead of forced inward.
- [ ] Verify TP is unchanged when modifying SL, and each same-symbol position is managed independently.
- [ ] Confirm native server-side trailing positions are left alone by default and native server-side break-even positions are skipped.

## Resilience and restart

- [ ] Test spread spikes and the market reopening after a quiet/closed period. BE/trailing should pause during a detected spread spike; the current SL must remain present.
- [ ] Test broker rejections, freeze distance, and retry/backoff logs.
- [ ] Restart the cBot with a naked demo position and verify it tries to protect it; immediately confirm whether the position actually has an SL.
- [ ] Check multiple manual positions and ensure one position's failure does not stop the others from being processed.
- [ ] Test suggested TP and pre-entry SL/TP guides: labels should remain visual only, no entry order should open, and the actual Take Profit must remain unchanged.

## Acceptance criteria

- [ ] All successful SL modifications are confirmed by refreshed position state and visible in cTrader.
- [ ] No observed path intentionally widens an existing stop.
- [ ] Both Buy and Sell tests pass with the broker's real pip/tick conventions.
- [ ] Commission/spread settings align with demo history.
- [ ] The user understands the 50 account-currency risk threshold is warning-only and a 1-lot manual position can exceed it if the structural wick is distant or the broker forces a wider SL.

Passing these tests does not establish profitability or eliminate execution risk. Keep the first runs on demo and compare actual results with the estimates.
