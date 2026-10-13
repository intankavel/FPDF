"""Archive Python OCR packages and source distributions with their origins."""
import concurrent.futures
import json
from pathlib import Path
from prepare_ocr_import import download

root = Path(__file__).resolve().parents[1]
destination = root / "vendor" / "ocr" / "python-sources"
destination.mkdir(parents=True, exist_ok=True)
requirements = (root / "scripts" / "ocr-requirements.txt").read_text().splitlines()

def fetch(requirement):
    import urllib.request
    name, version = requirement.split("==")
    with urllib.request.urlopen(f"https://pypi.org/pypi/{name}/{version}/json") as response:
        metadata = json.load(response)
    source = next(item for item in metadata["urls"] if item["packagetype"] == "sdist")
    record = download(source["url"], destination / source["filename"])
    if record["sha256"] != source["digests"]["sha256"]:
        raise RuntimeError("Python source checksum mismatch: " + name)
    return {"package": name, "version": version, "source": record}

with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    records = list(pool.map(fetch, requirements))
(destination.parent / "python-manifest.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
print("Python OCR source distributions prepared.")
