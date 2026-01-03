using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class GetFieldBench
{
    private static readonly uint[] _fieldBuffer = new uint[24 * 65535];
    private static readonly byte[] _quoteBuffer = new byte[24 * 65535];
    private static readonly string _chars0LF = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv");
    private static readonly string _chars1LF = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv");
    private static readonly byte[] _bytes0LF = Encoding.UTF8.GetBytes(_chars0LF);
    private static readonly byte[] _bytes1LF = Encoding.UTF8.GetBytes(_chars1LF);
    private static readonly string _chars0CRLF = _chars0LF.ReplaceLineEndings("\r\n");
    private static readonly string _chars1CRLF = _chars1LF.ReplaceLineEndings("\r\n");
    private static readonly byte[] _bytes0CRLF = Encoding.UTF8.GetBytes(_chars0CRLF);
    private static readonly byte[] _bytes1CRLF = Encoding.UTF8.GetBytes(_chars1CRLF);

    private static readonly CsvOptions<char> _dCharLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<byte> _dByteCRLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private static readonly CsvOptions<byte> _dByteLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<char> _dCharCRLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private readonly SimdTokenizer<char, FalseConstant, TrueConstant> _t128LF = new(_dCharLF);
    private readonly SimdTokenizer<byte, FalseConstant, TrueConstant> _t128bLF = new(_dByteLF);
    private readonly SimdTokenizer<byte, TrueConstant, TrueConstant> _t128bCRLF = new(_dByteCRLF);
    private readonly SimdTokenizer<char, TrueConstant, TrueConstant> _t128CRLF = new(_dCharCRLF);

    private readonly NoOpOwner _owner;
    private readonly RecordBuffer _rb2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (int, int, int, int, int, int, int, int) GetRandomInts()
    {
        return (7, 11, 2, 8, 3, 11, 4, 0);
    }

    [Benchmark]
    public void New()
    {
        var o = new NoOpOwner(_dByteLF);
        _rb2._eolIndex = 0;
        ref byte data = ref _bytes0LF[0];

        var (a, b, c, d, e, f, g, h) = GetRandomInts();

        while (_rb2.TryPop(out RecordView view))
        {
            var x = new CsvRecordRef<byte>(o, ref data, view);

            _ = x[a];
            _ = x[b];
            _ = x[c];
            _ = x[d];
            _ = x[e];
            _ = x[f];
            _ = x[g];
            _ = x[h];
        }
    }

    [Benchmark]
    public void New2()
    {
        var o = new NoOpOwner(_dByteLF);
        _rb2._eolIndex = 0;
        ref byte data = ref _bytes0LF[0];

        var (a, b, c, d, e, f, g, h) = GetRandomInts();

        while (_rb2.TryPop(out RecordView view))
        {
            var x = new CsvRecordRef<byte>(o, ref data, view);

            _ = x.GetFieldUnsafe(a);
            _ = x.GetFieldUnsafe(b);
            _ = x.GetFieldUnsafe(c);
            _ = x.GetFieldUnsafe(d);
            _ = x.GetFieldUnsafe(e);
            _ = x.GetFieldUnsafe(f);
            _ = x.GetFieldUnsafe(g);
            _ = x.GetFieldUnsafe(h);
        }
    }

    public GetFieldBench()
    {
        const int len = 16 * 65535;
        _owner = new NoOpOwner(_dByteLF);
        _rb2 = _owner._recordBuffer;
        _rb2._fields = new uint[len];
        _rb2._eols = new ushort[len];

        int count = _t128bCRLF.Tokenize(_rb2.GetUnreadBuffer(0, out int start1), start1, _bytes0LF);
        _rb2.SetFieldsRead(count);
    }

    private sealed class NoOpOwner(CsvOptions<byte> options) : RecordOwner<byte>(options, null!)
    {
        public override bool IsDisposed => false;

        internal override Span<byte> GetUnescapeBuffer(int length)
        {
            throw new NotImplementedException();
        }
    }
}
