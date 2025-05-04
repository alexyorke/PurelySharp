using NUnit.Framework;
using PurelySharp.Analyzer.Engine;
using System.Linq;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConstantsTests
    {
        [Test]
        public void StaticConstructor_DoesNotThrow_WhenInitialized()
        {
            // Arrange
            var pureMembers = Constants.KnownPureBCLMembers;
            var impureMethods = Constants.KnownImpureMethods;

            // Assert Preconditions (ensure lists are loaded)
            Assert.That(pureMembers, Is.Not.Null, "KnownPureBCLMembers should be loaded.");
            Assert.That(impureMethods, Is.Not.Null, "KnownImpureMethods should be loaded.");

            // Act
            var overlappingMethods = pureMembers.Intersect(impureMethods).ToList();

            // Assert No Overlap
            Assert.That(overlappingMethods, Is.Empty, 
                $"KnownImpureMethods and KnownPureBCLMembers should not overlap. Found overlaps: {string.Join(", ", overlappingMethods)}");
        }
    }
}
