using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using PurelySharp.Analyzer.Configuration;

namespace PurelySharp.Analyzer.Engine
{
	internal static class ImpurityCatalog
	{
		private static ImmutableHashSet<string> _extraImpureMethods = ImmutableHashSet<string>.Empty;
		private static ImmutableHashSet<string> _extraPureMethods = ImmutableHashSet<string>.Empty;
		private static ImmutableHashSet<string> _extraImpureNamespaces = ImmutableHashSet<string>.Empty;
		private static ImmutableHashSet<string> _extraImpureTypes = ImmutableHashSet<string>.Empty;

		public static void InitializeOverrides(AnalyzerConfiguration config)
		{
			_extraImpureMethods = config.ExtraKnownImpureMethods;
			_extraPureMethods = config.ExtraKnownPureMethods;
			_extraImpureNamespaces = config.ExtraKnownImpureNamespaces;
			_extraImpureTypes = config.ExtraKnownImpureTypes;
		}

		public static bool IsKnownPureBCLMember(ISymbol symbol)
		{
			if (symbol == null) return false;

			if (symbol.ContainingType?.ContainingNamespace?.ToString().StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true)
			{
				if (symbol.Name.Contains("Create") || symbol.Name.Contains("Add") || symbol.Name.Contains("Set") || symbol.Name.Contains("Remove"))
				{
					PurityAnalysisEngine.LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable member: {symbol.ToDisplayString()}");
					return true;
				}
				if (symbol.Kind == SymbolKind.Property && (symbol.Name == "Count" || symbol.Name == "Length" || symbol.Name == "IsEmpty"))
				{
					PurityAnalysisEngine.LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable property: {symbol.ToDisplayString()}");
					return true;
				}
				if (symbol.Kind == SymbolKind.Method && (symbol.Name == "Contains" || symbol.Name == "IndexOf" || symbol.Name == "TryGetValue"))
				{
					PurityAnalysisEngine.LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable method: {symbol.ToDisplayString()}");
					return true;
				}
			}

			string signature = symbol.OriginalDefinition.ToDisplayString();
			if (symbol.Kind == SymbolKind.Property)
			{
				if (!signature.EndsWith(".get") && !signature.EndsWith(".set"))
				{
					signature += ".get";
					PurityAnalysisEngine.LogDebug($"    [IsKnownPure] Appended .get to property signature: \"{signature}\"");
				}
			}

			PurityAnalysisEngine.LogDebug($"    [IsKnownPure] Checking HashSet.Contains for signature: \"{signature}\"");
			bool isKnownPure = Constants.KnownPureBCLMembers.Contains(signature) || _extraPureMethods.Contains(signature);
			PurityAnalysisEngine.LogDebug($"    [IsKnownPure] HashSet.Contains result: {isKnownPure}");

			if (!isKnownPure && symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
			{
				signature = methodSymbol.ConstructedFrom.ToDisplayString();
				isKnownPure = Constants.KnownPureBCLMembers.Contains(signature) || _extraPureMethods.Contains(signature);
			}
			else if (!isKnownPure && symbol is IPropertySymbol propertySymbol && propertySymbol.ContainingType.IsGenericType)
			{
				if (propertySymbol.IsIndexer)
				{
					signature = propertySymbol.OriginalDefinition.ToDisplayString();
				}
				else
				{
					signature = $"{propertySymbol.ContainingType.ConstructedFrom.ToDisplayString()}.{propertySymbol.Name}.get";
				}
				isKnownPure = Constants.KnownPureBCLMembers.Contains(signature) || _extraPureMethods.Contains(signature);
			}

			if (isKnownPure)
			{
				PurityAnalysisEngine.LogDebug($"Helper IsKnownPureBCLMember: Match found for {symbol.ToDisplayString()} using signature '{signature}'");
			}
			else if (symbol.ContainingNamespace?.ToString().Equals("System", StringComparison.Ordinal) == true &&
				symbol.ContainingType?.Name.Equals("Math", StringComparison.Ordinal) == true)
			{
				PurityAnalysisEngine.LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Math member: {symbol.ToDisplayString()}");
				isKnownPure = true;
			}

			return isKnownPure;
		}

		public static bool IsKnownImpure(ISymbol symbol)
		{
			if (symbol == null) return false;

			string signature = symbol.OriginalDefinition.ToDisplayString();
			if (symbol.Kind == SymbolKind.Property)
			{
				if (!signature.EndsWith(".get") && !signature.EndsWith(".set"))
				{
					signature += ".get";
					PurityAnalysisEngine.LogDebug($"    [IsKnownImpure] Appended .get to property signature: \"{signature}\"");
				}
			}

			PurityAnalysisEngine.LogDebug($"    [IsKnownImpure] Checking HashSet.Contains for signature: \"{signature}\"");
			if (Constants.KnownImpureMethods.Contains(signature) || _extraImpureMethods.Contains(signature))
			{
				PurityAnalysisEngine.LogDebug($"Helper IsKnownImpure: Match found for {symbol.ToDisplayString()} using full signature '{signature}'");
				return true;
			}

			if (symbol.ContainingType != null)
			{
				string simplifiedName = $"{symbol.ContainingType.Name}.{symbol.Name}";
				PurityAnalysisEngine.LogDebug($"    [IsKnownImpure] Checking HashSet.Contains for simplified name: \"{simplifiedName}\"");
				if (Constants.KnownImpureMethods.Contains(simplifiedName) || _extraImpureMethods.Contains(simplifiedName))
				{
					PurityAnalysisEngine.LogDebug($"Helper IsKnownImpure: Match found for {symbol.ToDisplayString()} using simplified name '{simplifiedName}'");
					return true;
				}
			}

			if (symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
			{
				signature = methodSymbol.ConstructedFrom.ToDisplayString();
				if (Constants.KnownImpureMethods.Contains(signature) || _extraImpureMethods.Contains(signature))
				{
					PurityAnalysisEngine.LogDebug($"Helper IsKnownImpure: Generic match found for {symbol.ToDisplayString()} using signature '{signature}'");
					return true;
				}
			}

			if (symbol is IPropertySymbol property && IsInImpureNamespaceOrType(property.ContainingType))
			{
				PurityAnalysisEngine.LogDebug($"Helper IsKnownImpure: Property access {symbol.ToDisplayString()} on known impure type {property.ContainingType.ToDisplayString()}.");
			}

			if (symbol.ContainingType?.ToString().Equals("System.Threading.Interlocked", StringComparison.Ordinal) ?? false)
			{
				PurityAnalysisEngine.LogDebug($"Helper IsKnownImpure: Member {symbol.ToDisplayString()} belongs to System.Threading.Interlocked.");
				return true;
			}

			if (symbol.ContainingType?.ToString().Equals("System.Threading.Volatile", StringComparison.Ordinal) ?? false)
			{
				PurityAnalysisEngine.LogDebug($"Helper IsKnownImpure: Member {symbol.ToDisplayString()} belongs to System.Threading.Volatile and is considered impure.");
				return true;
			}

			return false;
		}

		public static bool IsInImpureNamespaceOrType(ISymbol symbol)
		{
			if (symbol == null) return false;

			PurityAnalysisEngine.LogDebug($"    [INOT] Checking symbol: {symbol.ToDisplayString()}");
			INamedTypeSymbol? containingType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
			while (containingType != null)
			{
				string typeName = containingType.OriginalDefinition.ToDisplayString();
				PurityAnalysisEngine.LogDebug($"    [INOT] Checking type: {typeName}");
				PurityAnalysisEngine.LogDebug($"    [INOT] Comparing '{typeName}' against KnownImpureTypeNames...");
				if (Constants.KnownImpureTypeNames.Contains(typeName) || _extraImpureTypes.Contains(typeName))
				{
					PurityAnalysisEngine.LogDebug($"    [INOT] --> Match found for impure type: {typeName}");
					return true;
				}

				INamespaceSymbol? ns = containingType.ContainingNamespace;
				while (ns != null && !ns.IsGlobalNamespace)
				{
					string namespaceName = ns.ToDisplayString();
					PurityAnalysisEngine.LogDebug($"    [INOT] Checking namespace: {namespaceName}");
					if (Constants.KnownImpureNamespaces.Contains(namespaceName) || _extraImpureNamespaces.Contains(namespaceName))
					{
						PurityAnalysisEngine.LogDebug($"    [INOT] --> Match found for impure namespace: {namespaceName}");
						return true;
					}
					ns = ns.ContainingNamespace;
				}

				PurityAnalysisEngine.LogDebug($"    [INOT] Checking containing type of {containingType.Name}");
				containingType = containingType.ContainingType;
			}

			PurityAnalysisEngine.LogDebug($"    [INOT] No impure type or namespace match found for: {symbol.ToDisplayString()}");
			return false;
		}
	}
}

