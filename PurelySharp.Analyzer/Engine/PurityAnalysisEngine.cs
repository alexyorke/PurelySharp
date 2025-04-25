using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Contains the core logic for determining method purity using Control Flow Graph (CFG).
    /// </summary>
    internal static class PurityAnalysisEngine
    {
        // Add a set of known impure method signatures
        private static readonly HashSet<string> KnownImpureMethods = new HashSet<string>
        {
            "System.IO.File.WriteAllText",
            "System.DateTime.Now.get", // Property getters are methods like get_Now
            "System.DateTime.UtcNow.get",
            "System.Console.WriteLine", // Add more console methods
            "System.Console.Write",
            "System.Random.Next",
            // Add more known impure methods here
        };

        /// <summary>
        /// Represents the purity state during CFG analysis.
        /// </summary>
        private struct PurityAnalysisState : System.IEquatable<PurityAnalysisState>
        {
            public bool HasPotentialImpurity { get; set; }

            // Default state (pure)
            public static PurityAnalysisState Pure => new PurityAnalysisState { HasPotentialImpurity = false };
            // Impure state
            public static PurityAnalysisState Impure => new PurityAnalysisState { HasPotentialImpurity = true };

            // Merge function (logical OR)
            public static PurityAnalysisState Merge(IEnumerable<PurityAnalysisState> states)
            {
                bool mergedImpurity = false;
                foreach (var state in states)
                {
                    if (state.HasPotentialImpurity)
                    {
                        mergedImpurity = true;
                        break;
                    }
                }
                return new PurityAnalysisState { HasPotentialImpurity = mergedImpurity };
            }

            public bool Equals(PurityAnalysisState other) => this.HasPotentialImpurity == other.HasPotentialImpurity;
            public override bool Equals(object obj) => obj is PurityAnalysisState other && Equals(other);
            public override int GetHashCode() => HasPotentialImpurity.GetHashCode();
            public static bool operator ==(PurityAnalysisState left, PurityAnalysisState right) => left.Equals(right);
            public static bool operator !=(PurityAnalysisState left, PurityAnalysisState right) => !(left == right);
        }

        /// <summary>
        /// Checks if a method symbol is considered pure based on its implementation using CFG data-flow analysis.
        /// Manages the visited set for cycle detection across the entire analysis.
        /// </summary>
        internal static bool IsConsideredPure(IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            // Initialize cache and visited set for this top-level analysis call
            var purityCache = new Dictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default);
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            return DeterminePurityRecursive(methodSymbol.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache);
        }

        /// <summary>
        /// Recursive helper for purity determination. Handles caching and cycle detection.
        /// </summary>
        private static bool DeterminePurityRecursive(
            IMethodSymbol methodSymbol,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            Dictionary<IMethodSymbol, bool> purityCache)
        {
            // --- Cache Check ---
            if (purityCache.TryGetValue(methodSymbol, out bool cachedResult))
            {
                return cachedResult;
            }

            // --- Cycle Detection ---
            if (!visited.Add(methodSymbol))
            {
                // Cycle detected. Assume impure *within this path*, but don't permanently cache it as impure yet,
                // as other paths might prove it pure. Let the caller handle caching.
                return false; // Assume impure due to cycle
            }

            bool isPure;
            try // Ensure visited is cleaned up even if exceptions occur
            {
                // --- Initial Checks (Attributes, Kind, No Body) ---
                // Applying an attribute involves running its constructor. If the constructor is impure,
                // the method application itself introduces impurity.
                foreach (var attributeData in methodSymbol.GetAttributes())
                {
                    if (attributeData.AttributeConstructor != null)
                    {
                        // Need to create a new visited set for this recursive check to avoid incorrect cycle detection between attribute analysis and method analysis.
                        // No need to pass cache here? Or maybe share cache? Share for now.
                        var attributeVisited = new HashSet<IMethodSymbol>(visited, SymbolEqualityComparer.Default);
                        if (!DeterminePurityRecursive(attributeData.AttributeConstructor.OriginalDefinition, context, enforcePureAttributeSymbol, attributeVisited, purityCache))
                        {
                            isPure = false;
                            goto EndAnalysis; // Attribute constructor is impure
                        }
                    }
                }

                // --- Find Declaration Syntax (still needed to get the body node) ---
                SyntaxNode? bodyNode = null;
                foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
                {
                    var syntax = syntaxRef.GetSyntax(context.CancellationToken);
                    if (syntax is MethodDeclarationSyntax methodDecl) { bodyNode = (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody?.Expression; }
                    else if (syntax is AccessorDeclarationSyntax accessorDecl) { bodyNode = (SyntaxNode?)accessorDecl.Body ?? accessorDecl.ExpressionBody?.Expression; }
                    else if (syntax is ConstructorDeclarationSyntax ctorDecl) { bodyNode = (SyntaxNode?)ctorDecl.Body ?? ctorDecl.ExpressionBody?.Expression; }
                    else if (syntax is LocalFunctionStatementSyntax localFunc) { bodyNode = (SyntaxNode?)localFunc.Body ?? localFunc.ExpressionBody?.Expression; }
                    // TODO: Add support for OperatorDeclarationSyntax, ConversionOperatorDeclarationSyntax?
                    if (bodyNode != null) break;
                }

                if (bodyNode == null)
                {
                    // No implementation found (e.g., abstract, partial without definition, interface method?)
                    if (methodSymbol.MethodKind == MethodKind.Constructor && methodSymbol.Parameters.Length == 0 && !methodSymbol.IsStatic && methodSymbol.ContainingType.IsValueType) // Default struct ctor
                    { isPure = true; goto EndAnalysis; }
                    if (methodSymbol.MethodKind == MethodKind.Constructor && methodSymbol.Parameters.Length == 0 && !methodSymbol.IsStatic && methodSymbol.ContainingType.IsReferenceType && methodSymbol.IsImplicitlyDeclared) // Implicit reference type ctor
                    { isPure = true; goto EndAnalysis; }


                    // Check for interface methods FIRST
                    if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface && !methodSymbol.IsStatic)
                    {
                        // Interface methods without a direct body: Trust the [Pure] attribute if present, otherwise assume impure.
                        isPure = IsPureEnforced(methodSymbol, enforcePureAttributeSymbol);
                        goto EndAnalysis;
                    }

                    if (methodSymbol.IsAbstract) { isPure = true; goto EndAnalysis; } // Abstract methods assumed pure contractually
                    if (methodSymbol.IsExtern) { isPure = false; goto EndAnalysis; } // Extern methods assumed impure

                    // Other cases without body (partial?) - assume impure for safety
                    isPure = false;
                    goto EndAnalysis;
                }

                // --- Analyze Body using CFG ---
                isPure = AnalyzePurityUsingCFG(bodyNode, context, enforcePureAttributeSymbol, visited, methodSymbol, purityCache);

            }
            finally
            {
                // --- Backtrack ---
                visited.Remove(methodSymbol);
            }

        EndAnalysis:
            // --- Cache Result ---
            purityCache[methodSymbol] = isPure;
            return isPure;
        }

        /// <summary>
        /// Analyzes method purity using a Control Flow Graph (CFG).
        /// Implements the iterative data-flow analysis.
        /// </summary>
        private static bool AnalyzePurityUsingCFG(
            SyntaxNode bodyNode,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited, // Passed for recursive calls in transfer function
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, bool> purityCache) // Passed for recursive calls
        {
            // Use SemanticModel.AnalyzeControlFlow which returns ControlFlowAnalysisResult
            ControlFlowAnalysisResult cfgResult;
            try
            {
                cfgResult = context.SemanticModel.AnalyzeControlFlow(bodyNode);
            }
            catch (System.Exception ex) // Handle potential exceptions during CFG analysis
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing control flow for {containingMethodSymbol.ToDisplayString()}: {ex.Message}");
                return false; // Treat analysis failure as impure
            }

            // Check if analysis succeeded and if entry/exit blocks exist
            if (!cfgResult.Succeeded || cfgResult.EntryBasicBlock == null || cfgResult.ExitBasicBlock == null)
            {
                System.Diagnostics.Debug.WriteLine($"Control flow analysis failed or produced invalid result for {containingMethodSymbol.ToDisplayString()} despite having body node.");
                return false;
            }

            // Access properties directly from the non-nullable struct
            var blocks = cfgResult.BasicBlocks;
            var entryBlock = cfgResult.EntryBasicBlock;
            var exitBlock = cfgResult.ExitBasicBlock;

            var blockInputStates = new Dictionary<BasicBlock, PurityAnalysisState>(blocks.Length);
            var blockOutputStates = new Dictionary<BasicBlock, PurityAnalysisState>(blocks.Length);
            var worklist = new Queue<BasicBlock>();

            // Initialization
            foreach (var block in blocks)
            {
                blockInputStates[block] = PurityAnalysisState.Pure;
                blockOutputStates[block] = PurityAnalysisState.Pure;
            }

            // Entry block state remains Pure before execution
            blockInputStates[entryBlock] = PurityAnalysisState.Pure;
            // Calculate initial output state for entry block
            blockOutputStates[entryBlock] = ApplyTransferFunction(entryBlock, PurityAnalysisState.Pure, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);

            worklist.Enqueue(entryBlock);

            // Iteration (Fixed-Point Analysis)
            while (worklist.Count > 0)
            {
                var block = worklist.Dequeue();

                // Merge output states of predecessors to get the input state for the current block
                var mergedInputState = PurityAnalysisState.Pure; // Start with pure
                if (block != entryBlock) // Entry block has no predecessors in the CFG representation
                {
                    var predecessorOutputs = block.Predecessors.Select(edge =>
                    {
                        blockOutputStates.TryGetValue(edge.Source, out PurityAnalysisState state);
                        return state;
                    });
                    mergedInputState = PurityAnalysisState.Merge(predecessorOutputs);
                }

                blockInputStates.TryGetValue(block, out var currentInputState); // Default is Pure
                if (mergedInputState != currentInputState)
                {
                    blockInputStates[block] = mergedInputState;
                }
                else if (block != entryBlock)
                {
                    // continue; // Optimization removed for now
                }

                // Apply Transfer Function
                var computedOutputState = ApplyTransferFunction(block, blockInputStates[block], context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);

                blockOutputStates.TryGetValue(block, out var currentOutputState); // Default is Pure
                if (computedOutputState != currentOutputState)
                {
                    blockOutputStates[block] = computedOutputState;

                    EnqueueSuccessors(block.FallThroughSuccessor, worklist);
                    EnqueueSuccessors(block.ConditionalSuccessor, worklist);
                }
            }

            // Result Calculation: Merge output states of all blocks preceding the exit block
            var finalState = PurityAnalysisState.Pure;
            if (exitBlock != null && exitBlock.Predecessors.Any())
            {
                var exitPredecessorOutputs = exitBlock.Predecessors.Select(edge =>
                {
                    blockOutputStates.TryGetValue(edge.Source, out PurityAnalysisState state);
                    return state;
                });
                finalState = PurityAnalysisState.Merge(exitPredecessorOutputs);
            }
            else if (blocks.Length == 1 && entryBlock == blocks[0]) // Handle case of single block CFG
            {
                finalState = blockOutputStates[entryBlock];
            }
            else if (blocks.Length > 0 && exitBlock == null) // Should not happen based on earlier check
            {
                System.Diagnostics.Debug.WriteLine($"Warning: CFG for {containingMethodSymbol.ToDisplayString()} has blocks but no exit block (unexpected).");
                finalState = PurityAnalysisState.Impure;
            }

            return !finalState.HasPotentialImpurity;
        }

        private static void EnqueueSuccessors(ControlFlowBranch? branch, Queue<BasicBlock> worklist) // Type adjusted
        {
            if (branch != null && branch.Destination != null && !worklist.Contains(branch.Destination))
            {
                worklist.Enqueue(branch.Destination);
            }
        }

        /// <summary>
        /// Applies the transfer function for a basic block.
        /// Determines the output purity state based on the input state and operations within the block.
        /// </summary>
        private static PurityAnalysisState ApplyTransferFunction(
            BasicBlock block,
            PurityAnalysisState stateBefore,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, bool> purityCache)
        {
            var stateAfter = stateBefore;

            // If already impure, no need to check operations further
            if (stateAfter.HasPotentialImpurity)
            {
                return PurityAnalysisState.Impure; // Return immediately
            }

            // Check operations within the block
            foreach (var operation in block.Operations) // Revert to using block.Operations
            {
                if (operation != null) // opWrapper should be IOperation according to docs, but check for safety
                {
                    if (IsOperationImpureForCFG(operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache))
                    {
                        stateAfter.HasPotentialImpurity = true;
                        break; // Once impure, the rest of the block doesn't change the outcome
                    }
                }
                else
                {
                    // Failed to get operation for statement syntax - treat as potential issue
                    System.Diagnostics.Debug.WriteLine($"Failed to get IOperation for statement syntax in block {block.Ordinal}: {block.ToString()}");
                    // Decide policy: Assume impure? Or ignore?
                    // Assume impure for safety
                    stateAfter.HasPotentialImpurity = true;
                    break;
                }
            }

            // If still pure, check the condition operation (if any) of the conditional successor
            if (!stateAfter.HasPotentialImpurity && block.BranchValue != null)
            {
                IOperation conditionOperation = block.BranchValue;
                if (conditionOperation != null)
                {
                    if (IsOperationImpureForCFG(conditionOperation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache))
                    {
                        stateAfter.HasPotentialImpurity = true;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get IOperation for block {block.Ordinal}'s conditional successor condition: {block.BranchValue.ToString()}");
                    stateAfter.HasPotentialImpurity = true; // Assume impure if unknown
                }
            }

            return stateAfter;
        }


        /// <summary>
        /// Checks if a single IOperation introduces impurity in the context of CFG analysis.
        /// Returns TRUE if the operation is considered IMPURE.
        /// Adapts logic from the original IsOperationPure.
        /// </summary>
        private static bool IsOperationImpureForCFG(
            IOperation operation,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, bool> purityCache)
        {
            if (operation == null) return true; // Null operation -> impure

            // Check for constant values first - these are pure
            if (operation.ConstantValue.HasValue) return false;
            // Check explicit constant null literal
            if (operation is ILiteralOperation litOpNull && litOpNull.ConstantValue.HasValue && litOpNull.ConstantValue.Value == null) return false;


            switch (operation)
            {
                // --- Inherently Impure Operations ---
                case IIncrementOrDecrementOperation _: // ++, --
                case ICompoundAssignmentOperation _: // a += b etc.
                case ICoalesceAssignmentOperation _: // ??=
                case IEventAssignmentOperation _: // +=, -= event
                case IAddressOfOperation _: // Taking address
                case IThrowOperation _: // throw statement
                case IUsingOperation _: // using(..) or await using(..) - Resource management has side effects
                case IAwaitOperation _: // await - Interaction with scheduler, context switching assumed impure
                case ITranslatedQueryOperation _: // LINQ query translations are complex, assume impure
                case IEventReferenceOperation _: // Referencing event often leads to +=/-= (impure)
                    return true; // Impure

                // --- Assignment (Careful Checks) ---
                case IAssignmentOperation assignmentOp:
                    {
                        var target = assignmentOp.Target;

                        if (target is IFieldReferenceOperation fieldRef)
                        {
                            // Modifying static field is impure (unless maybe initonly in static ctor - future)
                            if (fieldRef.Field.IsStatic && !fieldRef.Field.IsReadOnly) return true; // Impure static field write
                                                                                                    // Modifying instance field is impure unless it's readonly within the instance constructor
                            if (!fieldRef.Field.IsStatic && !(fieldRef.Field.IsReadOnly && containingMethodSymbol.MethodKind == MethodKind.Constructor)) return true; // Impure instance field write
                        }
                        else if (target is IPropertyReferenceOperation propRef)
                        {
                            var propertySymbol = propRef.Property;
                            if (propertySymbol.SetMethod == null) return true; // Assigning to getter-only property? Should be compile error, but impure if possible.

                            // Check if assigning to a readonly property (like {get; init;}) outside a constructor
                            if (propertySymbol.SetMethod.IsInitOnly && containingMethodSymbol.MethodKind != MethodKind.Constructor) return true; // Impure init assignment outside constructor

                            // Check purity of the setter method itself (init or regular setter)
                            // Recursive call using the CFG logic
                            if (!DeterminePurityRecursive(propertySymbol.SetMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache))
                            {
                                return true; // Impure if setter is impure
                            }
                        }
                        else if (target is IParameterReferenceOperation paramRef)
                        {
                            // Assigning to ref/out parameters is impure from the caller's perspective
                            if (paramRef.Parameter.RefKind == RefKind.Ref || paramRef.Parameter.RefKind == RefKind.Out) return true;
                        }
                        // Assignment to local, discard is fine (pure in itself)
                        // else if (target is ILocalReferenceOperation) { /* Pure */ }
                        // else if (target is IDiscardOperation) { /* Pure */ }
                        else
                        {
                            // Assignment to unknown target (indexer? event access?) -> assume impure
                            return true;
                        }

                        // If target assignment is allowed (pure context), check the assigned value recursively
                        return IsOperationImpureForCFG(assignmentOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                    }

                // --- Invocations / Object Creation ---
                case IInvocationOperation invocationOp:
                    {
                        var targetMethod = invocationOp.TargetMethod;
                        // Handle nameof specifically (pure)
                        if (targetMethod.Name == "nameof" && targetMethod.ContainingType?.SpecialType == SpecialType.System_String) return false;

                        var methodDisplayString = targetMethod.OriginalDefinition.ToDisplayString();
                        if (KnownImpureMethods.Contains(methodDisplayString) ||
                            methodDisplayString.StartsWith("System.Console.") ||
                            methodDisplayString.StartsWith("System.IO.") ||
                            methodDisplayString.StartsWith("System.Net.") ||
                            methodDisplayString.StartsWith("System.Threading.") ||
                            methodDisplayString.Contains(".Random.")) // Broader check for Random
                        {
                            return true; // Known impure methods
                        }

                        // Check purity of arguments first? For boolean state, maybe prioritize target method.
                        // Check arguments first for side effects (e.g. passing ref/out could become impure here)
                        var arguments = invocationOp.Arguments;
                        var parameters = targetMethod.Parameters;
                        int checkLength = System.Math.Min(arguments.Length, parameters.Length);

                        for (int i = 0; i < checkLength; i++)
                        {
                            var argOp = arguments[i];
                            var paramSymbol = parameters[i];

                            // Check for ref/in mismatch - IMPURE if found (passing 'in' where 'ref' needed implies write)
                            if (paramSymbol.RefKind == RefKind.Ref &&
                                argOp.Value is IParameterReferenceOperation argValueParamRef &&
                                argValueParamRef.Parameter.RefKind == RefKind.In)
                            {
                                return true;
                            }
                            // Check if argument evaluation itself is impure
                            if (IsOperationImpureForCFG(argOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache))
                            {
                                return true;
                            }
                        }
                        // Also check remaining arguments if argCount > paramCount (e.g. params array)
                        for (int i = checkLength; i < arguments.Length; i++)
                        {
                            if (IsOperationImpureForCFG(arguments[i].Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache))
                            {
                                return true;
                            }
                        }

                        // If arguments are pure, check the target method itself recursively
                        return !DeterminePurityRecursive(targetMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache);
                    }
                case IObjectCreationOperation objCreationOp:
                    {
                        if (objCreationOp.Constructor == null) return true; // Should not happen? Assume impure.

                        // Check constructor purity recursively
                        if (!DeterminePurityRecursive(objCreationOp.Constructor.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache)) return true;

                        // Check arguments
                        foreach (var arg in objCreationOp.Arguments)
                        {
                            if (IsOperationImpureForCFG(arg.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                        }

                        // Check initializer (if any)
                        if (objCreationOp.Initializer != null)
                        {
                            if (IsOperationImpureForCFG(objCreationOp.Initializer, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                        }
                        return false; // Pure if constructor, args, and initializer are pure
                    }

                // --- References (Reads) ---
                case IFieldReferenceOperation fieldRefOp:
                    {
                        var fieldSymbol = fieldRefOp.Field;
                        // Reading static readonly is pure
                        if (fieldSymbol.IsStatic && fieldSymbol.IsReadOnly) return false;
                        // Reading instance readonly can be pure *if* accessed via pure context (e.g. 'in' param) - simplify for now
                        // Allow reading instance readonly fields if accessed via 'this' or 'base' or 'in'/'ref readonly' param
                        bool isInstanceReadonlyContext = !fieldSymbol.IsStatic && fieldSymbol.IsReadOnly &&
                            (fieldRefOp.Instance is IInstanceReferenceOperation ||
                             (fieldRefOp.Instance is IParameterReferenceOperation p && (p.Parameter.RefKind == RefKind.In || p.Parameter.RefKind == RefKind.RefReadOnly)));
                        if (isInstanceReadonlyContext) return false;

                        // Reading non-static-readonly, or instance fields (non-readonly or not in constructor/pure context) -> Impure Read
                        return true; // Assume reads of mutable state are impure
                    }
                case IPropertyReferenceOperation propRefOp: // Reading a property
                    {
                        var propSymbol = propRefOp.Property;
                        if (propSymbol.GetMethod == null) return true; // Cannot read if no getter -> Impure

                        // If accessing a readonly property via an 'in' or 'ref readonly' parameter instance, assume pure read.
                        if (propSymbol.IsReadOnly &&
                            propRefOp.Instance is IParameterReferenceOperation paramRef &&
                            (paramRef.Parameter.RefKind == RefKind.In || paramRef.Parameter.RefKind == RefKind.RefReadOnly))
                        {
                            return false; // Pure read in this context
                        }

                        // Check known impure getters
                        var propertyGetterName = propSymbol.ContainingType.ToDisplayString() + "." + propSymbol.Name + ".get";
                        if (KnownImpureMethods.Contains(propertyGetterName)) return true;

                        // Check getter purity recursively
                        return !DeterminePurityRecursive(propSymbol.GetMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache);
                    }
                // Reading local/parameter/'this'/'base' is pure
                case ILocalReferenceOperation _: return false;
                case IParameterReferenceOperation _: return false;
                case IInstanceReferenceOperation instRefOp when instRefOp.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance: return false; // 'this' or 'base'

                // --- Operations that depend only on operands ---
                case ILiteralOperation literalOp: return literalOp.Type?.SpecialType == SpecialType.System_IntPtr || literalOp.Type?.SpecialType == SpecialType.System_UIntPtr; // Pointer literals impure
                case IDefaultValueOperation _: return false; // Pure
                case ITypeOfOperation _: return false; // Pure
                case ISizeOfOperation _: return false; // Pure
                case IIsTypeOperation isOp: // Depends on operand
                    return IsOperationImpureForCFG(isOp.ValueOperand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check ValueOperand
                case IConversionOperation convOp: // Depends on operand and operator method
                    if (convOp.OperatorMethod != null)
                    {
                        if (!DeterminePurityRecursive(convOp.OperatorMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache)) return true; // Impure if operator method impure
                    }
                    return IsOperationImpureForCFG(convOp.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check operand
                case IBinaryOperation binaryOp:
                    if (binaryOp.OperatorMethod != null)
                    {
                        if (!DeterminePurityRecursive(binaryOp.OperatorMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache)) return true;
                    }
                    // Need to check both operands, as even short-circuiting ops might eval second operand depending on value
                    if (IsOperationImpureForCFG(binaryOp.LeftOperand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                    return IsOperationImpureForCFG(binaryOp.RightOperand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IUnaryOperation unaryOp:
                    if (unaryOp.OperatorMethod != null)
                    {
                        if (!DeterminePurityRecursive(unaryOp.OperatorMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache)) return true;
                    }
                    // Pointer indirection * / -> assume impure
                    // if (unaryOp.Syntax is PrefixUnaryExpressionSyntax { Kind: SyntaxKind.PointerIndirectionExpression } || ...) // Syntax check removed
                    // if (operation.Kind == OperationKind.PointerIndirectionReference) return true; // Check OperationKind instead? Seems unreliable. Assume unary impure for now if not op method.
                    // Let's refine: standard ops +, -, !, ~ depend on operand
                    if (unaryOp.OperatorKind == UnaryOperatorKind.Plus || unaryOp.OperatorKind == UnaryOperatorKind.Minus ||
                         unaryOp.OperatorKind == UnaryOperatorKind.Not || unaryOp.OperatorKind == UnaryOperatorKind.BitwiseNegation)
                    {
                        return IsOperationImpureForCFG(unaryOp.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                    }
                    return true; // Assume other unary ops (pointers?) are impure
                case IConditionalOperation conditionalOp: // ternary ?:
                    if (IsOperationImpureForCFG(conditionalOp.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                    if (IsOperationImpureForCFG(conditionalOp.WhenTrue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                    return conditionalOp.WhenFalse != null && IsOperationImpureForCFG(conditionalOp.WhenFalse, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case ICoalesceOperation coalesceOp: // ??
                    if (IsOperationImpureForCFG(coalesceOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                    return IsOperationImpureForCFG(coalesceOp.WhenNull, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IConditionalAccessOperation conditionalAccess: // ?.
                    if (IsOperationImpureForCFG(conditionalAccess.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; // Check receiver
                    return IsOperationImpureForCFG(conditionalAccess.WhenNotNull, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check accessed member
                case IConditionalAccessInstanceOperation _: return false; // The instance part of ?. is pure read
                case IParenthesizedOperation parenOp: return IsOperationImpureForCFG(parenOp.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IMethodReferenceOperation _: return false; // Referencing method is pure
                case INameOfOperation _: return false; // nameof() is pure
                case ITupleOperation tupleOp: // Tuple literal (a, b)
                    return tupleOp.Elements.Any(e => IsOperationImpureForCFG(e, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));
                case IArgumentOperation argOp: // Check the underlying value
                    return IsOperationImpureForCFG(argOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IInterpolatedStringOperation intpStrOp:
                    return intpStrOp.Parts.Any(p => IsOperationImpureForCFG(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));
                case IInterpolatedStringTextOperation _: return false; // Constant text part
                case IInterpolationOperation interpolationOp: // The {expr} part
                    return IsOperationImpureForCFG(interpolationOp.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IAnonymousFunctionOperation _: return false; // Defining lambda/anon method is pure (calling it is handled by invocation)
                case IDelegateCreationOperation delegateCreation: // new Action(...) or MethodGroup
                    return delegateCreation.Target != null && IsOperationImpureForCFG(delegateCreation.Target, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check target expression if any
                case IAnonymousObjectCreationOperation anonObjOp: // Check initializers
                    return anonObjOp.Initializers.Any(init => IsOperationImpureForCFG(init, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));
                case ITypeParameterObjectCreationOperation typeParamObjOp: // new T() -> default constructor
                                                                           // Relies on default constructor being pure
                    var ctor = typeParamObjOp.Type?.GetMembers(".ctor")
                                 .OfType<IMethodSymbol>()
                                 .FirstOrDefault(c => c.Parameters.Length == 0 && c.MethodKind == MethodKind.Constructor);
                    return ctor != null && !DeterminePurityRecursive(ctor.OriginalDefinition, context, enforcePureAttributeSymbol, visited, purityCache);
                case IObjectOrCollectionInitializerOperation initializerOp: // The initializer part { Prop = val }
                    return initializerOp.Initializers.Any(init => IsOperationImpureForCFG(init, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));
                case IMemberInitializerOperation memberInitOp: // Assignment within initializer Prop = Value
                    return IsOperationImpureForCFG(memberInitOp.Initializer, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check the assignment
                case IArrayCreationOperation arrayCreationOp: // new T[...] { ... }
                                                              // Check dimension sizes
                    foreach (var dim in arrayCreationOp.DimensionSizes) { if (IsOperationImpureForCFG(dim, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; }
                    // Check initializer
                    return arrayCreationOp.Initializer != null && IsOperationImpureForCFG(arrayCreationOp.Initializer, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IArrayInitializerOperation arrayInitOp: // { a, b, c }
                    return arrayInitOp.ElementValues.Any(e => IsOperationImpureForCFG(e, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));

                // Patterns (Generally pure checks, depend on sub-expressions/patterns)
                case IIsPatternOperation isPattern:
                    if (IsOperationImpureForCFG(isPattern.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; // Check value being matched
                    return IsOperationImpureForCFG(isPattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check pattern itself
                case IConstantPatternOperation constPattern:
                    return IsOperationImpureForCFG(constPattern.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check constant expression
                case IDeclarationPatternOperation _: return false; // Declaration itself is pure
                case IRecursivePatternOperation recursivePattern: // Type { Prop: pattern }
                    if (recursivePattern.DeconstructionSubpatterns.Any(p => IsOperationImpureForCFG(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache))) return true;
                    return recursivePattern.PropertySubpatterns.Any(p => IsOperationImpureForCFG(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));
                case IRelationalPatternOperation relPattern:
                    return IsOperationImpureForCFG(relPattern.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check the constant expression
                case ITypePatternOperation _: return false; // Type check is pure
                case INegatedPatternOperation notPattern:
                    return IsOperationImpureForCFG(notPattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check negated pattern
                case IBinaryPatternOperation binaryPattern: // and/or pattern
                    if (IsOperationImpureForCFG(binaryPattern.LeftPattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true;
                    return IsOperationImpureForCFG(binaryPattern.RightPattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);
                case IDiscardPatternOperation _: return false; // `_` pattern is pure
                case IPropertySubpatternOperation propSubpattern: // Prop: pattern
                                                                  // Check the member access + the subpattern
                    if (IsOperationImpureForCFG(propSubpattern.Member, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; // Check member access (e.g., property get)
                    return IsOperationImpureForCFG(propSubpattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check sub-pattern
                case IListPatternOperation listPattern: // [p1, p2, ..]
                    return listPattern.Patterns.Any(p => IsOperationImpureForCFG(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache));
                case ISlicePatternOperation slicePattern: // .. or .. p
                    return slicePattern.Pattern != null && IsOperationImpureForCFG(slicePattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache);

                // Switch Expressions
                case ISwitchExpressionOperation switchExpr:
                    if (IsOperationImpureForCFG(switchExpr.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; // Check value being switched
                    return switchExpr.Arms.Any(arm => IsOperationImpureForCFG(arm, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)); // Check arms
                case ISwitchExpressionArmOperation switchArm:
                    if (IsOperationImpureForCFG(switchArm.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; // Check pattern
                    if (switchArm.Guard != null && IsOperationImpureForCFG(switchArm.Guard, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache)) return true; // Check guard
                    return IsOperationImpureForCFG(switchArm.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, purityCache); // Check result value

                // Local functions: definition is pure, calls handled by invocation.
                case ILocalFunctionOperation _: return false;

                // Assume unhandled operations are impure for safety
                default:
                    System.Diagnostics.Debug.WriteLine($"Unhandled Operation Kind for CFG Purity: {operation.Kind} -> {operation.Syntax?.Kind()}");
                    return true; // Impure
            }
        }

        /// <summary>
        /// Checks if a symbol is marked with the [EnforcePure] attribute.
        /// </summary>
        internal static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            // Null check for safety
            if (enforcePureAttributeSymbol == null)
            {
                return false;
            }
            // Use OriginalDefinition for attribute class comparison
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol));
        }
    }
}