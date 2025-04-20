using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Analyzer.Rules; // Assuming IPurityRule is here
using PurelySharp.Analyzer.Configuration; // Add this
using PurelySharp.Analyzer.Engine;       // Add this

namespace PurelySharp.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurelySharpAnalyzer : DiagnosticAnalyzer
    {
        // TODO: Maintain a static, explicit list of all supported IPurityRule types.
        private static readonly ImmutableArray<Type> _ruleTypes = ImmutableArray.Create<Type>(
            // Example: typeof(NoImpureMethodCallRule)
            // Add other rule types here...
        );

        private ImmutableArray<IPurityRule> _rules;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            _rules.IsDefaultOrEmpty
                ? ImmutableArray<DiagnosticDescriptor>.Empty
                : _rules.Select(r => r.Descriptor).ToImmutableArray();

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None); // Or Analyze/ReportDiagnostics

            context.RegisterCompilationStartAction(CompilationStartAction);
        }

        private void CompilationStartAction(CompilationStartAnalysisContext context)
        {
            // TODO: Load AnalyzerConfiguration from .editorconfig
            AnalyzerConfiguration? analyzerConfiguration = null; // Placeholder

            // TODO: Instantiate PurityAnalysisEngine
            PurityAnalysisEngine? purityAnalysisEngine = null;     // Placeholder

            // TODO: Create CompilationState
            CompilationState? compilationState = null;           // Placeholder

            // Instantiate, initialize, and register actions for rules
            var rulesBuilder = ImmutableArray.CreateBuilder<IPurityRule>();
            foreach (var ruleType in _ruleTypes)
            {
                try
                {
                    if (Activator.CreateInstance(ruleType) is IPurityRule ruleInstance)
                    {
                        ruleInstance.InitializeRule(compilationState); // Initialize first
                        rulesBuilder.Add(ruleInstance);

                        // Now, register the actions using the CompilationStartAnalysisContext.
                        // The rule's implementation will call context.RegisterSyntaxNodeAction etc.
                        ruleInstance.RegisterActions(context);
                    }
                    // TODO: Log error if instantiation fails or type is wrong
                }
                catch (Exception ex)
                {
                    // TODO: Implement proper logging/diagnostics for analyzer errors
                }
            }
            _rules = rulesBuilder.ToImmutable();
        }
    }
} 