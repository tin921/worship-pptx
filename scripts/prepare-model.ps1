$ErrorActionPreference = 'Stop'
$modelRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets/models'
New-Item -ItemType Directory -Force -Path $modelRoot | Out-Null
$revision = 'e8f8c211226b894fcb81acc59f3b34ba3efd5f42'
$files = @(
    @{ Name='model.onnx'; Source='onnx/model_quint8_avx2.onnx'; Hash='98a01d88b7de996cdea58c32ca71208c09968d143798814b2ea09d3439dc334f' },
    @{ Name='sentencepiece.bpe.model'; Source='sentencepiece.bpe.model'; Hash='cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865' }
)
foreach ($file in $files) {
    $target = Join-Path $modelRoot $file.Name
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $file.Hash) { continue }
    $partial = "$target.partial"
    Invoke-WebRequest "https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/$revision/$($file.Source)" -OutFile $partial
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $file.Hash) { throw "Model checksum mismatch: $($file.Name)" }
    Move-Item -LiteralPath $partial -Destination $target -Force
}
Write-Output 'Local matching model is ready (verified, bundled for offline use).'
