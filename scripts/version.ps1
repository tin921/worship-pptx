$versionProject = Join-Path $PSScriptRoot '../src/WorshipPptx/WorshipPptx.csproj'
([xml](Get-Content -LiteralPath $versionProject -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
