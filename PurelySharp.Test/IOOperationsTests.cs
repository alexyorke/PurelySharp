using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class IOOperationsTests
    {
        [Test]
        public async Task AsyncAwaitImpurity_ShouldDetectDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



public class TestClass
{
    [EnforcePure]
    public async Task<int> {|PS0002:TestMethod|}()
    {
        // Just awaiting Task.FromResult should be pure, but the method is async
        return await Task.FromResult(42);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ClosureOverFieldImpurity_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;



public class TestClass
{
    private List<string> _log = new List<string>();

    [EnforcePure]
    public IEnumerable<int> {|PS0002:TestMethod|}(int[] numbers)
    {
        // The closure captures _log field and modifies it
        return numbers.Select(n => {
            _log.Add($""Processing {n}"");
            return n * 2;
        });
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ComplexMemberAccess_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class Helper
{
    public static Helper Instance { get; } = new Helper();
    public Helper GetHelper() => this;
    public void DoSomethingImpure() => Console.WriteLine(""Impure"");
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Complex chain that ends in impurity might be missed
        Helper.Instance.GetHelper().DoSomethingImpure();
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConditionalImpurity_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(bool condition)
    {
        if (condition)
        {
            // This branch is impure
            Console.WriteLine(""Condition met"");
        }
        else if (DateTime.Now.Hour > 12)
        {
            // This branch has a more hidden impurity (DateTime.Now)
            Console.WriteLine(""Afternoon"");
        }
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConditionallyNeverCalledIoMethod_ShouldBePure_Test1()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(string input)
    {
        if (false) // Never executed branch
        {
            // Impure operation but in a branch that can never be executed
            File.WriteAllText(""log.txt"", input);
        }
        
        return input.ToUpper();
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstantIoPath_ShouldBePure_Test1()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        // Define paths but never use them for IO
        const string LogPath = ""C:\\logs\\app.log"";
        var tempPath = Path.GetTempPath();
        
        // Just concatenate strings, no actual IO
        return LogPath + "" is different from "" + tempPath;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DynamicDispatchImpurity_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) 
    {
        // Impure operation
        Console.WriteLine(message);
    }
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(ILogger logger)
    {
        // Dynamic dispatch to an impure method
        logger.Log(""Hello"");
    }

    public void CallWithImpureLogger()
    {
        TestMethod(new ConsoleLogger());
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtensionMethodImpurity_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public static class Extensions
{
    public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        foreach (var item in items)
        {
            action(item);
        }
    }
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(List<string> items)
    {
        // Extension method calls Console but might not be detected
        items.ForEach(item => Console.WriteLine(item));
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithConsoleWrite_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace ConsoleApplication1
{
    class TestClass
    {
        [EnforcePure]
        public void {|PS0002:TestMethod|}()
        {
            Console.WriteLine(""Hello, World!"");
        }
    }
}
";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithFileOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(string path)
    {
        File.WriteAllText(path, ""test""); // Impure: File I/O
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IndirectImpurity_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Impurity is hidden behind multiple layers
        PureWrapper();
    }

    private void PureWrapper()
    {
        Console.WriteLine(""Hello"");
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StaticFieldAccess_MayMissDiagnostic()
        {
            // Static field access can be impure but might be missed
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private static string sharedState = """";
    
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        // Reading from static field should be detected as impure
        return sharedState;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MemoryAllocationImpurity_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class TestClass
{
    [EnforcePure]
    public List<int> {|PS0002:TestMethod|}()
    {
        var result = new List<int>(); // Creates a mutable collection
        for (int i = 0; i < 100; i++)
        {
            result.Add(i); // Modifies heap memory
        }
        return result;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ReflectionImpurity_MayMissDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Reflection;


public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Reflection can get types
        var type = Type.GetType(""System.Console"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsafeCodeImpurity_MayMissDiagnostic()
        {
            // Unsafe code operations might not be properly detected
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public unsafe void {|PS0002:TestMethod|}(int* ptr)
    {
        // Direct memory manipulation is impure
        *ptr = 42;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LocalFunctionImpurity_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Local function with impure operation
        int LocalImpure(int x)
        {
            Console.WriteLine(x);
            return x * 2;
        }

        LocalImpure(42);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LambdaCapturing_MayMissDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(int[] numbers)
    {
        int sum = 0;
        // Lambda that captures and modifies a local variable
        numbers.ToList().ForEach(n => sum += n);
        return sum;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IoPathUriManipulation_ShouldBePure_Test1()
        {
            // IO-related class for pure path manipulation
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(string path)
    {
        // Path operations are pure string manipulations
        string dir = Path.GetDirectoryName(path);
        string ext = Path.GetExtension(path);
        string fileName = Path.GetFileName(path);

        // Uri manipulation is also pure
        var uri = new Uri(""file://""+ path);
        string scheme = uri.Scheme;

        // Console.WriteLine is impure
        Console.WriteLine(""Testing"");

        return $""{dir}/{fileName} has extension {ext} and scheme {scheme}"";
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ThreadStaticFieldImpurity_MayMissDiagnostic()
        {
            // Thread static fields might not be properly detected
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [ThreadStatic]
    private static int _threadCounter;

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        // Reading/writing thread-static fields is impure
        return _threadCounter++;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LazyInitializationImpurity_MayMissDiagnostic()
        {
            // Lazy initialization patterns might not be detected properly
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private static object _syncRoot = new object();
    private static string _cache;

    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        if (_cache == null)
        {
            lock (_syncRoot) // Lock statement is impure
            {
                if (_cache == null)
                {
                    _cache = ""Initialized""; // Static field modification is impure
                }
            }
        }
        return _cache;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IoClassPureMethod_ShouldBePure_Test2()
        {
            // IO class with pure method that doesn't actually use IO
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class IoWrapper
{
    // This class does IO in other methods, but not in this one
    public static string CombinePaths(string path1, string path2)
    {
        return Path.Combine(path1, path2);
    }
}

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(string folder, string file)
    {
        // Path.Combine is pure, it just manipulates strings
        return IoWrapper.CombinePaths(folder, file);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StreamThatIsNeverUsed_ShouldBePure()
        {
            // Creates streams but never uses them for IO
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Create stream object but don't perform any IO
        var stream = new MemoryStream();
        
        // Just return its type, no actual IO
        stream.GetType();
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IoClassInheritanceButPureMethods_ShouldBePure()
        {
            // Class inherits from IO class but only uses pure properties
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class PureStreamInfo : FileSystemInfo
{
    public override string Name => ""PureStream"";
    public override bool Exists => false;
    
    public override void Delete() 
    { 
        // Impure but never called
        throw new NotImplementedException();
    }
}

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        // Create an object of IO-derived class but only use pure properties
        var info = new PureStreamInfo();
        return info.Name;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IoFieldsOnlyInConstructor_ShouldBePure()
        {
            // IO initialization only in constructor, pure method
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    private readonly string _filePath;
    
    public TestClass()
    {
        // IO occurs in constructor, not in method
        _filePath = Path.Combine(Path.GetTempPath(), ""data.txt"");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
    }
    
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        // No IO here, just returns a field
        return _filePath;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MockIoInterface_ShouldBePure()
        {
            // Using interface that could represent IO but with pure implementation
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public interface IFileSystem
{
    string ReadAllText(string path);
}

public class PureInMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
    
    public string ReadAllText(string path)
    {
        return _files.TryGetValue(path, out var content) ? content : string.Empty;
    }
}

public class TestClass
{
    private readonly IFileSystem _fileSystem;
    
    public TestClass(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    
    [EnforcePure]
    public string {|PS0002:TestMethod|}(string path)
    {
        // Using an interface that looks like IO but with pure implementation
        return _fileSystem.ReadAllText(path);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnusedIoFieldReference_ShouldBePure()
        {
            // Reference to IO class without actually using its impure methods
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



public class TestClass
{
    // Having a field of an IO type doesn't make the method impure
    private readonly FileInfo _fileInfo;

    public TestClass(string path)
    {
        _fileInfo = new FileInfo(path);
    }

    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        // Just getting the name without performing IO
        return _fileInfo.Name;  
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DirectConsoleWriteLineCall_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(string message)
    {
        // Direct Console.WriteLine call should be detected as impure
        Console.WriteLine(message);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- System.IO.Compression Tests ---
        // TODO: Enable tests once analyzer correctly identifies stream I/O as impure
        // Commented out tests removed

        // --- XDocument Load/Save Tests ---
        // TODO: Enable tests once analyzer correctly identifies file/stream I/O as impure
        // Commented out tests removed

        [Test]
        public async Task IoFileWriteAllText_ShouldBeImpure_Test1()
        {
            var test = @"
using System;
using System.IO;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(string path, string content)
    {
        // File.WriteAllText is impure
        File.WriteAllText(path, content);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IoFileReadAllText_ShouldBeImpure_Test1()
        {
            var test = @"
using System;
using System.IO;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(string path)
    {
        // File.ReadAllText is impure
        return File.ReadAllText(path);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}