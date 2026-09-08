// Internal compiler-option reflection remains confined to the build-time collector.
namespace SharpProof.CompilerArtifact;

internal static partial class CompilerOptionWireMappings
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        (Type DeclaringType, string Name), ReflectedBooleanProperty>
        BooleanProperties = new();

    internal static bool ReadInternalBoolean(
        CSharpCompilationOptions options,
        string name)
    {
        return ReadInternalBoolean(
            options,
            typeof(CompilationOptions),
            name);
    }

    internal static bool ReadInternalBoolean(
        MetadataReferenceProperties properties,
        string name)
    {
        return ReadInternalBoolean(
            properties,
            typeof(MetadataReferenceProperties),
            name);
    }

    private static bool ReadInternalBoolean(
        object value,
        Type declaringType,
        string name)
    {
        var reflected = BooleanProperties.GetOrAdd(
            (declaringType, name),
            static key =>
            {
                var property = key.DeclaringType.GetProperty(
                    key.Name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                return new ReflectedBooleanProperty(
                    property,
                    property is not null &&
                    property.PropertyType == typeof(bool) &&
                    property.GetIndexParameters().Length == 0);
            });
        if (!reflected.IsValid)
        {
            throw new InvalidOperationException(
                $"The compiler option '{name}' is unavailable or has an unexpected shape.");
        }

        return (bool)(reflected.Property!.GetValue(value) ??
            throw new InvalidOperationException(
                $"The compiler option '{name}' returned no value."));
    }

    private readonly record struct ReflectedBooleanProperty(
        System.Reflection.PropertyInfo? Property,
        bool IsValid);
}
