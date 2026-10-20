param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = if (Get-Command msbuild -ErrorAction SilentlyContinue) { (Get-Command msbuild).Source } elseif (Test-Path -LiteralPath $vswhere) { & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
if (!$msbuild) { throw 'Install Visual Studio Build Tools 2022 with MSBuild and .NET desktop build tools.' }
Push-Location $root
try {
    & $msbuild 'tests\WorshipPptx.Tests.csproj' /restore /p:Configuration=Release /v:minimal /nologo
    if ($LASTEXITCODE) { throw "Build failed ($LASTEXITCODE)." }
    if (!$SkipTests) {
        & '.\tests\bin\Release\WorshipPptx.Tests.exe' '.\artifacts\verification'
        if ($LASTEXITCODE) { throw "Tests failed ($LASTEXITCODE)." }
    }
} finally { Pop-Location }
