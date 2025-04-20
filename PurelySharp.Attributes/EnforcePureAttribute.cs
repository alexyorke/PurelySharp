using System;

namespace PurelySharp.Attributes
{
    /// <summary>
    /// When applied to a method, instructs the PurelySharp analyzer
    /// to enforce purity rules for the method body. Applying it to 
    /// other declaration types will result in a PS0003 warning.
    /// </summary>
    // Allow the attribute on any target to prevent CS0592, 
    // our analyzer (PS0003) will handle warning about misplaced usage.
    [AttributeUsage(AttributeTargets.All)] 
    public sealed class EnforcePureAttribute : Attribute
    {
    }
} 