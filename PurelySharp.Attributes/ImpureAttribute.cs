using System;

namespace PurelySharp.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class ImpureAttribute : Attribute
    {
    }
}
