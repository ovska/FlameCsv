using System.Text;
using FlameCsv.Binding;
using FlameCsv.Writing;

namespace FlameCsv;

public static partial class CsvWriter
{
    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <param name="values">Values to write to the string builder</param>
    /// <param name="typeMap">Type map to use for writing</param>
    /// <param name="options">Optional user configured options to use</param>
    /// <param name="builder">Optional builder to write the CSV to.</param>
    /// <returns>
    /// <see cref="StringBuilder"/> containing the CSV (same instance as <paramref name="builder"/> if provided)
    /// </returns>
    public static StringBuilder WriteToString<TValue>(
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        StringBuilder? builder = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(typeMap);

        options ??= CsvOptions<char>.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        builder ??= new();
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(builder), options, bufferSize: DefaultBufferSize, leaveOpen: false),
            dematerializer);
        return builder;
    }
}
