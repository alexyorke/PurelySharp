using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ListPatternsTests
    {
        [Test]
        public async Task ListPattern_BasicMatching_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ListPatternExample
    {
        [EnforcePure]
        public static string DescribeList(int[] numbers)
        {
            // C# 11 list pattern matching with different patterns
            return numbers switch
            {
                [] => ""Empty list"",
                [1] => ""List with just 1"",
                [1, 2] => ""List with 1 and 2"",
                [1, 2, 3] => ""List with 1, 2, and 3"",
                [0, .. var rest] => $""List starting with 0, followed by {rest.Length} more elements"",
                [var first, var second, _] => $""List with at least 3 elements, starting with {first} and {second}"",
                [var first, .. var middle, var last] => $""List with {first} at start, {last} at end, and {middle.Length} elements in between"",
                _ => ""Some other list""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListPattern_WithVariablePatterns_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ListPatternVariables
    {
        [EnforcePure]
        public static string ValidateNumbers(int[] numbers)
        {
            // List pattern with variable and relational patterns
            return numbers switch
            {
                [] => ""Empty array"",
                [> 0, > 0] => ""Two positive numbers"",
                [< 0, < 0] => ""Two negative numbers"",
                [0, 0] => ""Two zeros"",
                [var first, .. var rest] when first > 0 && rest.All(n => n > 0) => ""All positive numbers"",
                [var first, .. var rest] when first < 0 && rest.All(n => n < 0) => ""All negative numbers"",
                [_, .. var middle, _] when middle.Sum() == 0 => ""Middle elements sum to zero"",
                _ => ""Mixed numbers""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListPattern_WithNestedPatterns_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Point
    {
        public int X { get; set; }
        public int Y { get; set; }
        
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public class NestedListPatterns
    {
        [EnforcePure]
        public static string DescribePoints(List<Point> points)
        {
            // Nested patterns within list patterns
            return points switch
            {
                [] => ""No points"",
                [{ X: 0, Y: 0 }] => ""Just the origin"",
                [{ X: 0, Y: var y }, _] when y != 0 => ""First point on Y-axis"",
                [{ X: var x, Y: 0 }, _] when x != 0 => ""First point on X-axis"",
                [{ X: var x1, Y: var y1 }, { X: var x2, Y: var y2 }] => $""Two points at ({x1},{y1}) and ({x2},{y2})"",
                _ => ""Some other point configuration""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListPattern_WithImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ListPatternImpure
    {
        [EnforcePure]
        public static void ProcessLogMessages(string[] messages)
        {
            // List pattern in an impure method
            switch (messages)
            {
                case []:
                    Console.WriteLine(""No messages to process"");
                    break;
                case [var single]:
                    File.WriteAllText(""log.txt"", single);
                    break;
                case [var first, .. var rest]:
                    File.WriteAllText(""log.txt"", first);
                    File.AppendAllLines(""log.txt"", rest);
                    break;
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(19, 21, 19, 64).WithArguments("ProcessLogMessages");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ListPattern_WithComplexPatterns_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }
    }

    public class ComplexListPatterns
    {
        [EnforcePure]
        public static string AnalyzeGroup(List<Person> people)
        {
            return people switch
            {
                [] => ""Empty group"",
                [var p] when p.Age >= 18 => $""One adult: {p.Name}"",
                [var p] when p.Age < 18 => $""One child: {p.Name}"",
                [var p1, var p2] when p1.Age >= 18 && p2.Age >= 18 => $""Two adults: {p1.Name} and {p2.Name}"",
                [var p1, var p2] when p1.Age < 18 && p2.Age < 18 => $""Two children: {p1.Name} and {p2.Name}"",
                [var first, var last] => $""Mixed group with {first.Name} (age {first.Age}) first and {last.Name} (age {last.Age}) last"",
                _ => ""Group with more than two people""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListPattern_WithDifferentCollectionTypes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class DifferentCollectionTypes
    {
        [EnforcePure]
        public static string AnalyzeList(List<string> items)
        {
            // List patterns work with different collection types
            return items switch
            {
                [] => ""Empty collection"",
                [var single] => $""Collection with single item: {single}"",
                [var first, var second] => $""Collection with two items: {first} and {second}"",
                [var first, var second, var third, _] => $""Collection with at least four items, starting with {first}, {second}, {third}"",
                _ => ""Collection with three items""
            };
        }

        [EnforcePure]
        public static string AnalyzeArray(int[] numbers)
        {
            // List patterns also work with arrays
            return numbers switch
            {
                [] => ""Empty array"",
                [0] => ""Array containing just zero"",
                [0, 1] => ""Array containing 0 and 1"",
                [> 0, > 0, > 0] => ""Array with three positive numbers"",
                _ => ""Some other array pattern""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


