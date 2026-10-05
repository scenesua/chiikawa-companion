# macOS bundle packaging

Publish the self-contained Mac project to `Chiikawa Companion.app/Contents/MacOS`.
Copy `Mac/Info.plist` and the release notes before signing. Run:

```
python tools/mac_bundle.py prepare "/path/Chiikawa Companion.app"
```

This keeps the native apphost in MacOS and moves the .NET payload to
Resources/runtime. The apphost's SDK-reserved assembly binding points to
`../Resources/runtime/Chiikawa.Companion.dll`. The .NET host resolves its app root
and hostfxr from that assembly's directory, so the self-contained runtime remains
with the managed application.

On macOS, sign the runtime Mach-O files first, then the outer bundle, run
`codesign --verify --deep --strict`, and run:

```
python tools/mac_bundle.py verify "/path/Chiikawa Companion.app"
```

For a Windows cross-build, rcodesign signs the entire bundle and automatically
signs Mach-O payloads in Resources. Then `tools/package-mac.py` checks all seals,
resource hashes, native code-page hashes and the apphost Info/Resources slots,
and writes a ZIP with Unix executable permissions.

`python tools/test_mac_bundle.py` checks preparation and rejects unsealed,
added, modified or missing files, ambiguous host bindings and destination conflicts.
The old v0.4.0 layout must fail the complete-resource check.

These checks do not establish Developer ID trust or notarisation. Native macOS
signature/policy verification and an actual quarantined Finder launch are separate
distribution checks. Do not remove quarantine or alter signatures to claim that
the original download passes Gatekeeper.
