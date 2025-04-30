using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine; // Namespace for PurityAnalysisEngine
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes variable declaration groups for potential side effects in initializers.
    /// </summary>
    internal class VariableDeclarationGroupPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.VariableDeclarationGroup);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IVariableDeclarationGroupOperation groupOperation)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            PurityAnalysisEngine.LogDebug($"  [VarDeclGrpRule] Checking VariableDeclarationGroup: {groupOperation.Syntax}");

            foreach (var declaration in groupOperation.Declarations)
            {
                PurityAnalysisEngine.LogDebug($"    [VarDeclGrpRule] Checking Declaration: {declaration.Syntax}");
                foreach (var declarator in declaration.Declarators)
                {
                    PurityAnalysisEngine.LogDebug($"      [VarDeclGrpRule] Checking Declarator: {declarator.Symbol.Name}");
                    if (declarator.Initializer != null)
                    {
                        PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule] Checking Initializer: {declarator.Initializer.Syntax}");
                        var initializerResult = PurityAnalysisEngine.CheckSingleOperation(declarator.Initializer.Value, context);
                        if (!initializerResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule] --> IMPURE Initializer found: {declarator.Initializer.Syntax}");
                            // Propagate the specific impure node from the initializer if possible
                            return initializerResult.ImpureSyntaxNode != null
                                   ? PurityAnalysisEngine.PurityAnalysisResult.Impure(initializerResult.ImpureSyntaxNode)
                                   : PurityAnalysisEngine.PurityAnalysisResult.Impure(declarator.Initializer.Syntax);
                        }
                        PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule] Initializer is Pure.");
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"      [VarDeclGrpRule] Declarator has no initializer. Pure.");
                    }
                }
            }

            PurityAnalysisEngine.LogDebug($"  [VarDeclGrpRule] VariableDeclarationGroup determined PURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}