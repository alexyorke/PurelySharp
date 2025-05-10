using System;

namespace PurelySharp.Attributes
{
    /// <summary>
    /// Indicates that a method or property getter is intended to be pure,
    /// meaning it does not cause any externally visible side effects and
    /// its return value depends only on its input parameters (if any).
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public sealed class PureAttribute : Attribute
    {
    }
}