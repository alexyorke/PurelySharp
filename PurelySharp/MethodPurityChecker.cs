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
            "System.Math.Abs",
            "System.Math.Max",
            "System.Math.Min",
            "System.Math.Pow",
            "System.Math.Sqrt",
            "System.Math.Sin",
            "System.Math.Cos",
            "System.Math.Tan"
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