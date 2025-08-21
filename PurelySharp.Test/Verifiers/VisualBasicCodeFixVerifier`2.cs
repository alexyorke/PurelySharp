﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;
using System.Threading;
using System.Threading.Tasks;

namespace PurelySharp.Test
{
    public static partial class VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {

        public static DiagnosticResult Diagnostic()
            => VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic();


        public static DiagnosticResult Diagnostic(string diagnosticId)
            => VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);


        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(descriptor);


        public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new Test
            {
                TestCode = source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }


        public static async Task VerifyCodeFixAsync(string source, string fixedSource)
            => await VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);


        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
            => await VerifyCodeFixAsync(source, new[] { expected }, fixedSource);


        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
        {
            var test = new Test
            {
                TestCode = source,
                FixedCode = fixedSource,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }
    }
}

