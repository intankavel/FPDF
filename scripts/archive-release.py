"""Create separate app/source downloads; keep all sources on the same release."""
import hashlib
import sys
import zipfile
from pathlib import Path

package = Path(sys.argv[1]).resolve()
root = Path(__file__).resolve().parents[1] / "artifacts" / "releases"
if package.parent != root or not package.name.startswith("FPDF-"):
    raise ValueError("A pasta deve ser um pacote FPDF em artifacts/releases.")
version = package.name.removeprefix("FPDF-").removesuffix("-win-x64")
app_archive = package.with_suffix(".zip")
# with_suffix would treat the final version component as an extension.
app_archive = root / (package.name + ".zip")
source_archive = root / f"FPDF-{version}-fontes-terceiros.zip"
for archive_path, sources in ((app_archive, False), (source_archive, True)):
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for file in sorted(package.rglob("*")):
            if not file.is_file() or "__pycache__" in file.parts or file.suffix == ".pyc":
                continue
            relative = file.relative_to(package)
            if (relative.parts[0] == "fontes-terceiros") != sources:
                continue
            name = relative if sources else Path(package.name) / relative
            archive.write(file, name.as_posix())
    digest = hashlib.sha256(archive_path.read_bytes()).hexdigest().upper()
    Path(str(archive_path) + ".sha256").write_text(f"{digest}  {archive_path.name}\n", encoding="utf-8")
    print(f"{archive_path.name}: {archive_path.stat().st_size / 1_000_000:.1f} MB", flush=True)
