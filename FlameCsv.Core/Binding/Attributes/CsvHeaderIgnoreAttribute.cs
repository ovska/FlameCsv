using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Always ignores the specified fields when read CSV has a header record.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
[PublicAPI]
public sealed class CsvHeaderIgnoreAttribute : Attribute, ICsvBindingAttribute
{
    /// <summary>
    /// The header values to ignore.
    /// </summary>
    public string[] Values { get; }

    /// <inheritdoc cref="ICsvBindingAttribute.Scope"/>
    public CsvBindingScope Scope => CsvBindingScope.Read;

    /// <inheritdoc cref="CsvHeaderExcludeAttribute"/>
    public CsvHeaderIgnoreAttribute(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        for (int i = 0; i < values.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(values[i]);
        }

        Values = values;
    }
}
