using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Reflection;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class AssemblyLoadingTests
    {





        [Test]
        public async Task Assembly_GetExecutingAssembly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}()
        {
            // Assembly.GetExecutingAssembly() interacts with runtime state
            return Assembly.GetExecutingAssembly();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetCallingAssembly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}()
        {
            return Assembly.GetCallingAssembly();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetEntryAssembly_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly? {|PS0002:TestMethod|}()
        {
            return Assembly.GetEntryAssembly();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [Test]
        public async Task Assembly_GetTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Type[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            // Assembly.GetTypes() might load dependent assemblies, potentially impure
            return assembly.GetTypes();
        }
    }
            }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetExportedTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Type[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetExportedTypes();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetReferencedAssemblies_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public AssemblyName[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetManifestResourceNames_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public string[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetManifestResourceNames();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetLoadedModules_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Module[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetLoadedModules();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetModules_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Module[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetModules();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_DefinedTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public IEnumerable<TypeInfo> {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.DefinedTypes;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_ExportedTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public IEnumerable<Type> {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.ExportedTypes;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_Modules_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public IEnumerable<Module> {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.Modules;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_ManifestModule_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Module {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.ManifestModule;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_EntryPoint_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public MethodInfo? {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.EntryPoint;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_CustomAttributes_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public IEnumerable<CustomAttributeData> {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.CustomAttributes;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_Location_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public string {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.Location;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_IsDynamic_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public bool {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.IsDynamic;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_IsFullyTrusted_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public bool {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.IsFullyTrusted;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_HostContext_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public long {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.HostContext;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GlobalAssemblyCache_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public bool {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GlobalAssemblyCache;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_ReflectionOnly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using System.Security;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public bool {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.ReflectionOnly;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_SecurityRuleSet_Diagnostic()
        {
            var test = @"
using System.Reflection;
using System.Security;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public SecurityRuleSet {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.SecurityRuleSet;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_CodeBase_Diagnostic()
        {
            var test = @"
#pragma warning disable SYSLIB0012
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public string {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.CodeBase;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_EscapedCodeBase_Diagnostic()
        {
            var test = @"
#pragma warning disable SYSLIB0012
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public string {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.EscapedCodeBase;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetName_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public AssemblyName {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetName();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetFiles_Diagnostic()
        {
            var test = @"
using System.IO;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public FileStream[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetFiles();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetModule_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Module? {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetModule(""MainModule"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetFile_Diagnostic()
        {
            var test = @"
#nullable enable
using System.IO;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public FileStream? {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetFile(""data.bin"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetManifestResourceInfo_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public ManifestResourceInfo? {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetManifestResourceInfo(""asset.txt"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetManifestResourceStream_Diagnostic()
        {
            var test = @"
#nullable enable
using System.IO;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Stream? {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetManifestResourceStream(""asset.txt"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetModules_Overload_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Module[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetModules(true);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetLoadedModules_Overload_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Module[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetLoadedModules(true);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetSatelliteAssembly_Diagnostic()
        {
            var test = @"
using System.Globalization;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetSatelliteAssembly(CultureInfo.InvariantCulture);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetSatelliteAssembly_Overload_Diagnostic()
        {
            var test = @"
using System;
using System.Globalization;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}(Assembly assembly)
        {
            return assembly.GetSatelliteAssembly(CultureInfo.InvariantCulture, new Version(1, 0));
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_Assembly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}(Module module)
        {
            return module.Assembly;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_FullyQualifiedName_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public string {|PS0002:TestMethod|}(Module module)
        {
            return module.FullyQualifiedName;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_GetTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Type[] {|PS0002:TestMethod|}(Module module)
        {
            return module.GetTypes();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_GetType_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Type? {|PS0002:TestMethod|}(Module module)
        {
            return module.GetType(""TestNamespace.TestClass"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveMethod_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public MethodBase {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveMethod(0);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveType_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Type {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveType(0);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveField_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public FieldInfo {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveField(0);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveMember_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public MemberInfo {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveMember(0);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveString_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public string {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveString(0);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveSignature_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public byte[] {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveSignature(0);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Module_ResolveMethod_Overload_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public MethodBase {|PS0002:TestMethod|}(Module module)
        {
            return module.ResolveMethod(0, Array.Empty<Type>(), Array.Empty<Type>());
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_LoadFile_NoDiagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly TestMethod(string path)
        {
            // Assembly.LoadFile involves IO and is impure
            return Assembly.LoadFile(path);
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 25, 10, 35)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
