using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StaticInterfaceMembersTests
    {
        [Test]
        public async Task StaticInterfaceMethod_PureImplementation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public interface IAddable<T>
    {
        static abstract T Add(T a, T b);
    }

    public struct Integer : IAddable<Integer>
    {
        public int Value { get; }

        public Integer(int value)
        {
            Value = value;
        }

        public static Integer Add(Integer a, Integer b)
        {
            return new Integer(a.Value + b.Value);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Target runtime doesn't support static abstract members in interfaces
                DiagnosticResult.CompilerError("CS8919").WithSpan(11, 27, 11, 30)
            );
        }

        [Test]
        public async Task StaticInterfaceMethod_ImpureImplementation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public interface ILoggable<T>
    {
        static abstract T Create(string id);
    }

    public struct LoggedValue : ILoggable<LoggedValue>
    {
        private static int _instanceCount = 0;
        
        public string Id { get; }
        
        public LoggedValue(string id)
        {
            Id = id;
        }
        
        [EnforcePure]
        public static LoggedValue Create(string id)
        {
            // Impure operation: modifies static state
            _instanceCount++;
            return new LoggedValue(id);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Target runtime doesn't support static abstract members in interfaces
                DiagnosticResult.CompilerError("CS8919").WithSpan(11, 27, 11, 33),
                // Method is marked as pure but contains impure operations
                VerifyCS.Diagnostic().WithSpan(29, 13, 29, 27).WithArguments("Create")
            );
        }

        [Test]
        public async Task StaticInterfaceMethod_VirtualWithDefault_PureImplementation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public interface IMultiplyable<T>
    {
        static virtual T Multiply(T a, T b)
        {
            throw new NotImplementedException();
        }
    }

    public struct Double : IMultiplyable<Double>
    {
        public double Value { get; }

        public Double(double value)
        {
            Value = value;
        }

        public static Double Multiply(Double a, Double b)
        {
            return new Double(a.Value * b.Value);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Target runtime doesn't support static abstract members in interfaces
                DiagnosticResult.CompilerError("CS8919").WithSpan(11, 26, 11, 34)
            );
        }

        [Test]
        public async Task StaticInterfaceMethod_VirtualWithDefault_ImpureImplementation_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public interface ISerializable<T>
    {
        static virtual T Deserialize(string path)
        {
            throw new NotImplementedException();
        }
    }

    public struct FileData : ISerializable<FileData>
    {
        public byte[] Data { get; }
        
        public FileData(byte[] data)
        {
            Data = data;
        }
        
        [EnforcePure]
        public static FileData Deserialize(string path)
        {
            // Impure operation: reads from file system
            var data = File.ReadAllBytes(path);
            return new FileData(data);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Target runtime doesn't support static abstract members in interfaces
                DiagnosticResult.CompilerError("CS8919").WithSpan(12, 26, 12, 37),
                // Method is marked as pure but contains impure operations
                VerifyCS.Diagnostic().WithSpan(31, 24, 31, 47).WithArguments("Deserialize")
            );
        }
    }
}


