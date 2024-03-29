﻿namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Always ignores the specified fields when read CSV has a header record.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class CsvHeaderIgnoreAttribute : Attribute, ICsvBindingAttribute
{
    public ReadOnlyMemory<string> Values { get; }

    public CsvBindingScope Scope => CsvBindingScope.Read;

    /// <inheritdoc cref="CsvHeaderExcludeAttribute"/>
    public CsvHeaderIgnoreAttribute(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }
}
