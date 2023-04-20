using System.Globalization;
using System.Text;

namespace FlameCsv.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
public class CsvEnumerateBench
{
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");
    private static readonly string _chars = Encoding.ASCII.GetString(_bytes);
    private static Stream GetFileStream() => new MemoryStream(_bytes);

    [Benchmark(Baseline = true)]
    public void CsvHelper_Sync()
    {
        using var stream = GetFileStream();
        using var reader = new StringReader(_chars);

        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvHelper.CsvReader(reader, config);

        while (csv.Read())
        {
            for (int i = 0; i < 10; i++)
            {
                _ = csv.GetField(i);
            }
        }
    }

    [Benchmark]
    public async ValueTask CsvHelper_Async()
    {
        await using var stream = GetFileStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false);

        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvHelper.CsvReader(reader, config);

        while (await csv.ReadAsync())
        {
            for (int i = 0; i < 10; i++)
            {
                _ = csv.GetField(i);
            }
        }
    }

    [Benchmark]
    public void Flame_Utf8()
    {
        using var stream = GetFileStream();

        foreach (var record in CsvReader.GetEnumerable(_bytes, CsvUtf8ReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }


    [Benchmark]
    public async ValueTask Flame_Utf8_Async()
    {
        using var stream = GetFileStream();

        await foreach (var record in CsvReader.GetAsyncEnumerable(stream, CsvUtf8ReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }

    [Benchmark]
    public void Flame_Char()
    {
        foreach (var record in CsvReader.GetEnumerable(_chars, CsvTextReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }

    [Benchmark]
    public async ValueTask Flame_Char_Async()
    {
        await using var stream = GetFileStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false);

        await foreach (var record in CsvReader.GetAsyncEnumerable(reader, CsvTextReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }
}
