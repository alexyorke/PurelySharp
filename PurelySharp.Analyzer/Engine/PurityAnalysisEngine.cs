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
    internal static class PurityAnalysisEngine
    {
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
            new ReturnStatementPurityRule(),
            new BinaryOperationPurityRule(),
            new PropertyReferencePurityRule(),
            new ArrayElementReferencePurityRule(),
            new CollectionExpressionPurityRule(),
            new ArrayCreationPurityRule(),
            new ArrayInitializerPurityRule(),
            new InterpolatedStringPurityRule(),
            new SwitchStatementPurityRule(),
            new SwitchExpressionPurityRule(),
            new ConstantPatternPurityRule(),
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
            new FlowCaptureReferencePurityRule(),
            new ConditionalOperationPurityRule(),
            new UnaryOperationPurityRule(),
            new ObjectCreationPurityRule(),
            new CoalesceOperationPurityRule(),
            new ConditionalAccessPurityRule(),
            new ThrowOperationPurityRule(),
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
            new LockStatementPurityRule()
        );

        // --- Add list of known impure namespaces ---
        private static readonly ImmutableHashSet<string> KnownImpureNamespaces = ImmutableHashSet.Create(
            StringComparer.Ordinal, // Use ordinal comparison for namespace names
            "System.IO",
            "System.Net",
            "System.Data",
            "System.Threading", // Note: Tasks might be okay if awaited result is pure
            "System.Diagnostics", // Debug, Trace, Process
            "System.Security.Cryptography",
            "System.Runtime.InteropServices",
            "System.Reflection" // Reflection can often lead to impure actions
        );

        // --- Add list of specific impure types ---
        private static readonly ImmutableHashSet<string> KnownImpureTypeNames = ImmutableHashSet.Create(
             StringComparer.Ordinal,
            "System.Random",
            "System.DateTime", // Now, UtcNow properties are impure
            "System.Guid",     // NewGuid() is impure
            "System.Console",
            "System.Environment", // Accessing env variables, etc.
            "System.Timers.Timer"
        // "System.Text.StringBuilder" // REMOVED: Handled by method checks
        // Add specific types like File, HttpClient, Thread etc. if needed beyond namespace check
        );

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
        private static readonly HashSet<string> KnownImpureMethods = new HashSet<string>
        {
            // System.Console
            "System.Console.WriteLine()", // Parameterless overload
            "System.Console.WriteLine(string)",
            "System.Console.WriteLine(object)",
            "System.Console.Write(string)",
            "System.Console.Write(object)",
            "System.Console.ReadLine()",
            "System.Console.ReadKey()",
            "System.Console.Clear()",

            // System.Diagnostics.Debug / Trace
            "System.Diagnostics.Debug.WriteLine(string)",
            "System.Diagnostics.Trace.WriteLine(string)",
            "System.Diagnostics.Debug.Assert(bool)",
            "System.Diagnostics.Trace.Assert(bool)",

            // System.IO.File (Common methods)
            "System.IO.File.ReadAllText(string)",
            "System.IO.File.WriteAllText(string, string)",
            "System.IO.File.AppendAllText(string, string)",
            "System.IO.File.ReadAllBytes(string)",
            "System.IO.File.WriteAllBytes(string, byte[])",
            "System.IO.File.ReadAllLines(string)",
            "System.IO.File.WriteAllLines(string, System.Collections.Generic.IEnumerable<string>)",
            "System.IO.File.AppendAllLines(string, System.Collections.Generic.IEnumerable<string>)",
            "System.IO.File.Delete(string)",
            "System.IO.File.Exists(string)", // Technically pure read, but often used before write
            "System.IO.File.OpenRead(string)",
            "System.IO.File.OpenWrite(string)",
            "System.IO.File.Create(string)",

            // System.Net.Http.HttpClient (Common methods - simplified, full signatures complex)
            "System.Net.Http.HttpClient.GetAsync(string)",
            "System.Net.Http.HttpClient.PostAsync(string, System.Net.Http.HttpContent)",
            "System.Net.Http.HttpClient.PutAsync(string, System.Net.Http.HttpContent)",
            "System.Net.Http.HttpClient.DeleteAsync(string)",

            // System.Threading.Thread
            "System.Threading.Thread.Sleep(int)",
            "System.Threading.Thread.Start()", // Starts a new thread, side effect
            "System.Threading.Thread.Join()", // Waits for another thread

            // System.DateTime Properties (Accessors)
            "System.DateTime.Now.get",
            "System.DateTime.UtcNow.get",

            // System.Guid
            "System.Guid.NewGuid()",

            // System.Random (All methods modifying or returning state)
            "System.Random.Next()",
            "System.Random.Next(int)",
            "System.Random.Next(int, int)",
            "System.Random.NextDouble()",
            "System.Random.NextBytes(byte[])",
            "System.Random.NextInt64()", // C# 8+

            // System.Xml.Linq (Parsing potentially impure depending on input source)
            "System.Xml.Linq.XDocument.Parse(string)",
            "System.Xml.Linq.XElement.Parse(string)",
            "System.Xml.Linq.XDocument.Load(string)",
            "System.Xml.Linq.XElement.Load(string)",

            // System.Threading.Interlocked (Essential methods with correct signatures)
            "System.Threading.Interlocked.Increment(ref int)",
            "System.Threading.Interlocked.Increment(ref long)",
            "System.Threading.Interlocked.Decrement(ref int)",
            "System.Threading.Interlocked.Decrement(ref long)",
            "System.Threading.Interlocked.Add(ref int, int)",
            "System.Threading.Interlocked.Add(ref long, long)",
            "System.Threading.Interlocked.Exchange(ref int, int)",
            "System.Threading.Interlocked.Exchange(ref long, long)",
            "System.Threading.Interlocked.Exchange(ref object, object)",
            "System.Threading.Interlocked.CompareExchange(ref int, int, int)",
            "System.Threading.Interlocked.CompareExchange(ref long, long, long)",
            "System.Threading.Interlocked.CompareExchange(ref object, object, object)",
            "System.Threading.Interlocked.Read(ref long)", // Read for long is atomic

            // Environment properties involving external state
            "System.Environment.CurrentManagedThreadId.get",
            "System.Environment.TickCount.get",
            "System.Environment.TickCount64.get",
            "System.Environment.GetEnvironmentVariable(string)",
            "System.Environment.Exit(int)",

            // Regex constructor (can be complex/throw)
            "System.Text.RegularExpressions.Regex.Regex(string)",
            "System.Text.RegularExpressions.Regex.Regex(string, System.Text.RegularExpressions.RegexOptions)",

            // StringBuilder methods (Explicitly list common ones)
            // Note: IsInImpureNamespaceOrType should catch StringBuilder anyway, but explicit listing helps if that fails.
            "System.Text.StringBuilder.StringBuilder()", // Parameterless constructor
            "System.Text.StringBuilder.StringBuilder(int)", // Capacity constructor
            "System.Text.StringBuilder.StringBuilder(string?)", // String constructor
            "System.Text.StringBuilder.StringBuilder(System.ReadOnlySpan<char>)", // Span constructor
            "System.Text.StringBuilder.Append(string)",
            "System.Text.StringBuilder.Append(string?)",
            "System.Text.StringBuilder.Append(char)",
            "System.Text.StringBuilder.Append(object)",
            "System.Text.StringBuilder.AppendLine()",
            "System.Text.StringBuilder.AppendLine(string)",
            "System.Text.StringBuilder.Insert(int, string)",
            "System.Text.StringBuilder.Remove(int, int)",
            "System.Text.StringBuilder.Replace(string, string)",
            "System.Text.StringBuilder.Clear()",

            // Add more known impure methods as needed...
        };

        // Add a set of known PURE BCL method/property signatures (using OriginalDefinition.ToDisplayString() format)
        // This helps handle cases where CFG analysis might fail or be too complex for common BCL members.
        private static readonly HashSet<string> KnownPureBCLMembers = new HashSet<string>
        {
            // System.String
            "string.Length.get", // Note: Roslyn might represent this as get_Length
            "string.ToString()",
            "string.Equals(string)",
            "string.Equals(string, System.StringComparison)",
            "string.Equals(object)",
            "string.GetHashCode()",
            "string.IsNullOrEmpty(string)",
            "string.IsNullOrWhiteSpace(string)",
            "string.Substring(int)",
            "string.Substring(int, int)",
            "string.ToLower()",
            "string.ToUpper()",
            "string.Trim()",
            // +++ Add common pure string methods +++
            "string.ToUpperInvariant()",
            "string.ToLowerInvariant()",
            "string.TrimStart()",
            "string.TrimEnd()",
            "string.Contains(string)",
            "string.StartsWith(string)",
            "string.EndsWith(string)",
            "string.IndexOf(string)",
            "string.Replace(string, string)",
            // --- End added methods ---
            // System.Collections.Generic.List<T>
            "System.Collections.Generic.List<T>.Count.get",
            "System.Collections.Generic.List<T>.this[int].get", // Indexer Get
            "System.Collections.Generic.List<T>.Contains(T)",
            "System.Collections.Generic.List<T>.IndexOf(T)",
            "System.Collections.Generic.List<T>.Find(System.Predicate<T>)",
            "System.Collections.Generic.List<T>.Exists(System.Predicate<T>)",
            "System.Collections.Generic.List<T>.TrueForAll(System.Predicate<T>)",
            "System.Collections.Generic.List<T>.GetEnumerator()",
            // System.Collections.Generic.Dictionary<TKey, TValue>
            "System.Collections.Generic.Dictionary<TKey, TValue>.Count.get",
            "System.Collections.Generic.Dictionary<TKey, TValue>.ContainsKey(TKey)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.TryGetValue(TKey, out TValue)", // Often pure in usage context, though technically modifies out param
            "System.Collections.Generic.Dictionary<TKey, TValue>.this[TKey].get", // Indexer Get
            "System.Collections.Generic.Dictionary<TKey, TValue>.Keys.get",
            "System.Collections.Generic.Dictionary<TKey, TValue>.Values.get",
            "System.Collections.Generic.Dictionary<TKey, TValue>.GetEnumerator()",
            // System.Collections.Immutable
            "System.Collections.Immutable.ImmutableList<T>.Count.get",
            "System.Collections.Immutable.ImmutableList<T>.Contains(T)",
            "System.Collections.Immutable.ImmutableList<T>.IsEmpty.get",
            "System.Collections.Immutable.ImmutableList<T>.this[int].get",
            "System.Collections.Immutable.ImmutableArray<T>.Length.get",
            "System.Collections.Immutable.ImmutableArray<T>.IsEmpty.get",
            "System.Collections.Immutable.ImmutableArray<T>.Contains(T)",
            // Add common factory methods
            "System.Collections.Immutable.ImmutableArray.Create<T>()",
            "System.Collections.Immutable.ImmutableArray.Create<T>(params T[])",
            "System.Collections.Immutable.ImmutableList.Create<T>()",
            "System.Collections.Immutable.ImmutableList.Create<T>(params T[])",
            "System.Collections.Immutable.ImmutableHashSet.Create<T>()",
            "System.Collections.Immutable.ImmutableHashSet.Create<T>(params T[])",
            "System.Collections.Immutable.ImmutableDictionary.Create<TKey, TValue>()",
            // System.Linq (Common query operators - assuming deferred execution doesn't count as impurity itself)
            // Note: Analyzing LINQ thoroughly is complex. This is a basic approximation.
            "System.Linq.Enumerable.Select<TSource, TResult>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, TResult>)",
            "System.Linq.Enumerable.Where<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)",
            "System.Linq.Enumerable.ToList<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Creates new list, pure read of source
            "System.Linq.Enumerable.ToArray<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Creates new array, pure read of source
            "System.Linq.Enumerable.Count<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
            "System.Linq.Enumerable.Any<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
            "System.Linq.Enumerable.FirstOrDefault<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
            "System.Linq.Enumerable.FirstOrDefault<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)",
            // System.Math (already handled via StartsWith check later, but can be explicit)
            // Add specific Math methods if needed
            "System.Math.Abs(double)",
            // System.Object
            "object.Equals(object)",
            "object.Equals(object, object)",
            "object.GetHashCode()",
            "object.GetType()",
            "object.ReferenceEquals(object, object)",
            // System.Guid
            "System.Guid.ToString()",
            "System.Guid.Equals(System.Guid)",
            "System.Guid.Equals(object)",
            "System.Guid.GetHashCode()",
            // System.Type
            "System.Type.Equals(object)",
            "System.Type.Equals(System.Type)",
            "System.Type.GetHashCode()",
            "System.Type.ToString()",
            "System.Text.StringBuilder.ToString()", // Added explicitly pure
            // System.Xml.Linq
            "System.Xml.Linq.XDocument.Root.get",
            "System.Xml.Linq.XContainer.Elements()",
            "System.Xml.Linq.XContainer.Elements(System.Xml.Linq.XName)",
            "System.Xml.Linq.XContainer.Descendants()",
            "System.Xml.Linq.XContainer.Descendants(System.Xml.Linq.XName)",
            "System.Xml.Linq.XElement.Attribute(System.Xml.Linq.XName)",
            "System.Xml.Linq.XElement.Attributes()",
            "System.Xml.Linq.XElement.Name.get",
            "System.Xml.Linq.XElement.Value.get",
            "System.Xml.Linq.XAttribute.Name.get",
            "System.Xml.Linq.XAttribute.Value.get",
            // System.Array
            "System.Array.Length.get",
            // Add more known pure BCL methods/properties as needed
        };

        /// <summary>
        /// Represents the purity state during CFG analysis.
        /// </summary>
        private struct PurityAnalysisState : System.IEquatable<PurityAnalysisState>
        {
            public bool HasPotentialImpurity { get; set; }
            public SyntaxNode? FirstImpureSyntaxNode { get; set; }

            public static PurityAnalysisState Pure => new PurityAnalysisState { HasPotentialImpurity = false, FirstImpureSyntaxNode = null };

            public static PurityAnalysisState Merge(IEnumerable<PurityAnalysisState> states)
            {
                bool mergedImpurity = false;
                SyntaxNode? firstImpureNode = null;
                foreach (var state in states)
                {
                    if (state.HasPotentialImpurity)
                    {
                        mergedImpurity = true;
                        if (firstImpureNode == null) { firstImpureNode = state.FirstImpureSyntaxNode; }
                    }
                }
                return new PurityAnalysisState { HasPotentialImpurity = mergedImpurity, FirstImpureSyntaxNode = firstImpureNode };
            }

            public bool Equals(PurityAnalysisState other) =>
                this.HasPotentialImpurity == other.HasPotentialImpurity &&
                object.Equals(this.FirstImpureSyntaxNode, other.FirstImpureSyntaxNode); // Compare nodes too

            public override bool Equals(object obj) => obj is PurityAnalysisState other && Equals(other);

            // Fix CS0103: Implement GetHashCode manually for netstandard2.0 compatibility
            public override int GetHashCode()
            {
                // Combine hash codes of both properties
                int hash = 17;
                hash = hash * 23 + HasPotentialImpurity.GetHashCode();
                hash = hash * 23 + (FirstImpureSyntaxNode?.GetHashCode() ?? 0);
                return hash;
            }
            public static bool operator ==(PurityAnalysisState left, PurityAnalysisState right) => left.Equals(right);
            public static bool operator !=(PurityAnalysisState left, PurityAnalysisState right) => !(left == right);
        }

        /// <summary>
        /// Checks if a method symbol is considered pure based on its implementation using CFG data-flow analysis.
        /// Manages the visited set for cycle detection across the entire analysis.
        /// </summary>
        internal static PurityAnalysisResult IsConsideredPure(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {
            var purityCache = new Dictionary<IMethodSymbol, PurityAnalysisResult>(SymbolEqualityComparer.Default);
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            // Pass the (potentially null) attribute symbol down
            return DeterminePurityRecursiveInternal(methodSymbol.OriginalDefinition, semanticModel, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol, visited, purityCache);
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
            // *** LOG ENTRY ***
            LogDebug($"Entering DeterminePurityRecursiveInternal for {methodSymbol.ToDisplayString()}");

            // --- Cache Check ---
            if (purityCache.TryGetValue(methodSymbol, out PurityAnalysisResult cachedResult))
            {
                LogDebug($"Cache hit for {methodSymbol.ToDisplayString()}, IsPure={cachedResult.IsPure}");
                return cachedResult;
            }

            // --- Cycle Detection ---
            if (!visited.Add(methodSymbol))
            {
                // Cycle detected, assume impure to be safe
                LogDebug($"Cycle detected involving {methodSymbol.ToDisplayString()}, assuming impure.");
                // Cache the impure result for this cycle participant
                purityCache[methodSymbol] = PurityAnalysisResult.ImpureUnknownLocation;
                return PurityAnalysisResult.ImpureUnknownLocation;
            }

            // --- Basic Checks (Before CFG) ---
            // 2. Known Impure/Pure Checks (moved UP - check before assuming pure due to no body)
            if (IsKnownImpure(methodSymbol))
            {
                LogDebug($"Method {methodSymbol.ToDisplayString()} is known impure.");
                purityCache[methodSymbol] = ImpureResult(null); // Or find syntax if possible
                visited.Remove(methodSymbol);
                return ImpureResult(null);
            }
            if (IsKnownPureBCLMember(methodSymbol))
            {
                LogDebug($"Method {methodSymbol.ToDisplayString()} is known pure BCL member.");
                purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                visited.Remove(methodSymbol);
                return PurityAnalysisResult.Pure;
            }
            if (IsInImpureNamespaceOrType(methodSymbol))
            {
                LogDebug($"Method {methodSymbol.ToDisplayString()} is in known impure namespace/type.");
                purityCache[methodSymbol] = ImpureResult(null); // Or find syntax if possible
                visited.Remove(methodSymbol);
                return ImpureResult(null);
            }

            // 1. Abstract/External/Missing Body: Assumed pure (no implementation to violate purity)
            if (methodSymbol.IsAbstract || methodSymbol.IsExtern || GetBodySyntaxNode(methodSymbol, default) == null) // Use default CancellationToken
            {
                // Only assume pure here if it wasn't already caught by the known lists above
                LogDebug($"Method {methodSymbol.ToDisplayString()} is abstract, extern, or has no body AND not known impure/pure. Assuming pure.");
                purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                visited.Remove(methodSymbol); // Remove before returning
                return PurityAnalysisResult.Pure;
            }

            // --- Analyze Body using CFG ---
            PurityAnalysisResult result = PurityAnalysisResult.Pure; // Assume pure until proven otherwise by CFG
            var bodySyntaxNode = GetBodySyntaxNode(methodSymbol, default); // Pass CancellationToken.None
            if (bodySyntaxNode != null)
            {
                LogDebug($"Analyzing body of {methodSymbol.ToDisplayString()} using CFG.");
                // Call internal CFG analysis helper
                result = AnalyzePurityUsingCFGInternal(
                    bodySyntaxNode,
                    semanticModel,
                    enforcePureAttributeSymbol,
                    allowSynchronizationAttributeSymbol,
                    visited,
                    methodSymbol, // Pass the containing method symbol
                    purityCache);
            }
            else
            {
                LogDebug($"No body found for {methodSymbol.ToDisplayString()} to analyze with CFG. Assuming pure based on earlier checks.");
                // Result remains Pure if no body found (matches abstract/extern check)
            }

            // Get the IOperation for the body *after* potential CFG analysis
            // Used for post-CFG checks (Return, Throw)
            IOperation? methodBodyIOperation = null;
            if (bodySyntaxNode != null)
            {
                try
                {
                    methodBodyIOperation = semanticModel.GetOperation(bodySyntaxNode, default);
                }
                catch (Exception ex)
                {
                    LogDebug($"  Post-CFG: Error getting IOperation for method body: {ex.Message}");
                    methodBodyIOperation = null; // Ensure it's null if GetOperation fails
                }
            }

            // --- NEW: Post-CFG Full Operation Tree Walk ---
            // If CFG analysis didn't find impurity, perform a full walk of the
            // IOperation tree as a fallback to catch things missed by CFG structure.
            if (result.IsPure && methodBodyIOperation != null)
            {
                LogDebug($"  Post-CFG: CFG result is Pure. Performing full IOperation tree walk for {methodSymbol.ToDisplayString()}");
                // Use the REFINED walker
                var fullWalker = new FullOperationPurityWalker(semanticModel, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol, visited, purityCache, methodSymbol);
                fullWalker.Visit(methodBodyIOperation);

                if (!fullWalker.OverallPurityResult.IsPure)
                {
                    LogDebug($"  Post-CFG: IMPURITY FOUND during full IOperation walk. Overriding CFG result.");
                    result = fullWalker.OverallPurityResult;
                }
                else
                {
                    LogDebug($"  Post-CFG: No impurity found during full IOperation walk.");
                }
            }
            // --- END NEW ---

            // --- Post-CFG Check: Return Values (Original Check) ---
            // If the analysis result is still pure after CFG + Full Walk, explicitly check Return operations
            if (result.IsPure && methodBodyIOperation != null)
            {
                LogDebug($"Post-CFG: Result Pure. Performing post-CFG check on ReturnOperations in {methodSymbol.ToDisplayString()}");
                var pureAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

                var returnContext = new Rules.PurityAnalysisContext(
                    semanticModel,
                    enforcePureAttributeSymbol,
                    pureAttributeSymbol,
                    allowSynchronizationAttributeSymbol,
                    visited,
                    purityCache,
                    methodSymbol,
                    _purityRules,
                    CancellationToken.None);

                bool returnFound = false;
                foreach (var returnOp in methodBodyIOperation.DescendantsAndSelf().OfType<IReturnOperation>())
                {
                    returnFound = true;
                    LogDebug($"  Post-CFG: Found ReturnOperation: {returnOp.Syntax}");
                    if (returnOp.ReturnedValue != null)
                    {
                        LogDebug($"    Post-CFG: Checking ReturnedValue of kind {returnOp.ReturnedValue.Kind}: {returnOp.ReturnedValue.Syntax}");
                        var returnPurity = CheckSingleOperation(returnOp.ReturnedValue, returnContext);
                        if (!returnPurity.IsPure)
                        {
                            LogDebug($"    Post-CFG: Returned value found IMPURE. Overriding result.");
                            result = returnPurity;
                            break; // Found impurity, stop checking returns
                        }
                        else
                        {
                            LogDebug($"    Post-CFG: Returned value checked and found PURE.");
                        }
                    }
                    else
                    {
                        LogDebug($"  Post-CFG: ReturnOperation has no ReturnedValue (e.g., return;). Pure.");
                    }
                }
                if (!returnFound)
                {
                    LogDebug($"  Post-CFG: No ReturnOperation found in method body operation tree.");
                }
            }
            // --- END Post-CFG Check: Return Values (Original Check) ---

            // --- FIX: Post-CFG Check for Throw Operations ---
            // Even if CFG/Return checks passed, explicitly check for throw statements in the operation tree
            // Use the retrieved methodBodyIOperation
            if (result.IsPure && methodBodyIOperation != null)
            {
                LogDebug($"Post-CFG: Result still Pure. Performing post-CFG check for ThrowOperations in {methodSymbol.ToDisplayString()}");
                // Use the retrieved methodBodyIOperation
                var firstThrowOp = methodBodyIOperation.DescendantsAndSelf().OfType<IThrowOperation>().FirstOrDefault();
                if (firstThrowOp != null)
                {
                    LogDebug($"  Post-CFG: Found ThrowOperation: {firstThrowOp.Syntax}. Overriding result to Impure.");
                    result = PurityAnalysisResult.Impure(firstThrowOp.Syntax);
                }
                else
                {
                    LogDebug($"  Post-CFG: No ThrowOperation found in method body operation tree.");
                }
            }
            // --- END FIX ---

            // --- Caching and Cleanup ---
            purityCache[methodSymbol] = result;
            visited.Remove(methodSymbol); // Remove after analysis is complete

            LogDebug($"Exiting DeterminePurityRecursiveInternal for {methodSymbol.ToDisplayString()}, Final IsPure={result.IsPure}");
            return result;
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
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Exit Block #{exitBlock.Ordinal} Final State: HasImpurity={exitState.HasPotentialImpurity}, Node={exitState.FirstImpureSyntaxNode?.Kind()}, NodeText='{exitState.FirstImpureSyntaxNode?.ToString()}'");

                // --- FIX: Explicitly check operations in the exit block if state is currently pure ---
                if (!exitState.HasPotentialImpurity)
                {
                    LogDebug($"  [CFG] Exit block state is pure. Explicitly checking operations within Exit Block #{exitBlock.Ordinal}.");
                    var pureAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

                    var ruleContext = new PurelySharp.Analyzer.Engine.Rules.PurityAnalysisContext(
                        semanticModel,
                        enforcePureAttributeSymbol,
                        pureAttributeSymbol,
                        allowSynchronizationAttributeSymbol,
                        visited, // Note: visited might be incomplete here, but ok for stateless rules
                        purityCache,
                        containingMethodSymbol,
                        _purityRules,
                        CancellationToken.None); // Pass the token

                    foreach (var exitOp in exitBlock.Operations)
                    {
                        if (exitOp == null) continue;
                        LogDebug($"    [CFG] Checking exit operation: {exitOp.Kind} - '{exitOp.Syntax}'");
                        var opResult = CheckSingleOperation(exitOp, ruleContext);
                        if (!opResult.IsPure)
                        {
                            LogDebug($"    [CFG] Exit operation {exitOp.Kind} found IMPURE. Updating final result.");
                            exitState = new PurityAnalysisState { HasPotentialImpurity = true, FirstImpureSyntaxNode = opResult.ImpureSyntaxNode ?? exitOp.Syntax };
                            // Update exitState for the final result calculation below
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
                finalResult = exitState.HasPotentialImpurity
                    ? PurityAnalysisResult.Impure(exitState.FirstImpureSyntaxNode ?? bodyNode)
                    : PurityAnalysisResult.Pure;
            }
            else if (exitBlock != null) // Has exit block, but state not found?
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Could not get state for the exit block #{exitBlock.Ordinal}. Assuming impure (e.g., unreachable code).");
                finalResult = PurityAnalysisResult.Impure(bodyNode);
            }
            else // No blocks in CFG
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: CFG has no blocks. Assuming pure.");
                finalResult = PurityAnalysisResult.Pure; // Should have been caught earlier
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

            // +++ Log ALL raw operations in the block upon entry +++
            LogDebug($"    [ATF Raw Ops - Block {block.Ordinal}] START");
            foreach (var rawOp in block.Operations)
            {
                if (rawOp != null)
                {
                    LogDebug($"      - Raw Kind: {rawOp.Kind}, Raw Syntax: {rawOp.Syntax.ToString().Replace("\r\n", " ").Replace("\n", " ")}");
                }
                else
                {
                    LogDebug("      - Raw Null Operation");
                }
            }
            LogDebug($"    [ATF Raw Ops - Block {block.Ordinal}] END");
            // +++ End Raw Log +++

            if (stateBefore.HasPotentialImpurity) // Optimization: If already impure, no need to check further.
            {
                LogDebug($"ApplyTransferFunction SKIP for Block #{block.Ordinal} - Already impure.");
                return stateBefore;
            }

            // Create context ONCE for this block's analysis
            var pureAttributeSymbol_block = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

            var ruleContext = new PurelySharp.Analyzer.Engine.Rules.PurityAnalysisContext(
                semanticModel,
                enforcePureAttributeSymbol,
                pureAttributeSymbol_block,
                allowSynchronizationAttributeSymbol,
                visited,
                purityCache,
                containingMethodSymbol,
                _purityRules, // Pass the list of rules
                CancellationToken.None); // Pass the token

            // +++ Log ALL operations in the block +++
            LogDebug($"    [ATF Block {block.Ordinal}] Operations:");
            foreach (var op in block.Operations)
            {
                if (op != null)
                {
                    LogDebug($"      - Kind: {op.Kind}, Syntax: {op.Syntax.ToString().Replace("\r\n", " ").Replace("\n", " ")}");
                }
                else
                {
                    LogDebug("      - Null Operation");
                }
            }
            LogDebug($"    [ATF Block {block.Ordinal}] End Operations Log.");
            // +++ End Log +++

            // If we reach here, all operations in the block were handled by rules and deemed pure.
            LogDebug($"ApplyTransferFunction END for Block #{block.Ordinal} - All ops handled and pure. Returning previous state.");
            return stateBefore; // Return the initial (Pure) state if no operations caused impurity
        }

        /// <summary>
        /// Checks the purity of a single IOperation using the registered purity rules.
        /// </summary>
        internal static PurityAnalysisResult CheckSingleOperation(IOperation operation, Rules.PurityAnalysisContext context)
        {
            LogDebug($"    [CSO] Enter CheckSingleOperation for Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");

            // Explicitly handle FlowCaptureReference and FlowCapture as pure.
            // These represent compiler-generated temporaries and should not affect purity.
            if (operation.Kind == OperationKind.FlowCaptureReference || operation.Kind == OperationKind.FlowCapture)
            {
                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure - FlowCapture/Reference)");
                return PurityAnalysisResult.Pure;
            }


            // Find the first applicable rule
            var applicableRule = _purityRules.FirstOrDefault(rule => rule.ApplicableOperationKinds.Contains(operation.Kind));

            if (applicableRule != null)
            {
                // +++ Log Rule Application +++
                LogDebug($"    [CSO] Applying Rule '{applicableRule.GetType().Name}' to Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");
                var ruleResult = applicableRule.CheckPurity(operation, context);
                // +++ Log Rule Result +++
                LogDebug($"    [CSO] Rule '{applicableRule.GetType().Name}' Result: IsPure={ruleResult.IsPure}");
                if (!ruleResult.IsPure)
                {
                    LogDebug($"    [CSO] Exit CheckSingleOperation (Impure from rule)");
                    return ruleResult; // Return the impure result
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
            bool isKnownPure = KnownPureBCLMembers.Contains(signature);
            // +++ Log the result of Contains +++
            PurityAnalysisEngine.LogDebug($"    [IsKnownPure] HashSet.Contains result: {isKnownPure}");

            // Handle common generic cases (e.g., List<T>.Count) more robustly if direct match fails
            if (!isKnownPure && symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                signature = methodSymbol.ConstructedFrom.ToDisplayString();
                isKnownPure = KnownPureBCLMembers.Contains(signature);
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
                isKnownPure = KnownPureBCLMembers.Contains(signature);
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

            if (KnownImpureMethods.Contains(signature))
            {
                LogDebug($"Helper IsKnownImpure: Match found for {symbol.ToDisplayString()} using signature '{signature}'");
                return true;
            }

            // Handle generic methods if needed (e.g., Interlocked.CompareExchange<T>)
            if (symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                signature = methodSymbol.ConstructedFrom.ToDisplayString();
                if (KnownImpureMethods.Contains(signature))
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
                if (KnownImpureTypeNames.Contains(typeName)) // Compare against the known impure type list
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
                    if (KnownImpureNamespaces.Contains(namespaceName))
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
            return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol));
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
            // New logging implementation: Write to Console
            /* // Commented out to disable logging
            try
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [DEBUG] {message}");
            }
            catch (Exception ex)
            {
                // Fallback if Console logging fails for some reason
                System.Diagnostics.Debug.WriteLine($"Console Logging failed: {ex.Message}");
            }
            */
#endif
        }

        /// <summary>
        /// Gets the syntax node representing the body of a method symbol.
        /// </summary>
        private static SyntaxNode? GetBodySyntaxNode(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            // Try to get MethodDeclarationSyntax or LocalFunctionStatementSyntax body
            var declaringSyntaxes = methodSymbol.DeclaringSyntaxReferences;
            foreach (var syntaxRef in declaringSyntaxes)
            {
                var syntaxNode = syntaxRef.GetSyntax(cancellationToken); // Use cancellation token

                // Return the declaration node itself, ControlFlowGraph.Create can handle these.
                if (syntaxNode is MethodDeclarationSyntax ||
                    syntaxNode is LocalFunctionStatementSyntax ||
                    syntaxNode is AccessorDeclarationSyntax ||
                    syntaxNode is ConstructorDeclarationSyntax ||
                    syntaxNode is OperatorDeclarationSyntax) // Added Operator
                {
                    return syntaxNode;
                }

                // For properties with expression bodies, maybe return the ArrowExpressionClauseSyntax?
                // Let's stick to returning the main declaration syntax for now.
            }
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
            // If either state is impure, the merged state is impure.
            if (state1.HasPotentialImpurity || state2.HasPotentialImpurity)
            {
                // Try to keep the first impure node encountered.
                // This isn't perfect without path tracking, but choose the one that isn't null.
                SyntaxNode? firstImpureNode = null;
                if (state1.HasPotentialImpurity && state2.HasPotentialImpurity)
                {
                    // Both impure, prefer the existing one? Or the new one?
                    // Let's prefer the one that isn't null. If both are not null, pick state1's arbitrarily.
                    firstImpureNode = state1.FirstImpureSyntaxNode ?? state2.FirstImpureSyntaxNode;
                }
                else if (state1.HasPotentialImpurity)
                {
                    firstImpureNode = state1.FirstImpureSyntaxNode;
                }
                else // Only state2 is impure
                {
                    firstImpureNode = state2.FirstImpureSyntaxNode;
                }

                return new PurityAnalysisState { HasPotentialImpurity = true, FirstImpureSyntaxNode = firstImpureNode };
            }

            // Both are pure
            return PurityAnalysisState.Pure;
        }

        // +++ ADDED HasAttribute HELPER +++
        internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
        {
            if (attributeSymbol == null) return false; // Guard against null attribute symbol
            return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, attributeSymbol.OriginalDefinition));
        }
        // --- END ADDED HELPER ---

        // --- NEW: REFINED FullOperationPurityWalker Helper Class ---
        private class FullOperationPurityWalker : OperationWalker
        {
            private readonly Rules.PurityAnalysisContext _context;
            private PurityAnalysisResult _overallPurityResult = PurityAnalysisResult.Pure;
            private bool _firstImpurityFound = false;

            public FullOperationPurityWalker(
                SemanticModel semanticModel,
                INamedTypeSymbol enforcePureAttributeSymbol,
                INamedTypeSymbol? allowSynchronizationAttributeSymbol,
                HashSet<IMethodSymbol> visited,
                Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache,
                IMethodSymbol containingMethodSymbol)
            {
                var pureAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");
                _context = new Rules.PurityAnalysisContext(
                         semanticModel,
                         enforcePureAttributeSymbol,
                         pureAttributeSymbol,
                         allowSynchronizationAttributeSymbol,
                         visited,
                         purityCache,
                         containingMethodSymbol,
                         _purityRules,
                         CancellationToken.None);
            }

            public PurityAnalysisResult OverallPurityResult => _overallPurityResult;

            public override void VisitWith(IWithOperation operation)
            {
                if (_firstImpurityFound) return; // Stop walking if impurity already found

                LogDebug($"    [Final Walker] Visiting: With - '{operation.Syntax}'");
                // Explicitly check the 'with' operation itself using rules
                var withResult = CheckSingleOperation(operation, _context); // Calls WithOperationPurityRule
                if (!withResult.IsPure)
                {
                    LogDebug($"    [Final Walker] IMPURITY FOUND by CheckSingleOperation: With at '{operation.Syntax}'");
                    _overallPurityResult = withResult; // Use the result from the rule
                    _firstImpurityFound = true;
                    // Don't visit children if the operation itself is impure
                    return;
                }
                else
                {
                    // If the rule handled it and found it pure, we assume the rule correctly
                    // analyzed the necessary children (Operand, Initializer Values).
                    // Therefore, DO NOT call base.VisitWith(operation) which would descend further.
                    LogDebug($"    [Final Walker] Kind With checked pure by rule. SKIPPING base.VisitWith.");
                    // base.VisitWith(operation); // <-- DO NOT DESCEND FURTHER
                }
            }

            public override void DefaultVisit(IOperation operation)
            {
                if (_firstImpurityFound) return; // Stop walking if impurity already found

                // Log the kind being visited
                LogDebug($"    [Final Walker] Visiting: {operation.Kind} - '{operation.Syntax}'");

                // Check if the operation itself requires a direct purity check via rules
                bool requiresDirectCheck = _purityRules.Any(rule => rule.ApplicableOperationKinds.Contains(operation.Kind));

                if (requiresDirectCheck)
                {
                    LogDebug($"    [Final Walker] Kind {operation.Kind} needs check via CheckSingleOperation.");
                    var result = CheckSingleOperation(operation, _context);
                    if (!result.IsPure)
                    {
                        LogDebug($"    [Final Walker] IMPURITY FOUND by CheckSingleOperation: {operation.Kind} at '{operation.Syntax}'");
                        _overallPurityResult = result;
                        _firstImpurityFound = true;
                        return; // Stop walking this branch
                    }
                    else
                    {
                        LogDebug($"    [Final Walker] Kind {operation.Kind} checked pure. Visiting children.");
                        // If pure, continue visiting children
                        base.DefaultVisit(operation);
                    }
                }
                else
                {
                    // If no specific rule applies, assume structurally pure FOR THE WALK
                    // and visit children.
                    LogDebug($"    [Final Walker] Kind {operation.Kind} is structurally pure. Visiting children.");
                    base.DefaultVisit(operation);
                }
            }
        }
        // --- END NEW ---
    }
}