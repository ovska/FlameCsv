using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Utilities;

namespace FlameCsv.Extensions;

internal static class SealableExtensions
{
    /// <summary>
    /// Sets the value of <paramref name="field"/> after ensuring that the current instance is not read-only.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="sealable"></param>
    /// <param name="field">Reference to the field to modify</param>
    /// <param name="value">Value to set</param>
    /// <param name="memberName">Name of the property being set, used in exception messages</param>
    /// <exception cref="InvalidOperationException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TValue>(
        this ISealable sealable,
        ref TValue field,
        TValue value,
        [CallerMemberName] string memberName = "")
    {
        if (sealable.IsReadOnly)
            ThrowForIsReadOnly(memberName);

        field = value;
    }

    /// <summary>
    /// Throws if <see cref="ISealable.IsReadOnly"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="sealable"></param>
    /// <param name="memberName">Name of the calling property or method used in exception messages</param>
    /// <exception cref="InvalidOperationException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfReadOnly(
        this ISealable sealable,
        [CallerMemberName] string memberName = "")
    {
        if (sealable.IsReadOnly)
            ThrowForIsReadOnly(memberName);
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn]
    private static void ThrowForIsReadOnly(string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            throw new InvalidOperationException("The options-instance is read only.");

        throw new InvalidOperationException($"The options-instance is read only (accessed via {memberName}).");
    }
}
