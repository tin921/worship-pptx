param([switch]$SkipTests)
. (Join-Path $PSScriptRoot 'dotnet.ps1')
& (Join-Path $PSScriptRoot 'prepare-model.ps1')
Push-Location $projectRoot
try {
    & $dotnet build 'tests/WorshipPptx.Tests.csproj' -c Release --nologo
    if ($LASTEXITCODE) { throw "Build failed ($LASTEXITCODE)." }
    if (!$SkipTests) {
        & $dotnet 'tests/bin/Release/net10.0/WorshipPptx.Tests.dll' 'artifacts/verification'
        if ($LASTEXITCODE) { throw "Tests failed ($LASTEXITCODE)." }
    }
} finally { Pop-Location }
