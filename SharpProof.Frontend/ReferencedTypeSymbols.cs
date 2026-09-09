using System.Runtime.CompilerServices;

namespace SharpProof.Frontend;

internal static class ReferencedTypeSymbols
{
    private sealed class TypeSnapshot(
        ImmutableArray<INamedTypeSymbol> types)
    {
        internal ImmutableArray<INamedTypeSymbol> Types { get; } = types;
    }

    private static readonly ConditionalWeakTable<
        Compilation,
        TypeSnapshot> CachedSnapshots = new();

    internal static IEnumerable<INamedTypeSymbol> GetAll(
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        foreach (var type in GetAll(
                     compilation.Assembly.GlobalNamespace,
                     cancellationToken))
        {
            yield return type;
        }

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var type in GetAll(
                         assembly.GlobalNamespace,
                         cancellationToken))
            {
                yield return type;
            }
        }
    }

    internal static IEnumerable<INamedTypeSymbol> GetAllCached(
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = CachedSnapshots.GetValue(
            compilation,
            _ => new TypeSnapshot(
                GetAll(compilation, cancellationToken).ToImmutableArray()));
        foreach (var type in snapshot.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAll(
        INamespaceOrTypeSymbol container,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(container);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = pending.Pop();
            if (current is INamedTypeSymbol type)
            {
                yield return type;
            }

            if (current is INamespaceSymbol @namespace)
            {
                var namespaces = @namespace.GetNamespaceMembers()
                    .ToImmutableArray();
                for (var index = namespaces.Length - 1; index >= 0; index--)
                {
                    pending.Push(namespaces[index]);
                }
            }

            var types = current.GetTypeMembers();
            for (var index = types.Length - 1; index >= 0; index--)
            {
                pending.Push(types[index]);
            }
        }
    }
}
