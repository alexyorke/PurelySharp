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
using PurelySharp.Analyzer.Engine.Rules;
using System.Threading;

namespace PurelySharp.Analyzer.Engine
{

    internal class PurityAnalysisEngine
    {
        private readonly CompilationPurityService? _purityService;

        public PurityAnalysisEngine() { }

        public PurityAnalysisEngine(CompilationPurityService? purityService)
        {
            _purityService = purityService;
        }


        private static readonly SymbolDisplayFormat _signatureFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeContainingType |

                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeModifiers,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeParamsRefOut |
                SymbolDisplayParameterOptions.IncludeDefaultValue,



            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );


        private static readonly ImmutableList<IPurityRule> _purityRules = Rules.RuleRegistry.GetDefaultRules();






        public readonly struct PurityAnalysisResult
        {

            public bool IsPure { get; }


            public SyntaxNode? ImpureSyntaxNode { get; }


            private PurityAnalysisResult(bool isPure, SyntaxNode? impureSyntaxNode)
            {
                IsPure = isPure;
                ImpureSyntaxNode = impureSyntaxNode;
            }


            public static PurityAnalysisResult Pure => new PurityAnalysisResult(true, null);


            public static PurityAnalysisResult Impure(SyntaxNode impureSyntaxNode)
            {

                if (impureSyntaxNode == null)
                {
                    throw new ArgumentNullException(nameof(impureSyntaxNode), "Use ImpureUnknownLocation for impurity without a specific node.");
                }
                return new PurityAnalysisResult(false, impureSyntaxNode);
            }


            public static PurityAnalysisResult ImpureUnknownLocation => new PurityAnalysisResult(false, null);
        }







