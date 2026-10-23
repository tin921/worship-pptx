param([string]$Version=(& "$PSScriptRoot/version.ps1"))
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$build=Join-Path $root 'artifacts/publish/win-x64'
$stage=Join-Path $root "artifacts/package-$Version"
$inspect=[IO.Path]::GetFullPath((Join-Path $root "artifacts/package-inspection-$Version"))
$allowed=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if (!$inspect.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe inspection directory.' }
if (Test-Path -LiteralPath $inspect) { Remove-Item -LiteralPath $inspect -Recurse -Force }
New-Item -ItemType Directory -Force -Path $inspect | Out-Null
$msi=Join-Path $root "dist/WorshipPptx-$Version-Setup.msi"
& (Join-Path $root '.tools/wix/dark.exe') -nologo -x $inspect -o (Join-Path $inspect 'package.wxs') $msi
if ($LASTEXITCODE) { throw 'MSI extraction failed.' }
$manifest=[xml](Get-Content -LiteralPath (Join-Path $stage 'Files.wxs') -Raw)
$ns=[Xml.XmlNamespaceManager]::new($manifest.NameTable); $ns.AddNamespace('w','http://schemas.microsoft.com/wix/2006/wi')
$files=$manifest.SelectNodes('//w:File',$ns)
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $root "dist/WorshipPptx-$Version-Portable.zip"))
try {
    foreach ($file in $files) {
        $expected=(Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash
        $extracted=Join-Path (Join-Path $inspect 'File') $file.Id
        if (!(Test-Path -LiteralPath $extracted)) { throw "Missing MSI payload: $($file.Id)" }
        if ((Get-FileHash -LiteralPath $extracted -Algorithm SHA256).Hash -ne $expected) { throw "MSI payload mismatch: $($file.Source)" }
        $relative=[IO.Path]::GetRelativePath($build,$file.Source).Replace('\','/')
        $entry=$zip.GetEntry($relative)
        if (!$entry) { throw "Missing portable payload: $relative" }
        $stream=$entry.Open()
        try { $actual=(Get-FileHash -InputStream $stream -Algorithm SHA256).Hash } finally { $stream.Dispose() }
        if ($actual -ne $expected) { throw "Portable payload mismatch: $relative" }
    }
} finally { $zip.Dispose() }
$report=@{ version=$Version; windows_payload_files=$files.Count; msi_payload_matches=$true; portable_payload_matches=$true; media_choices=270; native_install_tested=$false; generated_utc=[DateTime]::UtcNow.ToString('o') }
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'artifacts/package-validation-media.json') -Encoding utf8
Write-Output "MSI and portable ZIP match all $($files.Count) intended payload files. Nothing was installed."
