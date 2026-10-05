"""Check a cross-signed Mac bundle and ZIP it with Unix executable permissions.

Usage: python tools/package-mac.py Apple-Silicon|Intel /path/to/rcodesign [release-directory]
These static checks do not replace macOS codesign/Gatekeeper or execution tests.
"""
import plistlib
import hashlib
import re
import struct
import subprocess
import sys
import zipfile
from pathlib import Path
from mac_bundle import verify_resources

root = Path(__file__).resolve().parents[1]
arch, signer = sys.argv[1:3]
assert arch in ("Apple-Silicon", "Intel")
version = re.search(r"<Version>(.*?)</Version>", (root / "Directory.Build.props").read_text()).group(1)
release = Path(sys.argv[3]).resolve() if len(sys.argv)>3 else root / "release" / ("v" + version)
bundle = release / ("macOS-" + arch) / "Chiikawa Companion.app"
contents = bundle / "Contents"
info = plistlib.loads((contents / "Info.plist").read_bytes())
assert info["CFBundleShortVersionString"] == version
main = contents / "MacOS" / info["CFBundleExecutable"]
header = main.read_bytes()[:32]
assert header[:4] == b"\xcf\xfa\xed\xfe"
assert struct.unpack_from("<I", header, 4)[0] == (0x100000C if arch == "Apple-Silicon" else 0x1000007)
resources = contents / "_CodeSignature" / "CodeResources"
assert resources.is_file()
plistlib.loads(resources.read_bytes())
verify_resources(bundle)
magics = (b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xca\xfe\xba\xbe", b"\xca\xfe\xba\xbf")
native = []
for path in sorted(contents.rglob("*")):
    if not path.is_file():
        continue
    with path.open("rb") as stream:
        if stream.read(4) not in magics:
            continue
    native.append(path)
    signature = subprocess.run([signer, "--config-file", "/dev/null", "print-signature-info", str(path)], capture_output=True, text=True, check=True).stdout
    assert "flags: CodeSignatureFlags(ADHOC)" in signature, path
    if path == main:
        for label, resource in (("Info (1)", contents / "Info.plist"), ("Resources (3)", resources)):
            assert label + ": " + hashlib.sha256(resource.read_bytes()).hexdigest() in signature, label
    check = subprocess.run([signer, "--config-file", "/dev/null", "verify", str(path)], capture_output=True, text=True)
    # rcodesign 0.29 wrongly expects CMS for certificate-free ad-hoc signatures.
    # Its verifier still checks code-page/slot hashes; reject every other problem.
    problems = [line for line in (check.stdout + check.stderr).splitlines()
                if line and not line.startswith("(the verify command") and line not in ("problems reported during verification", "Error: problems reported during verification")]
    assert check.returncode in (0, 1), (path, problems)
    assert all(re.fullmatch(r"(?:Error: |@[0-9]+: )?CMS error: missing further values \(at position 0\)", line) for line in problems), (path, problems)
    assert check.returncode == 0 or problems, path

output = bundle.parents[1] / ("Chiikawa-Companion-macOS-" + arch + ".zip")
with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in sorted(bundle.rglob("*")):
        if path.is_file():
            name = path.relative_to(bundle.parent).as_posix()
            entry = zipfile.ZipInfo(name)
            entry.create_system = 3
            entry.external_attr = (0o100755 if path in native else 0o100644) << 16
            archive.writestr(entry, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED)
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    assert archive.getinfo("Chiikawa Companion.app/Contents/MacOS/Chiikawa.Companion").external_attr >> 16 & 0o111
print(f"{arch}: {len(native)} native files with ad-hoc signatures and code hash checks; ZIP structure/permissions checked: {output}")