        internal readonly struct PurityAnalysisState : IEquatable<PurityAnalysisState>
        {

            public bool HasPotentialImpurity { get; }
            public SyntaxNode? FirstImpureSyntaxNode { get; }




            public ImmutableDictionary<ISymbol, PotentialTargets> DelegateTargetMap { get; }


            internal PurityAnalysisState(
                bool hasPotentialImpurity,
                SyntaxNode? firstImpureSyntaxNode,
                ImmutableDictionary<ISymbol, PotentialTargets>? delegateTargetMap)
            {
                HasPotentialImpurity = hasPotentialImpurity;
                FirstImpureSyntaxNode = firstImpureSyntaxNode;

                DelegateTargetMap = delegateTargetMap ?? ImmutableDictionary.Create<ISymbol, PotentialTargets>(SymbolEqualityComparer.Default);
            }


            public static PurityAnalysisState Pure => new PurityAnalysisState(false, null, null);


            public static PurityAnalysisState Merge(IEnumerable<PurityAnalysisState> states)
            {
                bool mergedImpurity = false;
                SyntaxNode? firstImpureNode = null;

                var mergedTargetsBuilder = ImmutableDictionary.CreateBuilder<ISymbol, PotentialTargets>(SymbolEqualityComparer.Default);
                var keysProcessed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

                foreach (var state in states)
                {

                    if (state.HasPotentialImpurity)
                    {
                        mergedImpurity = true;
                        if (firstImpureNode == null) { firstImpureNode = state.FirstImpureSyntaxNode; }
                    }


                    foreach (var kvp in state.DelegateTargetMap)
                    {
                        var symbol = kvp.Key;
                        var currentTargets = kvp.Value;

                        if (mergedTargetsBuilder.TryGetValue(symbol, out var existingTargets))
                        {

                            if (!keysProcessed.Contains(symbol))
                            {
                                mergedTargetsBuilder[symbol] = PotentialTargets.Merge(existingTargets, currentTargets);
                                keysProcessed.Add(symbol);
                            }
                        }
                        else
                        {

                            mergedTargetsBuilder.Add(symbol, currentTargets);
                            keysProcessed.Add(symbol);
                        }
                    }
                    keysProcessed.Clear();
                }

                return new PurityAnalysisState(mergedImpurity, firstImpureNode, mergedTargetsBuilder.ToImmutable());
            }


            public bool Equals(PurityAnalysisState other)
            {
                if (this.HasPotentialImpurity != other.HasPotentialImpurity ||
                    !object.Equals(this.FirstImpureSyntaxNode, other.FirstImpureSyntaxNode) ||
                    this.DelegateTargetMap.Count != other.DelegateTargetMap.Count)
                {
                    return false;
                }



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


            public override int GetHashCode()
            {

                int hash = 17;
                hash = hash * 23 + HasPotentialImpurity.GetHashCode();
                hash = hash * 23 + (FirstImpureSyntaxNode?.GetHashCode() ?? 0);

                foreach (var kvp in DelegateTargetMap.OrderBy(kv => kv.Key.Name))
                {
                    hash = hash * 23 + SymbolEqualityComparer.Default.GetHashCode(kvp.Key);
                    hash = hash * 23 + kvp.Value.GetHashCode();
                }
                return hash;
            }

            public static bool operator ==(PurityAnalysisState left, PurityAnalysisState right) => left.Equals(right);
            public static bool operator !=(PurityAnalysisState left, PurityAnalysisState right) => !(left == right);


            public PurityAnalysisState WithImpurity(SyntaxNode node)
            {
                if (HasPotentialImpurity) return this;
                return new PurityAnalysisState(true, node, this.DelegateTargetMap);
            }

            public PurityAnalysisState WithDelegateTarget(ISymbol delegateSymbol, PotentialTargets targets)
            {

                var newMap = this.DelegateTargetMap.SetItem(delegateSymbol, targets);
                return new PurityAnalysisState(this.HasPotentialImpurity, this.FirstImpureSyntaxNode, newMap);
            }
        }


        internal readonly struct PotentialTargets : IEquatable<PotentialTargets>
        {


            public ImmutableHashSet<IMethodSymbol> MethodSymbols { get; }



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


            public static PotentialTargets Merge(PotentialTargets first, PotentialTargets second)
            {
                return new PotentialTargets(first.MethodSymbols.Union(second.MethodSymbols));
            }

            public bool Equals(PotentialTargets other)
            {

                return this.MethodSymbols.SetEquals(other.MethodSymbols);
            }

            public override bool Equals(object obj) => obj is PotentialTargets other && Equals(other);

            public override int GetHashCode()
            {
                int hash = 17;
                foreach (var symbol in MethodSymbols.OrderBy(s => s.Name))
                {
                    hash = hash * 23 + SymbolEqualityComparer.Default.GetHashCode(symbol);
                }
                return hash;
            }
        }


        internal PurityAnalysisResult IsConsideredPure(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {




            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var purityCache = new Dictionary<IMethodSymbol, PurityAnalysisResult>(SymbolEqualityComparer.Default);

            LogDebug($">> Enter DeterminePurity: {methodSymbol.ToDisplayString(_signatureFormat)}");


            var result = DeterminePurityRecursiveInternal(
                methodSymbol,
                semanticModel,
                enforcePureAttributeSymbol,
                allowSynchronizationAttributeSymbol,
                visited,
                purityCache
            );

            LogDebug($"<< Exit DeterminePurity ({GetPuritySource(result)}): {methodSymbol.ToDisplayString(_signatureFormat)}, Final IsPure={result.IsPure}");
            LogDebug($"-- Removed Walker for: {methodSymbol.ToDisplayString(_signatureFormat)}");


            purityCache[methodSymbol] = result;

            return result;
        }


        private static string GetPuritySource(PurityAnalysisResult result)
        {

            if (result.IsPure) return "Assumed/Analyzed Pure";
            if (result.ImpureSyntaxNode != null) return "Analyzed Impure";

            return "Unknown/Default Impure";
        }


        internal static PurityAnalysisResult DeterminePurityRecursiveInternal(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {

            var indent = new string(' ', visited.Count * 2);
            LogDebug($"{indent}>> Enter DeterminePurity: {methodSymbol.ToDisplayString()}");



            if (purityCache.TryGetValue(methodSymbol, out var cachedResult))
            {
                LogDebug($"{indent}  Purity CACHED: {cachedResult.IsPure} for {methodSymbol.ToDisplayString()}");
                LogDebug($"{indent}<< Exit DeterminePurity (Cached): {methodSymbol.ToDisplayString()}");
                return cachedResult;
            }


            if (!visited.Add(methodSymbol))
            {
                LogDebug($"{indent}  Recursion DETECTED for {methodSymbol.ToDisplayString()}. Assuming impure for this path.");
                purityCache[methodSymbol] = PurityAnalysisResult.ImpureUnknownLocation;
                LogDebug($"{indent}<< Exit DeterminePurity (Recursion): {methodSymbol.ToDisplayString()}");
                return PurityAnalysisResult.ImpureUnknownLocation;
            }

            try
            {

                if (IsKnownImpure(methodSymbol))
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} is known impure.");
                    var knownImpureResult = ImpureResult(null);
                    purityCache[methodSymbol] = knownImpureResult;
                    LogDebug($"{indent}<< Exit DeterminePurity (Known Impure): {methodSymbol.ToDisplayString()}");
                    return knownImpureResult;
                }


                if (IsKnownPureBCLMember(methodSymbol))
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} is known pure BCL member.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    LogDebug($"{indent}<< Exit DeterminePurity (Known Pure): {methodSymbol.ToDisplayString()}");
                    return PurityAnalysisResult.Pure;
                }


                SyntaxNode? bodySyntaxNode = GetBodySyntaxNode(methodSymbol, default);


                if (methodSymbol.ReturnsByRef)
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} returns by ref. IMPURE.");

