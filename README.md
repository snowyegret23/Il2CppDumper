# Il2CppDumper

[![Build status](https://ci.appveyor.com/api/projects/status/anhqw33vcpmp8ofa?svg=true)](https://ci.appveyor.com/project/Perfare/il2cppdumper/branch/master/artifacts)

中文说明请戳[这里](README.zh-CN.md)

한국어 설명은 [여기](README.ko-KR.md)를 참고하세요.

Unity il2cpp reverse engineer

## Features

* Restores DLL metadata without original method bodies, for `MonoBehaviour` and `MonoScript` extraction
* Supports ELF, ELF64, Mach-O, PE, NSO and WASM format
* Supports legacy Unity 5.3+ and the Unity 6 metadata layouts listed below
* Keeps the existing metadata 16–31 parsers and adds 35, 38, 39, 104–108 and 110, including the 106.1 binary variant. Restores variable-width indices, enum types, encoded attribute constructors, relocated generic/RGCTX tables and computed metadata tokens.
* Restores assembly custom attributes (v21+), return-value custom attributes (v31+) and module custom attributes (v38+) when present in the metadata, in addition to type/member attributes.
* Restores embedded field-RVA initializer bytes, opaque value-type storage sizes, declared packing and explicit value-type field offsets in DummyDll. Native analysis scripts also label discovered field-RVA references.
* Supports generate IDA, Ghidra and Binary Ninja scripts to help them better analyze il2cpp files
* Supports generate structures header file
* Supports Android memory dumped `libil2cpp.so` file to bypass protection
* Support bypassing simple PE protection

### Version coverage

End-to-end Windows PE samples have been checked for metadata 24.4 (binary 24.5), 31, 39, 106 and 108. Other branches have layout/edge-case checks, not complete real-game coverage; in particular, 110 token reconstruction has only been checked with synthetic data. Unknown newer versions are rejected instead of being interpreted as an older layout.

Versions 106/107 can represent two binary layouts. Their attribute constructor tags distinguish 106 from 106.1 automatically. If these tags are absent, set `ForceIl2CppVersion` and `ForceVersion` to the appropriate binary layout in `config.json`.

Native `.h` generation covers 35, 38, 39, 104, 105, 106, 106.1, 107, 108 and 110 alongside the existing older layouts. Core class, method and type layouts were compared with official Unity distribution headers for both 32-bit and 64-bit Windows ABIs, including debug and code-coverage variants. Modern in-memory dumps and non-PE binaries have not yet received the same end-to-end validation.

## Usage

Run `Il2CppDumper.exe` and choose the il2cpp executable file and `global-metadata.dat` file, then enter the information as prompted

The program will then generate all the output files in current working directory

### Command-line

```
Il2CppDumper.exe <executable-file> <global-metadata> <output-directory>
```

Create the output directory before running the command. Optional flags can appear before or after the paths:

* `--strings-only`: write only `stringliteral.json`, skipping `dump.cs`, `script.json`, headers and DummyDll generation. Existing unrelated output files are left untouched. Binary/metadata initialization is still required; the file contains discovered string references and their RVAs, not every localization asset or unused metadata string. Addressed string extraction is unavailable for v16.
* `--restore-explicit-interfaces`: add unambiguous explicit interface mappings to DummyDll. This conservative, opt-in reconstruction requires a directly implemented non-generic interface and matching method name, flags and scoped signature. Generic methods/interfaces, inherited-only interfaces and ambiguous matches are left unchanged.

### Ghidra quick start

Use the native binary passed to Il2CppDumper and its generated `il2cpp.h` and `script.json`. For Windows, import `GameAssembly.dll` or the corresponding `*Assembly.dll`; for Android, import `libil2cpp.so`. Do not import a DLL from `DummyDll` as the native analysis target.

1. Create a Ghidra project with `File → New Project → Non-Shared Project`, then use `File → Import File` to import the native binary. Open it in CodeBrowser and choose `No` at the initial Auto Analysis prompt.

2. Place `il2cpp_header_to_ghidra.py` beside `il2cpp.h`. In that directory, run the converter with Python 3 outside Ghidra:

   ```text
   python -X utf8 il2cpp_header_to_ghidra.py
   ```

   This creates `il2cpp_ghidra.h`. Modern headers provide their pointer width automatically; add `--bits 32` for an older 32-bit header without an architecture marker.

3. In CodeBrowser, open `File → Parse C Source...`. Copy a parse profile matching the target architecture, replace its source-file list with `il2cpp_ghidra.h`, and remove unrelated include paths and parse options. Click `Parse to Program` to register the types in the current program.

4. Open `Window → Script Manager`. Use its script-directory manager to add the folder containing the Il2CppDumper Ghidra scripts, then refresh the list. For PyGhidra, start Ghidra in [PyGhidra mode](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/PyGhidra/src/main/py/README.md) and add `#@runtime PyGhidra` to the header comments of your local `ghidra_with_struct.py` copy. Jython users can run the bundled script with Jython instead.

5. Run `ghidra_with_struct.py` and select `script.json` in the file dialog. This applies names, function signatures and metadata types using the types imported in step 3. There is no need to run `ghidra.py` separately.

6. Run `Analysis → Auto Analyze...`. Start with the target's default analysis options and leave `Decompiler Parameter ID` unchecked for this initial pass. When analysis finishes, select a function and open `Window → Decompiler` to view the native pseudocode.

If Auto Analysis has already run, continue from the header conversion step. Import the header types before running `ghidra_with_struct.py`.

### Automated releases

Pushing to `master` publishes a GitHub Release tagged with the triggering commit's first 12 hexadecimal characters. The full commit SHA is recorded in the release notes. Windows x64 and x86 ZIPs include configuration, analysis scripts and documentation:

* `net8.0-win-x64-self-contained` / `net8.0-win-x86-self-contained` (recommended): include the .NET runtime. Extract the whole ZIP and run `Il2CppDumper.exe`; no separate .NET installation is needed.
* `net8.0-win-{x64,x86}-framework-dependent`: smaller downloads requiring the matching architecture of the .NET 8 runtime. The unsupported .NET 6 target is no longer built.

For Windows PE inputs, choose x86 for a 32-bit binary and x64 for a 64-bit binary. This also allows the custom PE loader to load a binary of the same architecture. Extract x86 and x64 packages into separate directories; their runtime DLLs must not be mixed.

The `Release commit` workflow can also be started manually on `master`. All four packages must build and pass a CLI smoke test before release creation. Uploads finish while the release is a draft; reruns can resume a draft but leave an already published release unchanged. The workflow uses the repository's built-in `GITHUB_TOKEN` with `contents: write`, without requiring a separate secret.

### Outputs

#### DummyDll

Folder, containing all restored dll files

Use [dnSpy](https://github.com/0xd4d/dnSpy), [ILSpy](https://github.com/icsharpcode/ILSpy) or other .Net decompiler tools to view

Custom attributes are attached to their original assembly, module or method
return value, not to a substitute type or method. This preserves information
such as assembly metadata, module annotations, readonly returns and return
tuple element names. Legacy v21–27 attribute arguments still have the existing
best-effort restoration limits; missing metadata is not inferred.

Embedded field-RVA data is restored for fixed-width primitive fields and
non-generic opaque value-type storage with a recoverable size. Byte ranges
are checked against the metadata default-value section. Unsupported storage
layouts produce a warning and retain their metadata-offset annotation;
arbitrary static-constructor logic and runtime-computed values are not restored.
This does not recreate original method bodies or guarantee that a decompiler
will reconstruct the original high-level array declaration.

With `DumpAttribute` enabled, `dump.cs` also includes these attributes using
`assembly:`, `module:` and `return:` targets. Assembly/module groups are labelled
with their source image because the dump combines multiple assemblies; it is
an analysis listing, not a single compilable C# assembly.

Can be used to extract Unity `MonoBehaviour` and `MonoScript`, for [UtinyRipper](https://github.com/mafaca/UtinyRipper), [UABE](https://7daystodie.com/forums/showthread.php?22675-Unity-Assets-Bundle-Extractor)

#### ida.py

For IDA

#### ida_with_struct.py

For IDA, read il2cpp.h file and apply structure information in IDA

Use `ida_py3.py` or `ida_with_struct_py3.py` with Python 3. The latter uses the [IDA 9 type APIs](https://python.docs.hex-rays.com/9.0/df/d81/namespaceida__typeinf.html); both use explicit module imports and handle cancelled file selection/imports. They add analysis annotations without patching binary bytes or creating synthetic string segments. The legacy Python 2 scripts remain unchanged.

#### il2cpp.h

structure information header file

Modern headers are generated for the input binary's pointer width. Set
`IL2CPP_DEBUG` to `1` before importing a v104+ header if the player was built
with that native runtime setting; set `IL2CPP_CODE_COVERAGE` to `1` for a v110
player using native code coverage. Undefined macros select the normal layout.
These build settings are not automatically detected from metadata and are
not equivalent to merely attaching a debugger. Import using the matching ABI.
`il2cpp_header_to_ghidra.py` detects the pointer width in modern headers;
use `--bits 32` when converting an older 32-bit header without that marker.

The headers retain unknown RGCTX slots instead of shifting subsequent entries,
and preserve the byte size of one-byte-packed opaque initializer storage.
Generated game-specific structures remain analysis aids, not a replacement
for the original Unity headers or compilable original game source.

#### ghidra.py

For Ghidra

`ghidra.py` and `ghidra_with_struct.py` accept Unicode text under Jython or Python 3/PyGhidra. The structured script requires importing a compatible header first (`il2cpp_header_to_ghidra.py` is a Python 3 converter). For the Script Manager's PyGhidra provider, add `#@runtime PyGhidra` to your local script copy and launch Ghidra in [PyGhidra mode](https://github.com/NationalSecurityAgency/ghidra/blob/master/Ghidra/Features/PyGhidra/src/main/py/README.md). Runtime selection is not changed automatically for existing Jython users. The WASM/plugin-specific script is unchanged.

#### Il2CppBinaryNinja

For BinaryNinja

#### ghidra_wasm.py

For Ghidra, work with [ghidra-wasm-plugin](https://github.com/nneonneo/ghidra-wasm-plugin)

#### script.json

For ida.py, ghidra.py and Il2CppBinaryNinja

#### stringliteral.json

Contains all stringLiteral information

### Configuration

All the configuration options are located in `config.json`

Available options:

* `DumpMethod`, `DumpField`, `DumpProperty`, `DumpAttribute`, `DumpFieldOffset`, `DumpMethodOffset`, `DumpTypeDefIndex`
  * Whether to output these information to dump.cs

* `GenerateDummyDll`, `GenerateStruct`
  * Whether to generate these things

* `StringsOnly` (default `false`)
  * Equivalent to `--strings-only`; takes priority over the other output settings.

* `RestoreExplicitInterfaces` (default `false`)
  * Equivalent to `--restore-explicit-interfaces`; only affects DummyDll generation.

* `DummyDllAddToken`
  * Whether to add token in DummyDll

* `RequireAnyKey`
  * Whether to press any key to exit at the end

* `ForceIl2CppVersion`, `ForceVersion`
  * If `ForceIl2CppVersion` is `true`, the program will use the version number specified in `ForceVersion` to choose parser for il2cpp binaries (does not affect the choice of metadata parser). This may be useful on some older il2cpp version (e.g. the program may need to use v16 parser on il2cpp v20 (Android) binaries in order to work properly)

* `ForceDump`
  * Force files to be treated as dumped

* `NoRedirectedPointer`
  * Treat pointers in dumped files as unredirected, This option needs to be `true` for files dumped from some devices

## Common errors

#### `ERROR: Metadata file supplied is not valid metadata file.`  

Make sure you choose the correct file. Sometimes games may obfuscate this file for content protection purposes and so on. Deobfuscating of such files is beyond the scope of this program, so please **DO NOT** file an issue regarding to deobfuscating.

If your file is `libil2cpp.so` and you have a rooted Android phone, you can try my other project [Zygisk-Il2CppDumper](https://github.com/Perfare/Zygisk-Il2CppDumper), it can bypass this protection.

#### `ERROR: Can't use auto mode to process file, try manual mode.`

Please note that the executable file for the PC platform is `GameAssembly.dll` or `*Assembly.dll`

You can open a new issue and upload the file, I will try to solve.

#### `ERROR: This file may be protected.`

Il2CppDumper detected that the executable file has been protected, use `GameGuardian` to dump `libil2cpp.so` from the game memory, then use Il2CppDumper to load and follow the prompts, can bypass most protections.

If you have a rooted Android phone, you can try my other project [Zygisk-Il2CppDumper](https://github.com/Perfare/Zygisk-Il2CppDumper), it can bypass almost all protections.

## Credits

- Jumboperson - [Il2CppDumper](https://github.com/Jumboperson/Il2CppDumper)
- [c01ns](https://github.com/c01ns/Il2CppDumper), [vmpprotect](https://github.com/vmpprotect/Il2CppDumper) and [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) - modern metadata layout references
- [Il2CppInspectorRedux](https://github.com/LukeFZ/Il2CppInspectorRedux) - metadata version/layout cross-checks; not a runtime dependency
- [jules-noelaudoux](https://github.com/jules-noelaudoux/Il2CppDumper) and [MSerperior](https://github.com/MSerperior/Il2CppDumper) - IDA/Python 3 compatibility references
- [liu-shuoye](https://github.com/liu-shuoye/Il2CppDumper) - explicit interface reconstruction approach
- [b1naryTR](https://github.com/b1naryTR/Il2CppDumper) - lightweight string extraction approach
