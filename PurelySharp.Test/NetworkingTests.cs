using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class NetworkingTests
    {
        // --- DNS Lookup (Impure) ---
        // TODO: Enable once analyzer flags Dns methods as impure
        /*
        [Test]
        public async Task Dns_GetHostEntry_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IPHostEntry TestMethod(string host)
    {
        return Dns.GetHostEntry(host); // Impure: Network I/O
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 16, 13, 39).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- IPAddress Parsing (Pure) ---
        /* // TODO: Fix - Analyzer flags TryParse (with out param) as impure
        [Test]
        public async Task IPAddress_Parse_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IPAddress? TestMethod(string ipString)
    {
        // Pure: Parses string input
        IPAddress.TryParse(ipString, out var address);
        return address;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        */

        // --- Socket Operations (Impure) ---
        // All socket operations involve OS interaction and/or network I/O
        // TODO: Enable tests below once analyzer flags Socket methods/constructor as impure

        /*
        [Test]
        public async Task Socket_Constructor_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Socket TestMethod()
    {
        // Impure: Creates OS resource
        return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 87).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Socket_Connect_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Socket s, EndPoint ep)
    {
        s.Connect(ep); // Impure: Network I/O
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(15, 9, 15, 23).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        
        [Test]
        public async Task Socket_Bind_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Socket s, EndPoint ep)
    {
        s.Bind(ep); // Impure: OS interaction
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(15, 9, 15, 19).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Socket_Listen_Diagnostic()
        {
             var test = @"
#nullable enable
using System;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Socket s)
    {
        s.Listen(10); // Impure: OS interaction
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 9, 14, 21).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Socket_Accept_Diagnostic()
        {
             var test = @"
#nullable enable
using System;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Socket TestMethod(Socket s)
    {
        return s.Accept(); // Impure: Blocks, Network I/O
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 28).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Socket_SendReceive_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(Socket s, byte[] buffer)
    {
        s.Send(buffer); // Impure: Network I/O
        return s.Receive(buffer); // Impure: Network I/O
    }
}";
            // Expect diagnostic on the first impure call (Send)
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 9, 14, 23).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Socket_CloseDispose_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Net.Sockets;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Socket s)
    {
        s.Shutdown(SocketShutdown.Both); // Impure: Network I/O / OS State
        s.Close(); // Impure: Releases OS resource
        s.Dispose(); // Impure: Releases OS resource
    }
}";
            // Expect diagnostic on the first impure call (Shutdown)
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 9, 14, 41).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 