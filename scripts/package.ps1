param([string]$Version = (& "$PSScriptRoot/version.ps1"), [switch]$SkipBuild)
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
$build = Join-Path $root 'artifacts/publish/win-x64'
& (Join-Path $PSScriptRoot 'publish.ps1') -Runtime win-x64 -Output $build -Version $Version
$unexpected = Get-ChildItem -LiteralPath $build -Directory | Where-Object { $_.Name -notin @('Fonts','Models','Media') }
if ($unexpected) { throw 'Unexpected publish subfolders: update installer harvesting before packaging.' }
$dist = Join-Path $root 'dist'
$stage = Join-Path $root "artifacts\package-$Version"
$stage = [IO.Path]::GetFullPath($stage)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$stage.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package staging directory.' }
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist,$stage | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $build
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $build
# Enumerate only the intended distributable files, never developer settings or test data.
$fontManifest = Get-Content -LiteralPath (Join-Path $root 'assets\fonts\manifest.json') -Raw | ConvertFrom-Json
$fontNames = @('manifest.json') + @($fontManifest | ForEach-Object { $_.file; $_.license })
$files = @(Get-ChildItem -LiteralPath $build -File | Where-Object { $_.Extension -ne '.pdb' }) + @($fontNames | Sort-Object -Unique | ForEach-Object { Get-Item -LiteralPath (Join-Path (Join-Path $build 'Fonts') $_) }) + @(Get-ChildItem -LiteralPath (Join-Path $build 'Models'),(Join-Path $build 'Media') -File -Recurse)
$directories = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$directories['']='INSTALLFOLDER'; $directories['Fonts']='FONTSFOLDER'
$directoryXml = [System.Text.StringBuilder]::new()
foreach ($folder in (Get-ChildItem -LiteralPath (Join-Path $build 'Media'),(Join-Path $build 'Models') -Directory -Recurse) + @(Get-Item -LiteralPath (Join-Path $build 'Media'),(Join-Path $build 'Models')) | Sort-Object { $_.FullName.Length }) {
    $relative=[IO.Path]::GetRelativePath($build,$folder.FullName)
    $directoryId='D'+[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($relative))).Substring(0,24)
    $directories[$relative]=$directoryId
    $parent=[IO.Path]::GetDirectoryName($relative)
    $name=[System.Security.SecurityElement]::Escape($folder.Name)
    [void]$directoryXml.AppendLine("<DirectoryRef Id=`"$($directories[$parent])`"><Directory Id=`"$directoryId`" Name=`"$name`"/></DirectoryRef>")
}
$components = [System.Text.StringBuilder]::new()
[void]$components.AppendLine('<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi"><Fragment><ComponentGroup Id="AppFiles">')
foreach ($file in $files) {
    $relativeDirectory=[IO.Path]::GetRelativePath($build,$file.Directory.FullName)
    if ($relativeDirectory -eq '.') { $relativeDirectory='' }
    $directory=$directories[$relativeDirectory]
    $source = [System.Security.SecurityElement]::Escape($file.FullName)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $digest = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("WorshipPptx|win-x64|$directory|$($file.Name)")) } finally { $sha.Dispose() }
    $id = 'F' + [System.BitConverter]::ToString($digest,0,12).Replace('-','')
    $guid = [System.Guid]::new([byte[]]$digest[0..15]).ToString()
    [void]$components.AppendLine("<Component Id=`"$id`" Guid=`"$guid`" Directory=`"$directory`" Win64=`"yes`"><File Id=`"Payload$id`" Source=`"$source`"/><RegistryValue Root=`"HKCU`" Key=`"Software\WorshipPptx\Files`" Name=`"$id`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`"/></Component>")
}
foreach ($relative in ($directories.Keys | Where-Object { $_ -ne '' -and $_ -ne 'Fonts' })) {
    $directory=$directories[$relative]
    $digest=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("WorshipPptx|remove-directory|$relative"))
    $guid=[Guid]::new([byte[]]$digest[0..15]).ToString()
    [void]$components.AppendLine("<Component Id=`"R$directory`" Guid=`"$guid`" Directory=`"$directory`" Win64=`"yes`"><RemoveFolder Id=`"Remove$directory`" On=`"uninstall`"/><RegistryValue Root=`"HKCU`" Key=`"Software\WorshipPptx\Folders`" Name=`"$directory`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`"/></Component>")
}
[void]$components.AppendLine('</ComponentGroup></Fragment><Fragment>')
[void]$components.AppendLine($directoryXml.ToString())
[void]$components.AppendLine('</Fragment></Wix>')
$generated = Join-Path $stage 'Files.wxs'
[System.IO.File]::WriteAllText($generated,$components.ToString(),[System.Text.UTF8Encoding]::new($false))
& (Join-Path $wix 'candle.exe') -nologo -arch x64 "-dVersion=$Version" "-dProjectRoot=$root" -out "$stage\" (Join-Path $root 'installer\Product.wxs') $generated
if ($LASTEXITCODE) { throw "WiX compile failed ($LASTEXITCODE)." }
$msi = Join-Path $dist "WorshipPptx-$Version-Setup.msi"
# ICE60 expects font files in the MSI Font table; our TTFs are private assets, not installed fonts.
# ICE91 concerns dual-scope packages; this installer intentionally supports per-user only.
& (Join-Path $wix 'light.exe') -nologo -sice:ICE60 -sice:ICE91 -ext WixUIExtension -out $msi (Join-Path $stage 'Product.wixobj') (Join-Path $stage 'Files.wixobj')
if ($LASTEXITCODE) { throw "MSI packaging failed ($LASTEXITCODE)." }
$portable = Join-Path $dist "WorshipPptx-$Version-Portable.zip"
$portableStage = Join-Path $stage 'portable'
New-Item -ItemType Directory -Force -Path (Join-Path $portableStage 'Fonts') | Out-Null
foreach ($file in $files) {
    $relative=[IO.Path]::GetRelativePath($build,$file.FullName)
    $target=Join-Path $portableStage $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portable -Force
$hashes = foreach ($path in @($msi,$portable)) { '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(),[System.IO.Path]::GetFileName($path) }
$hashes | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath $msi,$portable | Select-Object Name,Length
