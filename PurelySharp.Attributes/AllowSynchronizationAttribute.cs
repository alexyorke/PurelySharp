using System;

namespace PurelySharp.Attributes
{

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class AllowSynchronizationAttribute : Attribute
    {
    }
}