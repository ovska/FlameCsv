#if !NET7_0_OR_GREATER
#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace System.Diagnostics
{
    [CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
    [CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public")]
    internal sealed class UnreachableException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnreachableException"/>
        /// class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException"></param>
        public UnreachableException(string? message = null, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
#endif
