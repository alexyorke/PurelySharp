using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp
{
    public static class MethodPurityChecker
    {
        // Purity Strategies Instances
        private static readonly IPurityCheckStrategy attributePurityStrategy = new AttributePurityStrategy();
        private static readonly IPurityCheckStrategy delegateInvokePurityStrategy = new DelegateInvokePurityStrategy();
        private static readonly IPurityCheckStrategy builtinOperatorOrConversionPurityStrategy = new BuiltinOperatorOrConversionPurityStrategy();
        private static readonly IPurityCheckStrategy implicitGetterOrInitSetterPurityStrategy = new ImplicitGetterOrInitSetterPurityStrategy();
        private static readonly IPurityCheckStrategy staticInterfaceMemberPurityStrategy = new StaticInterfaceMemberPurityStrategy();
        private static readonly IPurityCheckStrategy knownPureListStrategy;
        private static readonly IPurityCheckStrategy pureNamespaceOrLinqExtensionStrategy;

        // Impurity Strategies Instances
        private static readonly IImpurityCheckStrategy knownImpureListImpurityStrategy;
        private static readonly IImpurityCheckStrategy impureTypeImpurityStrategy;
        private static readonly IImpurityCheckStrategy propertySetterImpurityStrategy = new PropertySetterImpurityStrategy();
        private static readonly IImpurityCheckStrategy asyncImpurityStrategy = new AsyncImpurityStrategy();
        private static readonly IImpurityCheckStrategy enumTryParsePurityOverrideStrategy = new EnumTryParsePurityOverrideStrategy();
        private static readonly IImpurityCheckStrategy refOutParameterImpurityStrategy = new RefOutParameterImpurityStrategy();

        // Strategy Lists
        private static readonly List<IPurityCheckStrategy> _purityStrategies;
        private static readonly List<IImpurityCheckStrategy> _standardImpurityStrategies; // Excludes EnumTryParse override

        // Methods that are known to be pure
        private static readonly HashSet<string> KnownPureMethods = new HashSet<string>
        {
            "System.String.Concat",
            "System.String.Format",
            "System.String.Join",
            "System.String.Substring",
            "System.String.Replace",
            "System.String.Trim",
            "System.String.TrimStart",
            "System.String.TrimEnd",
            "System.String.ToLower",
            "System.String.ToUpper",
            "System.String.ToLowerInvariant",
            "System.String.ToUpperInvariant",
            "System.String.Split",
            "System.String.Contains",
            "System.String.StartsWith",
            "System.String.EndsWith",
            "System.String.IndexOf",
            "System.String.LastIndexOf",
            "System.String.CompareTo",
            "System.String.Compare",
            "System.String.IsNullOrEmpty",
            "System.Object.GetType",
            "System.Type.GetType",
            "System.Type.GetMethod",
            "System.Type.GetMethods",
            "System.Type.GetProperty",
            "System.Type.GetProperties",
            "System.Type.GetField",
            "System.Type.GetFields",
            "System.Type.GetMember",
            "System.Type.GetMembers",
            "System.Type.GetConstructors",
            "System.Reflection.PropertyInfo.GetValue",
            "System.Reflection.FieldInfo.GetValue",
            "System.Convert.ChangeType",
            "System.Int32.Parse",
            "System.Double.Parse",
            "System.Boolean.Parse",
            "System.Decimal.Parse",
            "System.Guid.Parse",
            "System.DateTime.Parse",
            "System.DateTimeOffset.Parse",
            "System.TimeSpan.Parse",
            "System.Math.Abs",
            "System.Math.Max",
            "System.Math.Min",
            "System.Math.Pow",
            "System.Math.Sqrt",
            "System.Math.Sin",
            "System.Math.Cos",
            "System.Math.Tan",
            "System.Text.RegularExpressions.Regex.IsMatch",
            "System.Text.RegularExpressions.Regex.Match",
            "System.Text.Encoding.UTF8.GetString",
            "System.Text.Encoding.UTF8.GetBytes",
            "System.Text.Encoding.ASCII.GetString",
            "System.Text.Encoding.ASCII.GetBytes",
            "System.Text.Json.JsonSerializer.Serialize",
            "System.Text.Json.JsonSerializer.Deserialize",
            "System.Text.StringBuilder.ToString",
            "System.Xml.Linq.XDocument.Parse",
            "System.Xml.Linq.XElement.Parse",
            "System.Xml.Linq.XContainer.Descendants",
            "System.Xml.Linq.XElement.Attributes",
            "System.Xml.Linq.XElement.Attribute",
            "System.Xml.Linq.XAttribute.Value",
            "System.Xml.Linq.XNode.ToString",
            "System.ReadOnlySpan`1.Contains",
            "System.ReadOnlySpan`1.IndexOf",
            "System.ReadOnlySpan`1.Slice",
            "System.ReadOnlySpan`1.ToArray",
            "System.ReadOnlySpan`1.ToString",
            "System.MemoryExtensions.SequenceEqual",
            "System.MemoryExtensions.Equals",
            "System.Collections.Generic.Dictionary`2.ContainsKey",
            "System.Collections.Generic.List`1.Contains",
            "System.Collections.Immutable.ImmutableArray.Create",
            "System.Collections.Immutable.ImmutableArray.CreateRange",
            "System.Collections.Immutable.ImmutableList.Create",
            "System.Collections.Immutable.ImmutableList.CreateRange",
            "System.Collections.Immutable.ImmutableHashSet.Create",
            "System.Collections.Immutable.ImmutableHashSet.CreateRange",
            "System.Collections.Immutable.ImmutableDictionary.Create",
            "System.Collections.Immutable.ImmutableDictionary.CreateRange",
            "System.Linq.Enumerable.Select",
            "System.Linq.Enumerable.Where",
            "System.Linq.Enumerable.ToList",
            "System.Linq.Enumerable.ToArray",
            "System.Linq.Enumerable.ToDictionary",
            "System.Linq.Enumerable.ToHashSet",
            "System.Linq.Enumerable.Any",
            "System.Linq.Enumerable.All",
            "System.Linq.Enumerable.Count",
            "System.Linq.Enumerable.FirstOrDefault",
            "System.Linq.Enumerable.First",
            "System.Linq.Enumerable.LastOrDefault",
            "System.Linq.Enumerable.Last",
            "System.Linq.Enumerable.SingleOrDefault",
            "System.Linq.Enumerable.Single",
            "System.Linq.Enumerable.OrderBy",
            "System.Linq.Enumerable.OrderByDescending",
            "System.Linq.Enumerable.ThenBy",
            "System.Linq.Enumerable.ThenByDescending",
            "System.Linq.Enumerable.GroupBy",
            "System.Linq.Enumerable.Sum",
            "System.Linq.Enumerable.Average",
            "System.Linq.Enumerable.Min",
            "System.Linq.Enumerable.Max",
            "System.Linq.Enumerable.Cast",
            "System.Linq.Enumerable.OfType"
        };

        // Methods that are impure due to side effects (IO, state changes, etc.)
        private static readonly HashSet<string> KnownImpureMethods = new HashSet<string>
        {
            "System.Console.WriteLine",
            "System.Console.Write",
            "System.Console.ReadLine",
            "System.Console.ReadKey",
            "System.IO.File.ReadAllText",
            "System.IO.File.WriteAllText",
            "System.IO.File.Exists",
            "System.IO.File.Delete",
            "System.IO.File.Copy",
            "System.IO.File.Move",
            "System.IO.Directory.CreateDirectory",
            "System.IO.Directory.Delete",
            "System.IO.Directory.GetFiles",
            "System.Random.Next",
            "System.Random.NextBytes",
            "System.Random.NextDouble",
            "System.Threading.Thread.Sleep",
            "System.Threading.Tasks.Task.Delay"
        };

        // Types with methods that are always considered impure
        private static readonly HashSet<string> ImpureTypes = new HashSet<string>
        {
            "System.IO.File",
            "System.IO.Directory",
            "System.IO.FileStream",
            "System.IO.StreamWriter",
            "System.IO.StreamReader",
            "System.Console",
            "System.Random",
            "System.Net.WebClient",
            "System.Net.Http.HttpClient",
            "System.Data.SqlClient.SqlConnection",
            "System.Data.SqlClient.SqlCommand",
            "System.Data.SqlClient.SqlDataReader"
        };

        // Methods that modify state but are sometimes needed in pure methods
        private static readonly HashSet<string> ConditionallyPureMethods = new HashSet<string>
        {
            "System.Collections.Generic.List`1.Add",
            "System.Collections.Generic.Dictionary`2.Add",
            "System.Collections.Generic.HashSet`1.Add"
        };

        // LINQ methods are considered pure
        private static readonly HashSet<string> PureNamespaces = new HashSet<string>
        {
            "System.Linq",
            "System.Collections.Immutable"
        };

        // Static constructor to initialize strategies and lists
        static MethodPurityChecker()
        {
            // Initialize strategies requiring static data
            knownPureListStrategy = new KnownPureListStrategy(KnownPureMethods);
            pureNamespaceOrLinqExtensionStrategy = new PureNamespaceOrLinqExtensionStrategy(PureNamespaces);
            knownImpureListImpurityStrategy = new KnownImpureListImpurityStrategy(KnownImpureMethods);
            impureTypeImpurityStrategy = new ImpureTypeImpurityStrategy(ImpureTypes);

            // Populate Purity Strategy List
            _purityStrategies = new List<IPurityCheckStrategy>
            {
                attributePurityStrategy,
                delegateInvokePurityStrategy,
                implicitGetterOrInitSetterPurityStrategy,
                knownPureListStrategy,
                pureNamespaceOrLinqExtensionStrategy,
                staticInterfaceMemberPurityStrategy,
                builtinOperatorOrConversionPurityStrategy
            };

            // Populate Standard Impurity Strategy List
            _standardImpurityStrategies = new List<IImpurityCheckStrategy>
            {
                knownImpureListImpurityStrategy,
                impureTypeImpurityStrategy,
                propertySetterImpurityStrategy,
                asyncImpurityStrategy,
                refOutParameterImpurityStrategy
            };
        }

        public static bool IsKnownPureMethod(IMethodSymbol method)
        {
            if (method == null)
                return false;

            // Check if any purity strategy identifies the method as pure
            return _purityStrategies.Any(strategy => strategy.IsPure(method));
        }

        private static bool IsInKnownPureList(IMethodSymbol method)
        {
            // Check if it's a known pure method
            var fullName = method.ContainingType?.ToString() + "." + method.Name;
            return KnownPureMethods.Contains(fullName);
        }

        public static bool IsKnownImpureMethod(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            // Check for the Enum.TryParse override *first*
            if (enumTryParsePurityOverrideStrategy.IsImpure(methodSymbol))
            {
                return false; // Special case: Enum.TryParse is NOT considered impure by these rules.
            }

            // Check if any standard impurity strategy identifies the method as impure
            return _standardImpurityStrategies.Any(strategy => strategy.IsImpure(methodSymbol));
        }

        private static bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure");
        }
    }
}