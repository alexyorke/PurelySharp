using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: DeadCodeCleaner <path-to-Constants.cs>");
            return 2;
        }

        string constantsPath = args[0];
        if (!File.Exists(constantsPath))
        {
            Console.Error.WriteLine($"File not found: {constantsPath}");
            return 3;
        }

        string rootDir = Path.GetFullPath(Path.Combine(constantsPath, "..", "..", ".."));
        string constantsSource = File.ReadAllText(constantsPath);
        var tree = CSharpSyntaxTree.ParseText(constantsSource, new CSharpParseOptions(LanguageVersion.Preview));
        var root = (CompilationUnitSyntax)tree.GetRoot();

        var fieldNames = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            .SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.Text))
            .ToHashSet(StringComparer.Ordinal);

        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(rootDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith("Constants.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;

            string text = File.ReadAllText(file);
            foreach (var name in fieldNames)
            {
                if (referenced.Contains(name))
                    continue;
                if (text.Contains(name, StringComparison.Ordinal))
                {
                    referenced.Add(name);
                }
            }
        }

        var toRemove = fieldNames.Except(referenced).ToHashSet(StringComparer.Ordinal);
        if (toRemove.Count == 0)
        {
            Console.WriteLine("No unreferenced constants found.");
            return 0;
        }

        Console.WriteLine($"Unreferenced constants: {toRemove.Count}");

        var editor = root.ReplaceNodes(
            root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))),
            (original, rewritten) =>
            {
                var keepVariables = original.Declaration.Variables.Where(v => !toRemove.Contains(v.Identifier.Text)).ToList();
                if (keepVariables.Count == 0)
                {
                    return null; // remove whole field
                }
                if (keepVariables.Count == original.Declaration.Variables.Count)
                {
                    return original;
                }
                var newDecl = original.Declaration.WithVariables(SyntaxFactory.SeparatedList(keepVariables));
                return original.WithDeclaration(newDecl);
            });

        File.WriteAllText(constantsPath, editor.ToFullString(), Encoding.UTF8);
        Console.WriteLine("Constants cleaned.");
        return 0;
    }
}



