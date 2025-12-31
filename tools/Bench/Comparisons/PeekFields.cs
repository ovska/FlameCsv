using System.Globalization;
using System.Text;
using FlameCsv.Reading;
using nietras.SeparatedValues;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;
using RecordParser.Parsers;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public partial class PeekFields
{
    private static StreamReader GetReader() => new(new MemoryStream(_data), Encoding.UTF8);

    private static readonly byte[] _data = Encoding.UTF8.GetBytes(
        File.ReadAllText("Comparisons/Data/65K_Records_Data.csv").ReplaceLineEndings("\n")
    );

    private static readonly CsvOptions<byte> _flameCsvOptions = new()
    {
        HasHeader = true,
        Newline = CsvNewline.LF,
        Quote = null,
    };

    private static readonly CsvHelper.Configuration.CsvConfiguration _helperConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = "\n",
        HasHeaderRecord = true,
        Delimiter = ",",
    };

    private static readonly CsvDataReaderOptions _sylvanOptions = new()
    {
        CsvStyle = CsvStyle.Standard,
        Delimiter = ',',
        Quote = '"',
        HeaderComparer = StringComparer.OrdinalIgnoreCase,
    };

    [Benchmark(Baseline = true)]
    public double _FlameCsv()
    {
        using CsvReader<byte>.Enumerator enumerator = new CsvReader<byte>(_flameCsvOptions, _data)
            .ParseRecords()
            .GetEnumerator();

        // skip header record
        _ = enumerator.MoveNext();

        double sum = 0;

        while (enumerator.MoveNext())
        {
            sum += csFastFloat.FastDoubleParser.ParseDouble(enumerator.Current.GetRawSpan(11));
        }

        return sum;
    }

    [Benchmark]
    public double _Sep()
    {
        using var reader = Sep.Reader(o =>
                o with
                {
                    Sep = new Sep(','),
                    CultureInfo = CultureInfo.InvariantCulture,
                    HasHeader = true,
                    Unescape = false,
                    DisableQuotesParsing = true,
                }
            )
            .From(_data);

        double sum = 0;

        foreach (var row in reader)
        {
            sum += csFastFloat.FastDoubleParser.ParseDouble(row[11].Span);
        }

        return sum;
    }

    [Benchmark]
    public double _Sylvan()
    {
        using var csv = CsvDataReader.Create(GetReader(), _sylvanOptions);

        double sum = 0;

        while (csv.Read())
        {
            sum += csFastFloat.FastDoubleParser.ParseDouble(csv.GetFieldSpan(11));
        }

        return sum;
    }

    [Benchmark]
    public double _CsvHelper()
    {
        using var csv = new CsvHelper.CsvReader(GetReader(), _helperConfig);

        double sum = 0;

        // skip header
        _ = csv.Read();

        while (csv.Read())
        {
            sum += csFastFloat.FastDoubleParser.ParseDouble(csv.GetField(11));
        }

        return sum;
    }

    [Benchmark]
    public double _RecordParser()
    {
        double sum = 0;

        foreach (
            var row in GetReader()
                .ReadRecords(BuildRecordParserReader(), new() { HasHeader = true, ContainsQuotedFields = false })
        )
        {
            sum += row.Item1;
        }

        return sum;
    }

    private static IVariableLengthReader<ValueTuple<double>> BuildRecordParserReader()
    {
        return new VariableLengthReaderBuilder<ValueTuple<double>>()
            .Map(x => x.Item1, 11, s => csFastFloat.FastDoubleParser.ParseDouble(s))
            .Build(",", CultureInfo.InvariantCulture);
    }
}
