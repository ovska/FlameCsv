using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Utilities;
using JetBrains.Annotations;

// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace FlameCsv;

/// <summary>
/// Identifier for a CSV field, pointing to a specific field by index, or by header name.
/// Can be implicitly created from an integer or a string.<br/>
/// This type is not comparable to any other field identifier, as it's dependent on the header values.
/// </summary>
/// <remarks>
/// Field indexes are 0-based, so <c>default(CsvFieldIdentifier)</c> points to the first field.<br/>
/// </remarks>
[PublicAPI]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct CsvFieldIdentifier
{
    private readonly int _index;
    private readonly string? _name;

    /// <summary>
    /// Creates a field identifier pointing to a specific field index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldIdentifier(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _index = index;
    }

    /// <summary>
    /// Creates a field identifier pointing to a specific field by header name.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="name"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldIdentifier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _name = name;
    }

    /// <summary>
    /// Returns a field identifier pointing to a specific field index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CsvFieldIdentifier(int index) => new(index);

    /// <summary>
    /// Returns a field identifier pointing to a specific field by header name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CsvFieldIdentifier(string name) => new(name);

    /// <summary>
    /// Returns <c>true</c> if the identifier points to a field index,
    /// <c>false</c> if it points to a field by header name.
    /// </summary>
    /// <param name="index">Field index</param>
    /// <param name="name">Field header name</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetIndex(out int index, [NotNullWhen(false)] out string? name)
    {
        index = _index;
        name = _name;
        return _name is null;
    }

    /// <summary>
    /// Returns the raw string value.
    /// </summary>
    /// <remarks>
    /// May be null if the identifier is created from an <see cref="int"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? UnsafeName => _name;

    /// <summary>
    /// Returns the raw index value.
    /// </summary>
    /// <remarks>
    /// May not represent the actual field if the identifier is created from a <see cref="string"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int UnsafeIndex => _index;

    /// <inheritdoc />
    public override string ToString()
    {
        using var vsb = new ValueStringBuilder(stackalloc char[32]);

        vsb.Append("CsvFieldIdentifier[");

        if (_name is null)
        {
            vsb.AppendFormatted(_index);
        }
        else
        {
            vsb.Append('"');
            vsb.Append(_name);
            vsb.Append('"');
        }

        vsb.Append(']');

        return vsb.ToString();
    }

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => ToString();
}
