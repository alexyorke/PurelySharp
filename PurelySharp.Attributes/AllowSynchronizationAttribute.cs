using System;

namespace PurelySharp.Attributes
{
    /// <summary>
    /// Indicates that a method marked with [EnforcePure] is allowed to contain
    /// lock statements for thread synchronization purposes, provided the lock
    /// is taken on a readonly object (field, local, or parameter).
    /// The analyzer should still verify the purity of the code within the lock.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class AllowSynchronizationAttribute : Attribute
    {
    }
}