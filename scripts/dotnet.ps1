$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Find-DotNet {
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($installed) { return $installed.Source }
    foreach ($name in @('dotnet.exe','dotnet')) {
        $local = Join-Path $projectRoot ".tools/dotnet/$name"
        if (Test-Path -LiteralPath $local) { return $local }
    }
    throw 'Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0.'
}
$dotnet = Find-DotNet
