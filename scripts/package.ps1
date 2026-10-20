param([string]$Version = '1.0.0', [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part MSI version.' }
$root = Split-Path -Parent $PSScriptRoot
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
$toolsFolder = Join-Path $root '.tools'
$wix = Join-Path $toolsFolder 'wix'
$archive = Join-Path $toolsFolder 'wix.zip'
$expected = '6AC824E1642D6F7277D0ED7EA09411A508F6116BA6FAE0AA5F2C7DAA2FF43D31'
if (!(Test-Path -LiteralPath (Join-Path $wix 'candle.exe'))) {
    New-Item -ItemType Directory -Force -Path $toolsFolder | Out-Null
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip' -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'WiX download checksum mismatch.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $wix -Force
}
$build = Join-Path $root 'src\WorshipPptx\bin\Release'
$dist = Join-Path $root 'dist'
$stage = Join-Path $root "artifacts\package-$Version"
New-Item -ItemType Directory -Force -Path $dist,$stage | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $build
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $build
# Enumerate only the intended distributable files, never developer settings or test data.
$fontManifest = Get-Content -LiteralPath (Join-Path $root 'assets\fonts\manifest.json') -Raw | ConvertFrom-Json
$fontNames = @('manifest.json') + @($fontManifest | ForEach-Object { $_.file; $_.license })
$files = @(Get-ChildItem -LiteralPath $build -File | Where-Object { $_.Extension -in '.exe','.config','.md' -or $_.Name -eq 'LICENSE' }) + @($fontNames | Sort-Object -Unique | ForEach-Object { Get-Item -LiteralPath (Join-Path (Join-Path $build 'Fonts') $_) })
$components = [System.Text.StringBuilder]::new()
[void]$components.AppendLine('<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi"><Fragment><ComponentGroup Id="AppFiles">')
foreach ($file in $files) {
    $directory = if ($file.Directory.Name -eq 'Fonts') { 'FONTSFOLDER' } else { 'INSTALLFOLDER' }
    $source = [System.Security.SecurityElement]::Escape($file.FullName)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $digest = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("WorshipPptx|$directory|$($file.Name)")) } finally { $sha.Dispose() }
    $id = 'F' + [System.BitConverter]::ToString($digest,0,12).Replace('-','')
    $guid = [System.Guid]::new([byte[]]$digest[0..15]).ToString()
    [void]$components.AppendLine("<Component Id=`"$id`" Guid=`"$guid`" Directory=`"$directory`"><File Id=`"Payload$id`" Source=`"$source`"/><RegistryValue Root=`"HKCU`" Key=`"Software\WorshipPptx\Files`" Name=`"$id`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`"/></Component>")
}
[void]$components.AppendLine('</ComponentGroup></Fragment></Wix>')
$generated = Join-Path $stage 'Files.wxs'
[System.IO.File]::WriteAllText($generated,$components.ToString(),[System.Text.UTF8Encoding]::new($false))
& (Join-Path $wix 'candle.exe') -nologo -arch x86 "-dVersion=$Version" "-dProjectRoot=$root" -out "$stage\" (Join-Path $root 'installer\Product.wxs') $generated
if ($LASTEXITCODE) { throw "WiX compile failed ($LASTEXITCODE)." }
$msi = Join-Path $dist "WorshipPptx-$Version-Setup.msi"
# ICE60 expects font files in the MSI Font table; our TTFs are private assets, not installed fonts.
# ICE91 concerns dual-scope packages; this installer intentionally supports per-user only.
& (Join-Path $wix 'light.exe') -nologo -sice:ICE60 -sice:ICE91 -ext WixUIExtension -ext WixNetFxExtension -out $msi (Join-Path $stage 'Product.wixobj') (Join-Path $stage 'Files.wixobj')
if ($LASTEXITCODE) { throw "MSI packaging failed ($LASTEXITCODE)." }
$portable = Join-Path $dist "WorshipPptx-$Version-Portable.zip"
$portableStage = Join-Path $stage 'portable'
New-Item -ItemType Directory -Force -Path (Join-Path $portableStage 'Fonts') | Out-Null
foreach ($file in $files) { $target = if ($file.Directory.Name -eq 'Fonts') { Join-Path $portableStage 'Fonts' } else { $portableStage }; Copy-Item -LiteralPath $file.FullName -Destination $target -Force }
Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portable -Force
$hashes = foreach ($path in @($msi,$portable)) { '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(),[System.IO.Path]::GetFileName($path) }
$hashes | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath $msi,$portable | Select-Object Name,Length
