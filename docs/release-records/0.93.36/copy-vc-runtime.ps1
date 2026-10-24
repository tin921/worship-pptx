param([Parameter(Mandatory)][string]$Runtime,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
if (!$Runtime.StartsWith('win-')) { return }
$architecture=$Runtime.Substring(4)
$redist=$env:WORSHIP_VC_REDIST
if (!$redist) {
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install the Visual Studio C++ Build Tools, or set WORSHIP_VC_REDIST to its licensed CRT redist folder.' }
    $installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (!$installation) { throw 'The Visual Studio C++ tools are required to bundle the Windows ONNX runtime dependencies.' }
    $version=(Get-Content -LiteralPath (Join-Path $installation 'VC/Auxiliary/Build/Microsoft.VCRedistVersion.default.txt') -Raw).Trim()
    $folder=Join-Path $installation "VC/Redist/MSVC/$version/$architecture"
    $redist=(Get-ChildItem -LiteralPath $folder -Directory -Filter 'Microsoft.VC*.CRT' | Select-Object -First 1).FullName
}
foreach ($name in @('msvcp140.dll','msvcp140_1.dll','vcruntime140.dll','vcruntime140_1.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $redist $name))) { throw "Required runtime file missing: $name" }
}
$target=[IO.Path]::GetFullPath($Destination)
$allowed=[IO.Path]::GetFullPath("E:/WorshipPptx-work/worship-release-0.93.36")+[IO.Path]::DirectorySeparatorChar
if (!$target.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime copy destination must be under this repo''s artifacts folder.' }
New-Item -ItemType Directory -Force -Path $target | Out-Null
$records=foreach ($file in Get-ChildItem -LiteralPath $redist -File -Filter '*.dll') {
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    @{file=$file.Name;version=$file.VersionInfo.FileVersion;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$records | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'VC-RUNTIME-MANIFEST.json') -Encoding utf8
Write-Output 'Microsoft C++ runtime dependencies bundled beside the Windows executable.'
