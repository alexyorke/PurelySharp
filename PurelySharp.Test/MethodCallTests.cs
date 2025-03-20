using System.Threading.Tasks;
using NUnit.Framework;

namespace PurelySharp.Test
{
    [TestFixture]
    public class MethodCallTests
    {
        [Test]
        
        public async Task PureMethodCallingPureMethod_NoDiagnostic()
        {
            // Implementation of the test
        }

        [Test]
        
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            // Implementation of the test
        }

        [Test]
        
        public async Task ImpureMethodCallingPureMethod_Diagnostic()
        {
            // Implementation of the test
        }
    }
}
