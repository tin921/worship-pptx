param(
    [ValidateSet('win-x64','win-arm64','osx-arm64','osx-x64')][string]$Runtime = 'win-x64',
    [string]$Output,
    [string]$Version = (& "$PSScriptRoot/version.ps1")
)
. (Join-Path $PSScriptRoot 'dotnet.ps1')
& (Join-Path $PSScriptRoot 'prepare-model.ps1')
if (!$Output) { $Output = Join-Path $projectRoot "artifacts/publish/$Runtime" }
$Output = [IO.Path]::GetFullPath($Output)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$Output.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Publish output must be a folder inside this repository''s artifacts directory.' }
# Stage each build in a new folder so removed dependencies/configuration never survive a publish.
$stage = [IO.Path]::GetFullPath((Join-Path $artifactRoot ("publish-stage-$Runtime-" + [Guid]::NewGuid().ToString('N'))))
if (!$stage.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging directory.' }
& $dotnet publish (Join-Path $projectRoot 'src/WorshipPptx/WorshipPptx.csproj') -c Release -r $Runtime --self-contained true -p:PublishTrimmed=false "-p:Version=$Version" -o $stage --nologo
if ($LASTEXITCODE) { throw "Publish failed ($LASTEXITCODE)." }
if ($Runtime.StartsWith('win-')) { & (Join-Path $PSScriptRoot 'copy-vc-runtime.ps1') -Runtime $Runtime -Destination $stage }
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE'),(Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md'),(Join-Path $projectRoot 'THIRD-PARTY-LICENSES.txt') -Destination $stage -Force
if (Test-Path -LiteralPath $Output) { Remove-Item -LiteralPath $Output -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
Move-Item -LiteralPath $stage -Destination $Output
Write-Output "Self-contained $Runtime build: $Output"
