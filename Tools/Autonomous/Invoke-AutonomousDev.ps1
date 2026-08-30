[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Requirement,
    [int]$MaxAgentTurns = 12,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$protocol = Get-Content (Join-Path $PSScriptRoot 'AGENT_PROMPT.md') -Raw
$gate = if ($SkipBuild) { '.\Tools\Autonomous\Invoke-QualityGate.ps1 -SkipBuild' } else { '.\Tools\Autonomous\Invoke-QualityGate.ps1' }
$prompt = @"
$protocol

用户需求：
$Requirement

本回合最多进行 $MaxAgentTurns 次实现/验收迭代。验收命令：$gate
完成后给出简洁的最终变更、验证结果和 Git commit id。
"@

Push-Location $root
try {
    & codex --cd $root --sandbox workspace-write --approve-for-me exec $prompt
    if ($LASTEXITCODE -ne 0) { throw "Codex autonomous turn failed with exit code $LASTEXITCODE." }
}
finally { Pop-Location }
