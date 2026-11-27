using Il2CppDumper;

foreach (int width in new[] { 1, 2, 4 })
{
    int returnType = width switch { 1 => 245, 2 => 65000, _ => 70000 };
    using var metadata = new Metadata(Fixture(111, width, returnType));
    Check(metadata.Version == 111, "metadata version");
    var method = metadata.methodDefs[0];
    Check(method.returnType == returnType && method.parameterCount == 130, "variable-width return type and compressed count");
    Check(method.iflags == 8 && method.returnParameterToken == 0x08000001, "runtime flags and return token");
    Check(metadata.parameterDefs[1].typeIndex == 3 && metadata.parameterDefs[130].typeIndex == 132, "parameter types");
    Check(metadata.parameterDefs[1].token == 0x08000002 && metadata.parameterDefs[130].token == 0x08000083, "parameter tokens");
    Check(metadata.methodDefs[1].parameterCount == 0 && metadata.methodDefs[1].returnParameterToken == 0,
        "zero-parameter method with invalid start sentinel");

    using var older = new Metadata(Fixture(110, width, returnType));
    Check(older.methodDefs[0].returnType == returnType && older.methodDefs[0].parameterCount == 1 &&
        older.methodDefs[0].returnParameterToken == 0x08000005 && older.methodDefs[0].iflags == 8 &&
        older.parameterDefs[0].token == 0x08000006 && older.parameterDefs[0].typeIndex == 9, "v110 inline signature");
}
Reject(() => new Metadata(Fixture(111, 2, 400, badOffset: true)), "offset is outside");
Reject(() => new Metadata(Fixture(111, 2, 400, truncateCount: true)), "Truncated compressed");
Reject(() => new Metadata(Fixture(111, 2, 400, badParamRange: true)), "outside the parameter table");
try
{
    using var unsupported = new Metadata(Fixture(112, 2, 400));
    throw new Exception("Unknown v112 was accepted.");
}
catch (NotSupportedException) { }
Check(HeaderConstants.GetModernHeader(111, false).Contains("Native layout: metadata 111, 64-bit"), "v111 header");
Check(FindRegistration(111, "System.Private.CoreLib.dll") == 0x1500, "v111 CoreLib registration");
Check(FindRegistration(111, "mscorlib.dll") == 0x1500, "v111 mscorlib registration");
Check(FindRegistration(110, "mscorlib.dll") == 0x1500, "v110 legacy registration");
var rgctxRows = new byte[] { 1, 5, 6, 3, 7, 8, 1 }.Select(kind =>
    new Il2CppRGCTXDefinition { type_post29 = kind }).ToArray();
Check(RuntimeRgctx(110, rgctxRows).Count() == 7 && RuntimeRgctx(111, rgctxRows).Count() == 5,
    "v111 collapses paired runtime RGCTX rows without changing v110");
Console.WriteLine("IL2CPP metadata v110/v111 regression tests passed.");
if (args.Length == 1)
{
    using var stream = new MemoryStream(File.ReadAllBytes(args[0]));
    using var sample = new Metadata(stream);
    Check(sample.Version == 111, "Unity-generated fixture version");
    var method = sample.methodDefs.Single(m => sample.GetStringFromIndex(m.nameIndex) == "ExampleMethod");
    Check(method.parameterCount == 3 && method.returnParameterToken != 0, "Unity-generated method signature");
    var parameters = sample.parameterDefs.Skip(method.parameterStart).Take(3).ToArray();
    Check(parameters.Select(p => sample.GetStringFromIndex(p.nameIndex)).SequenceEqual(new[] { "a", "b", "c" }),
        "Unity-generated parameter names");
    Check(parameters[0].typeIndex == method.returnType && parameters[1].typeIndex != method.returnType,
        "Unity-generated return and parameter types");
    var image = sample.imageDefs.Single(i => sample.GetStringFromIndex(i.nameIndex) == "Example.exe");
    Check(sample.GetCustomAttributeIndex(image, -1, (uint)method.returnParameterToken) >= 0 &&
        sample.GetCustomAttributeIndex(image, -1, parameters[0].token) >= 0, "Unity-generated parameter attributes");
    int runtimeSlots = sample.rgctxRanges.Sum(r => RuntimeRgctx(111,
        sample.rgctxEntries.AsSpan(r.range.start, r.range.length).ToArray()).Count());
    Check(runtimeSlots > 0 && runtimeSlots < sample.rgctxRanges.Sum(r => r.range.length),
        "Unity-generated paired RGCTX records");
    Console.WriteLine("Unity-generated v111 metadata passed.");
}

