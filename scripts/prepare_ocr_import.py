"""Shared download helper for release preparation."""
import hashlib
import urllib.request

def download(url, target):
    if not target.exists():
        with urllib.request.urlopen(url, timeout=120) as response:
            target.write_bytes(response.read())
    return {"name": target.name, "url": url, "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "bytes": target.stat().st_size}
