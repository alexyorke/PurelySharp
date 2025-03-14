using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace PureMethodAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PureMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PMA0001";

        private static readonly LocalizableString Title = "Method marked with [EnforcePure] must be pure";
        private static readonly LocalizableString MessageFormat = "Method '{0}' is marked as [EnforcePure] but contains impure operations";
        private static readonly LocalizableString Description = "Methods marked with [EnforcePure] must be pure (no side effects, only pure operations).";
        private const string Category = "Purity";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null)
                return;

            bool hasEnforcePureAttribute = methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == "EnforcePureAttribute");

            if (!hasEnforcePureAttribute)
                return;

            if (!IsMethodPure(methodDeclaration, context.SemanticModel))
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodDeclaration.Identifier.Text);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsMethodPure(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null)
                return false;

            // For expression-bodied members
            if (methodDeclaration.ExpressionBody != null)
            {
                return IsExpressionPure(methodDeclaration.ExpressionBody.Expression, semanticModel, methodSymbol);
            }

            // For regular method bodies
            if (methodDeclaration.Body != null)
            {
                return AreStatementsPure(methodDeclaration.Body.Statements, semanticModel, methodSymbol);
            }

            // Abstract or interface methods are considered pure
            return true;
        }

        private bool IsExpressionPure(ExpressionSyntax? expression, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            if (expression == null) return true;

            switch (expression)
            {
                case LiteralExpressionSyntax:
                    return true;

                case IdentifierNameSyntax identifier:
                    var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                    return symbol != null && IsPureSymbol(symbol);

                case InvocationExpressionSyntax invocation:
                    var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                        return false;

                    // Handle recursive calls
                    if (currentMethod != null && SymbolEqualityComparer.Default.Equals(methodSymbol, currentMethod))
                        return invocation.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                    // Check if it's a known pure method (LINQ, Math, etc.)
                    if (IsKnownPureMethod(methodSymbol))
                        return invocation.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                    // Check if it's marked with [EnforcePure]
                    bool isCalledMethodPure = methodSymbol.GetAttributes().Any(attr =>
                        attr.AttributeClass?.Name == "EnforcePureAttribute");

                    return isCalledMethodPure && invocation.ArgumentList.Arguments
                        .All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                case MemberAccessExpressionSyntax memberAccess:
                    var memberSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (memberSymbol == null)
                        return false;

                    // Handle string methods
                    if (memberSymbol is IMethodSymbol stringMethod &&
                        stringMethod.ContainingType.SpecialType == SpecialType.System_String)
                    {
                        return true; // All string methods are pure
                    }

                    // Handle Math constants
                    if (memberSymbol is IFieldSymbol field &&
                        field.IsStatic &&
                        field.ContainingType.Name == "Math" &&
                        IsInNamespace(field.ContainingType, "System"))
                    {
                        return true;
                    }

                    // Handle other member access
                    return IsPureSymbol(memberSymbol) &&
                           (memberAccess.Expression == null || IsExpressionPure(memberAccess.Expression, semanticModel, currentMethod));

                case ObjectCreationExpressionSyntax objectCreation:
                    var typeSymbol = semanticModel.GetSymbolInfo(objectCreation.Type).Symbol as ITypeSymbol;
                    if (typeSymbol == null)
                        return false;

                    // Check if it's a known impure type
                    var impureTypes = new[] { "Random", "FileStream", "StreamWriter", "StreamReader" };
                    if (impureTypes.Contains(typeSymbol.Name))
                        return false;

                    return objectCreation.ArgumentList?.Arguments.All(arg =>
                        IsExpressionPure(arg.Expression, semanticModel, currentMethod)) ?? true;

                case BinaryExpressionSyntax binary:
                    return IsExpressionPure(binary.Left, semanticModel, currentMethod) &&
                           IsExpressionPure(binary.Right, semanticModel, currentMethod);

                case ParenthesizedExpressionSyntax paren:
                    return IsExpressionPure(paren.Expression, semanticModel, currentMethod);

                case ConditionalExpressionSyntax conditional:
                    return IsExpressionPure(conditional.Condition, semanticModel, currentMethod) &&
                           IsExpressionPure(conditional.WhenTrue, semanticModel, currentMethod) &&
                           IsExpressionPure(conditional.WhenFalse, semanticModel, currentMethod);

                case PrefixUnaryExpressionSyntax prefix:
                    return IsExpressionPure(prefix.Operand, semanticModel, currentMethod);

                case PostfixUnaryExpressionSyntax postfix:
                    return IsExpressionPure(postfix.Operand, semanticModel, currentMethod);

                case SimpleLambdaExpressionSyntax lambda:
                    return IsExpressionPure(lambda.Body as ExpressionSyntax, semanticModel, currentMethod);

                case ParenthesizedLambdaExpressionSyntax lambda:
                    if (lambda.Body is ExpressionSyntax expr)
                        return IsExpressionPure(expr, semanticModel, currentMethod);
                    if (lambda.Body is BlockSyntax block)
                        return AreStatementsPure(block.Statements, semanticModel, currentMethod);
                    return false;

                default:
                    return false;
            }
        }

        private bool AreStatementsPure(SyntaxList<StatementSyntax> statements, SemanticModel semanticModel, IMethodSymbol currentMethod)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case ReturnStatementSyntax returnStmt:
                        if (!IsExpressionPure(returnStmt.Expression, semanticModel, currentMethod))
                            return false;
                        break;

                    case LocalDeclarationStatementSyntax localDecl:
                        foreach (var variable in localDecl.Declaration.Variables)
                        {
                            if (!IsExpressionPure(variable.Initializer?.Value, semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case ExpressionStatementSyntax exprStmt:
                        if (!IsExpressionPure(exprStmt.Expression, semanticModel, currentMethod))
                            return false;
                        break;

                    case IfStatementSyntax ifStmt:
                        if (!IsExpressionPure(ifStmt.Condition, semanticModel, currentMethod))
                            return false;
                        if (ifStmt.Statement is BlockSyntax block)
                        {
                            if (!AreStatementsPure(block.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else
                        {
                            if (!AreStatementsPure(SyntaxFactory.SingletonList(ifStmt.Statement), semanticModel, currentMethod))
                                return false;
                        }
                        if (ifStmt.Else != null)
                        {
                            if (ifStmt.Else.Statement is BlockSyntax elseBlock)
                            {
                                if (!AreStatementsPure(elseBlock.Statements, semanticModel, currentMethod))
                                    return false;
                            }
                            else
                            {
                                if (!AreStatementsPure(SyntaxFactory.SingletonList(ifStmt.Else.Statement), semanticModel, currentMethod))
                                    return false;
                            }
                        }
                        break;

                    // Any other statement type is considered impure
                    default:
                        return false;
                }
            }

            return true;
        }

        private bool IsKnownPureMethod(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's a LINQ method
            if (IsInNamespace(method, "System.Linq") &&
                method.ContainingType.Name == "Enumerable")
            {
                return true;
            }

            // Check if it's a Math method
            if (IsInNamespace(method, "System") &&
                method.ContainingType.Name == "Math")
            {
                return true;
            }

            // Check if it's a string method
            if (method.ContainingType.SpecialType == SpecialType.System_String)
            {
                return true; // All string methods are pure
            }

            // Check for known impure types
            var impureNamespaces = new[] { "System.IO", "System.Net", "System.Data" };
            foreach (var ns in impureNamespaces)
            {
                if (IsInNamespace(method, ns))
                    return false;
            }

            // Check for known impure types
            var impureTypes = new[] { "Random", "DateTime", "File", "Console", "Process" };
            if (impureTypes.Contains(method.ContainingType.Name))
                return false;

            // Check if it's marked with [EnforcePure]
            if (method.GetAttributes().Any(attr => attr.AttributeClass?.Name == "EnforcePureAttribute"))
                return true;

            return false;
        }

        private bool IsPureSymbol(ISymbol symbol)
        {
            switch (symbol)
            {
                case IParameterSymbol:
                case ILocalSymbol:
                    return true;

                case IPropertySymbol property:
                    // Only allow get-only properties or auto-implemented properties
                    return property.IsReadOnly ||
                           (property.GetMethod != null && !property.SetMethod?.IsInitOnly != true);

                case IFieldSymbol field:
                    // Allow readonly fields and static constants
                    return field.IsReadOnly || (field.IsStatic && field.IsConst);

                case IMethodSymbol method:
                    // Allow pure methods
                    return IsKnownPureMethod(method);

                default:
                    return false;
            }
        }

        private bool IsInNamespace(ISymbol symbol, string ns)
        {
            var current = symbol.ContainingNamespace;
            while (current != null)
            {
                if (current.ToDisplayString() == ns)
                    return true;
                current = current.ContainingNamespace;
            }
            return false;
        }
    }
}