static MemoryStream Fixture(int version, int width, int returnType, bool badOffset = false,
    bool truncateCount = false, bool badParamRange = false)
{
    var format111 = version >= 111;
    var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
    writer.Write(0xFAB11BAFu);
    writer.Write(version);
    for (int index = 0; index < (format111 ? 44 : 43) * 3; index++)
        writer.Write(0);

    if (format111)
    {
        Section(17, 1, () => TypeIndex(1)); // interfaces are fixed-width TypeIndex values
        int noArgSignature = width + 2 + width * 130;
        Section(6, 2, () =>
        {
            MethodPrefix(1, badParamRange ? (byte)2 : (byte)1, 0xC008);
            writer.Write(badOffset ? int.MaxValue : 0);
            MethodPrefix(2, 255, 0);
            writer.Write(noArgSignature);
        });
        Section(11, 131, () =>
        {
            for (int index = 0; index < 131; index++)
                writer.Write((uint)index);
        });
        // Append the signature data last so the truncated case also ends the file.
        Section(5, 2, () =>
        {
            TypeIndex(returnType);
            writer.Write((byte)0x80);
            if (truncateCount)
                return;
            writer.Write((byte)130);
            for (int index = 3; index < 133; index++)
                TypeIndex(index);
            TypeIndex(returnType);
            writer.Write((byte)0);
        });
    }
    else
    {
        Section(5, 1, () =>
        {
            writer.Write(1u); // name
            writer.Write((byte)0); // declaring type
            TypeIndex(returnType);
            writer.Write(0x08000005); // return parameter token
            writer.Write((byte)0); // parameter start
            writer.Write(byte.MaxValue); // generic container
            writer.Write((ushort)0); // flags
            writer.Write((ushort)8); // iflags
            writer.Write(ushort.MaxValue); // slot
            writer.Write((ushort)1);
        });
        Section(10, 1, () =>
        {
            writer.Write(0u);
            writer.Write(0x08000006u);
            TypeIndex(9);
        });
    }

    stream.Position = 0;
    return stream;

    void MethodPrefix(uint name, byte parameterStart, ushort iflags)
    {
        writer.Write(name);
        writer.Write((byte)0); // declaring type
        writer.Write(parameterStart);
        writer.Write(byte.MaxValue); // generic container
        writer.Write((ushort)0); // flags
        writer.Write(iflags);
        writer.Write(ushort.MaxValue); // slot
    }

    void TypeIndex(int value)
    {
        if (width == 1) writer.Write(checked((byte)value));
        else if (width == 2) writer.Write(checked((ushort)value));
        else writer.Write(value);
    }

    void Section(int slot, int count, Action emit)
    {
        int offset = (int)stream.Position;
        emit();
        int end = (int)stream.Position;
        stream.Position = 8 + slot * 12;
        writer.Write(offset);
        writer.Write(end - offset);
        writer.Write(count);
        stream.Position = end;
    }
}

static void Check(bool pass, string description)
{
    if (!pass) throw new Exception("Regression: " + description);
}

static IEnumerable<Il2CppRGCTXDefinition> RuntimeRgctx(double version, Il2CppRGCTXDefinition[] rows)
{
    var helper = typeof(StructGenerator).GetMethod("EnumerateRuntimeRgctx",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    return (IEnumerable<Il2CppRGCTXDefinition>)helper.Invoke(null, new object[] { version, rows })!;
}

static ulong FindRegistration(int version, string moduleName)
{
    using var memory = new MemoryStream(new byte[0x2000]);
    using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, leaveOpen: true))
    {
        memory.Position = 0x1000;
        writer.Write(System.Text.Encoding.ASCII.GetBytes(moduleName + "\0"));
        memory.Position = 0x1200; // CodeGenModule.moduleName
        writer.Write(0x1000u);
        memory.Position = 0x1300; // codeGenModules[0]
        writer.Write(0x1200u);
        memory.Position = 0x1500 + 15 * sizeof(uint); // CodeRegistration.codeGenModulesCount
        writer.Write(1u);
        writer.Write(0x1300u); // CodeRegistration.codeGenModules
    }
    using var binary = new WebAssemblyMemory(memory, 0x2000);
    binary.SetProperties(version, 0);
    return binary.GetSectionHelper(0, 0, 1).FindCodeRegistration();
}

static void Reject(Func<Metadata> parse, string expectedMessage)
{
    try { using var _ = parse(); }
    catch (InvalidDataException error) when (error.Message.Contains(expectedMessage)) { return; }
    throw new Exception("Malformed metadata not rejected: " + expectedMessage);
}
