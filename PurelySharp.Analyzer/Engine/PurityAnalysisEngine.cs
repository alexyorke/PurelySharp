using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Collections.Immutable;
using System;
using System.IO;
using PurelySharp.Analyzer.Engine.Rules; // <-- Add this using
using PurelySharp.Attributes; // Added for PureAttribute
using System.Threading;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Contains the core logic for determining method purity using Control Flow Graph (CFG).
    /// </summary>
    internal class PurityAnalysisEngine
    {
        // Add a constructor (can be empty for now)
        public PurityAnalysisEngine() { }

        // Define a consistent format for symbol comparison strings
        private static readonly SymbolDisplayFormat _signatureFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeContainingType |
                // SymbolDisplayMemberOptions.IncludeExplicitInterfaceImplementation | // Removed for netstandard2.0
                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeModifiers, // Keep modifiers for now, might need removal
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeParamsRefOut | // Include ref/out/params
                SymbolDisplayParameterOptions.IncludeDefaultValue, // Include default value
                                                                   // SymbolDisplayParameterOptions.IncludeOptionalLocations, // Removed for netstandard2.0
                                                                   // Explicitly EXCLUDE parameter names:
                                                                   // SymbolDisplayParameterOptions.IncludeName,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier // Include nullable ?
        );

        // --- Updated list of Purity Rules ---
        private static readonly ImmutableList<IPurityRule> _purityRules = ImmutableList.Create<IPurityRule>(
            new AssignmentPurityRule(),
            new MethodInvocationPurityRule(),
            new ConstructorInitializerPurityRule(), // <-- ADDED RULE
            new ReturnStatementPurityRule(),
            new BinaryOperationPurityRule(),
            new BinaryPatternPurityRule(), // <-- REGISTER NEW RULE
            new PropertyReferencePurityRule(),
            new ArrayElementReferencePurityRule(),
            new CollectionExpressionPurityRule(),
            new ArrayCreationPurityRule(),
            new ArrayInitializerPurityRule(),
            new InterpolatedStringPurityRule(),
            new SwitchStatementPurityRule(),
            new SwitchExpressionPurityRule(),
            new ConstantPatternPurityRule(),
            new DeclarationPatternPurityRule(), // <-- REGISTER NEW RULE
            new DiscardPatternPurityRule(),
            new LoopPurityRule(),
            new FlowCapturePurityRule(),
            new ExpressionStatementPurityRule(),
            new UsingStatementPurityRule(),
            new ParameterReferencePurityRule(),
            new LocalReferencePurityRule(),
            new FieldReferencePurityRule(),
            new BranchPurityRule(),
            new SwitchCasePurityRule(),
            new LiteralPurityRule(),
            new ConversionPurityRule(),
            new DefaultValuePurityRule(), // <-- REGISTER NEW RULE
            new FlowCaptureReferencePurityRule(),
            new ConditionalOperationPurityRule(),
            new UnaryOperationPurityRule(),
            new ObjectCreationPurityRule(),
            new CoalesceOperationPurityRule(),
            new ConditionalAccessPurityRule(),
            new ThrowOperationPurityRule(),
            new VariableDeclarationGroupPurityRule(), // <-- REGISTER NEW RULE
            new IsPatternPurityRule(),
            new IsNullPurityRule(),
            new StructuralPurityRule(),
            new TuplePurityRule(),
            new TypeOfPurityRule(),
            new YieldReturnPurityRule(),
            new DelegateCreationPurityRule(),
            new WithOperationPurityRule(),
            new InstanceReferencePurityRule(),
            new ObjectOrCollectionInitializerPurityRule(),
            new LockStatementPurityRule(),
            new AwaitPurityRule(),
            new Utf8StringLiteralPurityRule(), // Added Rule
            new SizeOfPurityRule() // Added Rule
        );

        // --- Add list of known impure namespaces ---

        // --- Add list of specific impure types ---

        /// <summary>
        /// Represents the result of a purity analysis.
        /// </summary>
        public readonly struct PurityAnalysisResult
        {
            /// <summary>
            /// Indicates whether the analyzed element is considered pure.
            /// </summary>
            public bool IsPure { get; }

            /// <summary>
            /// The syntax node of the first operation determined to be impure, if any.
            /// Null if the element is pure or if the specific impure node couldn't be determined.
            /// </summary>
            public SyntaxNode? ImpureSyntaxNode { get; }

            // Private constructor to enforce usage of factory methods
            private PurityAnalysisResult(bool isPure, SyntaxNode? impureSyntaxNode)
            {
                IsPure = isPure;
                ImpureSyntaxNode = impureSyntaxNode;
            }

            /// <summary>
            /// Represents a pure result.
            /// </summary>
            public static PurityAnalysisResult Pure => new PurityAnalysisResult(true, null);

            /// <summary>
            /// Creates an impure result with the specific syntax node causing the impurity.
            /// </summary>
            public static PurityAnalysisResult Impure(SyntaxNode impureSyntaxNode)
            {
                // Ensure we don't pass null here, use the specific overload if syntax is unknown
                if (impureSyntaxNode == null)
                {
                    throw new ArgumentNullException(nameof(impureSyntaxNode), "Use ImpureUnknownLocation for impurity without a specific node.");
                }
                return new PurityAnalysisResult(false, impureSyntaxNode);
            }

            /// <summary>
            /// Creates an impure result where the specific location is unknown or not applicable.
            /// </summary>
            public static PurityAnalysisResult ImpureUnknownLocation => new PurityAnalysisResult(false, null);
        }

        // Add a set of known impure method signatures

        // Add a set of known PURE BCL method/property signatures (using OriginalDefinition.ToDisplayString() format)
        // This helps handle cases where CFG analysis might fail or be too complex for common BCL members.

        /// <summary>
        /// Represents the purity state during CFG analysis.
        /// Includes basic impurity tracking and potential delegate targets.
        /// </summary>
        internal readonly struct PurityAnalysisState : IEquatable<PurityAnalysisState>
        {
            // --- Existing Fields ---
            public bool HasPotentialImpurity { get; }
            public SyntaxNode? FirstImpureSyntaxNode { get; }

            // --- New Field for Delegate Tracking ---
            // Maps a delegate variable/parameter/field symbol to its potential targets.
            // Use ImmutableDictionary for state propagation.
            public ImmutableDictionary<ISymbol, PotentialTargets> DelegateTargetMap { get; }

            // --- Constructor ---
            internal PurityAnalysisState(
                bool hasPotentialImpurity,
                SyntaxNode? firstImpureSyntaxNode,
                ImmutableDictionary<ISymbol, PotentialTargets>? delegateTargetMap) // Nullable for initial state
            {
                HasPotentialImpurity = hasPotentialImpurity;
                FirstImpureSyntaxNode = firstImpureSyntaxNode;
                // Ensure map is never null, use Empty if needed. Use Ordinal comparer for ISymbol.
                DelegateTargetMap = delegateTargetMap ?? ImmutableDictionary.Create<ISymbol, PotentialTargets>(SymbolEqualityComparer.Default);
            }

            // --- Static Factory for Initial Pure State ---
            public static PurityAnalysisState Pure => new PurityAnalysisState(false, null, null); // Start with empty map

            // --- Updated Merge Logic ---
            public static PurityAnalysisState Merge(IEnumerable<PurityAnalysisState> states)
            {
                bool mergedImpurity = false;
                SyntaxNode? firstImpureNode = null;
                // Use a builder for efficient merging of dictionaries
                var mergedTargetsBuilder = ImmutableDictionary.CreateBuilder<ISymbol, PotentialTargets>(SymbolEqualityComparer.Default);
                var keysProcessed = new HashSet<ISymbol>(SymbolEqualityComparer.Default); // Track keys already merged

                foreach (var state in states)
                {
                    // Merge basic impurity
                    if (state.HasPotentialImpurity)
                    {
                        mergedImpurity = true;
                        if (firstImpureNode == null) { firstImpureNode = state.FirstImpureSyntaxNode; }
                    }

                    // Merge delegate targets
                    foreach (var kvp in state.DelegateTargetMap)
                    {
                        var symbol = kvp.Key;
                        var currentTargets = kvp.Value;

                        if (mergedTargetsBuilder.TryGetValue(symbol, out var existingTargets))
                        {
                            // Key exists, merge the PotentialTargets (union of method symbols)
                            if (!keysProcessed.Contains(symbol)) // Avoid merging the same key multiple times if it appears in multiple input states
                            {
                                mergedTargetsBuilder[symbol] = PotentialTargets.Merge(existingTargets, currentTargets);
                                keysProcessed.Add(symbol); // Mark as processed for this merge operation
                            }
                        }
                        else
                        {
                            // Key doesn't exist yet, add it
                            mergedTargetsBuilder.Add(symbol, currentTargets);
                            keysProcessed.Add(symbol); // Mark as processed
                        }
                    }
                    keysProcessed.Clear(); // Reset for the next state in the enumeration
                }

                return new PurityAnalysisState(mergedImpurity, firstImpureNode, mergedTargetsBuilder.ToImmutable());
            }

            // --- Updated Equals ---
            public bool Equals(PurityAnalysisState other)
            {
                if (this.HasPotentialImpurity != other.HasPotentialImpurity ||
                    !object.Equals(this.FirstImpureSyntaxNode, other.FirstImpureSyntaxNode) ||
                    this.DelegateTargetMap.Count != other.DelegateTargetMap.Count) // Quick count check
                {
                    return false;
                }

                // Compare dictionaries element by element (immutable dictionaries handle this)
                // Note: This relies on PotentialTargets implementing Equals correctly.
                foreach (var kvp in this.DelegateTargetMap)
                {
                    if (!other.DelegateTargetMap.TryGetValue(kvp.Key, out var otherValue) || !kvp.Value.Equals(otherValue))
                    {
                        return false;
                    }
                }
                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is PurityAnalysisState other && Equals(other);
            }

            // --- Updated GetHashCode ---
            public override int GetHashCode()
            {
                // Combine hash codes of properties
                int hash = 17;
                hash = hash * 23 + HasPotentialImpurity.GetHashCode();
                hash = hash * 23 + (FirstImpureSyntaxNode?.GetHashCode() ?? 0);
                // Add hash code for the dictionary content
                foreach (var kvp in DelegateTargetMap.OrderBy(kv => kv.Key.Name)) // Order for consistency
                {
                    hash = hash * 23 + kvp.Key.GetHashCode();
                    hash = hash * 23 + kvp.Value.GetHashCode(); // Relies on PotentialTargets.GetHashCode()
                }
                return hash;
            }

            public static bool operator ==(PurityAnalysisState left, PurityAnalysisState right) => left.Equals(right);
            public static bool operator !=(PurityAnalysisState left, PurityAnalysisState right) => !(left == right);

            // --- Methods to update state (immutable pattern) ---
            public PurityAnalysisState WithImpurity(SyntaxNode node)
            {
                if (HasPotentialImpurity) return this; // Already impure
                return new PurityAnalysisState(true, node, this.DelegateTargetMap);
            }

            public PurityAnalysisState WithDelegateTarget(ISymbol delegateSymbol, PotentialTargets targets)
            {
                // Use SetItem which adds or replaces
                var newMap = this.DelegateTargetMap.SetItem(delegateSymbol, targets);
                return new PurityAnalysisState(this.HasPotentialImpurity, this.FirstImpureSyntaxNode, newMap);
            }
        }

        /// <summary>
        /// Helper struct to store potential targets for a delegate symbol.
        /// </summary>
        internal readonly struct PotentialTargets : IEquatable<PotentialTargets>
        {
            // Store the method symbols this delegate might point to.
            // Use ImmutableHashSet for efficient union operations and equality.
            public ImmutableHashSet<IMethodSymbol> MethodSymbols { get; }

            // Could add a pre-computed PurityStatus here later if needed

            public PotentialTargets(ImmutableHashSet<IMethodSymbol>? methodSymbols)
            {
                MethodSymbols = methodSymbols ?? ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default);
            }

            public static PotentialTargets Empty => new PotentialTargets(null);

            public static PotentialTargets FromSingle(IMethodSymbol methodSymbol)
            {
                if (methodSymbol == null) return Empty;
                return new PotentialTargets(ImmutableHashSet.Create<IMethodSymbol>(SymbolEqualityComparer.Default, methodSymbol));
            }

            // Merge by taking the union of method symbols
            public static PotentialTargets Merge(PotentialTargets first, PotentialTargets second)
            {
                return new PotentialTargets(first.MethodSymbols.Union(second.MethodSymbols));
            }

            public bool Equals(PotentialTargets other)
            {
                // ImmutableHashSet implements structural equality check efficiently
                return this.MethodSymbols.SetEquals(other.MethodSymbols);
            }

            public override bool Equals(object obj) => obj is PotentialTargets other && Equals(other);

            public override int GetHashCode()
            {
                int hash = 17;
                foreach (var symbol in MethodSymbols.OrderBy(s => s.Name)) // Order for consistency
                {
                    hash = hash * 23 + SymbolEqualityComparer.Default.GetHashCode(symbol);
                }
                return hash;
            }
        }

        /// <summary>
        /// Checks if a method symbol is considered pure based on its implementation using CFG data-flow analysis.
        /// Manages the visited set for cycle detection across the entire analysis.
        /// </summary>
        internal PurityAnalysisResult IsConsideredPure(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {
            // Check if already computed (shouldn't happen with instance-based, but safe)
            // REMOVED STATIC CACHE CHECK

            // Create NEW state for this specific analysis run
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var purityCache = new Dictionary<IMethodSymbol, PurityAnalysisResult>(SymbolEqualityComparer.Default);

            LogDebug($">> Enter DeterminePurity: {methodSymbol.ToDisplayString(_signatureFormat)}");

            // Call the internal static recursive method with the fresh state
            var result = DeterminePurityRecursiveInternal(
                methodSymbol,
                semanticModel,
                enforcePureAttributeSymbol,
                allowSynchronizationAttributeSymbol,
                visited, // Pass the newly created set
                purityCache // Pass the newly created cache
            );

            LogDebug($"<< Exit DeterminePurity ({GetPuritySource(result)}): {methodSymbol.ToDisplayString(_signatureFormat)}, Final IsPure={result.IsPure}");
            LogDebug($"-- Removed Walker for: {methodSymbol.ToDisplayString(_signatureFormat)}");

            // Add result to cache AFTER computation (though cache is local now)
            purityCache[methodSymbol] = result; // Cache result for this run

            return result;
        }

        // Helper to determine source string for logging
        private static string GetPuritySource(PurityAnalysisResult result)
        {
            // Simplified logic - refine if needed based on how results are determined
            if (result.IsPure) return "Assumed/Analyzed Pure";
            if (result.ImpureSyntaxNode != null) return "Analyzed Impure";
            // Could add more cases based on how different results are constructed if necessary
            return "Unknown/Default Impure";
        }

        /// <summary>
        /// Recursive helper for purity determination. Handles caching and cycle detection.
        /// </summary>
        internal static PurityAnalysisResult DeterminePurityRecursiveInternal(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {
            // +++ DETAILED LOGGING +++
            var indent = new string(' ', visited.Count * 2);
            LogDebug($"{indent}>> Enter DeterminePurity: {methodSymbol.ToDisplayString()}");
            // --- END LOGGING ---

            // --- 1. Check Cache --- 
            if (purityCache.TryGetValue(methodSymbol, out var cachedResult))
            {
                LogDebug($"{indent}  Purity CACHED: {cachedResult.IsPure} for {methodSymbol.ToDisplayString()}");
                LogDebug($"{indent}<< Exit DeterminePurity (Cached): {methodSymbol.ToDisplayString()}");
                return cachedResult;
            }

            // --- 2. Detect Recursion --- 
            if (!visited.Add(methodSymbol))
            {
                LogDebug($"{indent}  Recursion DETECTED for {methodSymbol.ToDisplayString()}. Assuming impure for this path.");
                purityCache[methodSymbol] = PurityAnalysisResult.ImpureUnknownLocation;
                LogDebug($"{indent}<< Exit DeterminePurity (Recursion): {methodSymbol.ToDisplayString()}");
                return PurityAnalysisResult.ImpureUnknownLocation;
            }

            try // Use try/finally to ensure visited.Remove is always called
            {
                // --- 4. Known Impure BCL Members --- (Priority 2)
                if (IsKnownImpure(methodSymbol))
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} is known impure.");
                    var knownImpureResult = ImpureResult(null); // Or find syntax if possible
                    purityCache[methodSymbol] = knownImpureResult;
                    LogDebug($"{indent}<< Exit DeterminePurity (Known Impure): {methodSymbol.ToDisplayString()}");
                    return knownImpureResult;
                }

                // --- 5. Known Pure BCL Members --- (Priority 3)
                if (IsKnownPureBCLMember(methodSymbol))
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} is known pure BCL member.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    LogDebug($"{indent}<< Exit DeterminePurity (Known Pure): {methodSymbol.ToDisplayString()}");
                    return PurityAnalysisResult.Pure;
                }

                // --- GET BODY NODE EARLIER --- 
                SyntaxNode? bodySyntaxNode = GetBodySyntaxNode(methodSymbol, default); // Get body node once

                // --- ADDED: Check for ref returns --- (Priority 5)
                if (methodSymbol.ReturnsByRef)
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} returns by ref. IMPURE.");
                    // Find syntax node for diagnostic. Use return type syntax if possible.
                    SyntaxNode? locationSyntax = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.DescendantNodesAndSelf()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.RefTypeSyntax>()
                        .FirstOrDefault();
                    // Fallback to method identifier if RefTypeSyntax not found
                    locationSyntax ??= methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.DescendantNodesAndSelf()
                                            .FirstOrDefault(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax ins && ins.Identifier.ValueText == methodSymbol.Name)
                                            ?.Parent; // Get parent (e.g., MethodDeclarationSyntax) for better span

                    purityCache[methodSymbol] = ImpureResult(locationSyntax ?? bodySyntaxNode); // Use body as final fallback
                    LogDebug($"{indent}<< Exit DeterminePurity (ReturnsByRef): {methodSymbol.ToDisplayString()}"); // Log exit
                    return purityCache[methodSymbol];
                }

                // --- 6. Handle Abstract/External/Missing Body for Unknown Methods --- (Priority 6 - Renumbered)
                // SyntaxNode? bodySyntaxNode = GetBodySyntaxNode(methodSymbol, default); // MOVED UP
                if (methodSymbol.IsAbstract || methodSymbol.IsExtern || bodySyntaxNode == null)
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} is abstract, extern, or has no body AND not known impure/pure. Assuming pure.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    LogDebug($"{indent}<< Exit DeterminePurity (Abstract/Extern/NoBody): {methodSymbol.ToDisplayString()}"); // Log exit
                    return PurityAnalysisResult.Pure;
                }

                // --- 7. Analyze Body using CFG --- (Fallback for methods with bodies)
                PurityAnalysisResult result = PurityAnalysisResult.Pure; // Assume pure until proven otherwise by CFG
                if (bodySyntaxNode != null)
                {
                    LogDebug($"{indent}Analyzing body of {methodSymbol.ToDisplayString()} using CFG.");
                    result = AnalyzePurityUsingCFGInternal(
                        bodySyntaxNode,
                        semanticModel,
                        enforcePureAttributeSymbol,
                        allowSynchronizationAttributeSymbol,
                        visited,
                        methodSymbol,
                        purityCache);
                    // +++ DETAILED LOGGING +++
                    LogDebug($"{indent}  CFG Analysis Result for {methodSymbol.ToDisplayString()}: IsPure={result.IsPure}, ImpureNode={result.ImpureSyntaxNode?.Kind()}");
                    // --- END LOGGING ---
                }

                IOperation? methodBodyIOperation = null;
                if (bodySyntaxNode != null)
                {
                    try
                    {
                        methodBodyIOperation = semanticModel.GetOperation(bodySyntaxNode, default);
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"{indent}  Post-CFG: Error getting IOperation for method body: {ex.Message}");
                        methodBodyIOperation = null;
                    }
                }

                // --- Post-CFG Checks --- Only if CFG result was pure
                if (result.IsPure)
                {
                    LogDebug($"{indent}Post-CFG: CFG Result was Pure. Performing Post-CFG checks for {methodSymbol.ToDisplayString()}.");

                    if (methodBodyIOperation != null)
                    {
                        var pureAttrSymbolForContext = semanticModel.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName); // Define pureAttrSymbol here
                        var postCfgContext = new Rules.PurityAnalysisContext(
                            semanticModel,
                            enforcePureAttributeSymbol,
                            pureAttrSymbolForContext, // Pass the fetched symbol
                            allowSynchronizationAttributeSymbol,
                            visited,
                            purityCache,
                            methodSymbol,
                            _purityRules,
                            CancellationToken.None);

                        // Check Returns
                        LogDebug($"{indent}  Post-CFG: Checking ReturnOperations...");
                        foreach (var returnOp in methodBodyIOperation.DescendantsAndSelf().OfType<IReturnOperation>())
                        {
                            if (returnOp.ReturnedValue != null)
                            {
                                // Pass PurityAnalysisState.Pure here for isolated check
                                var returnPurity = CheckSingleOperation(returnOp.ReturnedValue, postCfgContext, PurityAnalysisState.Pure);
                                if (!returnPurity.IsPure)
                                {
                                    LogDebug($"{indent}    Post-CFG: Return value IMPURE: {returnOp.ReturnedValue.Syntax}");
                                    result = returnPurity;
                                    goto PostCfgChecksDone; // Exit checks early
                                }
                            }
                        }
                        LogDebug($"{indent}  Post-CFG: ReturnOperations check complete (result still pure).");

                        // Check Throws
                        LogDebug($"{indent}  Post-CFG: Checking ThrowOperations...");
                        var firstThrowOp = methodBodyIOperation.DescendantsAndSelf().OfType<IThrowOperation>().FirstOrDefault();
                        if (firstThrowOp != null)
                        {
                            LogDebug($"{indent}    Post-CFG: Found ThrowOperation IMPURE: {firstThrowOp.Syntax}");
                            result = PurityAnalysisResult.Impure(firstThrowOp.Syntax);
                            goto PostCfgChecksDone; // Exit checks early
                        }
                        LogDebug($"{indent}  Post-CFG: ThrowOperations check complete (result still pure).");

                        // Check Known Impure Invocations
                        LogDebug($"{indent}  Post-CFG: Checking Known Impure Invocations...");
                        foreach (var invocationOp in methodBodyIOperation.DescendantsAndSelf().OfType<IInvocationOperation>())
                        {
                            if (invocationOp.TargetMethod != null && IsKnownImpure(invocationOp.TargetMethod.OriginalDefinition))
                            {
                                LogDebug($"{indent}    Post-CFG: Found Known Impure Invocation IMPURE: {invocationOp.Syntax} calling {invocationOp.TargetMethod.ToDisplayString()}");
                                result = PurityAnalysisResult.Impure(invocationOp.Syntax);
                                goto PostCfgChecksDone; // Exit checks early
                            }
                        }
                        LogDebug($"{indent}  Post-CFG: Known Impure Invocations check complete (result still pure).");

                        // Check Checked Operations
                        LogDebug($"{indent}  Post-CFG: Checking Checked Operations...");
                        foreach (var operation in methodBodyIOperation.DescendantsAndSelf())
                        {
                            bool isChecked = false;
                            IMethodSymbol? operatorMethod = null;

                            if (operation is IBinaryOperation binaryOp && binaryOp.IsChecked)
                            {
                                isChecked = true;
                                operatorMethod = binaryOp.OperatorMethod;
                            }
                            else if (operation is IUnaryOperation unaryOp && unaryOp.IsChecked)
                            {
                                isChecked = true;
                                operatorMethod = unaryOp.OperatorMethod;
                            }

                            if (isChecked && operatorMethod != null)
                            {
                                LogDebug($"{indent}    Post-CFG: Found Checked Operation: {operation.Syntax} with operator method {operatorMethod.Name}");
                                var operatorPurity = DeterminePurityRecursiveInternal(
                                    operatorMethod.OriginalDefinition,
                                    semanticModel,
                                    enforcePureAttributeSymbol,
                                    allowSynchronizationAttributeSymbol,
                                    visited,
                                    purityCache);

                                if (!operatorPurity.IsPure)
                                {
                                    LogDebug($"{indent}    Post-CFG: Checked operator method '{operatorMethod.Name}' is IMPURE. Operation is Impure.");
                                    result = PurityAnalysisResult.Impure(operation.Syntax);
                                    goto PostCfgChecksDone; // Exit checks early
                                }
                            }
                        }
                        LogDebug($"{indent}  Post-CFG: Checked Operations check complete (result still pure).");
                    }
                    else
                    {
                        LogDebug($"{indent}Post-CFG: methodBodyIOperation was null, skipping post-CFG checks.");
                    }
                }

            PostCfgChecksDone:;
                // --- END Post-CFG Checks ---

                purityCache[methodSymbol] = result;
                LogDebug($"{indent}<< Exit DeterminePurity (Analyzed): {methodSymbol.ToDisplayString()}, Final IsPure={result.IsPure}"); // Log exit
                return result;
            }
            finally
            {
                visited.Remove(methodSymbol);
                LogDebug($"{indent}-- Removed Walker for: {methodSymbol.ToDisplayString()}");
            }
        }

        /// <summary>
        /// Performs the actual purity analysis using the Control Flow Graph.
        /// </summary>
        private static PurityAnalysisResult AnalyzePurityUsingCFGInternal(
            SyntaxNode bodyNode,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {
            ControlFlowGraph? cfg = null;
            try
            {
                cfg = ControlFlowGraph.Create(bodyNode, semanticModel);
                LogDebug($"CFG created successfully for node: {bodyNode.Kind()}");
            }
            catch (Exception ex)
            {
                LogDebug($"Error creating ControlFlowGraph for {containingMethodSymbol.ToDisplayString()}: {ex.Message}. Assuming impure.");
                return PurityAnalysisResult.Impure(bodyNode); // If CFG fails, assume impure
            }

            if (cfg == null || cfg.Blocks.IsEmpty)
            {
                LogDebug($"CFG is null or empty for {containingMethodSymbol.ToDisplayString()}. Assuming pure (no operations).");
                return PurityAnalysisResult.Pure; // Empty CFG means no operations, hence pure
            }

            // +++ Log CFG Block Count +++
            LogDebug($"  [CFG] Created CFG with {cfg.Blocks.Length} blocks for {containingMethodSymbol.ToDisplayString()}.");

            // --- Dataflow Analysis Setup ---
            var blockStates = new Dictionary<BasicBlock, PurityAnalysisState>(cfg.Blocks.Length);
            var worklist = new Queue<BasicBlock>();

            // Initialize: Assume all blocks start pure, add entry block to worklist
            LogDebug("  [CFG] Initializing CFG block states to Pure.");
            foreach (var block in cfg.Blocks)
            {
                blockStates[block] = PurityAnalysisState.Pure;
            }
            if (cfg.Blocks.Any()) // Use Any() for safety, though checked IsEmpty above
            {
                var entryBlock = cfg.Blocks.First();
                // +++ Log initial worklist add +++
                LogDebug($"  [CFG] Adding Entry Block #{entryBlock.Ordinal} to worklist.");
                worklist.Enqueue(entryBlock); // Use First() for entry block
            }
            else
            {
                LogDebug("  [CFG] CFG has no blocks. Exiting analysis.");
                return PurityAnalysisResult.Pure; // No blocks = pure
            }

            // --- Dataflow Analysis Loop ---
            LogDebug("  [CFG] Starting CFG dataflow analysis worklist loop.");
            int loopIterations = 0; // Add iteration counter for safety
            // +++ Log right before the loop condition check +++
            LogDebug($"  [CFG] BEFORE WHILE CHECK: worklist.Count = {worklist.Count}, loopIterations = {loopIterations}");
            while (worklist.Count > 0 && loopIterations < cfg.Blocks.Length * 5) // Add loop limit
            {
                // +++ Log immediately inside the loop +++
                LogDebug("  [CFG] ENTERED WHILE LOOP.");
                loopIterations++;
                // +++ Log worklist count and dequeued block +++
                LogDebug($"  [CFG] Worklist count: {worklist.Count}. Iteration: {loopIterations}");
                var currentBlock = worklist.Dequeue();
                LogDebug($"  [CFG] Processing CFG Block #{currentBlock.Ordinal}");

                var stateBefore = blockStates[currentBlock];

                LogDebug($"  [CFG] StateBefore for Block #{currentBlock.Ordinal}: Impure={stateBefore.HasPotentialImpurity}");

                // Apply transfer function to get state after this block's operations
                var stateAfter = ApplyTransferFunction(
                    currentBlock,
                    stateBefore,
                    semanticModel,
                    enforcePureAttributeSymbol,
                    allowSynchronizationAttributeSymbol,
                    visited,
                    containingMethodSymbol,
                    purityCache);

                LogDebug($"  [CFG] State after Block #{currentBlock.Ordinal}: Impure={stateAfter.HasPotentialImpurity}");

                // --- FIX: Always propagate the calculated stateAfter to successors ---
                // The PropagateToSuccessor method will handle whether the successor needs enqueuing.
                LogDebug($"  [CFG] Propagating stateAfter (Impure={stateAfter.HasPotentialImpurity}) to successors of Block #{currentBlock.Ordinal}.");
                PropagateToSuccessor(currentBlock.ConditionalSuccessor?.Destination, stateAfter, blockStates, worklist);
                PropagateToSuccessor(currentBlock.FallThroughSuccessor?.Destination, stateAfter, blockStates, worklist);
                // --- END FIX ---
            }
            // +++ Log loop termination reason +++
            if (worklist.Count == 0)
            {
                LogDebug("  [CFG] Finished CFG dataflow analysis worklist loop (worklist empty).");
            }
            else
            {
                LogDebug($"  [CFG] WARNING: Exited CFG dataflow loop due to iteration limit ({loopIterations}). Potential infinite loop?");
            }

            // --- Aggregate Result ---
            PurityAnalysisResult finalResult;
            BasicBlock? exitBlock = cfg.Blocks.LastOrDefault(b => b.Kind == BasicBlockKind.Exit); // Ensure we get the actual Exit block

            if (exitBlock != null && blockStates.TryGetValue(exitBlock, out var exitState))
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Exit Block #{exitBlock.Ordinal} Initial State: HasImpurity={exitState.HasPotentialImpurity}, Node={exitState.FirstImpureSyntaxNode?.Kind()}, NodeText='{exitState.FirstImpureSyntaxNode?.ToString()}'");

                // --- FIX: Explicitly check operations in the exit block if state is currently pure ---
                if (!exitState.HasPotentialImpurity)
                {
                    LogDebug($"  [CFG] Exit block state is pure. Explicitly checking operations within Exit Block #{exitBlock.Ordinal}.");
                    var pureAttrSymbolForContext = semanticModel.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);

                    var ruleContext = new Rules.PurityAnalysisContext(
                        semanticModel,
                        enforcePureAttributeSymbol,
                        pureAttrSymbolForContext,
                        allowSynchronizationAttributeSymbol,
                        visited, // Note: visited might be incomplete here, but ok for stateless rules
                        purityCache,
                        containingMethodSymbol,
                        _purityRules,
                        CancellationToken.None);

                    foreach (var exitOp in exitBlock.Operations)
                    {
                        if (exitOp == null) continue;
                        LogDebug($"    [CFG] Checking exit operation: {exitOp.Kind} - '{exitOp.Syntax}'");
                        var opResult = CheckSingleOperation(exitOp, ruleContext, exitState); // Added exitState argument
                        if (!opResult.IsPure)
                        {
                            LogDebug($"    [CFG] Exit operation {exitOp.Kind} found IMPURE. Updating final result.");
                            // Update exitState for the final result calculation below
                            // Ensure the node from opResult is used
                            exitState = exitState.WithImpurity(opResult.ImpureSyntaxNode ?? exitOp.Syntax); // Use WithImpurity method
                            break; // Found impurity, no need to check other exit operations
                        }
                    }
                    if (!exitState.HasPotentialImpurity)
                    {
                        LogDebug($"  [CFG] All exit block operations checked and found pure.");
                    }
                }
                // --- END FIX ---

                // Use the potentially updated exitState to determine the final result
                if (exitState.HasPotentialImpurity)
                {
                    // If impure, use the identified node. If node is null, return ImpureUnknownLocation.
                    finalResult = exitState.FirstImpureSyntaxNode != null
                        ? PurityAnalysisResult.Impure(exitState.FirstImpureSyntaxNode)
                        : PurityAnalysisResult.ImpureUnknownLocation;
                    LogDebug($"  [CFG] Final Result: IMPURE. Node={finalResult.ImpureSyntaxNode?.Kind() ?? (object)"NULL"}"); // Log node kind or NULL
                }
                else
                {
                    finalResult = PurityAnalysisResult.Pure;
                    LogDebug($"  [CFG] Final Result: PURE.");
                }
            }
            else if (exitBlock != null) // Has exit block, but state not found?
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Could not get state for the exit block #{exitBlock.Ordinal}. Assuming impure (unreachable?).");
                finalResult = PurityAnalysisResult.ImpureUnknownLocation; // Use Unknown instead of Impure(bodyNode)
            }
            else // No blocks / no exit block found
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: No reachable exit block found. Assuming pure.");
                finalResult = PurityAnalysisResult.Pure; // No operations executed on path to exit
            }

            return finalResult;
        }

        /// <summary>
        /// Applies the transfer function for a single basic block in the CFG.
        /// Determines the purity state after executing the operations in the block.
        /// </summary>
        private static PurityAnalysisState ApplyTransferFunction(
            BasicBlock block,
            PurityAnalysisState stateBefore,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {
            LogDebug($"ApplyTransferFunction START for Block #{block.Ordinal} - Initial State: Impure={stateBefore.HasPotentialImpurity}");

            if (stateBefore.HasPotentialImpurity)
            {
                LogDebug($"ApplyTransferFunction SKIP for Block #{block.Ordinal} - Already impure.");
                return stateBefore;
            }

            // Create context ONCE per block (no state needed in context)
            var pureAttributeSymbol_block = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");
            var ruleContext = new Rules.PurityAnalysisContext(
                semanticModel,
                enforcePureAttributeSymbol,
                pureAttributeSymbol_block,
                allowSynchronizationAttributeSymbol,
                visited,
                purityCache,
                containingMethodSymbol,
                _purityRules,
                CancellationToken.None);

            // Iterate through operations and check purity
            var currentStateInBlock = stateBefore; // Track state within the block
            foreach (var op in block.Operations)
            {
                if (op == null) continue;

                LogDebug($"    [ATF Block {block.Ordinal}] Checking Op Kind: {op.Kind}, Syntax: {op.Syntax.ToString().Replace("\r\n", " ").Replace("\n", " ")}");

                // MODIFIED: Pass the currentStateInBlock to CheckSingleOperation
                var opResult = CheckSingleOperation(op, ruleContext, currentStateInBlock);

                if (!opResult.IsPure)
                {
                    LogDebug($"ApplyTransferFunction IMPURE DETECTED in Block #{block.Ordinal} by Op: {op.Kind} ({op.Syntax})");
                    // Update the state for the *rest* of the block (though loop breaks)
                    currentStateInBlock = currentStateInBlock.WithImpurity(opResult.ImpureSyntaxNode ?? op.Syntax);
                    break; // Stop checking this block once impurity is found
                }

                // --- *** UPDATE Delegate Target Map using Helper *** ---
                LogDebug($"  [ApplyTF] Before UpdateDelegateMapForOperation: StateImpure={currentStateInBlock.HasPotentialImpurity}, MapCount={currentStateInBlock.DelegateTargetMap.Count}"); // *** ADDED LOG ***
                currentStateInBlock = UpdateDelegateMapForOperation(op, ruleContext, currentStateInBlock);
                LogDebug($"  [ApplyTF] After UpdateDelegateMapForOperation: StateImpure={currentStateInBlock.HasPotentialImpurity}, MapCount={currentStateInBlock.DelegateTargetMap.Count}"); // *** ADDED LOG ***
                // --- *** END UPDATE *** ---
            }

            LogDebug($"ApplyTransferFunction END for Block #{block.Ordinal} - Final State: Impure={currentStateInBlock.HasPotentialImpurity}");
            return currentStateInBlock; // Return the final state for this block
        }

        /// <summary>
        /// Checks the purity of a single IOperation using the registered purity rules.
        /// NOTE: This currently DOES NOT use the new DelegateTargetMap state.
        /// </summary>
        internal static PurityAnalysisResult CheckSingleOperation(IOperation operation, Rules.PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            LogDebug($"    [CSO] Enter CheckSingleOperation for Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");
            LogDebug($"    [CSO] Current DFA State: Impure={currentState.HasPotentialImpurity}, MapCount={currentState.DelegateTargetMap.Count}");

            // Explicitly handle FlowCaptureReference and FlowCapture as pure.
            // These represent compiler-generated temporaries and should not affect purity.
            if (operation.Kind == OperationKind.FlowCaptureReference || operation.Kind == OperationKind.FlowCapture)
            {
                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure - FlowCapture/Reference)");
                return PurityAnalysisResult.Pure;
            }

            // Check for checked operations first
            bool isChecked = false;
            IMethodSymbol? operatorMethod = null;

            if (operation is IBinaryOperation binaryOp && binaryOp.IsChecked)
            {
                LogDebug($"    [CSO] Found Checked Binary Operation: {operation.Syntax}");
                isChecked = true;
                operatorMethod = binaryOp.OperatorMethod;

                // Check operands first
                var leftResult = CheckSingleOperation(binaryOp.LeftOperand, context, currentState);
                if (!leftResult.IsPure)
                {
                    LogDebug($"    [CSO] Left operand of checked operation is Impure: {binaryOp.LeftOperand.Syntax}");
                    return leftResult;
                }

                var rightResult = CheckSingleOperation(binaryOp.RightOperand, context, currentState);
                if (!rightResult.IsPure)
                {
                    LogDebug($"    [CSO] Right operand of checked operation is Impure: {binaryOp.RightOperand.Syntax}");
                    return rightResult;
                }
            }
            else if (operation is IUnaryOperation unaryOp && unaryOp.IsChecked)
            {
                LogDebug($"    [CSO] Found Checked Unary Operation: {operation.Syntax}");
                isChecked = true;
                operatorMethod = unaryOp.OperatorMethod;

                // Check operand first
                var operandResult = CheckSingleOperation(unaryOp.Operand, context, currentState);
                if (!operandResult.IsPure)
                {
                    LogDebug($"    [CSO] Operand of checked operation is Impure: {unaryOp.Operand.Syntax}");
                    return operandResult;
                }
            }

            if (isChecked)
            {
                LogDebug($"    [CSO] Processing checked operation: {operation.Syntax}");

                // If there's a user-defined operator method, check its purity
                if (operatorMethod != null)
                {
                    // First, check if the operator method is already in the cache
                    if (context.PurityCache.TryGetValue(operatorMethod.OriginalDefinition, out var cachedResult))
                    {
                        if (!cachedResult.IsPure)
                        {
                            LogDebug($"    [CSO] Checked operator method '{operatorMethod.Name}' is IMPURE (cached). Operation is Impure.");
                            return PurityAnalysisResult.Impure(operation.Syntax);
                        }
                        LogDebug($"    [CSO] Checked operator method '{operatorMethod.Name}' is Pure (cached).");
                        return PurityAnalysisResult.Pure;
                    }

                    // If not in cache, check if it's a known pure/impure method
                    if (IsKnownPureBCLMember(operatorMethod))
                    {
                        LogDebug($"    [CSO] Checked operator method '{operatorMethod.Name}' is known pure BCL member.");
                        return PurityAnalysisResult.Pure;
                    }

                    if (IsKnownImpure(operatorMethod))
                    {
                        LogDebug($"    [CSO] Checked operator method '{operatorMethod.Name}' is known impure. Operation is Impure.");
                        return PurityAnalysisResult.Impure(operation.Syntax);
                    }

                    // If not known, analyze the operator method recursively
                    var operatorPurity = DeterminePurityRecursiveInternal(
                        operatorMethod.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    if (!operatorPurity.IsPure)
                    {
                        LogDebug($"    [CSO] Checked operator method '{operatorMethod.Name}' is IMPURE. Operation is Impure.");
                        return PurityAnalysisResult.Impure(operation.Syntax);
                    }

                    LogDebug($"    [CSO] Checked operator method '{operatorMethod.Name}' is Pure.");
                }

                // Check if this operation is part of a method marked with [EnforcePure]
                if (context.ContainingMethodSymbol != null &&
                    IsPureEnforced(context.ContainingMethodSymbol, context.EnforcePureAttributeSymbol))
                {
                    LogDebug($"    [CSO] Checked operation is part of a method marked with [EnforcePure]. Checking purity of containing method.");

                    // Check the containing method's purity
                    var containingMethodPurity = DeterminePurityRecursiveInternal(
                        context.ContainingMethodSymbol.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    if (!containingMethodPurity.IsPure)
                    {
                        LogDebug($"    [CSO] Containing method is IMPURE. Operation is Impure.");
                        return PurityAnalysisResult.Impure(operation.Syntax);
                    }
                }

                // If we get here, either there was no user-defined operator or it was pure
                LogDebug($"    [CSO] Checked operation is Pure.");
                return PurityAnalysisResult.Pure;
            }

            // Find the first applicable rule from the list (existing logic)
            var applicableRule = _purityRules.FirstOrDefault(rule => rule.ApplicableOperationKinds.Contains(operation.Kind));

            if (applicableRule != null)
            {
                // +++ Log Rule Application +++
                LogDebug($"    [CSO] Applying Rule '{applicableRule.GetType().Name}' to Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");
                // MODIFIED: Pass currentState to rule
                var ruleResult = applicableRule.CheckPurity(operation, context, currentState);
                // +++ Log Rule Result +++
                LogDebug($"    [CSO] Rule '{applicableRule.GetType().Name}' Result: IsPure={ruleResult.IsPure}");
                if (!ruleResult.IsPure)
                {
                    // *** NEW CHECK ***: If rule resulted in impurity but lost the node, use the current operation's node.
                    if (ruleResult.ImpureSyntaxNode == null)
                    {
                        LogDebug($"    [CSO] Rule '{applicableRule.GetType().Name}' returned impure result without syntax node. Using current operation syntax: {operation.Syntax}");
                        // Ensure we have a non-null syntax node before calling PurityAnalysisResult.Impure
                        return operation.Syntax != null
                               ? PurityAnalysisResult.Impure(operation.Syntax)
                               : PurityAnalysisResult.ImpureUnknownLocation; // Fallback if even current op syntax is null
                    }
                    LogDebug($"    [CSO] Exit CheckSingleOperation (Impure from rule)");
                    return ruleResult; // Return the original impure result (with node)
                }
                // Rule handled it and found it pure, stop checking this op
                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure from rule)");
                return PurityAnalysisResult.Pure;
            }
            else
            {
                // Default assumption: If no rule handles it, assume impure for safety.
                LogDebug($"    [CSO] No rule found for operation kind {operation.Kind}. Defaulting to impure. Syntax: '{operation.Syntax.ToString().Trim()}'");
                LogDebug($"    [CSO] Exit CheckSingleOperation (Impure default)");
                return ImpureResult(operation.Syntax); // Restore OLD BEHAVIOR
            }
        }

        // ========================================================================
        // Helper Methods (made internal or added)
        // ========================================================================

        /// <summary>
        /// Checks if a symbol (method, property) corresponds to a known BCL member considered pure.
        /// </summary>
        internal static bool IsKnownPureBCLMember(ISymbol symbol)
        {
            if (symbol == null) return false;

            // 1. Check specific immutable collection methods/properties by name/type
            if (symbol.ContainingType?.ContainingNamespace?.ToString().StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true)
            {
                // Assume most operations on immutable types are pure (reading properties, common methods)
                // Be slightly more specific for factory methods
                if (symbol.Name.Contains("Create") || symbol.Name.Contains("Add") || symbol.Name.Contains("Set") || symbol.Name.Contains("Remove"))
                {
                    // Factory/mutation methods on the *static* class (like ImmutableList.Create) are pure.
                    // Instance methods like Add/SetItem return *new* collections and are pure reads of the original.
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable member: {symbol.ToDisplayString()}");
                    return true;
                }
                // Check common read properties
                if (symbol.Kind == SymbolKind.Property && (symbol.Name == "Count" || symbol.Name == "Length" || symbol.Name == "IsEmpty"))
                {
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable property: {symbol.ToDisplayString()}");
                    return true;
                }
                // Check common read methods
                if (symbol.Kind == SymbolKind.Method && (symbol.Name == "Contains" || symbol.Name == "IndexOf" || symbol.Name == "TryGetValue"))
                {
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable method: {symbol.ToDisplayString()}");
                    return true;
                }
            }

            // 2. Check against the known pure list using the original definition's display string
            string signature = symbol.OriginalDefinition.ToDisplayString();

            // *** FIX: Append .get for Property Symbols before HashSet check ***
            if (symbol.Kind == SymbolKind.Property)
            {
                // We assume checks in this helper are for *reading* the property.
                // Append ".get" to match the HashSet entries for property getters.
                if (!signature.EndsWith(".get") && !signature.EndsWith(".set")) // Avoid double appending
                {
                    signature += ".get";
                    PurityAnalysisEngine.LogDebug($"    [IsKnownPure] Appended .get to property signature: \"{signature}\"");
                }
            }

            // +++ Add detailed logging before Contains check +++
            PurityAnalysisEngine.LogDebug($"    [IsKnownPure] Checking HashSet.Contains for signature: \"{signature}\"");
            bool isKnownPure = Constants.KnownPureBCLMembers.Contains(signature);
            // +++ Log the result of Contains +++
            PurityAnalysisEngine.LogDebug($"    [IsKnownPure] HashSet.Contains result: {isKnownPure}");

            // Handle common generic cases (e.g., List<T>.Count) more robustly if direct match fails
            if (!isKnownPure && symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                signature = methodSymbol.ConstructedFrom.ToDisplayString();
                isKnownPure = Constants.KnownPureBCLMembers.Contains(signature);
            }
            else if (!isKnownPure && symbol is IPropertySymbol propertySymbol && propertySymbol.ContainingType.IsGenericType)
            {
                // Check property on constructed generic type vs definition
                // Example: "System.Collections.Generic.List<T>.Count.get"
                // Special handling for indexers
                if (propertySymbol.IsIndexer)
                {
                    // Construct signature like "Namespace.Type<T>.this[params].get"
                    // Note: Getting exact parameter types for signature matching can be complex.
                    // For now, rely on the OriginalDefinition check first, which might handle it.
                    // If OriginalDefinition check fails, this specific generic check might still fail for indexers
                    // without more precise parameter type matching.
                    // Let's try matching the original definition string first for indexers.
                    signature = propertySymbol.OriginalDefinition.ToDisplayString(); // Use original definition string

                }
                else
                {
                    signature = $"{propertySymbol.ContainingType.ConstructedFrom.ToDisplayString()}.{propertySymbol.Name}.get"; // Assuming 'get' suffix
                }
                isKnownPure = Constants.KnownPureBCLMembers.Contains(signature);
            }


            if (isKnownPure)
            {
                LogDebug($"Helper IsKnownPureBCLMember: Match found for {symbol.ToDisplayString()} using signature '{signature}'");
            }
            else
            {
                // Fallback: Check if it's in System.Math as most Math methods are pure
                // This is a broad check; KnownPureBCLMembers is preferred for specifics
                if (symbol.ContainingNamespace?.ToString().Equals("System", StringComparison.Ordinal) == true &&
                    symbol.ContainingType?.Name.Equals("Math", StringComparison.Ordinal) == true)
                {
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Math member: {symbol.ToDisplayString()}");
                    isKnownPure = true; // Treat all System.Math as pure for now
                }
            }

            return isKnownPure;
        }

        /// <summary>
        /// Checks if a symbol (method, property) corresponds to a known member considered IMPURE.
        /// </summary>
        internal static bool IsKnownImpure(ISymbol symbol)
        {
            if (symbol == null) return false;
            // Check method/property signature against known impure list

            // +++ Log both original and constructed signatures +++
            string constructedSignature = symbol.ToDisplayString();
            string originalSignature = symbol.OriginalDefinition.ToDisplayString();
            LogDebug($"    [IsKnownImpure] Checking Symbol: '{constructedSignature}'");
            LogDebug($"    [IsKnownImpure] Checking Original Definition: '{originalSignature}'");
            // +++ End Logging +++

            string signature = symbol.OriginalDefinition.ToDisplayString();

            // *** FIX: Append .get for Property Symbols before HashSet check ***
            if (symbol.Kind == SymbolKind.Property)
            {
                // We assume checks in this helper are for *reading* the property.
                // Append ".get" to match the HashSet entries for property getters.
                if (!signature.EndsWith(".get") && !signature.EndsWith(".set")) // Avoid double appending
                {
                    signature += ".get";
                    PurityAnalysisEngine.LogDebug($"    [IsKnownImpure] Appended .get to property signature: \"{signature}\"");
                }
            }

            // +++ Add Logging: Show signature being checked +++
            LogDebug($"    [IsKnownImpure] Checking HashSet.Contains for signature: \"{signature}\"");

            // --- Check 1: Full Signature --- 
            if (Constants.KnownImpureMethods.Contains(signature))
            {
                LogDebug($"Helper IsKnownImpure: Match found for {symbol.ToDisplayString()} using full signature '{signature}'");
                return true;
            }

            // --- Check 2: Simplified Name (Fallback) --- 
            if (symbol.ContainingType != null)
            {
                string simplifiedName = $"{symbol.ContainingType.Name}.{symbol.Name}";
                LogDebug($"    [IsKnownImpure] Checking HashSet.Contains for simplified name: \"{simplifiedName}\"");
                if (Constants.KnownImpureMethods.Contains(simplifiedName))
                {
                    LogDebug($"Helper IsKnownImpure: Match found for {symbol.ToDisplayString()} using simplified name '{simplifiedName}'");
                    return true;
                }
            }

            // --- Check 3: Handle generic methods if needed (e.g., Interlocked.CompareExchange<T>) ---
            if (symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                signature = methodSymbol.ConstructedFrom.ToDisplayString();
                if (Constants.KnownImpureMethods.Contains(signature))
                {
                    LogDebug($"Helper IsKnownImpure: Generic match found for {symbol.ToDisplayString()} using signature '{signature}'");
                    return true;
                }
            }

            // Additional check: Property access on known impure types (e.g., DateTime.Now)
            if (symbol is IPropertySymbol property && IsInImpureNamespaceOrType(property.ContainingType)) // Check containing type too
            {
                // We might have specific properties listed in KnownImpureMethods (like DateTime.Now.get)
                // This is a fallback if the type itself is generally impure.
                LogDebug($"Helper IsKnownImpure: Property access {symbol.ToDisplayString()} on known impure type {property.ContainingType.ToDisplayString()}.");
                // return true; // Be careful: A type might have *some* pure properties. Rely on KnownImpureMethods first.
            }

            // Check if the method is an Interlocked operation (often requires special handling)
            if (symbol.ContainingType?.ToString().Equals("System.Threading.Interlocked", StringComparison.Ordinal) ?? false)
            {
                LogDebug($"Helper IsKnownImpure: Member {symbol.ToDisplayString()} belongs to System.Threading.Interlocked.");
                return true; // All Interlocked methods are treated as impure
            }

            // ADDED: Check for System.Threading.Volatile methods
            if (symbol.ContainingType?.ToString().Equals("System.Threading.Volatile", StringComparison.Ordinal) ?? false)
            {
                LogDebug($"Helper IsKnownImpure: Member {symbol.ToDisplayString()} belongs to System.Threading.Volatile and is considered impure.");
                return true; // All Volatile methods (Read/Write) are impure due to memory barriers
            }

            return false;
        }


        /// <summary>
        /// Checks if the symbol belongs to a namespace or type known to be generally impure.
        /// </summary>
        internal static bool IsInImpureNamespaceOrType(ISymbol symbol)
        {
            if (symbol == null) return false;

            PurityAnalysisEngine.LogDebug($"    [INOT] Checking symbol: {symbol.ToDisplayString()}");

            // Check the containing type first
            INamedTypeSymbol? containingType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            while (containingType != null)
            {
                // *** Key Check 1: Type Name ***
                string typeName = containingType.OriginalDefinition.ToDisplayString(); // Get the fully qualified name
                PurityAnalysisEngine.LogDebug($"    [INOT] Checking type: {typeName}"); // Log the exact string
                PurityAnalysisEngine.LogDebug($"    [INOT] Comparing '{typeName}' against KnownImpureTypeNames..."); // Log before comparison
                if (Constants.KnownImpureTypeNames.Contains(typeName)) // Compare against the known impure type list
                {
                    PurityAnalysisEngine.LogDebug($"    [INOT] --> Match found for impure type: {typeName}");
                    return true;
                }

                // Check containing namespace of the type
                INamespaceSymbol? ns = containingType.ContainingNamespace;
                while (ns != null && !ns.IsGlobalNamespace)
                {
                    string namespaceName = ns.ToDisplayString();
                    PurityAnalysisEngine.LogDebug($"    [INOT] Checking namespace: {namespaceName}");
                    if (Constants.KnownImpureNamespaces.Contains(namespaceName))
                    {
                        PurityAnalysisEngine.LogDebug($"    [INOT] --> Match found for impure namespace: {namespaceName}");
                        return true;
                    }
                    ns = ns.ContainingNamespace;
                }

                PurityAnalysisEngine.LogDebug($"    [INOT] Checking containing type of {containingType.Name}");
                containingType = containingType.ContainingType; // Check nested types
            }

            PurityAnalysisEngine.LogDebug($"    [INOT] No impure type or namespace match found for: {symbol.ToDisplayString()}");
            return false;
        }


        /// <summary>
        /// Checks if a symbol is marked with the [EnforcePure] attribute.
        /// </summary>
        internal static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            if (symbol == null || enforcePureAttributeSymbol == null)
            {
                return false;
            }
            // Also check for PureAttribute
            var pureAttributeSymbol = symbol.ContainingAssembly.GetTypeByMetadataName(typeof(PureAttribute).FullName);
            return symbol.GetAttributes().Any(ad =>
                SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol) ||
                (pureAttributeSymbol != null && SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, pureAttributeSymbol))
            );
        }

        /// <summary>
        /// Helper to create an impure result, using the unknown location if the syntax node is null.
        /// </summary>
        internal static PurityAnalysisResult ImpureResult(SyntaxNode? syntaxNode)
        {
            return syntaxNode != null ? PurityAnalysisResult.Impure(syntaxNode) : PurityAnalysisResult.ImpureUnknownLocation;
        }

        /// <summary>
        /// Logs debug messages (conditionally based on build configuration or settings).
        /// Made internal for access by rules.
        /// </summary>
        internal static void LogDebug(string message)
        {
#if DEBUG
            // Intentionally no-op in Release builds; keep minimal in Debug.
#endif
        }

        /// <summary>
        /// Gets the syntax node representing the body of a method symbol.
        /// </summary>
        private static SyntaxNode? GetBodySyntaxNode(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            // Try to get MethodDeclarationSyntax or LocalFunctionStatementSyntax body
            var declaringSyntaxes = methodSymbol.DeclaringSyntaxReferences;
            LogDebug($"  [GetBody] Checking {declaringSyntaxes.Length} declaring syntax refs for {methodSymbol.Name}"); // Log
            foreach (var syntaxRef in declaringSyntaxes)
            {
                var syntaxNode = syntaxRef.GetSyntax(cancellationToken); // Use cancellation token
                LogDebug($"  [GetBody]   SyntaxRef {syntaxRef.Span} yielded SyntaxNode of Kind: {syntaxNode?.Kind()}"); // Log

                // Return the declaration node itself, ControlFlowGraph.Create can handle these.
                if (syntaxNode is MethodDeclarationSyntax ||
                    syntaxNode is LocalFunctionStatementSyntax ||
                    syntaxNode is AccessorDeclarationSyntax ||
                    syntaxNode is ConstructorDeclarationSyntax ||
                    syntaxNode is OperatorDeclarationSyntax ||
                    syntaxNode is ConversionOperatorDeclarationSyntax) // Added Conversion Operator
                {
                    LogDebug($"  [GetBody]   Found usable body node of Kind: {syntaxNode.Kind()}");
                    return syntaxNode;
                }
            }
            LogDebug($"  [GetBody] No usable body node found for {methodSymbol.Name}."); // Log
            return null;
        }

        // --- Re-added PropagateToSuccessor --- Needs MergeStates
        private static void PropagateToSuccessor(BasicBlock? successor, PurityAnalysisState newState, Dictionary<BasicBlock, PurityAnalysisState> blockStates, Queue<BasicBlock> worklist)
        {
            if (successor == null) return;

            // +++ Check if successor state exists (indicates prior visit from propagation) +++
            bool previouslyVisited = blockStates.TryGetValue(successor, out var existingState);
            // If not previously visited, existingState defaults to 'Pure' (struct default)

            var mergedState = MergeStates(existingState, newState);

            // +++ Determine if state changed or if it's the first propagation visit +++
            bool stateChanged = !mergedState.Equals(existingState);
            // We determine first visit based on whether the key existed in blockStates before the merge.
            // Note: This assumes initialization didn't prepopulate blockStates.
            // If blockStates IS prepopulated (e.g., with PurityAnalysisState.Pure), this 'firstVisit' logic won't work.
            // Let's assume initialization leaves blockStates empty or doesn't include all blocks initially.
            // RETHINK: Our current init DOES prepopulate. So 'previouslyVisited' indicates if *any* propagation reached it.
            // We need a different way to track first processing via worklist, or change init.

            // --- Simpler Logic --- 
            // Always update the state. Enqueue if state changed OR if it's not in the worklist yet.
            // This ensures first visit gets enqueued, and subsequent changes also trigger re-enqueue.

            if (stateChanged)
            {
                LogDebug($"PropagateToSuccessor: State changed for Block #{successor.Ordinal} from Impure={existingState.HasPotentialImpurity} to Impure={mergedState.HasPotentialImpurity}. Updating state.");
                blockStates[successor] = mergedState;
            }
            else
            {
                // If state didn't change, but it was never added to blockStates before, update it now.
                if (!previouslyVisited)
                {
                    blockStates[successor] = mergedState;
                }
                // Log regardless if state changed or not
                LogDebug($"PropagateToSuccessor: State unchanged for Block #{successor.Ordinal} (Impure={existingState.HasPotentialImpurity}).");
            }

            // Enqueue if state changed OR if it's not already in the queue 
            // This ensures initial propagation and reprocessing on change.
            if (stateChanged || !worklist.Contains(successor)) // Check Contains *before* potentially adding
            {
                if (!worklist.Contains(successor))
                {
                    LogDebug($"PropagateToSuccessor: Enqueuing Block #{successor.Ordinal} (State Changed: {stateChanged}).");
                    worklist.Enqueue(successor);
                }
                else
                {
                    // Already in queue. If state changed, it will be reprocessed with new state.
                    // If state didn't change, no need to re-enqueue.
                    if (stateChanged)
                    {
                        LogDebug($"PropagateToSuccessor: Block #{successor.Ordinal} already in queue, state changed. Will reprocess.");
                    }
                    else
                    {
                        LogDebug($"PropagateToSuccessor: Block #{successor.Ordinal} already in queue, state unchanged.");
                    }
                }
            }
            else
            {
                LogDebug($"PropagateToSuccessor: Block #{successor.Ordinal} already in queue and state unchanged. No enqueue needed.");
            }
        }

        // --- Added MergeStates helper --- (Needed by PropagateToSuccessor)
        private static PurityAnalysisState MergeStates(PurityAnalysisState state1, PurityAnalysisState state2)
        {
            LogDebug($"  [Merge] Merging States: S1(Impure={state1.HasPotentialImpurity}, MapCount={state1.DelegateTargetMap.Count}) + S2(Impure={state2.HasPotentialImpurity}, MapCount={state2.DelegateTargetMap.Count})"); // *** ADDED LOG ***
            bool mergedImpurity = state1.HasPotentialImpurity || state2.HasPotentialImpurity;
            SyntaxNode? firstImpureNode = state1.FirstImpureSyntaxNode; // Default to state1's node
            if (state1.HasPotentialImpurity && state2.HasPotentialImpurity && state1.FirstImpureSyntaxNode != null && state2.FirstImpureSyntaxNode != null)
            {
                // Basic heuristic: take the one with the smaller starting position
                if (state2.FirstImpureSyntaxNode.SpanStart < state1.FirstImpureSyntaxNode.SpanStart)
                {
                    firstImpureNode = state2.FirstImpureSyntaxNode;
                }
            }
            else if (state2.HasPotentialImpurity)
            { // If only state2 was impure, use its node
                firstImpureNode = state2.FirstImpureSyntaxNode;
            }

            // --- UPDATED Delegate Map Merging --- 
            var mapBuilder = ImmutableDictionary.CreateBuilder<ISymbol, PotentialTargets>(SymbolEqualityComparer.Default);
            // Add all from state1 first
            foreach (var kvp in state1.DelegateTargetMap)
            {
                mapBuilder.Add(kvp.Key, kvp.Value);
            }
            // Now merge state2, performing union on duplicates
            foreach (var kvp in state2.DelegateTargetMap)
            {
                if (mapBuilder.TryGetValue(kvp.Key, out var existingTargets))
                {
                    // Key exists in state1, merge targets by taking the union of method symbols
                    mapBuilder[kvp.Key] = PotentialTargets.Merge(existingTargets, kvp.Value);
                }
                else
                {
                    // Key only exists in state2, add it
                    mapBuilder.Add(kvp.Key, kvp.Value);
                }
            }
            var finalMap = mapBuilder.ToImmutable();
            // --- END UPDATED Delegate Map Merging --- 

            return new PurityAnalysisState(mergedImpurity, firstImpureNode, finalMap);
        }

        // +++ ADDED HasAttribute HELPER +++
        internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
        {
            if (attributeSymbol == null) return false; // Guard against null attribute symbol
            return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, attributeSymbol.OriginalDefinition));
        }
        // --- END ADDED HELPER ---

        /// <summary>
        /// Checks the purity of the static constructor (.cctor) for a given type, if one exists.
        /// </summary>
        internal static PurityAnalysisResult CheckStaticConstructorPurity(ITypeSymbol? typeSymbol, Rules.PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            if (typeSymbol == null)
            {
                return PurityAnalysisResult.Pure; // No type, no cctor
            }

            // Find the static constructor
            IMethodSymbol? staticConstructor = typeSymbol.GetMembers(".cctor").OfType<IMethodSymbol>().FirstOrDefault();

            if (staticConstructor == null)
            {
                LogDebug($"    [CctorCheck] Type {typeSymbol.Name} has no static constructor. Pure.");
                return PurityAnalysisResult.Pure; // No static constructor found
            }

            LogDebug($"    [CctorCheck] Found static constructor for {typeSymbol.Name}. Checking purity recursively...");

            // Check its purity using the main recursive logic (handles caching and cycles)
            // Note: Pass a *new* visited set specific to this cctor check to avoid cross-contamination with the main method's visited set?
            // For now, reusing the main visited set, assuming cctor calls within the same analysis context are okay.
            var cctorResult = DeterminePurityRecursiveInternal(
                staticConstructor.OriginalDefinition,
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache
            );

            LogDebug($"    [CctorCheck] Static constructor purity result for {typeSymbol.Name}: IsPure={cctorResult.IsPure}");

            // If the static constructor is impure, report it using the type's syntax or a default node
            // Using the type symbol's declaration syntax might be best if available.
            // Fallback to ImpureUnknownLocation if necessary.
            return cctorResult.IsPure
                ? PurityAnalysisResult.Pure
                : PurityAnalysisResult.Impure(cctorResult.ImpureSyntaxNode ?? typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? ImpureResult(null).ImpureSyntaxNode ?? context.ContainingMethodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? throw new InvalidOperationException("Cannot find syntax node for static constructor impurity")); // Fallback with unknown location
        }

        // +++ ADDED HELPER to encapsulate delegate map update logic +++
        private static PurityAnalysisState UpdateDelegateMapForOperation(IOperation op, Rules.PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            LogDebug($"  [UpdMap] Trying Update: OpKind={op.Kind}, CurrentImpure={currentState.HasPotentialImpurity}"); // *** ADDED LOG ***
            // Only update if the current state is still pure (no need to track delegates if already impure)
            if (currentState.HasPotentialImpurity)
            {
                PurityAnalysisState nextState = currentState; // Start with current state

                // --- Logic from AssignmentPurityRule --- 
                if (op is IAssignmentOperation assignmentOperation)
                {
                    var targetOperation = assignmentOperation.Target;
                    var valueOperation = assignmentOperation.Value;
                    var targetSymbol = TryResolveSymbol(targetOperation);

                    if (valueOperation != null && targetSymbol != null && targetOperation.Type?.TypeKind == TypeKind.Delegate)
                    {
                        PurityAnalysisEngine.PotentialTargets? valueTargets = ResolvePotentialTargets(valueOperation, currentState); // Use helper
                        if (valueTargets != null)
                        {
                            nextState = currentState.WithDelegateTarget(targetSymbol, valueTargets.Value);
                            LogDebug($"    [ATF-DEL-ASSIGN] Updated map for {targetSymbol.Name} with {valueTargets.Value.MethodSymbols.Count} targets. New Map Count: {nextState.DelegateTargetMap.Count}");
                        }
                    }
                }
                // --- Logic from VariableDeclarationGroupPurityRule --- 
                else if (op is IVariableDeclarationGroupOperation groupOperation)
                {
                    foreach (var declaration in groupOperation.Declarations)
                    {
                        foreach (var declarator in declaration.Declarators)
                        {
                            if (declarator.Initializer != null)
                            {
                                var initializerValue = declarator.Initializer.Value;
                                ILocalSymbol declaredSymbol = declarator.Symbol;
                                if (declaredSymbol.Type?.TypeKind == TypeKind.Delegate)
                                {
                                    PurityAnalysisEngine.PotentialTargets? valueTargets = ResolvePotentialTargets(initializerValue, currentState); // Use helper
                                    if (valueTargets != null)
                                    {
                                        nextState = nextState.WithDelegateTarget(declaredSymbol, valueTargets.Value); // Update incrementally
                                        LogDebug($"    [ATF-DEL-VAR] Updated map for {declaredSymbol.Name} with {valueTargets.Value.MethodSymbols.Count} targets. New Map Count: {nextState.DelegateTargetMap.Count}");
                                    }
                                }
                            }
                        }
                    }
                }
                // Add other operation kinds if needed (e.g., return statements passing delegates?)

                return nextState;
            }
            return currentState;
        }

        // +++ ADDED HELPER to resolve potential targets from an operation +++ 
        private static PurityAnalysisEngine.PotentialTargets? ResolvePotentialTargets(IOperation valueOperation, PurityAnalysisState currentState)
        {
            if (valueOperation is IMethodReferenceOperation methodRef)
            {
                return PurityAnalysisEngine.PotentialTargets.FromSingle(methodRef.Method.OriginalDefinition);
            }
            else if (valueOperation is IDelegateCreationOperation delegateCreation)
            {
                if (delegateCreation.Target is IMethodReferenceOperation lambdaRef)
                {
                    return PurityAnalysisEngine.PotentialTargets.FromSingle(lambdaRef.Method.OriginalDefinition);
                }
            }
            else // Value is another variable/parameter/field/property reference
            {
                ISymbol? valueSourceSymbol = TryResolveSymbol(valueOperation);
                if (valueSourceSymbol != null && currentState.DelegateTargetMap.TryGetValue(valueSourceSymbol, out var sourceTargets))
                {
                    return sourceTargets; // Propagate targets from the source symbol
                }
            }
            return null; // Cannot resolve targets
        }

        // +++ ADDED TryResolveSymbol HELPER +++ 
        private static ISymbol? TryResolveSymbol(IOperation? operation)
        {
            return operation switch
            {
                ILocalReferenceOperation localRef => localRef.Local,
                IParameterReferenceOperation paramRef => paramRef.Parameter,
                IFieldReferenceOperation fieldRef => fieldRef.Field,
                IPropertyReferenceOperation propRef => propRef.Property,
                _ => null
            };
        }
    }
}