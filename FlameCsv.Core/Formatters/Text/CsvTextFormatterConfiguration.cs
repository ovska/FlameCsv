using System.Globalization;

namespace FlameCsv.Formatters.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class CsvTextFormatterConfiguration
{
    /// <summary>
    /// Format provider passed to built-in formatters if none is defined in <see cref="TypeFormatProviders"/>.
    /// Default is <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public virtual IFormatProvider? FormatProvider { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Value to write on null passed to built-in formatters if none is defined in <see cref="TypeNulls"/>.
    /// Default is null/empty.
    /// </summary>
    public virtual string? DefaultNull { get; set; } = null;

    /// <summary>
    /// Format providers indexed by type to use instead of <see cref="FormatProvider"/>.
    /// </summary>
    public virtual IDictionary<Type, IFormatProvider?> TypeFormatProviders { get; set; } =
        new Dictionary<Type, IFormatProvider?>();

    /// <summary>
    /// Formats indexed by type to use instead of default.
    /// </summary>
    public virtual IDictionary<Type, string?> TypeFormats { get; set; } = new Dictionary<Type, string?>();

    /// <summary>
    /// Values to write on null indexed by type to use instead of <see cref="DefaultNull"/>.
    /// </summary>
    public virtual IDictionary<Type, string?> TypeNulls { get; set; } = new Dictionary<Type, string?>();

    public CsvTextFormatterConfiguration WithFormatprovider<TValue>(IFormatProvider? formatProvider)
    {
        TypeFormatProviders[typeof(TValue)] = formatProvider;
        return this;
    }

    public CsvTextFormatterConfiguration WithNull<TValue>(string? nullValue)
    {
        TypeNulls[typeof(TValue)] = nullValue;
        return this;
    }

    public CsvTextFormatterConfiguration WithFormat<TValue>(string? format)
    {
        TypeFormats[typeof(TValue)] = format;
        return this;
    }
}
