using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp
{
    public static class MethodPurityChecker
    {
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

        public static bool IsKnownPureMethod(IMethodSymbol method)
        {
            // Check if the method has a pure attribute
            if (HasEnforcePureAttribute(method) ||
                method.GetAttributes().Any(a => a.AttributeClass?.Name == "PureAttribute" || a.AttributeClass?.Name == "Pure"))
                return true;

            // Check if it's a delegate's Invoke method
            if (method.MethodKind == MethodKind.DelegateInvoke)
            {
                // For delegate's Invoke method, we need to check the specific delegate instance
                // This requires runtime information, so we can't fully analyze here.
                // We'll consider it potentially pure and check the instance separately
                return true;
            }

            if (method == null)
                return false;

            // Check if it's a compiler-generated method (e.g., property getter/setter)
            if (method.IsImplicitlyDeclared)
            {
                // Property getters are generally pure
                if (method.MethodKind == MethodKind.PropertyGet)
                    return true;

                // Init-only setters are generally pure (C# 9.0+)
                if (method.MethodKind == MethodKind.PropertySet &&
                    method.ContainingSymbol is IPropertySymbol property &&
                    property.SetMethod?.IsInitOnly == true)
                    return true;
            }

            // Check if it's a known pure method
            var fullName = method.ContainingType?.ToString() + "." + method.Name;
            if (KnownPureMethods.Contains(fullName))
                return true;

            // Check if it's from a pure namespace like System.Linq
            var namespaceName = method.ContainingNamespace?.ToString() ?? string.Empty;
            if (PureNamespaces.Any(ns => namespaceName.StartsWith(ns)))
                return true;

            // Check for static virtual/abstract interface members
            if (method.IsStatic &&
                method.ContainingType?.TypeKind == TypeKind.Interface &&
                (method.IsVirtual || method.IsAbstract))
            {
                // Static interface members are considered pure by default in interfaces
                return true;
            }

            // Check for implementations of static virtual/abstract interface members
            if (method.IsStatic && method.IsOverride)
            {
                // Get the method being overridden
                var overriddenMethod = method.OverriddenMethod;
                if (overriddenMethod != null &&
                    overriddenMethod.ContainingType?.TypeKind == TypeKind.Interface)
                {
                    // The method overrides a static interface member, but we need to check purity separately
                    return false; // Will be checked through attributes
                }
            }

            // LINQ extension methods are pure
            if (method.IsExtensionMethod && namespaceName.StartsWith("System.Linq"))
                return true;

            // Basic operators (+, -, *, /, etc.) are generally pure
            if (method.MethodKind == MethodKind.BuiltinOperator)
                return true;

            // Check if it's a conversion method (MethodKind.Conversion), which are generally pure
            if (method.MethodKind == MethodKind.Conversion)
                return true;

            return false;
        }

        public static bool IsKnownImpureMethod(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            // Check if it's a known impure method
            var fullName = methodSymbol.ContainingType?.ToString() + "." + methodSymbol.Name;
            if (KnownImpureMethods.Contains(fullName))
                return true;

            // Check if it's in an impure type
            if (methodSymbol.ContainingType != null &&
                ImpureTypes.Contains(methodSymbol.ContainingType.ToString()))
                return true;

            // Property setters are generally impure (except init-only ones checked above)
            if (methodSymbol.MethodKind == MethodKind.PropertySet &&
                !(methodSymbol.ContainingSymbol is IPropertySymbol property && property.SetMethod?.IsInitOnly == true))
                return true;

            // For async methods, we now use the specialized checker instead of treating all async as impure
            if (methodSymbol.IsAsync)
            {
                // Method has the EnforcePure attribute, it will be checked elsewhere
                if (HasEnforcePureAttribute(methodSymbol))
                    return false;

                // Otherwise default to treating as impure for backward compatibility
                return true;
            }

            // Special case for System.Enum.TryParse methods - consider them pure despite having out parameters
            if (methodSymbol.Name == "TryParse" &&
                methodSymbol.ContainingType?.Name == "Enum" &&
                methodSymbol.ContainingType.ContainingNamespace?.Name == "System")
            {
                return false; // Not impure
            }

            // Methods with ref or out parameters modify state
            if (methodSymbol.Parameters.Any(p => p.RefKind == RefKind.Ref || p.RefKind == RefKind.Out))
                return true;

            return false;
        }

        private static bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            // Check for EnforcePure attribute
            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure");
        }
    }
}