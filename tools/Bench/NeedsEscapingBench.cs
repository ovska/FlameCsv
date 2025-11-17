using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Writing.Escaping;

namespace FlameCsv.Benchmark;

public class NeedsQuotingBench
{
    private const string Path = @"../../../../../../../Comparisons/Data/SampleCSVFile_556kb.csv";

    private readonly string[] _data;

    // [Benchmark(Baseline = true)]
    public void IndexOf()
    {
        var escaper = new RFC4180Escaper<char>('"');
        var needsQuoting = CsvOptions<char>.Default.NeedsQuoting;
        uint count = 0;

        foreach (var field in _data)
        {
            var index = field.IndexOfAny(needsQuoting);

            if (index != -1)
            {
                count += (uint)escaper.CountEscapable(field);
            }
        }
    }

    [Benchmark]
    public void LUT()
    {
        uint count = 0;
        Buffer LUT = default;
        ((Span<byte>)LUT).Clear();

        LUT['"'] = 0b01;
        LUT[','] = 0b10;
        LUT['\n'] = 0b10;
        LUT['\r'] = 0b10;

        foreach (var field in _data)
        {
            var (needsEscaping, quoteCount) = Count(field, LUT);
            count += quoteCount;
        }
    }

    [Benchmark]
    public void Scalar()
    {
        uint count = 0;

        foreach (var field in _data)
        {
            var (needsEscaping, quoteCount) = CountScalar(field);
            count += quoteCount;
        }
    }

    public NeedsQuotingBench()
    {
        List<string> data = [];

        foreach (var record in CsvReader.EnumerateFromFile(Path))
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                try
                {
                    data.Add(record.GetField(i).ToString());
                }
                catch (Exceptions.CsvFormatException) { }
            }
        }

        _data = data.ToArray();
    }

    private static (bool needsEscaping, uint quoteCount) CountScalar(ReadOnlySpan<char> data)
    {
        ref char first = ref MemoryMarshal.GetReference(data);
        nuint index = 0;
        nuint remaining = (nuint)data.Length;

        uint quotecount = 0;
        bool needsEscaping = false;

        while (remaining > 0)
        {
            char c = Unsafe.Add(ref first, index);

            bool isQuote = c == '"';
            bool isLF = c == '\n';
            bool isCR = c == '\r';
            bool isDelim = c == ',';

            needsEscaping |= isLF || isCR || isDelim;
            quotecount += isQuote ? 1u : 0u;

            index++;
            remaining--;
        }

        return (needsEscaping, quotecount);
    }

    private static (bool needsEscaping, uint quoteCount) Count(ReadOnlySpan<char> data, ReadOnlySpan<byte> LUT)
    {
        ref char first = ref MemoryMarshal.GetReference(data);
        ref byte lutPtr = ref MemoryMarshal.GetReference(LUT);
        nuint index = 0;
        nuint end = (nuint)data.Length;
        nuint unrolledEnd = Math.Max(0u, (uint)data.Length - 4);
        nuint vectorEnd = Math.Max(0u, (uint)data.Length - (uint)Vector128<short>.Count);

        Vector128<ushort> quote = Vector128.Create((ushort)'"');
        Vector128<ushort> delim = Vector128.Create((ushort)',');
        Vector128<ushort> lf = Vector128.Create((ushort)'\n');
        Vector128<ushort> cr = Vector128.Create((ushort)'\r');
        Vector128<ushort> any = Vector128<ushort>.Zero;
        Vector128<ushort> quoteSum = Vector128<ushort>.Zero;

        uint quoteCount = 0;
        uint needsEscaping = 0;

        while (index < vectorEnd)
        {
            Vector128<ushort> vec = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref first, index)));

            Vector128<ushort> cmpQuote = Vector128.Equals(vec, quote);
            Vector128<ushort> cmpDelim = Vector128.Equals(vec, delim);
            Vector128<ushort> cmpLF = Vector128.Equals(vec, lf);
            Vector128<ushort> cmpCR = Vector128.Equals(vec, cr);

            cmpQuote = AdvSimd.ShiftRightLogical(cmpQuote, 15);
            any |= cmpDelim | cmpLF | cmpCR;
            quoteSum += cmpQuote;

            index += (nuint)Vector128<short>.Count;
        }

        while (index < unrolledEnd)
        {
            ref char c0 = ref Unsafe.Add(ref first, index + 0);
            ref char c1 = ref Unsafe.Add(ref first, index + 1);
            ref char c2 = ref Unsafe.Add(ref first, index + 2);
            ref char c3 = ref Unsafe.Add(ref first, index + 3);

            ref byte f0 = ref Unsafe.Add(ref lutPtr, c0 & 0x7F);
            ref byte f1 = ref Unsafe.Add(ref lutPtr, c1 & 0x7F);
            ref byte f2 = ref Unsafe.Add(ref lutPtr, c2 & 0x7F);
            ref byte f3 = ref Unsafe.Add(ref lutPtr, c3 & 0x7F);

            if (c0 < 128)
            {
                needsEscaping |= f0;
                quoteCount += (uint)(f0 & 0b01);
            }

            if (c1 < 128)
            {
                needsEscaping |= f1;
                quoteCount += (uint)(f1 & 0b01);
            }

            if (c2 < 128)
            {
                needsEscaping |= f2;
                quoteCount += (uint)(f2 & 0b01);
            }

            if (c3 < 128)
            {
                needsEscaping |= f3;
                quoteCount += (uint)(f3 & 0b01);
            }

            index += 4;
        }

        while (index < end)
        {
            ref char current = ref Unsafe.Add(ref first, index);
            ref byte flag = ref Unsafe.Add(ref lutPtr, current & 0x7F);

            if (current < 128)
            {
                needsEscaping |= flag;
                quoteCount += (uint)(flag & 0b01);
            }

            index++;
        }

        Vector64<ushort> quoteAcross = AdvSimd.Arm64.AddAcross(quoteSum);
        Vector64<ushort> anyCross = AdvSimd.Arm64.MaxAcross(any);
        needsEscaping |= anyCross.ToScalar();

        return (needsEscaping != 0, quoteCount + quoteAcross.ToScalar());
    }

    [InlineArray(128)]
    private struct Buffer
    {
        public byte elem0;
    }
}
