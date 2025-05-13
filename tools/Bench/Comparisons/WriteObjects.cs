﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using nietras.SeparatedValues;
using Sylvan.Data;

// ReSharper disable all

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class WriteObjects
{
    [Params(100, 5000, 20_000)]
    public int Records { get; set; } = 5000;

    [Params(false, true)]
    public bool Async { get; set; }

    private static readonly TextWriter _destination = new YieldingNullTextWriter();

    private IEnumerable<Entry> Data =>
        Records switch
        {
            100 => _data5000.Take(100),
            5000 => _data5000,
            20_000 => _data20000,
            _ => throw new ArgumentOutOfRangeException(nameof(Records)),
        };

    [Benchmark(Baseline = true)]
    public async Task _Flame_SrcGen()
    {
        if (Async)
        {
            await CsvWriter.WriteAsync(_destination, Data, EntryTypeMap.Default);
            return;
        }

        CsvWriter.Write(_destination, Data, EntryTypeMap.Default);
    }

    [Benchmark]
    public async Task _Flame()
    {
        if (Async)
        {
            await CsvWriter.WriteAsync(_destination, Data);
            return;
        }

        CsvWriter.Write(_destination, Data);
    }

    [Benchmark]
    public async Task _Sep()
    {
        var writer = Sep.Writer(c => c with { Sep = new(','), Escape = true, WriteHeader = true }).To(_destination);

        writer.Header.Add(
            "Index",
            "Name",
            "Contact",
            "Count",
            "Latitude",
            "Longitude",
            "Height",
            "Location",
            "Category",
            "Popularity"
        );

        int count = 0;

        foreach (var entry in Data)
        {
            SepWriter.Row row = writer.NewRow();
            row[0].Format(entry.Index);
            row[1].Set(entry.Name);
            row[2].Set(entry.Contact);
            row[3].Format(entry.Count);
            row[4].Format(entry.Latitude);
            row[5].Format(entry.Longitude);
            row[6].Format(entry.Height);
            row[7].Set(entry.Location);
            row[8].Set(entry.Category);
            row[9].Set($"{entry.Popularity}");

            if (Async)
            {
                await row.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                row.Dispose();
            }

            if (++count == 100)
            {
                if (Async)
                {
                    await writer.FlushAsync().ConfigureAwait(false);
                }
                else
                {
                    writer.Flush();
                }

                count = 0;
            }
        }

        if (Async)
        {
            await writer.FlushAsync().ConfigureAwait(false);
            await writer.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            writer.Flush();
            writer.Dispose();
        }
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        if (Async)
        {
            await using var writer = Sylvan.Data.Csv.CsvDataWriter.Create(_destination);
            await writer.WriteAsync(Data.AsDataReader()).ConfigureAwait(false);
        }
        else
        {
            using var writer = Sylvan.Data.Csv.CsvDataWriter.Create(_destination);
            writer.Write(Data.AsDataReader());
        }
    }

    [Benchmark]
    public async Task _CsvHelper()
    {
        if (Async)
        {
            await using CsvHelper.CsvWriter writer = new(_destination, CultureInfo.InvariantCulture);
            await writer.WriteRecordsAsync(Data).ConfigureAwait(false);
        }
        else
        {
            using CsvHelper.CsvWriter writer = new(_destination, CultureInfo.InvariantCulture);
            writer.WriteRecords(Data);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _data5000 = CsvReader.Read<Entry>(File.OpenRead("Comparisons/Data/SampleCSVFile_556kb.csv")).ToArray();
        _data20000 = CsvReader.Read<Entry>(File.OpenRead("Comparisons/Data/SampleCSVFile_556kb_4x.csv")).ToArray();
    }

    private Entry[] _data5000 = null!;
    private Entry[] _data20000 = null!;
}

// TextWriter.Null equivalent with forced async yielding
file sealed class YieldingNullTextWriter : TextWriter
{
    public override IFormatProvider FormatProvider => CultureInfo.InvariantCulture;
    public override Encoding Encoding => Encoding.UTF8;

    [AllowNull]
    public override string NewLine
    {
        get => base.NewLine;
        set { }
    }

    // To avoid all unnecessary overhead in the base, override all Flush/Write methods as pure nops.

    public override void Flush() { }

    public override async Task FlushAsync() => await Task.Yield();

    public override async Task FlushAsync(CancellationToken cancellationToken) => await Task.Yield();

    public override void Write(char value) { }

    public override void Write(char[]? buffer) { }

    public override void Write(char[] buffer, int index, int count) { }

    public override void Write(ReadOnlySpan<char> buffer) { }

    public override void Write(bool value) { }

    public override void Write(int value) { }

    public override void Write(uint value) { }

    public override void Write(long value) { }

    public override void Write(ulong value) { }

    public override void Write(float value) { }

    public override void Write(double value) { }

    public override void Write(decimal value) { }

    public override void Write(string? value) { }

    public override void Write(object? value) { }

    public override void Write(StringBuilder? value) { }

    public override void Write([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0) { }

    public override void Write(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        object? arg0,
        object? arg1
    ) { }

    public override void Write(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        object? arg0,
        object? arg1,
        object? arg2
    ) { }

    public override void Write(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        params object?[] arg
    ) { }

    public override void Write(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        params ReadOnlySpan<object?> arg
    ) { }

    public override async Task WriteAsync(char value) => await Task.Yield();

    public override async Task WriteAsync(string? value) => await Task.Yield();

    public override async Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default) =>
        await Task.Yield();

    public override async Task WriteAsync(char[] buffer, int index, int count) => await Task.Yield();

    public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
        await Task.Yield();

    public override void WriteLine() { }

    public override void WriteLine(char value) { }

    public override void WriteLine(char[]? buffer) { }

    public override void WriteLine(char[] buffer, int index, int count) { }

    public override void WriteLine(ReadOnlySpan<char> buffer) { }

    public override void WriteLine(bool value) { }

    public override void WriteLine(int value) { }

    public override void WriteLine(uint value) { }

    public override void WriteLine(long value) { }

    public override void WriteLine(ulong value) { }

    public override void WriteLine(float value) { }

    public override void WriteLine(double value) { }

    public override void WriteLine(decimal value) { }

    public override void WriteLine(string? value) { }

    public override void WriteLine(StringBuilder? value) { }

    public override void WriteLine(object? value) { }

    public override void WriteLine(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        object? arg0
    ) { }

    public override void WriteLine(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        object? arg0,
        object? arg1
    ) { }

    public override void WriteLine(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        object? arg0,
        object? arg1,
        object? arg2
    ) { }

    public override void WriteLine(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        params object?[] arg
    ) { }

    public override void WriteLine(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format,
        params ReadOnlySpan<object?> arg
    ) { }

    public override async Task WriteLineAsync(char value) => await Task.Yield();

    public override async Task WriteLineAsync(string? value) => await Task.Yield();

    public override async Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default) =>
        await Task.Yield();

    public override async Task WriteLineAsync(char[] buffer, int index, int count) => await Task.Yield();

    public override async Task WriteLineAsync(
        ReadOnlyMemory<char> buffer,
        CancellationToken cancellationToken = default
    ) => await Task.Yield();

    public override async Task WriteLineAsync() => await Task.Yield();
}
