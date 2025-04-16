using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp
{
    public static class MethodPurityChecker
    {
        // Instantiate the strategies
        private static readonly IPurityCheckStrategy attributePurityStrategy = new AttributePurityStrategy();
        private static readonly IPurityCheckStrategy delegateInvokePurityStrategy = new DelegateInvokePurityStrategy();
        private static readonly IPurityCheckStrategy builtinOperatorOrConversionPurityStrategy = new BuiltinOperatorOrConversionPurityStrategy();

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
            if (method == null)
                return false;

            // Use the strategies
            return attributePurityStrategy.IsPure(method) ||
                   delegateInvokePurityStrategy.IsPure(method) ||
                   IsImplicitGetterOrInitSetter(method) ||
                   IsInKnownPureList(method) ||
                   IsInPureNamespaceOrLinqExtension(method) ||
                   IsStaticInterfaceMemberImplementationOrOverride(method) ||
                   builtinOperatorOrConversionPurityStrategy.IsPure(method);
        }

        private static bool IsImplicitGetterOrInitSetter(IMethodSymbol method)
        {
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
            return false;
        }

        private static bool IsInKnownPureList(IMethodSymbol method)
        {
            // Check if it's a known pure method
            var fullName = method.ContainingType?.ToString() + "." + method.Name;
            return KnownPureMethods.Contains(fullName);
        }

        private static bool IsInPureNamespaceOrLinqExtension(IMethodSymbol method)
        {
            // Check if it's from a pure namespace like System.Linq or System.Collections.Immutable
            var namespaceName = method.ContainingNamespace?.ToString() ?? string.Empty;
            if (PureNamespaces.Any(ns => namespaceName.StartsWith(ns)))
                return true;

            // LINQ extension methods are pure
            return method.IsExtensionMethod && namespaceName.StartsWith("System.Linq");
        }

        private static bool IsStaticInterfaceMemberImplementationOrOverride(IMethodSymbol method) // Renamed helper
        {
            // Check for static virtual/abstract interface members (definition)
            if (method.IsStatic &&
                method.ContainingType?.TypeKind == TypeKind.Interface &&
                (method.IsVirtual || method.IsAbstract))
            {
                // Static interface member definitions are considered pure *within the interface* context
                return true;
            }

            // Check for implementations/overrides of static virtual/abstract interface members
            // We consider the *implementation* potentially impure unless marked pure.
            // The original logic returned false here, meaning it didn't classify it as known pure.
            // Keeping original logic: if it's an override of static interface member, it's NOT known pure by default.
            if (method.IsStatic && method.IsOverride)
            {
                var overriddenMethod = method.OverriddenMethod;
                if (overriddenMethod != null &&
                    overriddenMethod.ContainingType?.TypeKind == TypeKind.Interface &&
                    (overriddenMethod.IsVirtual || overriddenMethod.IsAbstract)) // Check if overridden is static virt/abs
                {
                    // This method implements/overrides a static interface member.
                    // We don't automatically consider it pure; requires attribute or further analysis.
                    return false; // Explicitly not known pure based on this rule alone.
                }
            }

            // If none of the above conditions related to static interface members apply, this rule doesn't make it pure.
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

            // Check for EnforcePure attribute - This specific check might still be needed elsewhere
            // or refactored further later. For now, keep it if IsKnownImpureMethod or others use it.
            // It was used by the old HasPurityAttribute, which is removed.
            // IsKnownImpureMethod uses it directly.
            // So, keep HasEnforcePureAttribute for now.
            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure");
        }
    }
}