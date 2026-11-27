# Metadata regression tests

From the repository root:

```sh
dotnet run --project tests/MetadataRegression
```

The fixtures exercise v110 and v111 with all three TypeIndex widths (1, 2
and 4 bytes), two-byte compressed parameter counts, parameter/return tokens,
zero-argument methods, malformed v111 signatures, .NET CoreLib code registration
search, and paired RGCTX runtime slots. No Unity installation is needed for
this test.

To check an actual v111 file, build the adjacent `Example.cs.txt` source as
`Example.exe` against the Unity player's AOT class library. Convert it and its
dependencies with the Unity 6000.7.0b11 IL2CPP compiler using
`--convert-to-cpp` and `--data-folder`. Then run:

```sh
dotnet run --project tests/MetadataRegression -- /path/to/Metadata/global-metadata.dat
```

This checks the known method, parameter types and names, and the presence of
the return and parameter custom attributes in the compiler's metadata. The
generated binary and metadata are not distributed in this repository. This is
a metadata reader test, not a native registration or full dump test.

The v111 support adapts the layout work from
[roytu/Il2CppDumper](https://github.com/roytu/Il2CppDumper/commit/079549baba2533d5a40033dd060c8a2b494b601c)
and [AndnixSH/Il2CppDumper](https://github.com/AndnixSH/Il2CppDumper/commit/3f3f6e1f8a80a8e95fd1c96c58ba5aec0e9fce77).
The compressed count, parameter tokens and native structure layout were checked
against the Unity 6000.7.0b11 IL2CPP distribution (revision `9a8d8be46479`).
