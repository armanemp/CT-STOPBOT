# Static regression checks for visual and risk-management invariants.
$ErrorActionPreference = "Stop"

$sourcePath = Join-Path $PSScriptRoot "..\src\CT-STOPBOT\CTStopBot.cs"
$source = Get-Content -Raw -LiteralPath $sourcePath

$checks = @(
    @{ Name = "compact guide line span"; Pattern = 'GuideLineBarsLeft\s*=\s*20\s*;[\s\S]*GuideLineBarsRight\s*=\s*20\s*;' },
    @{ Name = "time-based trend-line coordinates"; Pattern = 'Chart\.DrawTrendLine\(lineName,\s*startTime,' },
    @{ Name = "time-based guide text coordinates"; Pattern = 'Chart\.DrawText\(textName,\s*labelText,\s*endTime,' },
    @{ Name = "shared pure risk/reward engine"; Pattern = 'RiskRewardEngine\.TryCalculateNetRewardRisk' },
    @{ Name = "initial R used for position target"; Pattern = 'Always anchor the TP budget to the original R' },
    @{ Name = "low-RR targets explicitly marked"; Pattern = 'context \+= "/RRLOW";' },
    @{ Name = "pre-entry qualification uses retained displayed prices"; Pattern = 'PredictDisplayedMarketGuidePrice\(' },
    @{ Name = "risk/reward is calculated from cost-adjusted price distances"; Pattern = 'TryCalculateNetRewardRisk' },
    @{ Name = "pre-entry commission reserve is explicit"; Pattern = 'PreEntryCommissionReservePips' },
    @{ Name = "live positions use separate cost reserve"; Pattern = 'GetBaseExecutionCostReservePrice' },
    @{ Name = "position TP marks unknown original risk"; Pattern = 'context \+= "/R_UNKNOWN";' },
    @{ Name = "position TP marks unknown costs"; Pattern = 'context \+= "/COST_UNKNOWN";' },
    @{ Name = "near structure warning is explicit"; Pattern = 'context \+= "/TIGHT";' },
    @{ Name = "labels use wider ATR gap"; Pattern = 'atrPrice \* 0\.25' },
    @{ Name = "initial stop is kept on loss side of ENTRY"; Pattern = 'rawPrice = Math\.Min\(rawPrice, position\.EntryPrice - minimumEntryRiskGap\)' },
    @{ Name = "original R saved only from directionally-valid stop"; Pattern = 'var confirmedRisk = riskDirection \*' },
    @{ Name = "emergency initial stop is available without structure"; Pattern = 'TryCalculateEmergencyInitialStop\(' },
    @{ Name = "emergency initial stop is loss-side"; Pattern = 'return direction \* \(position\.EntryPrice - stopPrice\) > Symbol\.TickSize;' },
    @{ Name = "TP label remains concise"; Pattern = '"{0} \| \{1:F1\} pips"' },
    @{ Name = "TP quality lives in status summary"; Pattern = 'GetOpenPositionTpQualitySummary\(' },
    @{ Name = "next TP extends only after current target is reached"; Pattern = 'Do not move the target away merely because price is approaching it\.' },
    @{ Name = "TP structure refresh overrides smoothing"; Pattern = 'newBar && structureIsCurrentlyCappingTarget' },
    @{ Name = "TP structure refresh overrides cooldown"; Pattern = 'if \(cooldownActive && !mustAdoptCandidate\)' },
    @{ Name = "position TP caps the outward-rounded target"; Pattern = 'targetForBarrierCheck = NormalizeSuggestedTpPrice\(target, position\.TradeType\)' },
    @{ Name = "pre-entry TP caps the outward-rounded target"; Pattern = 'targetForBarrierCheck = NormalizeSuggestedTpPrice\(target, tradeType\)' }
)

foreach ($check in $checks) {
    if ($source -notmatch $check.Pattern) {
        throw "Invariant failed: $($check.Name)"
    }
}

if ($source -match 'Chart\.DrawTrendLine\(lineName,\s*startBarIndex') {
    throw "Invariant failed: future bar indices are still used for trend lines."
}
if ($source -match 'Chart\.DrawText\(textName,\s*labelText,\s*endBarIndex') {
    throw "Invariant failed: future bar indices are still used for guide labels."
}
if ($source -match 'target - referencePrice\)\s*<=\s*minimumTargetDistance') {
    throw "Invariant failed: the next TP is advanced while price is still approaching the current target."
}
if ($source -notmatch 'contextChanged \|\|\s*\(newBar && structureIsCurrentlyCappingTarget\)') {
    throw "Invariant failed: context/structure changes do not force a TP refresh."
}
if ($source -match 'ScrollXBy') {
    throw "Invariant failed: chart viewport auto-scrolling was reintroduced."
}
if ($source -match 'Suggested TP hidden: target does not meet the minimum cost-adjusted reward/risk filter') {
    throw "Invariant failed: low-R:R structural TP is still removed instead of shown as unqualified."
}
if ($source -notmatch 'MinimumQualityRewardRisk\s*=\s*1\.8\s*;') {
    throw "Invariant failed: minimum net R:R policy changed unexpectedly."
}

Write-Host "PASS: cBot drawing, initial-R, and risk/reward invariants." -ForegroundColor Green
