using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class PropertyGetterDispatchTests
    {
        [Test]
        public async Task InterfacePropertyGetter_WithImpureImplementation_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public interface ICounter
{
    int Count { get; }
}

public sealed class ImpureCounter : ICounter
{
    private int _reads;

    public int Count
    {
        get
        {
            _reads++;
            return _reads;
        }
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:Read|}(ICounter counter) => counter.Count;
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task VirtualPropertyGetter_WithImpureOverride_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class BaseCounter
{
    public virtual int Count => 0;
}

public sealed class ImpureCounter : BaseCounter
{
    private int _reads;

    public override int Count
    {
        get
        {
            _reads++;
            return _reads;
        }
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:Read|}(BaseCounter counter) => counter.Count;
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
