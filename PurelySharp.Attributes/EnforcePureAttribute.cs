using System;

namespace PurelySharp.Attributes
{
    /// <summary>
    /// When applied to a method, instructs the PurelySharp analyzer
    /// to enforce purity rules for the method body.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EnforcePureAttribute : Attribute
    {
    }
} 