import hashlib
import plistlib
import tempfile
import unittest
from pathlib import Path
from mac_bundle import ASSEMBLY, ASSEMBLY_PATH, bind_apphost, prepare, verify_resources


class BundleChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.bundle = Path(self.temp.name) / "Test.app"
        self.contents = self.bundle / "Contents"
        self.macos = self.contents / "MacOS"
        self.macos.mkdir(parents=True)
        self.main = self.macos / "Chiikawa.Companion"
        self.main.write_bytes(b"header" + ASSEMBLY.encode() + b"\0" * 1025)
        (self.contents / "Info.plist").write_bytes(plistlib.dumps({"CFBundleExecutable": self.main.name}))
        for name in (ASSEMBLY, "Chiikawa.Companion.runtimeconfig.json", "libhostfxr.dylib"):
            (self.macos / name).write_bytes(b"fixture")

    def seal(self):
        entries = {}
        for path in self.contents.rglob("*"):
            if path.is_file() and path not in (self.main, self.contents / "Info.plist") and "_CodeSignature" not in path.parts:
                entries[path.relative_to(self.contents).as_posix()] = {"hash2": hashlib.sha256(path.read_bytes()).digest()}
        signature = self.contents / "_CodeSignature"
        signature.mkdir(exist_ok=True)
        (signature / "CodeResources").write_bytes(plistlib.dumps({"files2": entries}))

    def test_prepare_and_complete_seal(self):
        prepare(self.bundle)
        self.assertEqual(list(self.macos.iterdir()), [self.main])
        self.assertIn(ASSEMBLY_PATH.encode() + b"\0", self.main.read_bytes())
        prepare(self.bundle)  # Re-running does not move or overwrite runtime files.
        self.seal()
        verify_resources(self.bundle)

    def test_original_unsealed_bundle_is_rejected(self):
        signature = self.contents / "_CodeSignature"
        signature.mkdir()
        (signature / "CodeResources").write_bytes(plistlib.dumps({"files2": {}}))
        with self.assertRaisesRegex(ValueError, "Unsealed file"):
            verify_resources(self.bundle)

    def test_added_file_is_rejected(self):
        prepare(self.bundle)
        self.seal()
        (self.contents / "Resources" / "new.json").write_bytes(b"added")
        with self.assertRaisesRegex(ValueError, "Unsealed file"):
            verify_resources(self.bundle)

    def test_modified_file_is_rejected(self):
        prepare(self.bundle)
        self.seal()
        (self.contents / "Resources" / "runtime" / ASSEMBLY).write_bytes(b"modified")
        with self.assertRaisesRegex(ValueError, "Resource hash mismatch"):
            verify_resources(self.bundle)

    def test_deleted_file_is_rejected(self):
        prepare(self.bundle)
        self.seal()
        (self.contents / "Resources" / "runtime" / ASSEMBLY).unlink()
        with self.assertRaisesRegex(ValueError, "Sealed file missing"):
            verify_resources(self.bundle)

    def test_binding_rejects_ambiguous_and_overflow_data(self):
        with self.assertRaises(ValueError):
            bind_apphost(self.main.read_bytes() * 2)
        with self.assertRaises(ValueError):
            bind_apphost(ASSEMBLY.encode() + b"\0NOT_PADDING")

    def test_destination_conflict_preserves_main(self):
        before = self.main.read_bytes()
        runtime = self.contents / "Resources" / "runtime"
        runtime.mkdir(parents=True)
        (runtime / ASSEMBLY).write_bytes(b"existing")
        with self.assertRaisesRegex(ValueError, "overwrite"):
            prepare(self.bundle)
        self.assertEqual(before, self.main.read_bytes())


if __name__ == "__main__":
    unittest.main()
