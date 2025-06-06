using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures field indexes to always ignore when reading headerless CSV, or to leave empty when writing.
/// </summary>
[PublicAPI]
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    AllowMultiple = true, // type and assembly need AllMultiple = true
    Inherited = false
)]
public sealed class CsvIgnoredIndexesAttribute : CsvConfigurationAttribute
{
    private int[] _value = [];

    /// <summary>
    /// Indexes to ignore when reading CSV, and to leave empty when writing.
    /// </summary>
    public int[] Value
    {
        get => _value;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            for (int i = 0; i < value.Length; i++)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value[i]);
            }

            _value = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvIgnoredIndexesAttribute"/> class.
    /// </summary>
    /// <param name="indexes">Indexes to ignore when reading CSV, and to leave empty when writing.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public CsvIgnoredIndexesAttribute(params int[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);

        for (int i = 0; i < indexes.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(indexes[i]);
        }

        _value = indexes;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvIgnoredIndexesAttribute"/> class.
    /// </summary>
    public CsvIgnoredIndexesAttribute() { }
}
