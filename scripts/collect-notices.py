"""Collect unmodified license texts from official source archives; no source execution."""
import pathlib
import tarfile

root = pathlib.Path(__file__).resolve().parents[1]
destination = root / "vendor" / "notices"
for filename, component in [
    ("ghostscript-10.08.0.tar.xz", "ghostscript"),
    ("gcc-core-3.4.5-20060117-1-src.tar.gz", "gcc-core"),
    ("gcc-java-3.4.5-20060117-1-src.tar.gz", "gcc-java"),
    ("gcc-g++-3.4.5-20060117-1-src.tar.gz", "gcc-cpp"),
]:
    component_root = (destination / component).resolve()
    with tarfile.open(root / "vendor" / "downloads" / filename) as archive:
        for entry in archive:
            name = pathlib.PurePosixPath(entry.name)
            if not entry.isfile() or not name.name.upper().startswith(("LICENSE", "LICENCE", "COPYING", "COPYRIGHT", "NOTICE")):
                continue
            target = (component_root / pathlib.Path(*name.parts)).resolve()
            if not target.is_relative_to(component_root):
                raise ValueError("Unexpected archive path")
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.extractfile(entry) as source:
                target.write_bytes(source.read())
    print("Collected notices:", component)
