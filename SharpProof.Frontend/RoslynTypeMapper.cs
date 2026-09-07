namespace SharpProof.Frontend;

// Type mapping has no dependency on operation visitors or variable bindings.
internal sealed class RoslynTypeMapper(IrFactory factory)
{
    private readonly IrFactory _factory =
        ArgumentNullGuard.NotNull(factory, nameof(factory));
    internal Func<ITypeSymbol?, ITypeSymbol?> TypeSpecializer = static type => type;

    internal IrTypeId GetTypeId(
        ITypeSymbol? type, bool typeAlreadySpecialized = false)
    {
        if (!typeAlreadySpecialized)
        {
            type = TypeSpecializer(type);
        }
        if (type == null)
        {
            return _factory.ObjectType;
        }

        if (type.TypeKind == TypeKind.Error)
        {
            return _factory.GetOrCreateReferenceType(
                CompilerIdentityBridge.InternType(_factory, type),
                "error:" + CompilerIdentityBridge.CreateTypeDisplay(type));
        }

        if (type is IArrayTypeSymbol array)
        {
            var element = GetTypeId(array.ElementType, typeAlreadySpecialized);
            return _factory.GetOrCreateSequenceType(
                CompilerIdentityBridge.InternType(_factory, array), element,
                CompilerIdentityBridge.CreateTypeDisplay(array));
        }
        if (CSharpScalarSemantics.IsSupportedInteger(type.SpecialType))
        {
            return _factory.IntegerType;
        }

        return CSharpScalarSemantics.TryGetBuiltInType(
                _factory, type.SpecialType) ??
            _factory.GetOrCreateReferenceType(
                CompilerIdentityBridge.InternType(_factory, type),
                CompilerIdentityBridge.CreateTypeDisplay(type));
    }

    internal bool IsSupportedValueDomain(
        ITypeSymbol? type, bool typeAlreadySpecialized = false)
    {
        return CompilerIdentityBridge.IsSupportedValueDomain(
            typeAlreadySpecialized ? type : TypeSpecializer(type));
    }
}
