# Il2CppDumper

[![Build status](https://ci.appveyor.com/api/projects/status/anhqw33vcpmp8ofa?svg=true)](https://ci.appveyor.com/project/Perfare/il2cppdumper/branch/master/artifacts)

中文说明请戳[这里](README.zh-CN.md)

Unity il2cpp reverse engineer

## Features

* Complete DLL restore (except code), can be used to extract `MonoBehaviour` and `MonoScript`
* Supports ELF, ELF64, Mach-O, PE, NSO and WASM format
* Supports Unity 5.3 - 2022.2
* Keeps the existing metadata 16–31 parsers and adds 35, 38, 39, 104–108 and 110, including the 106.1 binary variant. Restores variable-width indices, enum types, encoded attribute constructors, relocated generic/RGCTX tables and computed metadata tokens.
* Supports generate IDA, Ghidra and Binary Ninja scripts to help them better analyze il2cpp files
* Supports generate structures header file
* Supports Android memory dumped `libil2cpp.so` file to bypass protection
* Support bypassing simple PE protection

### Version coverage

End-to-end Windows PE samples have been checked for metadata 24.4 (binary 24.5), 31, 39, 106 and 108. Other branches have layout/edge-case checks, not complete real-game coverage; in particular, 110 token reconstruction has only been checked with synthetic data. Unknown newer versions are rejected instead of being interpreted as an older layout.

Versions 106/107 can represent two binary layouts. Their attribute constructor tags distinguish 106 from 106.1 automatically. If these tags are absent, set `ForceIl2CppVersion` and `ForceVersion` to the appropriate binary layout in `config.json`.

Native `.h` generation for v35+ is still unsupported. Modern in-memory dumps and non-PE binaries have not yet received the same end-to-end validation.

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

Can be used to extract Unity `MonoBehaviour` and `MonoScript`, for [UtinyRipper](https://github.com/mafaca/UtinyRipper), [UABE](https://7daystodie.com/forums/showthread.php?22675-Unity-Assets-Bundle-Extractor)

#### ida.py

For IDA

#### ida_with_struct.py

For IDA, read il2cpp.h file and apply structure information in IDA

Use `ida_py3.py` or `ida_with_struct_py3.py` with Python 3. The latter uses the [IDA 9 type APIs](https://python.docs.hex-rays.com/9.0/df/d81/namespaceida__typeinf.html); both use explicit module imports and handle cancelled file selection/imports. They add analysis annotations without patching binary bytes or creating synthetic string segments. The legacy Python 2 scripts remain unchanged.

#### il2cpp.h

structure information header file

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
