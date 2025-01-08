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
    /// <param name="obj"></param>
    /// <param name="field">Reference to the field to modify</param>
    /// <param name="value">Value to set</param>
    /// <param name="memberName">Name of the property being set, used in exception messages</param>
    /// <exception cref="InvalidOperationException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TValue>(
        this ICanBeReadOnly obj,
        ref TValue field,
        TValue value,
        [CallerMemberName] string memberName = "")
    {
        if (obj.IsReadOnly)
            ThrowForIsReadOnly(memberName);

        field = value;
    }

    /// <summary>
    /// Throws if <see cref="ICanBeReadOnly.IsReadOnly"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="memberName">Name of the calling property or method used in exception messages</param>
    /// <exception cref="InvalidOperationException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfReadOnly(
        this ICanBeReadOnly obj,
        [CallerMemberName] string memberName = "")
    {
        if (obj.IsReadOnly)
            ThrowForIsReadOnly(memberName);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForIsReadOnly(string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            throw new InvalidOperationException("The instance is read only.");

        throw new InvalidOperationException($"The instance is read only (accessed via member {memberName}).");
    }
}
