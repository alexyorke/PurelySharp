using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: CommentRemover <rootDir>");
            return 2;
        }

        string root = args[0];
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Directory not found: {root}");
            return 3;
        }

        int filesProcessed = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
             
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                path.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            string fileName = Path.GetFileName(path);
            try
            {
                 
                if (string.Equals(fileName, ".gitignore", StringComparison.OrdinalIgnoreCase))
                {
                    StripHashLineComments(path);
                    filesProcessed++;
                }
                else if (string.Equals(fileName, ".editorconfig", StringComparison.OrdinalIgnoreCase))
                {
                    StripEditorConfigComments(path);
                    filesProcessed++;
                }
                else
                {
                    switch (ext)
                    {
                        case ".cs":
                            RemoveCommentsFromCSharp(path);
                            filesProcessed++;
                            break;
                        case ".xml":
                        case ".csproj":
                        case ".props":
                        case ".targets":
                        case ".resx":
                        case ".config":
                            StripXmlComments(path);
                            filesProcessed++;
                            break;
                        case ".md":
                            StripMarkdownComments(path);
                            filesProcessed++;
                            break;
                        case ".yml":
                        case ".yaml":
                            StripYamlComments(path);
                            filesProcessed++;
                            break;
                        case ".ps1":
                            StripPowerShellComments(path);
                            filesProcessed++;
                            break;
                        case ".sln":
                            StripHashLineComments(path);
                            filesProcessed++;
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to process {path}: {ex.Message}");
            }
        }

        Console.WriteLine($"Processed {filesProcessed} files");
        return 0;
    }

    private static void StripHashLineComments(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int idx = FindUnquotedHashIndex(line);
            if (idx == 0)
            {
                line = string.Empty;
            }
            lines[i] = line;
        }
        File.WriteAllLines(filePath, lines, Encoding.UTF8);
    }

    private static void StripEditorConfigComments(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int semicolon = line.IndexOf(';');
            int hash = line.IndexOf('#');
            int cut = -1;
            if (semicolon >= 0 && hash >= 0) cut = Math.Min(semicolon, hash);
            else if (semicolon >= 0) cut = semicolon;
            else if (hash >= 0) cut = hash;
            if (cut == 0)
            {
                line = string.Empty;
            }
            else if (cut > 0)
            {
                line = line.Substring(0, cut).TrimEnd();
            }
            lines[i] = line;
        }
        File.WriteAllLines(filePath, lines, Encoding.UTF8);
    }

    private static void RemoveCommentsFromCSharp(string filePath)
    {
        string sourceText = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(LanguageVersion.Preview));
        var root = tree.GetRoot();

         
        var rewriter = new CommentRemovingRewriter();
        var newRoot = rewriter.Visit(root);
        File.WriteAllText(filePath, newRoot.ToFullString(), Encoding.UTF8);
    }

    private static void StripXmlComments(string filePath)
    {
        string text = File.ReadAllText(filePath);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?s)<!--.*?-->", string.Empty);
        File.WriteAllText(filePath, text, Encoding.UTF8);
    }

    private static void StripMarkdownComments(string filePath)
    {
        string text = File.ReadAllText(filePath);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?s)<!--.*?-->", string.Empty);
        File.WriteAllText(filePath, text, Encoding.UTF8);
    }

    private static void StripYamlComments(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int idx = FindUnquotedHashIndex(line);
            if (idx >= 0)
            {
                line = line.Substring(0, idx).TrimEnd();
            }
            lines[i] = line;
        }
        File.WriteAllLines(filePath, lines, Encoding.UTF8);
    }

    private static void StripPowerShellComments(string filePath)
    {
        string text = File.ReadAllText(filePath);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?s)<#.*?#>", string.Empty);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(l =>
            {
                int idx = FindUnquotedHashIndex(l);
                return idx >= 0 ? l.Substring(0, idx).TrimEnd() : l;
            });
        File.WriteAllText(filePath, string.Join(Environment.NewLine, lines), Encoding.UTF8);
    }

    private static int FindUnquotedHashIndex(string line)
    {
        bool inSingle = false;
        bool inDouble = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }
            else if (c == '#' && !inSingle && !inDouble)
            {
                return i;
            }
        }
        return -1;
    }

    private sealed class CommentRemovingRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                string text = trivia.ToFullString();
                if (text.Contains('\n') || text.Contains('\r'))
                {
                    return SyntaxFactory.ElasticCarriageReturnLineFeed;
                }
                return SyntaxFactory.Space;
            }
            return base.VisitTrivia(trivia);
        }
    }
}


