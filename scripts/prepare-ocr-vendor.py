"""Prepare official MSYS2 OCR binaries, notices and corresponding sources."""
import concurrent.futures
import hashlib
import json
import re
import tarfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "vendor" / "ocr"


def download(url, target):
    if not target.exists():
        with urllib.request.urlopen(url, timeout=120) as response:
            target.write_bytes(response.read())
    return {"name": target.name, "url": url, "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "bytes": target.stat().st_size}


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    download("https://repo.msys2.org/mingw/mingw64/mingw64.db", DEST / "mingw64.db")
    packages = {}
    with tarfile.open(DEST / "mingw64.db") as db:
        for entry in db:
            if not entry.name.endswith("/desc"):
                continue
            fields = re.split(r"%([A-Z0-9]+)%\n", db.extractfile(entry).read().decode())
            data = {fields[i]: fields[i+1].strip().splitlines() for i in range(1, len(fields), 2)}
            packages[data["NAME"][0]] = data
    needed = set()
    def add(name):
        name = re.split(r"[<>=]", name)[0]
        if name in needed:
            return
        if name not in packages:
            providers = [key for key, value in packages.items() if name in [re.split(r"[<>=]", p)[0] for p in value.get("PROVIDES", [])]]
            if not providers:
                raise RuntimeError("Dependency absent from official database: " + name)
            name = sorted(providers)[0]
        needed.add(name)
        for dependency in packages[name].get("DEPENDS", []):
            add(dependency)
    add("mingw-w64-x86_64-tesseract-ocr")
    print("MSYS2 packages:", len(needed), flush=True)
    for name in sorted(needed):
        print(name, packages[name]["VERSION"][0], flush=True)
    binary = DEST / "binaries"
    sources = DEST / "sources"
    runtime = DEST / "msys"
    for directory in (binary, sources, runtime):
        directory.mkdir(parents=True, exist_ok=True)
    def fetch(name):
        data = packages[name]
        filename = data["FILENAME"][0]
        record = download("https://repo.msys2.org/mingw/mingw64/" + filename, binary / filename)
        if record["sha256"] != data["SHA256SUM"][0]:
            raise RuntimeError("MSYS2 package checksum mismatch: " + filename)
        source_name = data["BASE"][0] + "-" + data["VERSION"][0].split(":")[-1] + ".src.tar.zst"
        source_record = download("https://repo.msys2.org/mingw/sources/" + source_name, sources / source_name)
        return {"package": name, "version": data["VERSION"][0], "licenses": data.get("LICENSE", []), "binary": record, "source": source_record}
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        records = list(pool.map(fetch, sorted(needed)))
    for record in records:
        with tarfile.open(binary / record["binary"]["name"]) as archive:
            archive.extractall(runtime, filter="data")
    (DEST / "msys-manifest.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
    print("Official OCR binaries and sources prepared.", flush=True)


if __name__ == "__main__":
    main()
