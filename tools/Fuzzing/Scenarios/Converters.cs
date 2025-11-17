using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace FlameCsv.Fuzzing.Scenarios;

public class Converters : IScenario
{
    public static bool SupportsUtf16 => true;

    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement) => TestAll(data.Span);

    public static void Run(ReadOnlyMemory<char> data, PoisonPagePlacement placement) => TestAll(data.Span);

    private static void TestAll<T>(ReadOnlySpan<T> data)
        where T : unmanaged, IBinaryInteger<T>
    {
        var options = CsvOptions<T>.Default;
        Test<bool>.Run(options, data);
        Test<int>.Run(options, data);
        Test<char>.Run(options, data);
        Test<long>.Run(options, data);
        Test<sbyte>.Run(options, data);
        Test<byte>.Run(options, data);
        Test<short>.Run(options, data);
        Test<ushort>.Run(options, data);
        Test<uint>.Run(options, data);
        Test<ulong>.Run(options, data);
        Test<float>.Run(options, data);
        Test<double>.Run(options, data);
        Test<decimal>.Run(options, data);
        Test<DateTime>.Run(options, data);
        Test<DateTimeOffset>.Run(options, data);
        Test<TimeSpan>.Run(options, data);
        Test<Guid>.Run(options, data);
        Test<DayOfWeek>.Run(options, data);
        Test<MethodImplOptions>.Run(options, data);
        Test<NonAscii>.Run(options, data);
        Test<bool?>.Run(options, data);
        Test<int?>.Run(options, data);
        Test<char?>.Run(options, data);
        Test<long?>.Run(options, data);
        Test<sbyte?>.Run(options, data);
        Test<byte?>.Run(options, data);
        Test<short?>.Run(options, data);
        Test<ushort?>.Run(options, data);
        Test<uint?>.Run(options, data);
        Test<ulong?>.Run(options, data);
        Test<float?>.Run(options, data);
        Test<double?>.Run(options, data);
        Test<decimal?>.Run(options, data);
        Test<DateTime?>.Run(options, data);
        Test<DateTimeOffset?>.Run(options, data);
        Test<TimeSpan?>.Run(options, data);
        Test<Guid?>.Run(options, data);
        Test<DayOfWeek?>.Run(options, data);
        Test<MethodImplOptions?>.Run(options, data);
        Test<NonAscii?>.Run(options, data);
        Test<string>.Run(options, data);
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
}
