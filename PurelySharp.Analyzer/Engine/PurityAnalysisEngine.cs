using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Contains the core logic for determining method purity using IOperation.
    /// </summary>
    internal static class PurityAnalysisEngine
    {
        // Add a set of known impure method signatures
        private static readonly HashSet<string> KnownImpureMethods = new HashSet<string>
        {
            "System.IO.File.WriteAllText",
            "System.DateTime.Now.get", // Property getters are methods like get_Now
            // Add more known impure methods here
        };

        /// <summary>
        /// Checks if a method symbol is considered pure based on its implementation using IOperation.
        /// </summary>
        internal static bool IsConsideredPure(IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited)
        {
            // --- Cycle Detection ---
            var originalMethodSymbol = methodSymbol.OriginalDefinition;
            if (!visited.Add(originalMethodSymbol))
            {
                return false; // Cycle detected, assume impure
            }

            bool isPure = DeterminePurity(methodSymbol, context, enforcePureAttributeSymbol, visited);

            // --- Backtrack & Return ---
            visited.Remove(originalMethodSymbol);
            return isPure;
        }

        private static bool DeterminePurity(IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited)
        {
            // --- Check Attributes First ---
            // Applying an attribute involves running its constructor. If the constructor is impure,
            // the method application itself introduces impurity.
            foreach (var attributeData in methodSymbol.GetAttributes())
            {
                if (attributeData.AttributeConstructor != null)
                {
                    // Use OriginalDefinition for constructor check
                    // Need to create a new visited set for this recursive check to avoid incorrect cycle detection
                    // Cloning the set prevents cycles between method analysis and attribute analysis.
                    var attributeVisited = new HashSet<IMethodSymbol>(visited, SymbolEqualityComparer.Default);
                    if (!IsConsideredPure(attributeData.AttributeConstructor.OriginalDefinition, context, enforcePureAttributeSymbol, attributeVisited))
                    {
                        return false; // Attribute constructor is impure
                    }
                }
            }
            // Also check attributes on containing type? Potentially complex, skip for now.

            // --- Find Declaration Syntax (still needed to get the body node) ---
            SyntaxNode? bodyNode = null;
            foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax(context.CancellationToken);
                if (syntax is MethodDeclarationSyntax methodDecl)
                {
                    bodyNode = (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody?.Expression;
                    if (bodyNode != null) break;
                }
                else if (syntax is AccessorDeclarationSyntax accessorDecl) // Handle properties/indexers
                {
                    bodyNode = (SyntaxNode?)accessorDecl.Body ?? accessorDecl.ExpressionBody?.Expression;
                    if (bodyNode != null) break;
                }
                else if (syntax is ConstructorDeclarationSyntax ctorDecl) // Handle constructors
                {
                    bodyNode = (SyntaxNode?)ctorDecl.Body ?? ctorDecl.ExpressionBody?.Expression;
                    if (bodyNode != null) break;
                }
                else if (syntax is LocalFunctionStatementSyntax localFunc) // Handle local functions
                {
                    bodyNode = (SyntaxNode?)localFunc.Body ?? localFunc.ExpressionBody?.Expression;
                    if (bodyNode != null) break;
                }
                // TODO: Add support for OperatorDeclarationSyntax, ConversionOperatorDeclarationSyntax?
            }

            if (bodyNode == null)
            {
                // No implementation found (e.g., abstract, partial without definition, interface method?)
                // Handle implicit default constructors as pure.
                if (methodSymbol.MethodKind == MethodKind.Constructor) return true;

                // Determine purity based on method kind or attributes

                // Check for interface methods FIRST
                if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface && !methodSymbol.IsStatic)
                {
                    // Interface methods without a direct body: Trust the [Pure] attribute if present, otherwise assume impure.
                    // Note: enforcePureAttributeSymbol is passed in, representing the [Pure] attribute we're looking for.
                    return IsPureEnforced(methodSymbol, enforcePureAttributeSymbol);
                }

                // THEN check for abstract methods (which might include interface methods not caught above, though unlikely now)
                if (methodSymbol.IsAbstract) return true; // Abstract methods (not interfaces handled above) assumed pure contractually

                // Interface methods: Assume impure unless specifically marked pure (future enhancement)
                // --- This comment block is now redundant due to the explicit check above ---
                // if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface && !methodSymbol.IsStatic)
                // {
                //     ...
                // }

                if (methodSymbol.IsExtern) return false; // Extern methods assumed impure
                // Partial methods without implementation body are problematic, assume impure?
                return false; // Assume impure/unknown if no body analyzable or other cases
            }

            // --- Analyze Body using IOperation ---
            IOperation? bodyOperation = context.SemanticModel.GetOperation(bodyNode, context.CancellationToken);
            if (bodyOperation == null)
            {
                // Failed to get operation, assume impure for safety
                return false;
            }

            var initialLocalPurity = new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);

            if (bodyOperation is IBlockOperation blockOp)
            {
                // Analyze the block operation
                return AnalyzeBlockOperationTopLevel(blockOp, context, enforcePureAttributeSymbol, visited, methodSymbol, initialLocalPurity);
            }
            else
            {
                // Treat other body operations (e.g., expression body) as a single expression to check
                return IsOperationPure(bodyOperation, context, enforcePureAttributeSymbol, visited, methodSymbol, initialLocalPurity);
            }
        }

        /// <summary>
        /// Analyzes a top-level block operation (method body).
        /// </summary>
        private static bool AnalyzeBlockOperationTopLevel(
            IBlockOperation blockOp,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<ILocalSymbol, bool> localPurityStatus) // Top level starts with empty dict
        {
            // Use the internal helper, allowing return only if it's the last statement
            return AnalyzeNestedBlockOperation(blockOp, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus, allowReturnAtEnd: true);
        }

        /// <summary>
        /// Internal recursive implementation for analyzing a block operation.
        /// Takes a mutable dictionary for local purity status within the block.
        /// </summary>
        private static bool AnalyzeNestedBlockOperation(
            IBlockOperation blockOp,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<ILocalSymbol, bool> localPurityStatus, // Mutable dictionary for this scope
            bool allowReturnAtEnd = false) // Default to false for nested blocks
        {
            var operations = blockOp.Operations;
            for (int i = 0; i < operations.Length; i++)
            {
                var operation = operations[i];

                switch (operation)
                {
                    case IVariableDeclarationGroupOperation localDeclGroup:
                        foreach (var decl in localDeclGroup.Declarations)
                        {
                            foreach (var declarator in decl.Declarators)
                            {
                                var localSymbol = declarator.Symbol;
                                bool isInitializerPure = true;
                                if (declarator.Initializer != null)
                                {
                                    isInitializerPure = IsOperationPure(declarator.Initializer.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                                    if (!isInitializerPure) return false;
                                }
                                localPurityStatus[localSymbol] = isInitializerPure;
                            }
                        }
                        break;

                    case IReturnOperation returnOp:
                        // Allow return only if flag is set AND it's the last operation
                        if (allowReturnAtEnd && i == operations.Length - 1)
                        {
                            return IsOperationPure(returnOp.ReturnedValue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                        }
                        else
                        {
                            return false; // Impure: Nested return or return not at end
                        }

                    case IExpressionStatementOperation exprStmtOp:
                        if (!IsOperationPure(exprStmtOp.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                        {
                            return false;
                        }
                        break;

                    case IConditionalOperation ifOp:
                        if (!IsOperationPure(ifOp.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        // Analyze branches recursively using copies of the current local state
                        // Pass allowReturnAtEnd=false to nested blocks
                        var trueBranchLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                        var falseBranchLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                        if (!AnalyzeOperationConditionalBranch(ifOp.WhenTrue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, trueBranchLocals))
                            return false;
                        if (ifOp.WhenFalse != null && !AnalyzeOperationConditionalBranch(ifOp.WhenFalse, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, falseBranchLocals))
                            return false;
                        // Merging local state changes from branches is complex, ignore for now.
                        // Assume purity depends only on the operations executed, not state merging.
                        break;

                    case IBlockOperation nestedBlockOp: // Handles nested blocks explicitly
                        var nestedLocalPurityStatus = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                        // Pass allowReturnAtEnd=false to nested blocks
                        if (!AnalyzeNestedBlockOperation(nestedBlockOp, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, nestedLocalPurityStatus, allowReturnAtEnd: false))
                        {
                            return false;
                        }
                        break;

                    case IEmptyOperation _: break;
                    case ILabeledOperation labelOp: // Use ILabeledOperation directly for labels
                        // Analyze the labeled operation itself
                        if (labelOp.Operation != null)
                        {
                            if (labelOp.Operation is IBlockOperation labeledBlock)
                            {
                                if (!AnalyzeNestedBlockOperation(labeledBlock, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus, allowReturnAtEnd)) return false;
                            }
                            else
                            {
                                // Crude handling if not a block - analyze as single op if possible
                                // Need to create a copy as IsOperationPure expects ReadOnly but might call helpers needing mutable
                                var tempLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                                if (!IsOperationPure(labelOp.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, tempLocals)) return false;
                            }
                        }
                        break;
                    case IBranchOperation branchOp:
                        // GoTo handled; Break/Continue are okay within loops/switch
                        if (branchOp.BranchKind == BranchKind.GoTo) return false;
                        break;

                    // --- Loop Handling --- (Basic initial implementation)
                    case ILoopOperation loopOp: // Base for For, While, Foreach
                        {
                            // Basic check: Condition (if any) and Body must be pure.
                            // This doesn't handle state changes across iterations well.
                            bool bodyPure = true;
                            if (loopOp.Body != null) // Analyze body if present
                            {
                                var loopLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                                bodyPure = AnalyzeOperationConditionalBranch(loopOp.Body, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, loopLocals);
                            }

                            if (!bodyPure) return false;

                            // Check condition if applicable (While, For)
                            if (loopOp is IWhileLoopOperation whileLoop)
                            {
                                if (whileLoop.Condition != null && !IsOperationPure(whileLoop.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                            }
                            else if (loopOp is IForLoopOperation forLoop)
                            {
                                if (forLoop.Condition != null && !IsOperationPure(forLoop.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                                // Also check Before/AtLoopBottom operations for purity
                                foreach (var op in forLoop.Before)
                                {
                                    if (!IsOperationPure(op, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                                }
                                foreach (var op in forLoop.AtLoopBottom)
                                {
                                    if (!IsOperationPure(op, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                                }
                            }
                            // ForEach requires checking the collection expression
                            else if (loopOp is IForEachLoopOperation forEachLoop)
                            {
                                if (!IsOperationPure(forEachLoop.Collection, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                            }

                            // If condition/body seem pure, we continue. This is a simplification.
                            break;
                        }

                    // --- Try/Catch Handling --- (Basic initial implementation)
                    case ITryOperation tryOp:
                        {
                            // Body, Catches, and Finally must all be pure
                            var tryLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                            if (tryOp.Body != null && !AnalyzeOperationConditionalBranch(tryOp.Body, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, tryLocals)) return false;

                            foreach (var catchClause in tryOp.Catches)
                            {
                                var catchLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                                // Check filter if present
                                if (catchClause.Filter != null && !IsOperationPure(catchClause.Filter, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, catchLocals)) return false;
                                // Check handler body
                                if (!AnalyzeOperationConditionalBranch(catchClause.Handler, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, catchLocals)) return false;
                            }

                            if (tryOp.Finally != null)
                            {
                                var finallyLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                                if (!AnalyzeOperationConditionalBranch(tryOp.Finally, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, finallyLocals)) return false;
                            }
                            break;
                        }

                    case ISwitchOperation switchOp:
                        {
                            // Check expression being switched on
                            if (!IsOperationPure(switchOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                            // Check all sections
                            foreach (var section in switchOp.Cases)
                            {
                                var sectionLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                                // Check labels (cases)
                                foreach (var clause in section.Clauses)
                                { // Use ICaseClauseOperation
                                  // Case Guard (when clause) analysis might need refinement or be handled by body analysis.
                                  // Removing direct check on clause:
                                  // if (clause.Guard != null && !IsOperationPure(clause.Guard, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, sectionLocals)) return false; // Removed !
                                }
                                // Check body operations
                                foreach (var bodyOp in section.Body)
                                {
                                    // Analyze body statements using a helper that understands switch context (break is allowed, return is not)
                                    if (!AnalyzeSwitchSectionOperation(bodyOp, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, sectionLocals)) return false;
                                }
                            }
                            break;
                        }

                    case IStopOperation _: // End of execution, like end of iterator method - Pure
                        break;

                    default:
                        // System.Diagnostics.Debug.WriteLine($"Unhandled Operation in Block: {operation.Kind} -> {operation.Syntax?.Kind()}");
                        return false;
                }
            }

            // If the loop completes, the block is pure unless it's non-void and doesn't end with a return.
            if (!allowReturnAtEnd || containingMethodSymbol.ReturnsVoid)
            {
                return true;
            }
            else
            {
                // If it's the top-level block of a non-void method, it must end with a return OR throw.
                // Check if the last operation was effectively a return or throw.
                if (operations.Length == 0) return containingMethodSymbol.IsAbstract; // Empty block for non-void is ok only if abstract

                IOperation lastOp = operations[operations.Length - 1];
                // Allow for labeled statements containing the final operation
                while (lastOp is ILabeledOperation labeledOp)
                {
                    // Add null check before assignment
                    if (labeledOp.Operation == null) return false; // Cannot be valid end if label points to null
                    lastOp = labeledOp.Operation;
                }
                // Check if the last operation is Return or Throw
                return lastOp is IReturnOperation || lastOp is IThrowOperation;
            }
        }

        /// <summary>
        /// Helper to analyze an operation within a switch section body.
        /// Disallows return, allows break.
        /// </summary>
        private static bool AnalyzeSwitchSectionOperation(
           IOperation operation,
           SyntaxNodeAnalysisContext context,
           INamedTypeSymbol enforcePureAttributeSymbol,
           HashSet<IMethodSymbol> visited,
           IMethodSymbol containingMethodSymbol,
           Dictionary<ILocalSymbol, bool> localPurityStatus)
        {
            // Very similar to AnalyzeNestedBlockOperation's switch, but with different termination rules

            switch (operation)
            {
                case IVariableDeclarationGroupOperation localDeclGroup:
                    {
                        foreach (var decl in localDeclGroup.Declarations)
                        {
                            foreach (var declarator in decl.Declarators)
                            {
                                var localSymbol = declarator.Symbol;
                                bool isInitializerPure = true;
                                if (declarator.Initializer != null)
                                {
                                    isInitializerPure = IsOperationPure(declarator.Initializer.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                                    if (!isInitializerPure) return false;
                                }
                                localPurityStatus[localSymbol] = isInitializerPure;
                            }
                        }
                        return true;
                    }

                case IReturnOperation _: return false; // Return not allowed directly in switch section

                case IExpressionStatementOperation exprStmtOp:
                    return IsOperationPure(exprStmtOp.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);

                case IConditionalOperation ifOp: // Allow simple if/else if pure
                    {
                        if (!IsOperationPure(ifOp.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        var trueBranchLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                        var falseBranchLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                        // Use AnalyzeConditionalBranchInSwitch recursively for branches
                        if (!AnalyzeConditionalBranchInSwitch(ifOp.WhenTrue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, trueBranchLocals)) return false;
                        if (ifOp.WhenFalse != null && !AnalyzeConditionalBranchInSwitch(ifOp.WhenFalse, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, falseBranchLocals)) return false;
                        return true;
                    }

                case IBlockOperation nestedBlockOp:
                    {
                        var nestedLocalPurityStatus = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                        // Recursively call this helper for nested blocks within the switch section
                        foreach (var op in nestedBlockOp.Operations)
                        {
                            if (!AnalyzeSwitchSectionOperation(op, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, nestedLocalPurityStatus))
                            {
                                return false;
                            }
                        }
                        return true;
                    }

                case IEmptyOperation _: return true;
                case ILabeledOperation lblOp: // Allow labels within switch sections
                                              // Analyze the operation associated with the label using switch rules
                    if (lblOp.Operation != null)
                    { // Add null check
                        return AnalyzeSwitchSectionOperation(lblOp.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                    // If label has no operation (shouldn't happen?), treat as pure.
                    return true;
                case IBranchOperation branchOp when branchOp.BranchKind == BranchKind.Break:
                    return true; // Break is allowed within switch
                case IBranchOperation branchOp when branchOp.BranchKind == BranchKind.GoTo: // GoTo Case or GoTo Default
                                                                                            // Goto another case label is allowed, assume pure jump for now.
                    return true;

                // Allow loops if their bodies use AnalyzeSwitchSectionOperation
                case ILoopOperation loopOp:
                    {
                        bool bodyPure = true;
                        if (loopOp.Body != null)
                        {
                            var loopLocals = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                            bodyPure = AnalyzeConditionalBranchInSwitch(loopOp.Body, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, loopLocals);
                        }
                        if (!bodyPure) return false;

                        // Check condition/collection/etc.
                        if (loopOp is IWhileLoopOperation whileLoop)
                        {
                            if (whileLoop.Condition != null && !IsOperationPure(whileLoop.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        }
                        else if (loopOp is IForLoopOperation forLoop)
                        {
                            if (forLoop.Condition != null && !IsOperationPure(forLoop.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                            foreach (var op in forLoop.Before) { if (!IsOperationPure(op, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false; }
                            foreach (var op in forLoop.AtLoopBottom) { if (!IsOperationPure(op, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false; }
                        }
                        else if (loopOp is IForEachLoopOperation forEachLoop)
                        {
                            if (!IsOperationPure(forEachLoop.Collection, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        }
                        return true;
                    }

                default:
                    // System.Diagnostics.Debug.WriteLine($"Unhandled Operation in Switch Section: {operation.Kind} -> {operation.Syntax?.Kind()}");
                    return false;
            }
        }

        // Helper specifically for analyzing conditional branches within a switch section
        private static bool AnalyzeConditionalBranchInSwitch(
            IOperation branchOperation,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<ILocalSymbol, bool> localPurityStatus)
        {
            if (branchOperation is IBlockOperation branchBlock)
            {
                // Analyze the nested block using switch rules
                var nestedStatus = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                foreach (var op in branchBlock.Operations)
                {
                    if (!AnalyzeSwitchSectionOperation(op, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, nestedStatus)) return false;
                }
                return true;
            }
            else
            {
                // Analyze the single operation using switch rules
                return AnalyzeSwitchSectionOperation(branchOperation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
        }


        /// <summary>
        /// Helper to analyze an operation that represents a branch in a conditional (if/else).
        /// It might be a BlockOperation or some other single operation.
        /// </summary>
        private static bool AnalyzeOperationConditionalBranch(
            IOperation branchOperation,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<ILocalSymbol, bool> localPurityStatus)
        {
            if (branchOperation is IBlockOperation branchBlock)
            {
                // Analyze the nested block, disallowing returns within it.
                // Create mutable copy for the nested analysis
                var nestedStatus = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                return AnalyzeNestedBlockOperation(branchBlock, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, nestedStatus, allowReturnAtEnd: false);
            }
            else
            {
                // Analyze the single operation.
                // A return statement is invalid here (must be inside a block analyzed by AnalyzeNestedBlockOperation)
                if (branchOperation is IReturnOperation) return false;
                // Delegate to IsOperationPure for single operations in branches
                // IsOperationPure takes IReadOnlyDictionary, so no copy needed here.
                return IsOperationPure(branchOperation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
        }


        /// <summary>
        /// Checks if a given IOperation is considered pure based on the current rules.
        /// </summary>
        private static bool IsOperationPure(
            IOperation? operation,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            IReadOnlyDictionary<ILocalSymbol, bool> localPurityStatus)
        {
            // --- Debug Logging ---
            // System.Diagnostics.Debug.WriteLine($"IsOperationPure Start: Kind={operation?.Kind}, Syntax='{operation?.Syntax?.ToString() ?? "N/A"}'");
            // --- End Debug Logging ---

            if (operation == null) return false; // Null operation -> impure

            // Check for constant values first
            var constantValue = operation.ConstantValue;
            // Allow constant null. Check HasValue first.
            // Add null suppression (!) assuming if HasValue is true, Value is intended to be non-null for primitive types.
            if (constantValue.HasValue && constantValue.Value != null)
            {
                return true;
            }
            // Check explicit constant null literal
            if (operation is ILiteralOperation litOp && litOp.ConstantValue.HasValue && litOp.ConstantValue.Value == null)
            {
                return true;
            }

            switch (operation)
            {
                // --- Assignments need to be ordered carefully ---
                case IIncrementOrDecrementOperation incDecOp: // ++, -- (More specific than IAssignmentOperation)
                    {
                        // These always modify state.
                        return false;
                    }
                case ICompoundAssignmentOperation compoundAssignOp: // a += b etc. (More specific than IAssignmentOperation)
                    {
                        // Desugars to read + write, inherently impure.
                        return false;
                    }
                case ICoalesceAssignmentOperation _: return false; // ??=
                case IEventAssignmentOperation _: return false; // +=, -=
                case IAssignmentOperation assignmentOp: // General assignment
                    {
                        var target = assignmentOp.Target;
                        bool isTargetPureContext = true; // Assume assignment itself is pure contextually if target symbol allows it

                        if (target is IFieldReferenceOperation fieldRef)
                        {
                            // Allow assignment only to instance readonly fields in constructor
                            isTargetPureContext = !fieldRef.Field.IsStatic && fieldRef.Field.IsReadOnly && containingMethodSymbol.MethodKind == MethodKind.Constructor;
                        }
                        else if (target is IPropertyReferenceOperation propRef)
                        {
                            var propertySymbol = propRef.Property;
                            // Check if assigning to a readonly property (like {get;}) inside a constructor
                            if (propertySymbol.IsReadOnly && containingMethodSymbol.MethodKind == MethodKind.Constructor)
                            {
                                isTargetPureContext = true; // Pure to assign readonly property backing field in constructor
                            }
                            else
                            {
                                // Otherwise, purity depends on the setter method analysis (init or regular setter)
                                isTargetPureContext = IsPropertySetterPure(propertySymbol, context, enforcePureAttributeSymbol, visited, containingMethodSymbol);
                            }
                        }
                        else if (target is IParameterReferenceOperation paramRef)
                        {
                            // Cannot assign to 'in' or non-ref/out parameters implicitly.
                            // Explicit assignment to ref/out is inherently impure from caller perspective.
                            isTargetPureContext = paramRef.Parameter.RefKind == RefKind.None || paramRef.Parameter.RefKind == RefKind.In;
                            if (!isTargetPureContext) return false; // Direct impurity if assigning to ref/out
                        }
                        else if (target is ILocalReferenceOperation)
                        {
                            isTargetPureContext = true; // Assignment to local is fine
                        }
                        else if (target is IDiscardOperation)
                        {
                            isTargetPureContext = true; // Assignment to discard is fine
                        }
                        else
                        {
                            // Assignment to unknown target (indexer, event, etc.) -> impure
                            isTargetPureContext = false;
                        }

                        if (!isTargetPureContext) return false;

                        // Assignment is pure only if the assigned value is pure
                        return IsOperationPure(assignmentOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }

                // --- Invocations ---
                case IInvocationOperation invocationOp:
                    {
                        var targetMethod = invocationOp.TargetMethod;
                        // Handle nameof specifically
                        if (targetMethod.Name == "nameof" && targetMethod.ContainingType?.SpecialType == SpecialType.System_String) return true; // Added null check

                        var methodDisplayString = targetMethod.OriginalDefinition.ToDisplayString();
                        if (KnownImpureMethods.Contains(methodDisplayString) ||
                            methodDisplayString.StartsWith("System.Console.") ||
                            methodDisplayString.StartsWith("System.IO.") ||
                            methodDisplayString.StartsWith("System.Net.") ||
                            methodDisplayString.StartsWith("System.Threading.") ||
                            methodDisplayString.Contains("Random"))
                        {
                            return false;
                        }

                        // *** Check arguments FIRST ***
                        var arguments = invocationOp.Arguments;
                        var parameters = targetMethod.Parameters;
                        int argCount = arguments.Length;
                        int paramCount = parameters.Length;
                        int checkLength = System.Math.Min(argCount, paramCount); // Handle params arrays etc.

                        for (int i = 0; i < checkLength; i++)
                        {
                            var argOp = arguments[i];
                            var paramSymbol = parameters[i];

                            // Check for ref/in mismatch - IMPURE if found
                            if (paramSymbol.RefKind == RefKind.Ref &&
                                argOp.Value is IParameterReferenceOperation argValueParamRef &&
                                argValueParamRef.Parameter.RefKind == RefKind.In)
                            {
                                return false;
                            }

                            // Check argument purity
                            if (!IsOperationPure(argOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                            {
                                return false; // Impure if any argument is impure
                            }
                        }
                        // Also check remaining arguments if argCount > paramCount (e.g. params array)
                        for (int i = checkLength; i < argCount; i++)
                        {
                            if (!IsOperationPure(arguments[i].Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                            {
                                return false; // Impure if any extra argument is impure
                            }
                        }
                        // *** END Argument Checks ***

                        // If arguments are pure, check the target method itself
                        // Use OriginalDefinition to handle generics correctly in visited set
                        return IsConsideredPure(targetMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                    }

                // --- References (Reads) ---
                case IFieldReferenceOperation fieldRefOp:
                    {
                        var fieldSymbol = fieldRefOp.Field;
                        if (fieldSymbol.IsStatic && fieldSymbol.IsReadOnly) return true;
                        // Allow reading instance readonly fields - Temporarily disable to align with test expectations
                        // if (!fieldSymbol.IsStatic && fieldSymbol.IsReadOnly) return true;
                        // Allow reading from 'in' or 'ref readonly' parameters (field access on parameter)
                        if (fieldRefOp.Instance is IParameterReferenceOperation paramRef &&
                            (paramRef.Parameter.RefKind == RefKind.In || paramRef.Parameter.RefKind == RefKind.RefReadOnly))
                        {
                            // Reading a field of an 'in' or 'ref readonly' parameter is pure
                            return true;
                        }
                        // Reading non-static-readonly fields (or non-constructor instance readonly) is impure
                        return false;
                    }
                case IPropertyReferenceOperation propRefOp: // Reading a property
                    {
                        var propSymbol = propRefOp.Property;
                        if (propSymbol.GetMethod == null) return false; // No getter

                        // *** ADDED CHECK ***
                        // If accessing a readonly property via an 'in' or 'ref readonly' parameter instance, assume pure.
                        if (propSymbol.IsReadOnly &&
                            propRefOp.Instance is IParameterReferenceOperation paramRef &&
                            (paramRef.Parameter.RefKind == RefKind.In || paramRef.Parameter.RefKind == RefKind.RefReadOnly))
                        {
                            return true;
                        }
                        // *** END ADDED CHECK ***

                        // Check known impure getters first
                        var propertyGetterName = propSymbol.ContainingType.ToDisplayString() + "." + propSymbol.Name + ".get";
                        if (KnownImpureMethods.Contains(propertyGetterName)) return false;

                        // Check getter purity (use original definition)
                        // Use null-forgiving operator !. since we checked for null getter earlier.
                        return IsConsideredPure(propSymbol.GetMethod!.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                    }
                case ILocalReferenceOperation localRefOp:
                    {
                        // If local isn't found, it might be an error state, assume impure.
                        // Add null-forgiving operator as compiler warns localPurityStatus might be null.
                        return localPurityStatus.TryGetValue(localRefOp.Local, out bool isPure) && isPure;
                    }
                case IParameterReferenceOperation paramRefOp:
                    {
                        return true;
                    }
                case IInstanceReferenceOperation instRefOp when instRefOp.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance: // 'this' or 'base'
                    return true;
                case IEventReferenceOperation _: return false; // Referencing event often leads to +=/-=

                // --- Literals / Constants / Defaults ---
                case ILiteralOperation literalOp: return literalOp.Type?.SpecialType != SpecialType.System_IntPtr && literalOp.Type?.SpecialType != SpecialType.System_UIntPtr; // Exclude pointers
                case IDefaultValueOperation _: return true;

                // --- Binary/Unary Operations ---
                case IBinaryOperation binaryOp:
                    {
                        // Check for user-defined operator
                        if (binaryOp.OperatorMethod != null)
                        {
                            return IsConsideredPure(binaryOp.OperatorMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                        }
                        // Handle && and || specifically? Might short-circuit.
                        // For now, assume both operands are evaluated conceptually for purity check.
                        return IsOperationPure(binaryOp.LeftOperand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                               IsOperationPure(binaryOp.RightOperand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                case IUnaryOperation unaryOp:
                    {
                        if (unaryOp.OperatorMethod != null)
                        {
                            return IsConsideredPure(unaryOp.OperatorMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                        }
                        // Check for pointer indirection * / ->
                        // OperationKind.PointerIndirectionReference doesn't seem reliable.
                        // Remove syntax check causing CS0154
                        // if (unaryOp.Syntax is PrefixUnaryExpressionSyntax { Kind: SyntaxKind.PointerIndirectionExpression } ||
                        //     unaryOp.Syntax is MemberAccessExpressionSyntax { Kind: SyntaxKind.PointerMemberAccessExpression }) return false;

                        // Purity depends on operand for standard ops like +, -, !, ~
                        return IsOperationPure(unaryOp.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }

                // --- Conditional Operations ---
                case IConditionalOperation conditionalOp: // ternary ?: or if statement expression
                    {
                        return IsOperationPure(conditionalOp.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                               IsOperationPure(conditionalOp.WhenTrue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                               (conditionalOp.WhenFalse == null || IsOperationPure(conditionalOp.WhenFalse, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus));
                    }
                case ICoalesceOperation coalesceOp: // ??
                    {
                        return IsOperationPure(coalesceOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                               IsOperationPure(coalesceOp.WhenNull, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                case IConditionalAccessOperation conditionalAccess: // ?.
                    {
                        // Pure if the accessed operation (WhenNotNull) is pure AND the receiver is pure.
                        bool receiverPure = IsOperationPure(conditionalAccess.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                        bool accessedPure = IsOperationPure(conditionalAccess.WhenNotNull, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                        return receiverPure && accessedPure;
                    }
                case IConditionalAccessInstanceOperation conditionalInstance: return true; // Reading the instance part of ?. is pure if the underlying expr is pure, which is checked by receiverPure above.


                // --- Type Operations ---
                case ITypeOfOperation _: return true;
                case ISizeOfOperation _:
                    return true;

                case IIsTypeOperation isOp: // Handles `is T` and `is T x`
                    // --- Debug Logging ---
                    // System.Diagnostics.Debug.WriteLine($"IsOperationPure In IIsTypeOperation Check: Kind={operation.Kind}, Type={operation.GetType().FullName}, ChildrenCount={operation.ChildOperations.Count()}, Syntax='{operation.Syntax?.ToString() ?? "N/A"}'");
                    // --- End Debug Logging ---
                    // Try accessing via ChildOperations instead of Operand
                    if (operation.ChildOperations.Count() != 1) return false; // Safety check - Use ChildOperations
                    var operandFromChildren = operation.ChildOperations.Single(); // Use ChildOperations
                    return IsOperationPure(operandFromChildren, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                case IConversionOperation convOp: // Casts, implicit conversions
                    if (convOp.OperatorMethod != null)
                    {
                        return IsConsideredPure(convOp.OperatorMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                    }
                    return IsOperationPure(convOp.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);

                /* Start Commenting Object Creation etc.
                // --- Object Creation ---
                case IObjectCreationOperation objCreationOp:
                    {
                        if (objCreationOp.Constructor == null) return false;
                        // Use OriginalDefinition for constructor check
                        bool constructorIsPure = IsConsideredPure(objCreationOp.Constructor.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                        if (!constructorIsPure) return false;

                        // Check arguments
                        foreach (var arg in objCreationOp.Arguments)
                        {
                            if (!IsOperationPure(arg.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        }

                        // Check initializer (if any)
                        if (objCreationOp.Initializer != null)
                        {
                            if (!IsOperationPure(objCreationOp.Initializer, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        }

                        return true;
                    }
                case IAnonymousObjectCreationOperation anonObjOp:
                    { // Check initializers
                        return anonObjOp.Initializers.All(init => IsOperationPure(init, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus));
                    }
                case ITypeParameterObjectCreationOperation typeParamObjOp: // new T()
                    { // Relies on default constructor being pure
                        // Find parameterless constructor using GetMembers
                        var ctor = typeParamObjOp.Type?.GetMembers(".ctor")
                                     .OfType<IMethodSymbol>()
                                     .FirstOrDefault(c => c.Parameters.Length == 0 && c.MethodKind == MethodKind.Constructor);
                        return ctor == null || IsConsideredPure(ctor.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
                    }
                case IObjectOrCollectionInitializerOperation initializerOp: // The initializer part itself
                    {
                        foreach (var init in initializerOp.Initializers)
                        {
                            if (!IsOperationPure(init, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                case IMemberInitializerOperation memberInitOp: // e.g., new X { Prop = Value }
                    {
                        // The initializer is the assignment operation itself
                        return IsOperationPure(memberInitOp.Initializer, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                case IArrayCreationOperation arrayCreationOp: // new T[...]
                    {
                        // Check dimension sizes
                        foreach (var dim in arrayCreationOp.DimensionSizes)
                        {
                            if (!IsOperationPure(dim, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        }
                        // Check initializer
                        if (arrayCreationOp.Initializer != null && !IsOperationPure(arrayCreationOp.Initializer, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                        return true;
                    }
                case IArrayInitializerOperation arrayInitOp: // { a, b, c }
                    {
                        return arrayInitOp.ElementValues.All(e => IsOperationPure(e, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus));
                    }
                case IDelegateCreationOperation delegateCreation: // new Action(...) or MethodGroup
                    { // Creating delegate is pure if target expression (if any) is pure
                        return delegateCreation.Target == null || IsOperationPure(delegateCreation.Target, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                case IAnonymousFunctionOperation _: return true; // Defining a lambda/anon method is pure
                End Commenting Object Creation etc. */


                // Remove comment markers
                // --- Others ---
                case IParenthesizedOperation parenOp: // (expr)
                    return IsOperationPure(parenOp.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); // Removed !
                case IMethodReferenceOperation _: return true; // Referencing a method itself is pure
                case INameOfOperation _: return true; // nameof() is pure
                case ITupleOperation tupleOp: // Tuple literal (a, b)
                    return tupleOp.Elements.All(e => IsOperationPure(e, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)); // Removed !
                case IArgumentOperation argOp: // Check the underlying value of the argument
                    return IsOperationPure(argOp.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); // Removed !
                case IInterpolatedStringOperation intpStrOp:
                    return intpStrOp.Parts.All(p => IsOperationPure(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)); // Removed !
                case IInterpolatedStringTextOperation _: return true; // Constant text part
                // Remove comment markers
                /* Comment out IInterpolationOperation 
                case IInterpolationOperation interpolationOp: // The {expr} part
                    return IsOperationPure(interpolationOp.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); // Removed !
                */
                // Uncomment IBlockOperation
                /* Start commenting IBlockOperation again
                case IBlockOperation blockOp: // Block in expression context (e.g., lambda expr body)
                    var blockLocalStatus = new Dictionary<ILocalSymbol, bool>(localPurityStatus, SymbolEqualityComparer.Default);
                    // Blocks used as expressions are only pure if they consist of a single return statement with a pure value.
                    if (blockOp.Operations.Length == 1 && blockOp.Operations[0] is IReturnOperation returnOp)
                    {
                        return IsOperationPure(returnOp.ReturnedValue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, blockLocalStatus!); // Re-added !
                    }
                    return false; // Otherwise impure in expression context
                End commenting IBlockOperation again */
                case IDeclarationExpressionOperation declExpr: return IsOperationPure(declExpr.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); // Removed !
                case IAwaitOperation awaitOp: // await
                    // Await itself doesn't cause impurity, depends on awaited expression
                    // return IsOperationPure(awaitOp.Operation, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); // Removed !
                    return false; // Assume await is impure unless origin is known pure task (future enhancement)
                case ITranslatedQueryOperation _: return false; // LINQ query translations are complex, assume impure

                // Patterns (Check value operand + subpatterns if applicable)
                case IIsPatternOperation isPattern:
                    // Restore Original:
                    return IsOperationPure(isPattern.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) && IsOperationPure(isPattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                case IConstantPatternOperation constPattern:
                    // Restore Original:
                    return IsOperationPure(constPattern.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                case IDeclarationPatternOperation _: return true; // Pattern itself is pure check - KEEP AS IS
                case IRecursivePatternOperation recursivePattern: // Type { Prop: pattern }
                                                                  // Restore Original:
                    return recursivePattern.DeconstructionSubpatterns.All(p => IsOperationPure(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) && recursivePattern.PropertySubpatterns.All(p => IsOperationPure(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus));
                case IRelationalPatternOperation relPattern:
                    // Restore Original:
                    return IsOperationPure(relPattern.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                case ITypePatternOperation _: return true; // Pattern itself is pure check - KEEP AS IS
                case INegatedPatternOperation notPattern:
                    // Restore Original:
                    return IsOperationPure(notPattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                case IBinaryPatternOperation binaryPattern: // and/or pattern
                    // Restore Original:
                    return IsOperationPure(binaryPattern.LeftPattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) && IsOperationPure(binaryPattern.RightPattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                case IDiscardPatternOperation _: return true; // `_` pattern is pure - KEEP AS IS
                case IPropertySubpatternOperation propSubpattern: // Prop: pattern
                    // Restore Original: 
                    // Need to check the member reference + the subpattern
                    bool memberPure = IsOperationPure(propSubpattern.Member, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    bool patternPure = IsOperationPure(propSubpattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    return memberPure && patternPure;
                /* Comment out IListPatternOperation case
                case IListPatternOperation listPattern: // [p1, p2, ..]
                    // --- Debug Logging ---
                    System.Diagnostics.Debug.WriteLine($"IsOperationPure In IListPatternOperation: PatternsCount={listPattern.Patterns.Length}, Syntax='{listPattern.Syntax?.ToString() ?? "N/A"}', LocalCount={localPurityStatus?.Count ?? -1}");
                    // --- End Debug Logging ---
                    return listPattern.Patterns.All(p => 
                    {
                        // --- Debug Logging ---
                        System.Diagnostics.Debug.WriteLine($"  IListPatternOp Checking Pattern: Kind={p?.Kind}, Syntax='{p?.Syntax?.ToString() ?? "N/A"}'");
                        if (p == null) return false; // Add null check for pattern
                        // --- End Debug Logging ---
                        // Try removing null-forgiving operator here specifically
                        return IsOperationPure(p, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); 
                    }); // Use Patterns
                */
                case ISlicePatternOperation slicePattern: // .. or .. p
                                                          // --- Debug Logging ---
                                                          // System.Diagnostics.Debug.WriteLine($"IsOperationPure In ISlicePatternOperation: HasPattern={slicePattern.Pattern != null}, Syntax='{slicePattern.Syntax?.ToString() ?? "N/A"}', LocalCount={localPurityStatus?.Count ?? -1}");
                                                          // --- End Debug Logging ---
                    return slicePattern.Pattern == null || IsOperationPure(slicePattern.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus!);


                // Switch Expressions
                case ISwitchExpressionOperation switchExpr:
                    // Restore Original:
                    return IsOperationPure(switchExpr.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) && // Added !
                           switchExpr.Arms.All(arm => IsOperationPure(arm, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)); // Added !
                case ISwitchExpressionArmOperation switchArm:
                    // Restore Original: 
                    return IsOperationPure(switchArm.Pattern, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) && // Check pattern first Added !
                           (switchArm.Guard == null || IsOperationPure(switchArm.Guard, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) && // Added !
                           IsOperationPure(switchArm.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus); // Added !

                // --- Return Value --- Add case for IReturnOperation
                case IReturnOperation returnOp:
                    return IsOperationPure(returnOp.ReturnedValue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);

                // --- Operations considered IMPURE by default --- 
                case IAddressOfOperation _: return false; // Taking address
                case ILocalFunctionOperation _: return false; // Defining is pure, but using it depends on call site analysis (handled by invocation)
                case IThrowOperation _: return false; // throw statement
                case IUsingOperation _: return false; // using(..) or await using(..)

                // Add explicit handling for indexers - REMOVED as handled by IPropertyReferenceOperation
                // case IElementAccessOperation _: return false; // Assume impure for now // Fixed type name

                default:
                    // If we haven't handled this specific operation type, assume impure.
                    // System.Diagnostics.Debug.WriteLine($"Unhandled Operation for Purity: {operation.Kind} -> {operation.Syntax?.ToString() ?? "N/A"}");
                    return false;
            }
        }

        /// <summary>
        /// Checks property setter purity. Currently assumes impure unless init-only in constructor.
        /// </summary>
        private static bool IsPropertySetterPure(
            IPropertySymbol propertySymbol,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol)
        {
            // If setter is null (e.g. getter-only property), assignment isn't possible via normal means.
            if (propertySymbol.SetMethod == null) return true; // Cannot assign

            // Check for IsInitOnly property
            bool isInitOnly = propertySymbol.SetMethod.IsInitOnly;

            // Allow assigning init-only properties only within the constructor
            if (isInitOnly && containingMethodSymbol.MethodKind == MethodKind.Constructor)
            {
                return false; // Safer assumption without analysis
            }

            // return IsConsideredPure(propertySymbol.SetMethod.OriginalDefinition, context, enforcePureAttributeSymbol, visited);
            return false; // Default to impure for general setters without analysis
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