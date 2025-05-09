using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using FlameCsv.Attributes;

namespace FlameCsv.Fuzzing.Scenarios;

public partial class SourceGenConverters : IScenario
{
    public static bool SupportsUtf16 => true;

    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement) => TestAll(data.Span);

    public static void Run(ReadOnlyMemory<char> data, PoisonPagePlacement placement) => TestAll(data.Span);

    private static void TestAll<T>(ReadOnlySpan<T> data)
        where T : unmanaged, IBinaryInteger<T>
    {
        foreach (var configure in ConfigureOptions<T>.Configure)
        {
            CsvOptions<T> options = new();

            try
            {
                if (typeof(T) == typeof(byte))
                {
                    var opts = (CsvOptions<byte>)(object)options;
                    opts.Converters.Add(new EnumByteDof(opts));
                    opts.Converters.Add(new EnumByteMio(opts));
                    opts.Converters.Add(new EnumByteNa(opts));
                }
                else if (typeof(T) == typeof(char))
                {
                    var opts = (CsvOptions<char>)(object)options;
                    opts.Converters.Add(new EnumCharDof(opts));
                    opts.Converters.Add(new EnumCharMio(opts));
                    opts.Converters.Add(new EnumCharNa(opts));
                }
            }
            catch (NotSupportedException)
            {
                continue;
            }

            configure(options);

            Test<DayOfWeek>.Run(options, data);
            Test<MethodImplOptions>.Run(options, data);
            Test<NonAscii>.Run(options, data);
        }
    }

    private static class Test<TValue>
    {
        public static void Run<T>(CsvOptions<T> options, ReadOnlySpan<T> data)
            where T : unmanaged, IBinaryInteger<T>
        {
            CsvConverter<T, TValue> converter = options.GetConverter<TValue>();

            try
            {
                _ = converter.TryParse(data, out _);
            }
            catch (Exception ex)
            {
                throw new UnreachableException($"{converter.GetType()} threw an exception", ex);
            }
        }
    }

    [CsvEnumConverter<byte, DayOfWeek>]
    partial class EnumByteDof;

    [CsvEnumConverter<char, DayOfWeek>]
    partial class EnumCharDof;

    [CsvEnumConverter<byte, MethodImplOptions>]
    partial class EnumByteMio;

    [CsvEnumConverter<char, MethodImplOptions>]
    partial class EnumCharMio;

    [CsvEnumConverter<byte, NonAscii>]
    partial class EnumByteNa;

    [CsvEnumConverter<char, NonAscii>]
    partial class EnumCharNa;

    enum NonAscii
    {
        [EnumMember(Value = "ÿÿu")]
        A = -1,

        [EnumMember(Value = "__?")]
        B = 1234,

        [EnumMember(Value = "!!!!")]
        C = 777,

        [EnumMember(Value = "🍕")]
        D = 0xFF,

        [EnumMember(Value = "🥩🥩🥩🥩🥩🥩🥩")]
        E = 0,
    }

    private static class ConfigureOptions<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        public static readonly Action<CsvOptions<T>>[] Configure = (
            from ignoreCase in (bool[])[true, false]
            from allowUndefinedEnumValues in (bool[])[true, false]
            from enumFormat in (string?[])[null, "G", "D", "X", "F"]
            select (Action<CsvOptions<T>>)(
                o =>
                {
                    o.IgnoreEnumCase = ignoreCase;
                    o.AllowUndefinedEnumValues = allowUndefinedEnumValues;
                    o.EnumFormat = enumFormat;
                }
            )
        ).ToArray();
    }
}
