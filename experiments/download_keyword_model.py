"""Download pinned, data-only assets for the local image-keyword experiment."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import hashlib
import json
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / ".tools" / "models" / "multilingual-minilm-int8"
REVISION = "e8f8c211226b894fcb81acc59f3b34ba3efd5f42"
REPO = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
WEIGHTS_SHA256 = "98a01d88b7de996cdea58c32ca71208c09968d143798814b2ea09d3439dc334f"
FILES = ["onnx/model_quint8_avx2.onnx", "tokenizer.json", "tokenizer_config.json",
         "config.json", "sentence_bert_config.json", "1_Pooling/config.json", "README.md"]


def download(name):
    target = MODEL / name
    target.parent.mkdir(parents=True, exist_ok=True)
    url = f"https://huggingface.co/{REPO}/resolve/{REVISION}/{name}"
    if not target.exists():
        with urllib.request.urlopen(url, timeout=90) as response, target.with_suffix(target.suffix + ".partial").open("wb") as out:
            while chunk := response.read(1024 * 1024):
                out.write(chunk)
        target.with_suffix(target.suffix + ".partial").replace(target)
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    if name.endswith(".onnx") and digest != WEIGHTS_SHA256:
        raise RuntimeError("Model SHA256 mismatch")
    print(f"Ready: {name} ({target.stat().st_size:,} bytes)", flush=True)
    return {"file": name, "bytes": target.stat().st_size, "sha256": digest, "url": url}


if __name__ == "__main__":
    with ThreadPoolExecutor(max_workers=4) as pool:
        files = list(pool.map(download, FILES))
    (MODEL / "manifest.json").write_text(json.dumps({"repository": REPO, "revision": REVISION,
        "license": "Apache-2.0", "files": files}, indent=2), encoding="utf-8")
    print(f"Downloaded and verified model assets in {MODEL}")
