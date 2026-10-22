param([switch]$SkipBuild)
. (Join-Path $PSScriptRoot 'dotnet.ps1')
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'publish.ps1') -Runtime win-x64 }
$app = Join-Path $projectRoot 'artifacts/publish/win-x64/WorshipPptx.exe'
if (!(Test-Path -LiteralPath $app)) { throw 'Build the Windows app first with scripts/publish.ps1.' }
# This launcher intentionally opens the interactive application for local review.
Start-Process -FilePath $app -WorkingDirectory (Split-Path -Parent $app)
