using System;

namespace PurelySharp
{
    /// <summary>
    /// Indicates that the method is allowed to use synchronization constructs like lock statements
    /// while still being considered pure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AllowSynchronizationAttribute : Attribute
    {
        public AllowSynchronizationAttribute()
        {
        }
    }
}