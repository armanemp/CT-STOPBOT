# Static regression checks for visual and risk-management invariants.
$ErrorActionPreference = "Stop"

$sourcePath = Join-Path $PSScriptRoot "..\src\CT-STOPBOT\CTStopBot.cs"
$source = Get-Content -Raw -LiteralPath $sourcePath

$checks = @(
    @{ Name = "compact guide line span"; Pattern = 'GuideLineBarsLeft\s*=\s*20\s*;[\s\S]*GuideLineBarsRight\s*=\s*20\s*;' },
    @{ Name = "time-based trend-line coordinates"; Pattern = 'Chart\.DrawTrendLine\(lineName,\s*startTime,' },
    @{ Name = "time-based guide text coordinates"; Pattern = 'Chart\.DrawText\(textName,\s*labelText,\s*endTime,' },
    @{ Name = "no future-index trend-line drawing"; Pattern = '(?!)Chart\.DrawTrendLine\(lineName,\s*startBarIndex' },
    @{ Name = "shared pure risk/reward engine"; Pattern = 'RiskRewardEngine\.TryCalculateNetRewardRisk' },
    @{ Name = "initial R used for position target"; Pattern = 'Always anchor the TP budget to the original R' },
    @{ Name = "low-RR targets explicitly marked"; Pattern = 'context \+= "/RRLOW";' },
    @{ Name = "pre-entry qualification uses retained displayed prices"; Pattern = 'PredictDisplayedMarketGuidePrice\(' },
    @{ Name = "no chart auto-scroll side effects"; Pattern = '(?!)ScrollXBy' }
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
if ($source -match 'Suggested TP hidden: target does not meet the minimum cost-adjusted reward/risk filter') {
    throw "Invariant failed: low-R:R structural TP is still removed instead of shown as unqualified."
}
if ($source -notmatch 'MinimumQualityRewardRisk\s*=\s*1\.8\s*;') {
    throw "Invariant failed: minimum net R:R policy changed unexpectedly."
}

Write-Host "PASS: cBot drawing, initial-R, and risk/reward invariants." -ForegroundColor Green
