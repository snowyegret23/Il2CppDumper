using System.IO;

namespace Il2CppDumper
{
    public static class DummyAssemblyExporter
    {
        public static void Export(Il2CppExecutor il2CppExecutor, string outputDir, bool addToken)
            => Export(il2CppExecutor, outputDir, addToken, false);

        public static void Export(Il2CppExecutor il2CppExecutor, string outputDir, bool addToken, bool restoreExplicitInterfaces)
        {
            Directory.SetCurrentDirectory(outputDir);
            if (Directory.Exists("DummyDll"))
                Directory.Delete("DummyDll", true);
            Directory.CreateDirectory("DummyDll");
            Directory.SetCurrentDirectory("DummyDll");
            var dummy = new DummyAssemblyGenerator(il2CppExecutor, addToken, restoreExplicitInterfaces);
            foreach (var assembly in dummy.Assemblies)
            {
                using var stream = new MemoryStream();
                assembly.Write(stream);
                File.WriteAllBytes(assembly.MainModule.Name, stream.ToArray());
            }
        }
    }
}
