"""Build an offline, relocatable OCR runtime from archived official distributions."""
import hashlib
import json
import shutil
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
vendor = root / "vendor" / "ocr"
package = Path(sys.argv[1]).resolve()
python = package / "tools" / "ocr"
tesseract = package / "tools" / "tesseract"
python.mkdir(parents=True)
tesseract.mkdir(parents=True)
with zipfile.ZipFile(vendor / "python-3.14.7-embed-amd64.zip") as archive:
    archive.extractall(python)
(python / "python314._pth").write_text("python314.zip\n.\nLib/site-packages\nimport site\n")
libraries = python / "Lib" / "site-packages"
libraries.mkdir(parents=True)
for wheel in sorted((vendor / "wheels").glob("*.whl")):
    with zipfile.ZipFile(wheel) as archive:
        for name in archive.namelist():
            if name.endswith("/"):
                continue
            # Wheel data files intended for purelib/platlib belong on sys.path.
            relative = name
            if ".data/" in name:
                _, area, relative = name.split("/", 2)
                if area not in ("purelib", "platlib"):
                    continue
            destination = libraries / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.read(name))
for file in (vendor / "msys" / "mingw64" / "bin").iterdir():
    if file.suffix.lower() == ".dll" or file.name == "tesseract.exe":
        shutil.copy2(file, tesseract / file.name)
data = tesseract / "tessdata"
data.mkdir()
for file in (vendor / "msys" / "mingw64" / "share" / "tessdata").iterdir():
    if file.is_dir():
        shutil.copytree(file, data / file.name)
    else:
        shutil.copy2(file, data / file.name)
for language in ("por", "eng"):
    shutil.copy2(vendor / "models" / (language + ".traineddata"), data)
licenses = package / "licencas" / "ocr"
licenses.mkdir(parents=True)
shutil.copy2(python / "LICENSE.txt", licenses / "Python-LICENSE.txt")
shutil.copy2(vendor / "tessdata-LICENSE.txt", licenses / "tessdata-LICENSE.txt")
shutil.copy2(vendor / "NotoSans-OFL.txt", licenses / "NotoSans-OFL.txt")
(licenses / "font-notices.txt").write_text("NotoSans-Regular.ttf: Copyright 2015-2021 Google LLC. All Rights Reserved. SIL Open Font License 1.1.\nOcculta.ttf: Copyright 2026 James R. Barlow. Apache License 2.0 (see tessdata-LICENSE.txt for the full license text).\n", encoding="utf-8")
shutil.copytree(vendor / "msys" / "mingw64" / "share" / "licenses", licenses / "msys2")
for directory in libraries.glob("*.dist-info"):
    target = licenses / "python" / directory.name
    for file in directory.rglob("*"):
        if file.is_file() and any(word in file.name.lower() for word in ("license", "copying", "notice", "authors")):
            destination = target / file.relative_to(directory)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(file, destination)
    # Preserve the distribution metadata (license expressions, versions, origins).
    target.mkdir(parents=True, exist_ok=True)
    shutil.copy2(directory / "METADATA", target / "METADATA")
for file in (libraries / "ocrmypdf" / "data").iterdir():
    if file.is_file() and file.suffix.lower() != ".ttf":
        shutil.copy2(file, licenses / file.name)
sources = package / "fontes-terceiros" / "ocr"
sources.mkdir(parents=True)
shutil.copytree(vendor / "sources", sources / "msys2")
shutil.copytree(vendor / "python-sources", sources / "python")
for filename in ("msys-manifest.json", "python-manifest.json"):
    shutil.copy2(vendor / filename, sources)
records = []
for folder in (python, tesseract):
    for file in sorted(folder.rglob("*")):
        if file.is_file():
            records.append({"path": str(file.relative_to(package)).replace("\\", "/"), "sha256": hashlib.sha256(file.read_bytes()).hexdigest(), "bytes": file.stat().st_size})
(sources / "runtime-manifest.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
print("Portable OCR, licenses and corresponding sources included.")
