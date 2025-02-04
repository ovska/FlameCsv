using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv;

/// <summary>
/// Identifier for a CSV field, pointing to a specific field by index, or by header name.
/// Can be implicitly created from an integer or a string.
/// </summary>
/// <remarks>
/// Field indexes are 0-based, so <c>default(CsvFieldIdentifier)</c> points to the first field.<br/>
/// </remarks>
public readonly struct CsvFieldIdentifier
{
    private readonly int _index;
    private readonly string? _name;

    /// <summary>
    /// Creates a field identifier pointing to a specific field index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldIdentifier(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _index = index;
    }

    /// <summary>
    /// Creates a field identifier pointing to a specific field by header name.
    /// </summary>
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
}
