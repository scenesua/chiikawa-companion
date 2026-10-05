"""Prepare .NET macOS bundles and reject incomplete resource seals."""
import hashlib
import plistlib
import shutil
import sys
from pathlib import Path

MACHO_MAGICS = (b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xca\xfe\xba\xbe", b"\xca\xfe\xba\xbf")
ASSEMBLY = "Chiikawa.Companion.dll"
ASSEMBLY_PATH = "../Resources/runtime/" + ASSEMBLY


def bind_apphost(data, assembly=ASSEMBLY, target=ASSEMBLY_PATH):
    old, new = assembly.encode() + b"\0", target.encode() + b"\0"
    if new in data:
        return data
    if data.count(old) != 1:
        raise ValueError("Expected exactly one .NET apphost assembly binding")
    offset = data.index(old)
    # The SDK apphost reserves a NUL-padded 1025-byte binding buffer.
    if len(new) > 1025 or len(data) < offset + len(new) or any(data[offset + len(old):offset + len(new)]):
        raise ValueError("Apphost binding has insufficient NUL padding")
    return data[:offset] + new + data[offset + len(new):]


def prepare(bundle):
    contents = Path(bundle).resolve() / "Contents"
    main = contents / "MacOS" / "Chiikawa.Companion"
    data = bind_apphost(main.read_bytes())
    runtime = contents / "Resources" / "runtime"
    paths = [p for p in (contents / "MacOS").iterdir() if p != main]
    for path in paths:
        if (runtime / path.name).exists():
            raise ValueError(f"Refusing to overwrite runtime file: {runtime / path.name}")
    for name in (ASSEMBLY, "Chiikawa.Companion.runtimeconfig.json", "libhostfxr.dylib"):
        if not (runtime / name).is_file() and not (contents / "MacOS" / name).is_file():
            raise ValueError(f"Runtime file missing: {name}")
    runtime.mkdir(parents=True, exist_ok=True)
    for path in paths:
        shutil.move(str(path), str(runtime / path.name))
    main.write_bytes(data)
    main.chmod(0o755)


def verify_resources(bundle):
    contents = Path(bundle).resolve() / "Contents"
    info = plistlib.loads((contents / "Info.plist").read_bytes())
    main = contents / "MacOS" / info["CFBundleExecutable"]
    seal = plistlib.loads((contents / "_CodeSignature" / "CodeResources").read_bytes())
    entries = seal.get("files2", {})
    files = {p.relative_to(contents).as_posix(): p for p in contents.rglob("*") if p.is_file()}
    errors = []
    for name, path in files.items():
        if name.startswith("_CodeSignature/") or name in ("Info.plist", "PkgInfo") or path == main:
            continue
        entry = entries.get(name)
        if not isinstance(entry, dict):
            errors.append(f"Unsealed file: {name}")
            continue
        if "hash2" in entry:
            if hashlib.sha256(path.read_bytes()).digest() != entry["hash2"]:
                errors.append(f"Resource hash mismatch: {name}")
        elif "cdhash" in entry:
            if path.read_bytes()[:4] not in MACHO_MAGICS or "requirement" not in entry:
                errors.append(f"Invalid nested code seal: {name}")
        else:
            errors.append(f"Missing resource hash: {name}")
    for name in entries:
        if name not in files:
            errors.append(f"Sealed file missing: {name}")
    if errors:
        raise ValueError("\n".join(errors))
    if ASSEMBLY_PATH.encode() + b"\0" not in main.read_bytes():
        raise ValueError("Apphost does not point to Resources/runtime")
    print(f"Complete resource seal checked: {len(entries)} files")


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in ("prepare", "verify"):
        raise SystemExit("Usage: python mac_bundle.py prepare|verify /path/App.app")
    (prepare if sys.argv[1] == "prepare" else verify_resources)(sys.argv[2])
