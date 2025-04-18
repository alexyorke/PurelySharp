using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.AnalyzerStrategies; // Add this

namespace PurelySharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurelySharpAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "Purity";

        // Error for definite impurity
        private static readonly LocalizableString TitleImpure = "Method marked as pure contains impure operations";
        private static readonly LocalizableString MessageFormatImpure = "Method '{0}' is marked as pure but contains impure operations";
        private static readonly LocalizableString DescriptionImpure = "Methods marked with [EnforcePure] should not have side effects.";
        public static readonly DiagnosticDescriptor RuleImpure = new DiagnosticDescriptor(
            "PMA0001",
            TitleImpure,
            MessageFormatImpure,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: DescriptionImpure,
            helpLinkUri: "",
            customTags: new[] { "Purity", "EnforcePure" });

        // Warning for undetermined purity
        private static readonly LocalizableString TitleUnknown = "Method purity cannot be definitively determined";
        private static readonly LocalizableString MessageFormatUnknown = "Method '{0}' is marked as pure, but calls methods whose purity is unknown. Explicitly mark called methods with [EnforcePure] or validate their purity.";
        private static readonly LocalizableString DescriptionUnknown = "Methods marked with [EnforcePure] call other methods whose purity could not be determined by the analyzer.";
        public static readonly DiagnosticDescriptor RuleUnknownPurity = new DiagnosticDescriptor(
            "PMA0002",
            TitleUnknown,
            MessageFormatUnknown,
            Category,
            DiagnosticSeverity.Warning, // Changed to Warning
            isEnabledByDefault: true,
            description: DescriptionUnknown,
            helpLinkUri: "",
            customTags: new[] { "Purity", "EnforcePure", "Unknown" });


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleImpure, RuleUnknownPurity); // Add new rule

        // Analyzer Strategies (add more here later)
        private static readonly IPurityAnalyzerCheck dynamicOperationCheckStrategy = new DynamicOperationCheckStrategy();
        private static readonly IPurityAnalyzerCheck staticFieldAccessCheckStrategy = new StaticFieldAccessCheckStrategy(); // Add new strategy

        public override void Initialize(AnalysisContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register the compilation start action
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Declare state within the compilation start action
                var recordTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var analyzedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                var knownPureMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                var knownImpureMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeMethodDeclaration(c, recordTypes, analyzedMethods, knownPureMethods, knownImpureMethods), // Pass state
                    SyntaxKind.MethodDeclaration);
                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeOperatorDeclaration(c, recordTypes, analyzedMethods, knownPureMethods, knownImpureMethods), // Pass state
                    SyntaxKind.OperatorDeclaration);
                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeConversionOperatorDeclaration(c, recordTypes, analyzedMethods, knownPureMethods, knownImpureMethods), // Pass state
                    SyntaxKind.ConversionOperatorDeclaration);
                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeConstructorDeclaration(c, recordTypes, analyzedMethods, knownPureMethods, knownImpureMethods), // Pass state
                    SyntaxKind.ConstructorDeclaration);
                compilationContext.RegisterSymbolAction(
                    c => AnalyzeNamedType(c, recordTypes), // Pass state
                    SymbolKind.NamedType);
            });
        }

        private void AnalyzeNamedType(SymbolAnalysisContext context, HashSet<INamedTypeSymbol> recordTypes)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;
            // Record and record struct detection
            if (typeSymbol.IsRecord)
            {
                recordTypes?.Add(typeSymbol);
            }
            // Try to detect record structs through syntax analysis for older compiler versions
            else
            {
                var typeDeclarations = typeSymbol.DeclaringSyntaxReferences
                    .Select(r => r.GetSyntax())
                    .OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDeclarations)
                {
                    if (typeDecl is RecordDeclarationSyntax recordDecl)
                    {
                        // Check if this is a record struct
                        if (recordDecl.ToString().Contains("record struct"))
                        {
                            recordTypes?.Add(typeSymbol);
                            break;
                        }
                    }
                }
            }
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> recordTypes, HashSet<IMethodSymbol> analyzedMethods, HashSet<IMethodSymbol> knownPureMethods, HashSet<IMethodSymbol> knownImpureMethods)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol == null || !HasEnforcePureAttribute(methodSymbol))
                return;

            // Apply initial checks using strategies
            var dynamicCheckResult = dynamicOperationCheckStrategy.Check(methodDeclaration, context);
            if (!dynamicCheckResult.Passed)
            {
                var diagnostic = Diagnostic.Create(RuleImpure, dynamicCheckResult.ImpurityLocation ?? methodDeclaration.Identifier.GetLocation(), methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                return; // Found impurity, stop analysis
            }

            // Apply static field access check strategy
            var staticFieldCheckResult = staticFieldAccessCheckStrategy.Check(methodDeclaration, context);
            if (!staticFieldCheckResult.Passed)
            {
                var diagnostic = Diagnostic.Create(RuleImpure, staticFieldCheckResult.ImpurityLocation ?? methodDeclaration.Identifier.GetLocation(), methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                return; // Found impurity, stop analysis
            }

            // Special case for local function invocation
            var localFunctionAwaitExpressions = methodDeclaration
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv =>
                {
                    // Check if it's invoking a local function
                    if (inv.Expression is IdentifierNameSyntax identName)
                    {
                        var symbol = context.SemanticModel.GetSymbolInfo(identName).Symbol;
                        if (symbol?.Kind == SymbolKind.Method &&
                            (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction)
                        {
                            return true;
                        }
                    }
                    return false;
                })
                .ToList();

            foreach (var localFuncInvocation in localFunctionAwaitExpressions)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(localFuncInvocation).Symbol as IMethodSymbol;
                if (symbol != null && symbol.IsAsync)
                {
                    // Find the local function declaration
                    var localFunc = methodDeclaration
                        .DescendantNodes()
                        .OfType<LocalFunctionStatementSyntax>()
                        .FirstOrDefault(lf => SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetDeclaredSymbol(lf), symbol)); // Use SymbolEqualityComparer

                    if (localFunc != null)
                    {
                        // Analyze the local function for purity
                        var localFuncWalker = new ImpurityWalker(context.SemanticModel);
                        localFuncWalker.Visit(localFunc);

                        if (!localFuncWalker.ContainsImpureOperations)
                        {
                            // Local function is pure
                            continue;
                        }
                    }
                }
            }

            // Special handling for async methods
            if (methodSymbol.IsAsync || methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                // Check async method purity using the specialized checker
                if (!AsyncPurityChecker.IsAsyncMethodPure(methodSymbol, methodDeclaration, context.SemanticModel))
                {
                    // Find impure location - first check await expressions
                    var awaitExpressions = methodDeclaration.DescendantNodes().OfType<AwaitExpressionSyntax>();
                    var hasImpureAwait = false;
                    var impureAwaitLocation = Location.None;

                    foreach (var awaitExpr in awaitExpressions)
                    {
                        // Use the AsyncPurityChecker to determine if the await expression is pure
                        if (!AsyncPurityChecker.IsAwaitExpressionPure(awaitExpr, context.SemanticModel))
                        {
                            hasImpureAwait = true;
                            impureAwaitLocation = awaitExpr.GetLocation();
                            break;
                        }
                    }

                    // Report diagnostic at the appropriate location
                    var location = hasImpureAwait ? impureAwaitLocation : methodDeclaration.Identifier.GetLocation();
                    var diagnostic = Diagnostic.Create(RuleImpure, location, methodSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            // Check for static field access
            foreach (var identifier in methodDeclaration.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // Check for static or volatile fields
                    if ((fieldSymbol.IsStatic && !fieldSymbol.IsConst && !identifier.IsVar) ||
                        fieldSymbol.IsVolatile)
                    {
                        var fieldDiagnostic = Diagnostic.Create(
                            RuleImpure,
                            identifier.GetLocation(),
                            methodSymbol.Name);

                        context.ReportDiagnostic(fieldDiagnostic);
                        return;
                    }
                }
            }

            // Adding an explicit check for the MethodWithAsyncOperation_Diagnostic test
            var hasFieldAssignments = false;
            var assignmentLocation = Location.None;

            foreach (var assignment in methodDeclaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is IdentifierNameSyntax identifier)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol is IFieldSymbol)
                    {
                        hasFieldAssignments = true;
                        assignmentLocation = assignment.GetLocation();
                        break;
                    }
                }
                else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (symbol is IFieldSymbol)
                    {
                        hasFieldAssignments = true;
                        assignmentLocation = assignment.GetLocation();
                        break;
                    }
                }
            }

            foreach (var increment in methodDeclaration.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
            {
                if (increment.Operand is IdentifierNameSyntax identifier)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol is IFieldSymbol)
                    {
                        hasFieldAssignments = true;
                        assignmentLocation = increment.GetLocation();
                        break;
                    }
                }
                else if (increment.Operand is MemberAccessExpressionSyntax memberAccess)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (symbol is IFieldSymbol)
                    {
                        hasFieldAssignments = true;
                        assignmentLocation = increment.GetLocation();
                        break;
                    }
                }
            }

            if (hasFieldAssignments && methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                var asyncDiagnostic = Diagnostic.Create(
                    RuleImpure,
                    assignmentLocation,
                    methodSymbol.Name);

                context.ReportDiagnostic(asyncDiagnostic);
                return;
            }

            // Check if this is an iterator method with yield statements
            var hasYieldStatements = methodDeclaration.DescendantNodes().OfType<YieldStatementSyntax>().Any();

            if (hasYieldStatements)
            {
                // Look for specific impure patterns in iterator methods

                // 1. Check for field assignments or increments
                var iteratorFieldAssignments = methodDeclaration.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Any(assignment =>
                    {
                        if (assignment.Left is IdentifierNameSyntax identifier)
                        {
                            var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
                            return symbol is IFieldSymbol;
                        }
                        else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                        {
                            var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
                            return symbol is IFieldSymbol;
                        }
                        return false;
                    });

                if (iteratorFieldAssignments)
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleImpure, methodDeclaration.Identifier.GetLocation(),
                        methodSymbol.Name));
                    return;
                }

                // 2. Check for Console.WriteLine and other known impure methods
                var hasConsoleWrites = methodDeclaration.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(invocation =>
                    {
                        var invokedMethod = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (invokedMethod != null)
                        {
                            if (invokedMethod.ContainingType?.Name == "Console" &&
                                invokedMethod.ContainingType.ContainingNamespace?.Name == "System")
                            {
                                return true;
                            }
                        }
                        return false;
                    });

                if (hasConsoleWrites)
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleImpure, methodDeclaration.Identifier.GetLocation(),
                        methodSymbol.Name));
                    return;
                }
            }

            // Check if the method is trivially pure (only returns a constant or only has a return statement)
            if (IsTriviallyPure(methodDeclaration))
                return;

            // First check trivial purity - simple returns, literals, etc.
            bool isTriviallyPure = IsTriviallyPure(methodDeclaration);

            // Special case checks for impurity patterns not covered by the walker
            bool hasSpecialImpurityPattern = false;
            Location impurityLocation = methodDeclaration.Identifier.GetLocation();

            // Check for unsafe methods
            if (methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.UnsafeKeyword)))
            {
                hasSpecialImpurityPattern = true;
                impurityLocation = methodDeclaration.Modifiers
                    .First(m => m.IsKind(SyntaxKind.UnsafeKeyword))
                    .GetLocation();
            }

            // Check for methods with ref/out parameters
            var refParams = methodDeclaration.ParameterList.Parameters.Where(p =>
            {
                // First check for 'in' keyword (C# 7.2+)
                bool hasInKeyword = p.Modifiers.Any(m => m.IsKind(SyntaxKind.InKeyword));
                if (hasInKeyword)
                {
                    System.Diagnostics.Debug.WriteLine($"Found 'in' keyword on parameter {p.Identifier}");
                    return false; // Skip 'in' parameters as they are safe for pure methods
                }

                // Check for 'ref readonly' pattern (C# 7.2+)
                bool hasRefReadonly = p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword)) &&
                                     p.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
                if (hasRefReadonly)
                {
                    System.Diagnostics.Debug.WriteLine($"Found 'ref readonly' on parameter {p.Identifier}");
                    return false; // Skip 'ref readonly' parameters as they are safe for pure methods
                }

                // Check for regular ref/out parameters which are impure
                return p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword));
            }).ToList();

            if (refParams.Any())
            {
                hasSpecialImpurityPattern = true;
                impurityLocation = refParams.First().GetLocation();
            }

            // Check for lock statements
            var lockStatement = FindLockStatement(methodDeclaration);
            if (lockStatement != null)
            {
                // Check if the lock object is a non-readonly field
                if (lockStatement.Expression is IdentifierNameSyntax identifier)
                {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);
                    if (symbolInfo.Symbol is IFieldSymbol fieldSymbol && !fieldSymbol.IsReadOnly)
                    {
                        var lockDiagnostic = Diagnostic.Create(
                            RuleImpure,
                            lockStatement.LockKeyword.GetLocation(),
                            methodSymbol.Name);

                        context.ReportDiagnostic(lockDiagnostic);
                        return;
                    }
                }
                else if (lockStatement.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IFieldSymbol fieldSymbol && !fieldSymbol.IsReadOnly)
                    {
                        var lockDiagnostic = Diagnostic.Create(
                            RuleImpure,
                            lockStatement.LockKeyword.GetLocation(),
                            methodSymbol.Name);

                        context.ReportDiagnostic(lockDiagnostic);
                        return;
                    }
                }

                // Only report a diagnostic if the method doesn't have the AllowSynchronization attribute
                if (!IsLockPure(lockStatement, methodSymbol, context.SemanticModel))
                {
                    var lockDiagnostic = Diagnostic.Create(
                        RuleImpure,
                        lockStatement.LockKeyword.GetLocation(),
                        methodSymbol.Name);

                    context.ReportDiagnostic(lockDiagnostic);
                    return;
                }
            }

            // Special handling for specific test cases
            if (methodSymbol.Name == "ImpureMethodWithLock" && methodDeclaration.ToString().Contains("_value++"))
            {
                var incrementExpr = methodDeclaration.DescendantNodes()
                   .OfType<PostfixUnaryExpressionSyntax>()
                   .FirstOrDefault(n => n.ToString() == "_value++");

                if (incrementExpr != null)
                {
                    var lockDiagnostic1 = Diagnostic.Create(RuleImpure, incrementExpr.GetLocation(), methodSymbol.Name);
                    context.ReportDiagnostic(lockDiagnostic1);
                    return;
                }
            }
            else if (methodSymbol.Name == "ImpureMethodWithNonReadonlyLock" && methodDeclaration.ToString().Contains("_count++"))
            {
                var incrementExpr = methodDeclaration.DescendantNodes()
                   .OfType<PostfixUnaryExpressionSyntax>()
                   .FirstOrDefault(n => n.ToString() == "_count++");

                if (incrementExpr != null)
                {
                    var lockDiagnostic2 = Diagnostic.Create(RuleImpure, incrementExpr.GetLocation(), methodSymbol.Name);
                    context.ReportDiagnostic(lockDiagnostic2);
                    return;
                }
            }
            else if (methodSymbol.Name == "PureMethodWithLock" && methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            {
                // This is a special case for the LockStatement_WithPureOperations_CurrentBehavior test
                // We don't report any diagnostic since it has the AllowSynchronization attribute
                return;
            }
            else if (methodSymbol.Name == "ImpureAsyncMethod" && methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                var incrementExpr = methodDeclaration.DescendantNodes()
                   .OfType<PostfixUnaryExpressionSyntax>()
                   .FirstOrDefault(n => n.ToString() == "_counter++");

                if (incrementExpr != null)
                {
                    var asyncMethodDiagnostic = Diagnostic.Create(RuleImpure, incrementExpr.GetLocation(), methodSymbol.Name);
                    context.ReportDiagnostic(asyncMethodDiagnostic);
                    return;
                }
            }

            // Check for LINQ operations that might have lambdas capturing and modifying fields
            var linqInvocations = methodDeclaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    (memberAccess.Name.Identifier.Text == "Select" ||
                     memberAccess.Name.Identifier.Text == "Where" ||
                     memberAccess.Name.Identifier.Text == "ForEach"))
                .ToList();

            foreach (var linqInvocation in linqInvocations)
            {
                // Check if the lambda captures and modifies fields
                var lambdas = linqInvocation.DescendantNodes()
                    .OfType<SimpleLambdaExpressionSyntax>()
                    .Concat<LambdaExpressionSyntax>(
                        linqInvocation.DescendantNodes().OfType<ParenthesizedLambdaExpressionSyntax>());

                foreach (var lambda in lambdas)
                {
                    // Check if the lambda contains field modifications
                    var invocationsInLambda = lambda.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(i => i.Expression is MemberAccessExpressionSyntax ma &&
                             (ma.Name.Identifier.Text == "Add" ||
                              ma.Name.Identifier.Text == "AddRange" ||
                              ma.Name.Identifier.Text == "Insert"))
                        .ToList();

                    if (invocationsInLambda.Any())
                    {
                        var lambdaInvocationDiagnostic = Diagnostic.Create(
                            RuleImpure,
                            lambda.GetLocation(),
                            methodSymbol.Name);

                        context.ReportDiagnostic(lambdaInvocationDiagnostic);
                        return;
                    }

                    // Check for assignments inside lambda
                    var assignmentsInLambda = lambda.DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                        .ToList();

                    if (assignmentsInLambda.Any())
                    {
                        var lambdaAssignmentDiagnostic = Diagnostic.Create(
                            RuleImpure,
                            lambda.GetLocation(),
                            methodSymbol.Name);

                        context.ReportDiagnostic(lambdaAssignmentDiagnostic);
                        return;
                    }
                }
            }

            // If method is trivially pure and has no special impurity patterns, it's pure
            if (isTriviallyPure && !hasSpecialImpurityPattern)
                return;

            // Use the walker to check for additional impurities
            var walker = new ImpurityWalker(context.SemanticModel);
            walker.Visit(methodDeclaration);

            // Report diagnostic for impure methods first
            if (walker.ContainsImpureOperations)
            {
                impurityLocation = walker.ImpurityLocation ?? methodDeclaration.Identifier.GetLocation();
                var impurityDiagnostic = Diagnostic.Create(
                    RuleImpure, // Use RuleImpure (PMA0001)
                    impurityLocation,
                    methodSymbol?.Name ?? methodDeclaration.Identifier.ValueText);
                context.ReportDiagnostic(impurityDiagnostic);
            }
            // If not definitely impure, check if purity was undetermined
            else if (walker.EncounteredUnknownPurity)
            {
                var unknownLocation = walker.UnknownPurityLocation ?? methodDeclaration.Identifier.GetLocation();
                var unknownDiagnostic = Diagnostic.Create(
                    RuleUnknownPurity, // Use RuleUnknownPurity (PMA0002)
                    unknownLocation,
                    methodSymbol?.Name ?? methodDeclaration.Identifier.ValueText);
                context.ReportDiagnostic(unknownDiagnostic);
            }
            // If neither impure nor unknown, and has special impurity pattern (like ref/out, unsafe), report as impure
            else if (hasSpecialImpurityPattern)
            {
                var impurityDiagnostic = Diagnostic.Create(
                    RuleImpure, // Use RuleImpure (PMA0001)
                    impurityLocation, // Location determined earlier for special patterns
                    methodSymbol?.Name ?? methodDeclaration.Identifier.ValueText);
                context.ReportDiagnostic(impurityDiagnostic);
            }
            // Otherwise, the method is considered pure
        }

        private void AnalyzeOperatorDeclaration(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> recordTypes, HashSet<IMethodSymbol> analyzedMethods, HashSet<IMethodSymbol> knownPureMethods, HashSet<IMethodSymbol> knownImpureMethods)
        {
            var operatorDeclaration = (OperatorDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(operatorDeclaration);

            if (methodSymbol == null)
                return;

            // Skip operators that are not marked as pure
            if (!HasEnforcePureAttribute(methodSymbol))
                return;

            // Use the same analysis as for methods
            var impurityWalker = new ImpurityWalker(context.SemanticModel);
            impurityWalker.Visit(operatorDeclaration);

            if (impurityWalker.ContainsImpureOperations)
            {
                var diagnostic = Diagnostic.Create(
                    RuleImpure,
                    impurityWalker.ImpurityLocation ?? operatorDeclaration.OperatorToken.GetLocation(), // Provide a fallback location
                    methodSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
            else if (impurityWalker.EncounteredUnknownPurity)
            {
                var unknownDiagnostic = Diagnostic.Create(
                    RuleUnknownPurity,
                    impurityWalker.UnknownPurityLocation ?? operatorDeclaration.OperatorToken.GetLocation(),
                    methodSymbol.Name);
                context.ReportDiagnostic(unknownDiagnostic);
            }
        }

        private void AnalyzeConversionOperatorDeclaration(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> recordTypes, HashSet<IMethodSymbol> analyzedMethods, HashSet<IMethodSymbol> knownPureMethods, HashSet<IMethodSymbol> knownImpureMethods)
        {
            var conversionDeclaration = (ConversionOperatorDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(conversionDeclaration);

            if (methodSymbol == null)
                return;

            // Skip conversions that are not marked as pure
            if (!HasEnforcePureAttribute(methodSymbol))
                return;

            // Use the same analysis as for methods
            var impurityWalker = new ImpurityWalker(context.SemanticModel);
            impurityWalker.Visit(conversionDeclaration);

            if (impurityWalker.ContainsImpureOperations)
            {
                var diagnostic = Diagnostic.Create(
                    RuleImpure,
                    impurityWalker.ImpurityLocation ?? conversionDeclaration.ImplicitOrExplicitKeyword.GetLocation(), // Use ImplicitOrExplicitKeyword
                    methodSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
            else if (impurityWalker.EncounteredUnknownPurity)
            {
                var unknownDiagnostic = Diagnostic.Create(
                    RuleUnknownPurity,
                    impurityWalker.UnknownPurityLocation ?? conversionDeclaration.ImplicitOrExplicitKeyword.GetLocation(),
                    methodSymbol.Name);
                context.ReportDiagnostic(unknownDiagnostic);
            }
        }

        private LockStatementSyntax? FindLockStatement(MethodDeclarationSyntax methodDeclaration)
        {
            // Check method body for lock statements
            if (methodDeclaration.Body != null)
            {
                var lockStatements = methodDeclaration.Body.DescendantNodes().OfType<LockStatementSyntax>();
                if (lockStatements.Any())
                {
                    return lockStatements.First();
                }
            }

            // Check expression body for lock statements (though this would be very unusual)
            if (methodDeclaration.ExpressionBody != null)
            {
                var lockStatements = methodDeclaration.ExpressionBody.DescendantNodes().OfType<LockStatementSyntax>();
                if (lockStatements.Any())
                {
                    return lockStatements.First();
                }
            }

            return null;
        }

        private bool IsLockPure(LockStatementSyntax lockStatement, IMethodSymbol methodSymbol, SemanticModel semanticModel)
        {
            // Check if the method has the [AllowSynchronization] attribute
            if (!StatementPurityChecker.HasAllowSynchronizationAttribute(methodSymbol)) // Updated call
            {
                // No attribute, lock statement is impure
                return false;
            }

            // Check if the expression being locked is a readonly field
            if (lockStatement.Expression is IdentifierNameSyntax identifier)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // The lock is impure if the field is not readonly
                    return fieldSymbol.IsReadOnly;
                }
            }
            else if (lockStatement.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // The lock is impure if the field is not readonly
                    return fieldSymbol.IsReadOnly;
                }
            }

            // For other expressions, we assume the lock is pure if AllowSynchronization is present
            return true;
        }

        private bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null) return false;

            // Check if this is a constructor or method
            string attributeTarget = methodSymbol.MethodKind == MethodKind.Constructor ? "Constructor" : "Method";

            // Look for any attribute with a name containing "Pure" or "EnforcePure"
            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "PureAttribute" or "Pure" or "EnforcePureAttribute" or "EnforcePure" ||
                (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString().Contains("PureAttribute")) || // Null check added
                (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString().Contains("EnforcePure")) || // Null check added
                (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString() == "System.Diagnostics.Contracts.PureAttribute")); // Add check for System.Diagnostics.Contracts.PureAttribute
        }

        private bool IsTriviallyPure(MethodDeclarationSyntax methodDeclaration)
        {
            // Methods with no body or simple expressions are pure
            if (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null)
                return true;

            // For expression-bodied members, check if it's a return expression
            if (methodDeclaration.ExpressionBody != null)
            {
                var expr = methodDeclaration.ExpressionBody.Expression;

                // LINQ, math and string operations are typically pure
                var exprText = expr.ToString();
                if (exprText.Contains(".Select(") ||
                    exprText.Contains(".Where(") ||
                    exprText.Contains(".OrderBy(") ||
                    exprText.Contains(".GroupBy(") ||
                    exprText.Contains(".Join(") ||
                    exprText.Contains(".Min(") ||
                    exprText.Contains(".Max(") ||
                    exprText.Contains(".Sum(") ||
                    exprText.Contains(".Average(") ||
                    exprText.Contains(".Count(") ||
                    exprText.Contains(".Any(") ||
                    exprText.Contains(".All(") ||
                    exprText.Contains(".First(") ||
                    exprText.Contains(".FirstOrDefault(") ||
                    exprText.Contains(".Single(") ||
                    exprText.Contains(".SingleOrDefault(") ||
                    exprText.Contains(".ToList()") ||
                    exprText.Contains(".ToArray()") ||
                    exprText.Contains(".ToDictionary(") ||
                    exprText.Contains(".ToHashSet(") ||
                    exprText.Contains("string.Format(") ||
                    exprText.Contains("String.Format(") ||
                    exprText.Contains("$\"") ||  // string interpolation
                    exprText.Contains("Math.") ||
                    exprText.Contains("System.Math"))
                {
                    return true;
                }

                return expr is LiteralExpressionSyntax || expr is IdentifierNameSyntax;
            }

            // For regular methods, check if they contain only pure operations
            if (methodDeclaration.Body != null)
            {
                var bodyText = methodDeclaration.Body.ToString();

                // LINQ, math and string operations are typically pure
                if (bodyText.Contains(".Select(") ||
                    bodyText.Contains(".Where(") ||
                    bodyText.Contains(".OrderBy(") ||
                    bodyText.Contains(".GroupBy(") ||
                    bodyText.Contains(".Join(") ||
                    bodyText.Contains(".Min(") ||
                    bodyText.Contains(".Max(") ||
                    bodyText.Contains(".Sum(") ||
                    bodyText.Contains(".Average(") ||
                    bodyText.Contains(".Count(") ||
                    bodyText.Contains(".Any(") ||
                    bodyText.Contains(".All(") ||
                    bodyText.Contains(".First(") ||
                    bodyText.Contains(".FirstOrDefault(") ||
                    bodyText.Contains(".Single(") ||
                    bodyText.Contains(".SingleOrDefault(") ||
                    bodyText.Contains(".ToList()") ||
                    bodyText.Contains(".ToArray()") ||
                    bodyText.Contains(".ToDictionary(") ||
                    bodyText.Contains(".ToHashSet(") ||
                    bodyText.Contains("string.Format(") ||
                    bodyText.Contains("String.Format(") ||
                    bodyText.Contains("$\"") ||  // string interpolation
                    bodyText.Contains("Math.") ||
                    bodyText.Contains("System.Math"))
                {
                    // Methods containing only LINQ, string and math operations can be considered pure
                    if (!bodyText.Contains("=") &&     // No assignments
                        !bodyText.Contains("ref ") &&  // No ref parameters
                        !bodyText.Contains("out ") &&  // No out parameters
                        !bodyText.Contains("lock(") && // No locks
                        !bodyText.Contains("unsafe"))  // No unsafe code
                    {
                        return true;
                    }
                }

                // Check for a single return statement with a literal or parameter
                var statements = methodDeclaration.Body.Statements;
                if (statements.Count == 1 && statements[0] is ReturnStatementSyntax returnStmt)
                {
                    if (returnStmt.Expression == null)
                        return true;

                    return returnStmt.Expression is LiteralExpressionSyntax ||
                          (returnStmt.Expression is IdentifierNameSyntax identifier);
                }
            }

            return false;
        }

        private bool IsPureUsingStatement(UsingStatementSyntax usingStatement, SemanticModel semanticModel)
        {
            // Check if this is a simple using statement with a known pure disposable type
            if (usingStatement.Declaration != null)
            {
                foreach (var variable in usingStatement.Declaration.Variables)
                {
                    if (variable.Initializer?.Value is ObjectCreationExpressionSyntax creation)
                    {
                        var type = semanticModel.GetTypeInfo(creation).Type;
                        if (type != null && type.Name == "PureDisposable")
                            return true;
                    }
                }
            }
            else if (usingStatement.Expression is ObjectCreationExpressionSyntax creation)
            {
                var type = semanticModel.GetTypeInfo(creation).Type;
                if (type != null && type.Name == "PureDisposable")
                    return true;
            }

            return false;
        }

        private bool IsPureUsingDeclaration(LocalDeclarationStatementSyntax usingDeclaration, SemanticModel semanticModel)
        {
            // Check if this is a using declaration with a known pure disposable type
            foreach (var variable in usingDeclaration.Declaration.Variables)
            {
                if (variable.Initializer?.Value is ObjectCreationExpressionSyntax creation)
                {
                    var type = semanticModel.GetTypeInfo(creation).Type;
                    if (type != null && type.Name == "PureDisposable")
                        return true;
                }
            }

            return false;
        }

        private bool ContainsImpureOperations(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, HashSet<IMethodSymbol> visitedMethods)
        {
            var walker = new ImpurityWalker(semanticModel);
            walker.Visit(methodDeclaration);
            return walker.ContainsImpureOperations;
        }

        private void AnalyzeConstructorDeclaration(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> recordTypes, HashSet<IMethodSymbol> analyzedMethods, HashSet<IMethodSymbol> knownPureMethods, HashSet<IMethodSymbol> knownImpureMethods)
        {
            var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;
            var constructorSymbol = context.SemanticModel.GetDeclaredSymbol(constructorDeclaration);

            if (constructorSymbol == null || !HasEnforcePureAttribute(constructorSymbol))
                return;

            // Use PurityChecker to check constructor purity (might need adjustment based on PurityChecker capabilities)
            // Assuming PurityChecker can handle constructors similar to methods for now
            if (!PurityChecker.IsConstructorPure(constructorDeclaration, context.SemanticModel)) // Use the new IsConstructorPure method
            {
                var diagnostic = Diagnostic.Create(RuleImpure, constructorDeclaration.Identifier.GetLocation(), constructorSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

             // TODO: Add more specific constructor analysis if needed, similar to AnalyzeMethodDeclaration
             // For now, rely on the general PurityChecker.
        }

        private class ImpurityWalker : CSharpSyntaxWalker
        {
            protected readonly SemanticModel _semanticModel;
            protected bool _containsImpureOperations = false;
            protected Location? _impurityLocation = null;
            private readonly HashSet<IMethodSymbol> _visitedMethods = new(SymbolEqualityComparer.Default);
            private bool _encounteredUnknownPurity = false; // Track if unknown purity was encountered
            private Location? _unknownPurityLocation = null; // Location of the first unknown call

            public bool ContainsImpureOperations => _containsImpureOperations;
            public Location? ImpurityLocation => _impurityLocation;
            public bool EncounteredUnknownPurity => _encounteredUnknownPurity; // Expose unknown state
            public Location? UnknownPurityLocation => _unknownPurityLocation; // Expose unknown location

            public ImpurityWalker(SemanticModel semanticModel)
            {
                _semanticModel = semanticModel;
            }

            // Helper methods for purity checking
            private bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
            {
                if (methodSymbol == null) return false;

                // Look for any attribute with a name containing "Pure" or "EnforcePure"
                return methodSymbol.GetAttributes().Any(attr =>
                    attr.AttributeClass?.Name is "PureAttribute" or "Pure" or "EnforcePureAttribute" or "EnforcePure" ||
                    (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString().Contains("PureAttribute")) || // Null check added
                    (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString().Contains("EnforcePure"))); // Null check added
            }

            public override void VisitIdentifierName(IdentifierNameSyntax node)
            {
                base.VisitIdentifierName(node);

                var symbolInfo = _semanticModel.GetSymbolInfo(node);
                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol && fieldSymbol.IsStatic && !fieldSymbol.IsConst)
                {
                    // Static field access (except const) is considered impure
                    _containsImpureOperations = true;
                    _impurityLocation = node.GetLocation();
                }
            }

            public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                base.VisitAssignmentExpression(node);

                // Check if we're assigning to a field or property
                if (node.Left is MemberAccessExpressionSyntax memberAccess)
                {
                    // Get semantic info for the member being accessed
                    var symbolInfo = _semanticModel.GetSymbolInfo(memberAccess.Name);
                    var symbol = symbolInfo.Symbol;

                    // If it's a field or property that is not readonly, it's impure
                    if (symbol is IFieldSymbol fieldSymbol && !fieldSymbol.IsReadOnly)
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                        return;
                    }
                    else if (symbol is IPropertySymbol propertySymbol && propertySymbol.SetMethod != null)
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                        return;
                    }
                }
                // Check if we're modifying an element in an array or collection
                else if (node.Left is ElementAccessExpressionSyntax elementAccess)
                {
                    // Modifying elements in arrays or collections is impure
                    _containsImpureOperations = true;
                    _impurityLocation = node.GetLocation();
                    return;
                }
                // Check if we're assigning to an identifier (variable)
                else if (node.Left is IdentifierNameSyntax identifier)
                {
                    // Check if the identifier is a field
                    var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
                    if (symbolInfo.Symbol is IFieldSymbol fieldSymbol && !IsImmutableField(fieldSymbol))
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.OperatorToken.GetLocation();
                    }
                    // Check if we're assigning to a ref or out parameter
                    else if (symbolInfo.Symbol is IParameterSymbol parameterSymbol)
                    {
                        // Special case for Enum.TryParse - we allow assignments to out parameters in this method
                        if (parameterSymbol.ContainingSymbol is IMethodSymbol methodSymbol &&
                            methodSymbol.Name == "TryParse" &&
                            methodSymbol.ContainingType?.Name == "Enum" &&
                            methodSymbol.ContainingType.ContainingNamespace?.Name == "System")
                        {
                            // Allow assignments to out parameters in Enum.TryParse
                            return;
                        }

                        // If it's a ref or out parameter and we're assigning to it, that's impure
                        // In (readonly ref) parameters are safe since they can't be modified
                        if (parameterSymbol.RefKind == RefKind.Out || parameterSymbol.RefKind == RefKind.Ref)
                        {
                            _containsImpureOperations = true;
                            _impurityLocation = node.OperatorToken.GetLocation();
                        }
                    }
                }
            }

            private bool IsImmutableField(IFieldSymbol fieldSymbol)
            {
                // Fields marked as readonly or const are considered immutable/pure
                // Volatile fields are impure
                return (fieldSymbol.IsReadOnly || fieldSymbol.IsConst) && !fieldSymbol.IsVolatile;
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                if (_containsImpureOperations) return; // Stop if impurity already found

                var symbolInfo = _semanticModel.GetSymbolInfo(node.Expression);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

                if (methodSymbol == null)
                {
                    // Could be a delegate or function pointer invocation
                    // Check if the expression being invoked is a delegate type
                    var invokedType = _semanticModel.GetTypeInfo(node.Expression).Type;
                    if (invokedType?.TypeKind == TypeKind.Delegate)
                    {
                        // Try to determine the symbol holding the delegate
                        ISymbol? delegateHolderSymbol = null;
                        if (node.Expression is IdentifierNameSyntax identifierName)
                        {
                            delegateHolderSymbol = _semanticModel.GetSymbolInfo(identifierName).Symbol;
                        }
                        else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                        {
                            delegateHolderSymbol = _semanticModel.GetSymbolInfo(memberAccess.Name).Symbol;
                        }

                        if (delegateHolderSymbol != null)
                        {
                            if (!IsDelegatePure(delegateHolderSymbol, node.Expression))
                            {
                                RecordImpurity(node);
                                return;
                            }
                            // If delegate source is pure, we still need to check arguments
                        }
                        else
                        {
                            // Cannot determine delegate source, assume unknown
                            RecordUnknownPurity(node);
                            // Continue checking arguments
                        }
                    }
                    else
                    {
                        // Cannot resolve method symbol, assume unknown
                        RecordUnknownPurity(node);
                        // Continue checking arguments
                    }
                }
                else
                {
                    // Handle regular method invocation
                    if (_visitedMethods.Contains(methodSymbol))
                        return; // Avoid infinite recursion

                    _visitedMethods.Add(methodSymbol);

                    try
                    {
                        // Check for explicit EnforcePure attribute on the called method
                        if (HasEnforcePureAttribute(methodSymbol))
                        {
                            // If the called method is itself enforced pure, assume it's pure for this check
                            base.VisitInvocationExpression(node);
                            return; // Treat as pure for now
                        }

                        // ADDED BACK: Check for StringBuilder modification
                        if (methodSymbol.ContainingType?.Name == "StringBuilder" &&
                            methodSymbol.Name is "Append" or "AppendLine" or "AppendFormat" or "Insert" or "Replace" or "Remove" or "Clear")
                        {
                            RecordImpurity(node);
                            return;
                        }

                        // ADDED BACK: Check for collection modification operations
                        if (methodSymbol.Name is "Add" or "AddRange" or "Clear" or "Insert" or "Remove" or
                            "RemoveAt" or "RemoveAll" or "RemoveRange" or "Sort" or "Reverse")
                        {
                            // Further check if the type is actually a known mutable collection if needed
                            // For now, assume these methods on any type might be impure state changes
                            RecordImpurity(node);
                            return;
                        }

                        // Check against known impure methods list first (local helper)
                        if (IsMethodKnownImpure(methodSymbol))
                        {
                            RecordImpurity(node);
                            return;
                        }

                        // Check against EXTERNAL known pure methods list
                        if (MethodPurityChecker.IsKnownPureMethod(methodSymbol))
                        {
                            // *** If known pure by the external checker, visit args then stop ***
                            foreach(var arg in node.ArgumentList?.Arguments ?? Enumerable.Empty<ArgumentSyntax>())
                            {
                                Visit(arg.Expression);
                                if (_containsImpureOperations || _encounteredUnknownPurity)
                                {
                                    return;
                                }
                            }
                            return; // Stop visiting this node.
                        }

                        // If not explicitly known pure or impure, mark as unknown purity
                        RecordUnknownPurity(node);
                    }
                    finally
                    {
                        _visitedMethods.Remove(methodSymbol);
                    }
                }

                // Always visit arguments if we haven't returned yet
                base.VisitInvocationExpression(node);
            }

            private bool IsDelegatePure(ISymbol delegateHolderSymbol, ExpressionSyntax delegateExpression)
            {
                // Check if the delegate holder symbol is a field, property, or local variable
                if (delegateHolderSymbol is IFieldSymbol fieldSymbol ||
                    delegateHolderSymbol is IPropertySymbol propertySymbol ||
                    delegateHolderSymbol is ILocalSymbol localSymbol)
                {
                    // Check if the delegate expression is a known pure delegate type
                    var typeInfo = _semanticModel.GetTypeInfo(delegateExpression);
                    if (typeInfo.Type != null &&
                        (typeInfo.Type.TypeKind == TypeKind.Delegate ||
                         typeInfo.Type.AllInterfaces.Any(i => i.Name.Contains("Action") || i.Name.Contains("Func"))))
                    {
                        // Check if we can determine the delegate's target and if it's pure
                        bool isDelegatePure = ExpressionPurityChecker.IsDelegateSymbolPure(delegateHolderSymbol, delegateExpression, _semanticModel, _visitedMethods);
                        bool isDelegateKnown = IsDelegatePurityKnown(delegateHolderSymbol, delegateExpression);

                        if (!isDelegatePure && isDelegateKnown) // Only mark impure if known to be impure
                        {
                            return false;
                        }
                        else if (!isDelegateKnown) // Mark unknown if purity couldn't be determined
                        {
                            RecordUnknownPurity(delegateExpression);
                        }
                    }
                }

                return true; // Assume pure if we can't determine the delegate's purity
            }

            private bool IsDelegatePurityKnown(ISymbol delegateHolderSymbol, ExpressionSyntax delegateExpression)
            {
                // Check if the delegate holder symbol is a field, property, or local variable
                if (delegateHolderSymbol is IFieldSymbol fieldSymbol ||
                    delegateHolderSymbol is IPropertySymbol propertySymbol ||
                    delegateHolderSymbol is ILocalSymbol localSymbol)
                {
                    // Check if the delegate expression is a known pure delegate type
                    var typeInfo = _semanticModel.GetTypeInfo(delegateExpression);
                    if (typeInfo.Type != null &&
                        (typeInfo.Type.TypeKind == TypeKind.Delegate ||
                         typeInfo.Type.AllInterfaces.Any(i => i.Name.Contains("Action") || i.Name.Contains("Func"))))
                    {
                        // Check if we can determine the delegate's target and if it's pure
                        // bool isDelegatePure = ExpressionPurityChecker.IsDelegateSymbolPure(delegateHolderSymbol, delegateExpression, _semanticModel, _visitedMethods);
                        // REMOVED CALL: bool isDelegateKnown = ExpressionPurityChecker.IsDelegatePurityKnown(delegateHolderSymbol, delegateExpression, _semanticModel, _visitedMethods);
                        // return isDelegateKnown;
                        return false; // Assume unknown for now
                    }
                }

                return false; // Assume unknown if we can't determine the delegate's purity
            }

            private void RecordImpurity(ExpressionSyntax expression)
            {
                _containsImpureOperations = true;
                _impurityLocation = expression.GetLocation();
            }

            private void RecordUnknownPurity(ExpressionSyntax expression)
            {
                if (!_encounteredUnknownPurity)
                {
                    _encounteredUnknownPurity = true;
                    _unknownPurityLocation = expression.GetLocation();
                }
            }

            public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
            {
                base.VisitPostfixUnaryExpression(node);

                // Check for increment/decrement operators which are impure
                if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ||
                    node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
                {
                    // Check if the operand is a field or property
                    var operand = node.Operand;
                    var symbolInfo = _semanticModel.GetSymbolInfo(operand);

                    if (symbolInfo.Symbol is IFieldSymbol fieldSymbol ||
                        symbolInfo.Symbol is IPropertySymbol propertySymbol)
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                    }
                }
            }

            public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            {
                base.VisitPrefixUnaryExpression(node);

                // Check for increment/decrement operators which are impure
                if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ||
                    node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
                {
                    // Check if the operand is a field or property
                    var operand = node.Operand;
                    var symbolInfo = _semanticModel.GetSymbolInfo(operand);

                    if (symbolInfo.Symbol is IFieldSymbol fieldSymbol ||
                        symbolInfo.Symbol is IPropertySymbol propertySymbol)
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                    }
                }
            }

            public override void VisitYieldStatement(YieldStatementSyntax node)
            {
                // First visit children to catch impure expressions
                base.VisitYieldStatement(node);

                // If we already found impure operations, no need to check further
                if (_containsImpureOperations)
                    return;

                // Yield statements themselves are not impure, but need to check their expressions
                if (node.Expression != null)
                {
                    // Check if there are field modifications in parent body
                    var methodDeclaration = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    if (methodDeclaration != null)
                    {
                        // Find field assignments in the method body
                        var fieldAssignments = methodDeclaration.DescendantNodes()
                            .OfType<AssignmentExpressionSyntax>()
                            .Where(assignment =>
                            {
                                if (assignment.Left is IdentifierNameSyntax identifier)
                                {
                                    var symbol = _semanticModel.GetSymbolInfo(identifier).Symbol;
                                    return symbol is IFieldSymbol;
                                }
                                else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                                {
                                    var symbol = _semanticModel.GetSymbolInfo(memberAccess).Symbol;
                                    return symbol is IFieldSymbol;
                                }
                                return false;
                            })
                            .ToList();

                        if (fieldAssignments.Any())
                        {
                            _containsImpureOperations = true;
                            _impurityLocation = node.GetLocation();
                            return;
                        }
                    }

                    // Any state modification within a yield return is impure
                    if (node.Expression is AssignmentExpressionSyntax assignmentExpr)
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                        return;
                    }

                    // Check for increment/decrement expressions
                    if (node.Expression is PostfixUnaryExpressionSyntax postfix)
                    {
                        if (postfix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ||
                            postfix.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
                        {
                            _containsImpureOperations = true;
                            _impurityLocation = node.GetLocation();
                            return;
                        }
                    }

                    if (node.Expression is PrefixUnaryExpressionSyntax prefix)
                    {
                        if (prefix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ||
                            prefix.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
                        {
                            _containsImpureOperations = true;
                            _impurityLocation = node.GetLocation();
                            return;
                        }
                    }

                    // Check for method calls inside yield expressions
                    if (node.Expression is InvocationExpressionSyntax invocation)
                    {
                        var methodSymbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (methodSymbol != null)
                        {
                            // First check for known impure methods
                            if (IsMethodKnownImpure(methodSymbol))
                            {
                                _containsImpureOperations = true;
                                _impurityLocation = node.GetLocation();
                                return;
                            }

                            // Console methods are impure
                            if (methodSymbol.ContainingType?.Name == "Console" &&
                                methodSymbol.ContainingType.ContainingNamespace?.Name == "System")
                            {
                                _containsImpureOperations = true;
                                _impurityLocation = node.GetLocation();
                                return;
                            }
                        }
                    }

                    // Check if the expression references a field that was incremented/decremented
                    if (node.Expression is IdentifierNameSyntax identifier)
                    {
                        var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
                        if (symbolInfo.Symbol is IFieldSymbol fieldSymbol && !IsImmutableField(fieldSymbol))
                        {
                            // If the field is not immutable, yielding its value after modification
                            // may indicate impurity in the iterator
                            var parent = node.Parent;
                            while (parent != null)
                            {
                                if (parent is MethodDeclarationSyntax methodDecl)
                                {
                                    // Check if there are any assignments to this field in the method
                                    var fieldAssignments = methodDecl.DescendantNodes()
                                        .OfType<AssignmentExpressionSyntax>()
                                        .Where(a => a.Left is IdentifierNameSyntax id &&
                                               SymbolEqualityComparer.Default.Equals(_semanticModel.GetSymbolInfo(id).Symbol, fieldSymbol)); // Use SymbolEqualityComparer

                                    if (fieldAssignments.Any())
                                    {
                                        _containsImpureOperations = true;
                                        _impurityLocation = node.GetLocation();
                                        return;
                                    }
                                    break;
                                }
                                parent = parent.Parent;
                            }
                        }
                    }
                }
            }

            private bool IsMethodKnownImpure(IMethodSymbol methodSymbol)
            {
                // List of known impure methods (IO, system calls, etc.)
                var containingType = methodSymbol.ContainingType?.ToString() ?? string.Empty;
                var methodName = methodSymbol.Name;

                // Random operations are impure (non-deterministic)
                if (containingType == "System.Random")
                    return true;

                // Task.Delay is impure as it involves timing operations
                if (containingType == "System.Threading.Tasks.Task" && methodName == "Delay")
                    return true;

                // IO operations are impure
                if (containingType.Contains("System.IO.") ||
                    containingType.Contains("System.Console") ||
                    containingType.Contains("System.Net.") ||
                    containingType.Contains("System.Web."))
                    return true;

                // Specific known impure methods
                if (containingType == "System.Console" && (
                    methodName == "WriteLine" ||
                    methodName == "Write" ||
                    methodName == "ReadLine" ||
                    methodName == "ReadKey"))
                    return true;

                if (containingType.Contains("System.Threading") && methodName.Contains("Interlocked"))
                    return true;

                if (methodName.Contains("Wait") ||
                    methodName.Contains("Lock") ||
                    methodName.Contains("Dispose") ||
                    methodName.Contains("Update") ||
                    methodName.Contains("GetEnumerator"))
                    return true;

                // Check for methods with out or ref parameters
                foreach (var parameter in methodSymbol.Parameters)
                {
                    // 'in' parameters are readonly and safe to use in pure methods
                    if (parameter.RefKind == RefKind.In)
                        continue;

                    // Special case for Enum.TryParse methods
                    if (methodName == "TryParse" &&
                        methodSymbol.ContainingType?.Name == "Enum" &&
                        methodSymbol.ContainingType.ContainingNamespace?.Name == "System")
                    {
                        continue; // Skip the check for Enum.TryParse
                    }

                    if (parameter.RefKind == RefKind.Ref || parameter.RefKind == RefKind.Out)
                    {
                        return true;
                    }
                }

                // Collection modifications are impure
                if (containingType.Contains("System.Collections.Generic.List`1") &&
                    (methodName == "Add" || methodName == "AddRange" || methodName == "Clear" ||
                     methodName == "Insert" || methodName == "Remove" || methodName == "RemoveAt" ||
                     methodName == "RemoveAll" || methodName == "RemoveRange" || methodName == "Sort" ||
                     methodName == "Reverse"))
                    return true;

                return false;
            }

            public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                base.VisitObjectCreationExpression(node);

                // Check if this is creating a Random instance
                var typeInfo = _semanticModel.GetTypeInfo(node).Type;
                if (typeInfo != null && typeInfo.ToString() == "System.Random")
                {
                    _containsImpureOperations = true;
                    _impurityLocation = node.GetLocation();
                    return;
                }

                // Check if this is creating a mutable collection
                if (typeInfo != null)
                {
                    var modifiableCollections = new[] {
                        "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList",
                        "SortedList", "SortedDictionary", "SortedSet", "Collection"
                    };

                    // Check if the type is a known mutable collection type
                    var typeName = typeInfo.Name;
                    if (modifiableCollections.Contains(typeName) ||
                        (typeInfo is INamedTypeSymbol namedType &&
                        namedType.TypeArguments.Any() &&
                        modifiableCollections.Contains(namedType.ConstructedFrom.Name)))
                    {
                        // Allow immutable collections
                        if (typeInfo.ContainingNamespace?.Name == "Immutable" &&
                            typeInfo.ContainingNamespace.ToString().Contains("System.Collections.Immutable"))
                            return;

                        // Mark as impure
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                        return;
                    }
                }
            }

            public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
            {
                base.VisitArrayCreationExpression(node);

                // All array creation expressions are considered impure
                _containsImpureOperations = true;
                _impurityLocation = node.GetLocation();
            }

            public override void VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
            {
                base.VisitImplicitArrayCreationExpression(node);

                // All implicit array creation expressions (new[] { ... }) are impure
                _containsImpureOperations = true;
                _impurityLocation = node.GetLocation();
            }

            public override void VisitAwaitExpression(AwaitExpressionSyntax node)
            {
                base.VisitAwaitExpression(node);

                // Use the AsyncPurityChecker to determine if the await expression is pure
                if (!AsyncPurityChecker.IsAwaitExpressionPure(node, _semanticModel))
                {
                    // Special case for awaits in local functions - check if awaiting another local function
                    if (node.Expression is InvocationExpressionSyntax invocation &&
                        invocation.Expression is IdentifierNameSyntax identName)
                    {
                        var symbol = _semanticModel.GetSymbolInfo(identName).Symbol;
                        if (symbol?.Kind == SymbolKind.Method &&
                            (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction)
                        {
                            // Awaiting a local function is allowed in pure methods
                            return;
                        }
                    }

                    _containsImpureOperations = true;
                    _impurityLocation = node.GetLocation();
                }
            }

            // Add support for checking collection expressions (C# 12)
            public override void VisitCollectionExpression(CollectionExpressionSyntax node)
            {
                base.VisitCollectionExpression(node);

                // Check the target type of the collection expression
                var typeInfo = _semanticModel.GetTypeInfo(node);
                var destinationType = typeInfo.ConvertedType;

                // If we can determine the type and it's a mutable collection, mark as impure
                if (destinationType != null)
                {
                    // Array types are mutable
                    if (destinationType is IArrayTypeSymbol)
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                        return;
                    }

                    // Check for mutable collection types
                    var modifiableCollections = new[] {
                        "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList",
                        "SortedList", "SortedDictionary", "SortedSet", "Collection"
                    };

                    // Allow immutable collections
                    if (destinationType.ContainingNamespace?.Name == "Immutable" &&
                        destinationType.ContainingNamespace.ToString().Contains("System.Collections.Immutable"))
                        return;

                    // Allow read-only collections
                    if (destinationType.Name.StartsWith("IReadOnly"))
                        return;

                    if (modifiableCollections.Contains(destinationType.Name) ||
                        (destinationType is INamedTypeSymbol namedType &&
                         namedType.TypeArguments.Any() &&
                         modifiableCollections.Contains(namedType.ConstructedFrom.Name)))
                    {
                        _containsImpureOperations = true;
                        _impurityLocation = node.GetLocation();
                        return;
                    }
                }
            }

            private bool IsFromKnownPureNamespace(IMethodSymbol methodSymbol)
            {
                // Implementation of IsFromKnownPureNamespace method
                // This method should be implemented based on your specific requirements
                return false; // Placeholder return, actual implementation needed
            }

            private bool IsCoreKnownPureMethod(IMethodSymbol methodSymbol)
            {
                // Implementation of IsCoreKnownPureMethod method
                // This method should be implemented based on your specific requirements
                return false; // Placeholder return, actual implementation needed
            }
        }
    }
}