                    SyntaxNode? locationSyntax = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.DescendantNodesAndSelf()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.RefTypeSyntax>()
                        .FirstOrDefault();

                    locationSyntax ??= methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.DescendantNodesAndSelf()
                                            .FirstOrDefault(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax ins && ins.Identifier.ValueText == methodSymbol.Name)
                                            ?.Parent;

                    purityCache[methodSymbol] = ImpureResult(locationSyntax ?? bodySyntaxNode);
                    LogDebug($"{indent}<< Exit DeterminePurity (ReturnsByRef): {methodSymbol.ToDisplayString()}");
                    return purityCache[methodSymbol];
                }



                if (methodSymbol.IsAbstract || methodSymbol.IsExtern || bodySyntaxNode == null)
                {
                    LogDebug($"{indent}Method {methodSymbol.ToDisplayString()} is abstract, extern, or has no body AND not known impure/pure. Assuming pure.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    LogDebug($"{indent}<< Exit DeterminePurity (Abstract/Extern/NoBody): {methodSymbol.ToDisplayString()}");
                    return PurityAnalysisResult.Pure;
                }


                PurityAnalysisResult result = PurityAnalysisResult.Pure;
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

                    LogDebug($"{indent}  CFG Analysis Result for {methodSymbol.ToDisplayString()}: IsPure={result.IsPure}, ImpureNode={result.ImpureSyntaxNode?.Kind()}");

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


                if (result.IsPure)
                {
                    LogDebug($"{indent}Post-CFG: CFG Result was Pure. Performing Post-CFG checks for {methodSymbol.ToDisplayString()}.");

                    if (methodBodyIOperation != null)
                    {
                        var pureAttrSymbolForContext = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");
                        var postCfgContext = new Rules.PurityAnalysisContext(
                            semanticModel,
                            enforcePureAttributeSymbol,
                            pureAttrSymbolForContext,
                            allowSynchronizationAttributeSymbol,
                            visited,
                            purityCache,
                            methodSymbol,
                            _purityRules,
                            CancellationToken.None,
                            null);


                        LogDebug($"{indent}  Post-CFG: Checking ReturnOperations...");
                        foreach (var returnOp in methodBodyIOperation.DescendantsAndSelf().OfType<IReturnOperation>())
                        {
                            if (returnOp.ReturnedValue != null)
                            {

                                var returnPurity = CheckSingleOperation(returnOp.ReturnedValue, postCfgContext, PurityAnalysisState.Pure);
                                if (!returnPurity.IsPure)
                                {
                                    LogDebug($"{indent}    Post-CFG: Return value IMPURE: {returnOp.ReturnedValue.Syntax}");
                                    result = returnPurity;
                                    goto PostCfgChecksDone;
                                }
                            }
                        }
                        LogDebug($"{indent}  Post-CFG: ReturnOperations check complete (result still pure).");


                        LogDebug($"{indent}  Post-CFG: Checking ThrowOperations...");
                        var firstThrowOp = methodBodyIOperation.DescendantsAndSelf().OfType<IThrowOperation>().FirstOrDefault();
                        if (firstThrowOp != null)
                        {
                            LogDebug($"{indent}    Post-CFG: Found ThrowOperation IMPURE: {firstThrowOp.Syntax}");
                            result = PurityAnalysisResult.Impure(firstThrowOp.Syntax);
                            goto PostCfgChecksDone;
                        }
                        LogDebug($"{indent}  Post-CFG: ThrowOperations check complete (result still pure).");


                        LogDebug($"{indent}  Post-CFG: Checking Known Impure Invocations...");
                        foreach (var invocationOp in methodBodyIOperation.DescendantsAndSelf().OfType<IInvocationOperation>())
                        {
                            if (invocationOp.TargetMethod != null && IsKnownImpure(invocationOp.TargetMethod.OriginalDefinition))
                            {
                                LogDebug($"{indent}    Post-CFG: Found Known Impure Invocation IMPURE: {invocationOp.Syntax} calling {invocationOp.TargetMethod.ToDisplayString()}");
                                result = PurityAnalysisResult.Impure(invocationOp.Syntax);
                                goto PostCfgChecksDone;
                            }
                        }
                        LogDebug($"{indent}  Post-CFG: Known Impure Invocations check complete (result still pure).");


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
                                    goto PostCfgChecksDone;
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


                purityCache[methodSymbol] = result;
                LogDebug($"{indent}<< Exit DeterminePurity (Analyzed): {methodSymbol.ToDisplayString()}, Final IsPure={result.IsPure}");
                return result;
            }
            finally
            {
                visited.Remove(methodSymbol);
                LogDebug($"{indent}-- Removed Walker for: {methodSymbol.ToDisplayString()}");
            }
        }


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
                return PurityAnalysisResult.Impure(bodyNode);
            }

            if (cfg == null || cfg.Blocks.IsEmpty)
            {
                LogDebug($"CFG is null or empty for {containingMethodSymbol.ToDisplayString()}. Assuming pure (no operations).");
                return PurityAnalysisResult.Pure;
            }


            LogDebug($"  [CFG] Created CFG with {cfg.Blocks.Length} blocks for {containingMethodSymbol.ToDisplayString()}.");


            var blockStates = new Dictionary<BasicBlock, PurityAnalysisState>(cfg.Blocks.Length);
            var worklist = new Queue<BasicBlock>();


            LogDebug("  [CFG] Initializing CFG block states to Pure.");
            foreach (var block in cfg.Blocks)
            {
                blockStates[block] = PurityAnalysisState.Pure;
            }
            if (cfg.Blocks.Any())
            {
                var entryBlock = cfg.Blocks.First();

                LogDebug($"  [CFG] Adding Entry Block #{entryBlock.Ordinal} to worklist.");
                worklist.Enqueue(entryBlock);
            }
            else
            {
                LogDebug("  [CFG] CFG has no blocks. Exiting analysis.");
                return PurityAnalysisResult.Pure;
            }


            LogDebug("  [CFG] Starting CFG dataflow analysis worklist loop.");
            int loopIterations = 0;

            LogDebug($"  [CFG] BEFORE WHILE CHECK: worklist.Count = {worklist.Count}, loopIterations = {loopIterations}");
            while (worklist.Count > 0 && loopIterations < cfg.Blocks.Length * 5)
            {

                LogDebug("  [CFG] ENTERED WHILE LOOP.");
                loopIterations++;

                LogDebug($"  [CFG] Worklist count: {worklist.Count}. Iteration: {loopIterations}");
                var currentBlock = worklist.Dequeue();
                LogDebug($"  [CFG] Processing CFG Block #{currentBlock.Ordinal}");

                var stateBefore = blockStates[currentBlock];

                LogDebug($"  [CFG] StateBefore for Block #{currentBlock.Ordinal}: Impure={stateBefore.HasPotentialImpurity}");


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



                LogDebug($"  [CFG] Propagating stateAfter (Impure={stateAfter.HasPotentialImpurity}) to successors of Block #{currentBlock.Ordinal}.");
                PropagateToSuccessor(currentBlock.ConditionalSuccessor?.Destination, stateAfter, blockStates, worklist);
                PropagateToSuccessor(currentBlock.FallThroughSuccessor?.Destination, stateAfter, blockStates, worklist);

            }

            if (worklist.Count == 0)
            {
                LogDebug("  [CFG] Finished CFG dataflow analysis worklist loop (worklist empty).");
            }
            else
            {
                LogDebug($"  [CFG] WARNING: Exited CFG dataflow loop due to iteration limit ({loopIterations}). Potential infinite loop?");
            }


            PurityAnalysisResult finalResult;
            BasicBlock? exitBlock = cfg.Blocks.LastOrDefault(b => b.Kind == BasicBlockKind.Exit);

            if (exitBlock != null && blockStates.TryGetValue(exitBlock, out var exitState))
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Exit Block #{exitBlock.Ordinal} Initial State: HasImpurity={exitState.HasPotentialImpurity}, Node={exitState.FirstImpureSyntaxNode?.Kind()}, NodeText='{exitState.FirstImpureSyntaxNode?.ToString()}'");


                if (!exitState.HasPotentialImpurity)
                {
                    LogDebug($"  [CFG] Exit block state is pure. Explicitly checking operations within Exit Block #{exitBlock.Ordinal}.");
                    var pureAttrSymbolForContext = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

                    var ruleContext = new Rules.PurityAnalysisContext(
                        semanticModel,
                        enforcePureAttributeSymbol,
                        pureAttrSymbolForContext,
                        allowSynchronizationAttributeSymbol,
                        visited,
                        purityCache,
                        containingMethodSymbol,
                        _purityRules,
                        CancellationToken.None,
                        null);

                    foreach (var exitOp in exitBlock.Operations)
                    {
                        if (exitOp == null) continue;
                        LogDebug($"    [CFG] Checking exit operation: {exitOp.Kind} - '{exitOp.Syntax}'");
                        var opResult = CheckSingleOperation(exitOp, ruleContext, exitState);
                        if (!opResult.IsPure)
                        {
                            LogDebug($"    [CFG] Exit operation {exitOp.Kind} found IMPURE. Updating final result.");


                            exitState = exitState.WithImpurity(opResult.ImpureSyntaxNode ?? exitOp.Syntax);
                            break;
                        }
                    }
                    if (!exitState.HasPotentialImpurity)
                    {
                        LogDebug($"  [CFG] All exit block operations checked and found pure.");
                    }
                }



                if (exitState.HasPotentialImpurity)
                {

                    finalResult = exitState.FirstImpureSyntaxNode != null
                        ? PurityAnalysisResult.Impure(exitState.FirstImpureSyntaxNode)
                        : PurityAnalysisResult.ImpureUnknownLocation;
                    LogDebug($"  [CFG] Final Result: IMPURE. Node={finalResult.ImpureSyntaxNode?.Kind() ?? (object)"NULL"}");
                }
                else
                {
                    finalResult = PurityAnalysisResult.Pure;
                    LogDebug($"  [CFG] Final Result: PURE.");
                }
            }
            else if (exitBlock != null)
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Could not get state for the exit block #{exitBlock.Ordinal}. Assuming impure (unreachable?).");
                finalResult = PurityAnalysisResult.ImpureUnknownLocation;
            }
            else
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: No reachable exit block found. Assuming pure.");
                finalResult = PurityAnalysisResult.Pure;
            }

            return finalResult;
        }


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
                CancellationToken.None,
                null);


            var currentStateInBlock = stateBefore;
            foreach (var op in block.Operations)
            {
                if (op == null) continue;

                LogDebug($"    [ATF Block {block.Ordinal}] Checking Op Kind: {op.Kind}, Syntax: {op.Syntax.ToString().Replace("\r\n", " ").Replace("\n", " ")}");


                var opResult = CheckSingleOperation(op, ruleContext, currentStateInBlock);

                if (!opResult.IsPure)
                {
                    LogDebug($"ApplyTransferFunction IMPURE DETECTED in Block #{block.Ordinal} by Op: {op.Kind} ({op.Syntax})");

                    currentStateInBlock = currentStateInBlock.WithImpurity(opResult.ImpureSyntaxNode ?? op.Syntax);
                    break;
                }


                LogDebug($"  [ApplyTF] Before UpdateDelegateMapForOperation: StateImpure={currentStateInBlock.HasPotentialImpurity}, MapCount={currentStateInBlock.DelegateTargetMap.Count}");
                currentStateInBlock = UpdateDelegateMapForOperation(op, ruleContext, currentStateInBlock);
                LogDebug($"  [ApplyTF] After UpdateDelegateMapForOperation: StateImpure={currentStateInBlock.HasPotentialImpurity}, MapCount={currentStateInBlock.DelegateTargetMap.Count}");

            }

            LogDebug($"ApplyTransferFunction END for Block #{block.Ordinal} - Final State: Impure={currentStateInBlock.HasPotentialImpurity}");
            return currentStateInBlock;
        }


        internal static PurityAnalysisResult CheckSingleOperation(IOperation operation, Rules.PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            LogDebug($"    [CSO] Enter CheckSingleOperation for Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");
            LogDebug($"    [CSO] Current DFA State: Impure={currentState.HasPotentialImpurity}, MapCount={currentState.DelegateTargetMap.Count}");



            if (operation.Kind == OperationKind.FlowCaptureReference || operation.Kind == OperationKind.FlowCapture)
            {
                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure - FlowCapture/Reference)");
                return PurityAnalysisResult.Pure;
            }


            bool isChecked = false;
            IMethodSymbol? operatorMethod = null;

            if (operation is IBinaryOperation binaryOp && binaryOp.IsChecked)
            {
                LogDebug($"    [CSO] Found Checked Binary Operation: {operation.Syntax}");
                isChecked = true;
                operatorMethod = binaryOp.OperatorMethod;


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


                if (operatorMethod != null)
                {

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


                if (context.ContainingMethodSymbol != null &&
                    IsPureEnforced(context.ContainingMethodSymbol, context.EnforcePureAttributeSymbol))
                {
                    LogDebug($"    [CSO] Checked operation is part of a method marked with [EnforcePure]. Checking purity of containing method.");


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


                LogDebug($"    [CSO] Checked operation is Pure.");
                return PurityAnalysisResult.Pure;
            }


            var applicableRule = _purityRules.FirstOrDefault(rule => rule.ApplicableOperationKinds.Contains(operation.Kind));

            if (applicableRule != null)
            {

                LogDebug($"    [CSO] Applying Rule '{applicableRule.GetType().Name}' to Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");

                var ruleResult = applicableRule.CheckPurity(operation, context, currentState);

                LogDebug($"    [CSO] Rule '{applicableRule.GetType().Name}' Result: IsPure={ruleResult.IsPure}");
                if (!ruleResult.IsPure)
                {

                    if (ruleResult.ImpureSyntaxNode == null)
                    {
                        LogDebug($"    [CSO] Rule '{applicableRule.GetType().Name}' returned impure result without syntax node. Using current operation syntax: {operation.Syntax}");

                        return operation.Syntax != null
                               ? PurityAnalysisResult.Impure(operation.Syntax)
                               : PurityAnalysisResult.ImpureUnknownLocation;
                    }
                    LogDebug($"    [CSO] Exit CheckSingleOperation (Impure from rule)");
                    return ruleResult;
                }

                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure from rule)");
                return PurityAnalysisResult.Pure;
            }
            else
            {

                LogDebug($"    [CSO] No rule found for operation kind {operation.Kind}. Defaulting to impure. Syntax: '{operation.Syntax.ToString().Trim()}'");
                LogDebug($"    [CSO] Exit CheckSingleOperation (Impure default)");
                return ImpureResult(operation.Syntax);
            }
        }






        internal static bool IsKnownPureBCLMember(ISymbol symbol) => ImpurityCatalog.IsKnownPureBCLMember(symbol);


        internal static bool IsKnownImpure(ISymbol symbol) => ImpurityCatalog.IsKnownImpure(symbol);


        internal static PurityAnalysisResult GetCalleePurity(
            IMethodSymbol methodSymbol,
            Rules.PurityAnalysisContext context)
        {
            if (context.PurityService != null)
            {
                return context.PurityService.GetPurity(
                    methodSymbol.OriginalDefinition,
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol);
            }

            return DeterminePurityRecursiveInternal(
                methodSymbol.OriginalDefinition,
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache);
        }



        internal static bool IsInImpureNamespaceOrType(ISymbol symbol) => ImpurityCatalog.IsInImpureNamespaceOrType(symbol);



        internal static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            if (symbol == null || enforcePureAttributeSymbol == null)
            {
                return false;
            }

            var pureAttributeSymbol = symbol.ContainingAssembly.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");
            return symbol.GetAttributes().Any(ad =>
                SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol) ||
                (pureAttributeSymbol != null && SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, pureAttributeSymbol))
            );
        }


        internal static PurityAnalysisResult ImpureResult(SyntaxNode? syntaxNode)
        {
            return syntaxNode != null ? PurityAnalysisResult.Impure(syntaxNode) : PurityAnalysisResult.ImpureUnknownLocation;
        }


        internal static void LogDebug(string message)
        {
#if DEBUG
            // Intentionally no-op in Release builds; keep minimal in Debug.
#endif
        }


        private static SyntaxNode? GetBodySyntaxNode(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {

            var declaringSyntaxes = methodSymbol.DeclaringSyntaxReferences;
            LogDebug($"  [GetBody] Checking {declaringSyntaxes.Length} declaring syntax refs for {methodSymbol.Name}");
            foreach (var syntaxRef in declaringSyntaxes)
            {
                var syntaxNode = syntaxRef.GetSyntax(cancellationToken);
                LogDebug($"  [GetBody]   SyntaxRef {syntaxRef.Span} yielded SyntaxNode of Kind: {syntaxNode?.Kind()}");


                if (syntaxNode is MethodDeclarationSyntax ||
                    syntaxNode is LocalFunctionStatementSyntax ||
                    syntaxNode is AccessorDeclarationSyntax ||
                    syntaxNode is ConstructorDeclarationSyntax ||
                    syntaxNode is OperatorDeclarationSyntax ||
                    syntaxNode is ConversionOperatorDeclarationSyntax)
                {
                    LogDebug($"  [GetBody]   Found usable body node of Kind: {syntaxNode.Kind()}");
                    return syntaxNode;
                }
            }
            LogDebug($"  [GetBody] No usable body node found for {methodSymbol.Name}.");
            return null;
        }


        private static void PropagateToSuccessor(BasicBlock? successor, PurityAnalysisState newState, Dictionary<BasicBlock, PurityAnalysisState> blockStates, Queue<BasicBlock> worklist)
        {
            if (successor == null) return;


            bool previouslyVisited = blockStates.TryGetValue(successor, out var existingState);


            var mergedState = MergeStates(existingState, newState);


            bool stateChanged = !mergedState.Equals(existingState);











            if (stateChanged)
            {
                LogDebug($"PropagateToSuccessor: State changed for Block #{successor.Ordinal} from Impure={existingState.HasPotentialImpurity} to Impure={mergedState.HasPotentialImpurity}. Updating state.");
                blockStates[successor] = mergedState;
            }
            else
            {

                if (!previouslyVisited)
                {
                    blockStates[successor] = mergedState;
                }

                LogDebug($"PropagateToSuccessor: State unchanged for Block #{successor.Ordinal} (Impure={existingState.HasPotentialImpurity}).");
            }



            if (stateChanged || !worklist.Contains(successor))
            {
                if (!worklist.Contains(successor))
                {
                    LogDebug($"PropagateToSuccessor: Enqueuing Block #{successor.Ordinal} (State Changed: {stateChanged}).");
                    worklist.Enqueue(successor);
                }
                else
                {


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


        private static PurityAnalysisState MergeStates(PurityAnalysisState state1, PurityAnalysisState state2)
        {
            LogDebug($"  [Merge] Merging States: S1(Impure={state1.HasPotentialImpurity}, MapCount={state1.DelegateTargetMap.Count}) + S2(Impure={state2.HasPotentialImpurity}, MapCount={state2.DelegateTargetMap.Count})");
            bool mergedImpurity = state1.HasPotentialImpurity || state2.HasPotentialImpurity;
            SyntaxNode? firstImpureNode = state1.FirstImpureSyntaxNode;
            if (state1.HasPotentialImpurity && state2.HasPotentialImpurity && state1.FirstImpureSyntaxNode != null && state2.FirstImpureSyntaxNode != null)
            {

                if (state2.FirstImpureSyntaxNode.SpanStart < state1.FirstImpureSyntaxNode.SpanStart)
                {
                    firstImpureNode = state2.FirstImpureSyntaxNode;
                }
            }
            else if (state2.HasPotentialImpurity)
            {
                firstImpureNode = state2.FirstImpureSyntaxNode;
            }


            var mapBuilder = ImmutableDictionary.CreateBuilder<ISymbol, PotentialTargets>(SymbolEqualityComparer.Default);

            foreach (var kvp in state1.DelegateTargetMap)
            {
                mapBuilder.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in state2.DelegateTargetMap)
            {
                if (mapBuilder.TryGetValue(kvp.Key, out var existingTargets))
                {

                    mapBuilder[kvp.Key] = PotentialTargets.Merge(existingTargets, kvp.Value);
                }
                else
                {

                    mapBuilder.Add(kvp.Key, kvp.Value);
                }
            }
            var finalMap = mapBuilder.ToImmutable();


            return new PurityAnalysisState(mergedImpurity, firstImpureNode, finalMap);
        }


        internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
        {
            if (attributeSymbol == null) return false;
            return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, attributeSymbol.OriginalDefinition));
        }



        internal static PurityAnalysisResult CheckStaticConstructorPurity(ITypeSymbol? typeSymbol, Rules.PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            if (typeSymbol == null)
            {
                return PurityAnalysisResult.Pure;
            }


            IMethodSymbol? staticConstructor = typeSymbol.GetMembers(".cctor").OfType<IMethodSymbol>().FirstOrDefault();

            if (staticConstructor == null)
            {
                LogDebug($"    [CctorCheck] Type {typeSymbol.Name} has no static constructor. Pure.");
                return PurityAnalysisResult.Pure;
            }

            LogDebug($"    [CctorCheck] Found static constructor for {typeSymbol.Name}. Checking purity recursively...");




            var cctorResult = DeterminePurityRecursiveInternal(
                staticConstructor.OriginalDefinition,
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache
            );

            LogDebug($"    [CctorCheck] Static constructor purity result for {typeSymbol.Name}: IsPure={cctorResult.IsPure}");




            return cctorResult.IsPure
                ? PurityAnalysisResult.Pure
                : PurityAnalysisResult.Impure(cctorResult.ImpureSyntaxNode ?? typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? ImpureResult(null).ImpureSyntaxNode ?? context.ContainingMethodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? throw new InvalidOperationException("Cannot find syntax node for static constructor impurity"));
        }


        private static PurityAnalysisState UpdateDelegateMapForOperation(IOperation op, Rules.PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            LogDebug($"  [UpdMap] Trying Update: OpKind={op.Kind}, CurrentImpure={currentState.HasPotentialImpurity}");

            PurityAnalysisState nextState = currentState;


                if (op is IAssignmentOperation assignmentOperation)
                {
                    var targetOperation = assignmentOperation.Target;
                    var valueOperation = assignmentOperation.Value;
                    var targetSymbol = TryResolveSymbol(targetOperation);

                    if (valueOperation != null && targetSymbol != null && targetOperation.Type?.TypeKind == TypeKind.Delegate)
                    {
                        PurityAnalysisEngine.PotentialTargets? valueTargets = ResolvePotentialTargets(valueOperation, currentState);
                        if (valueTargets != null)
                        {
                            nextState = currentState.WithDelegateTarget(targetSymbol, valueTargets.Value);
                            LogDebug($"    [ATF-DEL-ASSIGN] Updated map for {targetSymbol.Name} with {valueTargets.Value.MethodSymbols.Count} targets. New Map Count: {nextState.DelegateTargetMap.Count}");
                        }
                    }
                }

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
                                    PurityAnalysisEngine.PotentialTargets? valueTargets = ResolvePotentialTargets(initializerValue, currentState);
                                    if (valueTargets != null)
                                    {
                                        nextState = nextState.WithDelegateTarget(declaredSymbol, valueTargets.Value);
                                        LogDebug($"    [ATF-DEL-VAR] Updated map for {declaredSymbol.Name} with {valueTargets.Value.MethodSymbols.Count} targets. New Map Count: {nextState.DelegateTargetMap.Count}");
                                    }
                                }
                            }
                        }
                    }
                }


            return nextState;
        }


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
            else
            {
                ISymbol? valueSourceSymbol = TryResolveSymbol(valueOperation);
                if (valueSourceSymbol != null && currentState.DelegateTargetMap.TryGetValue(valueSourceSymbol, out var sourceTargets))
                {
                    return sourceTargets;
                }
            }
            return null;
        }


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