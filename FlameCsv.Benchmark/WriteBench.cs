﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FlameCsv.Attributes;
using FlameCsv.Writing;

// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public partial class WriteBench
{
    [Params(5, 1_000, 10_000)] public int Count { get; set; }

    [Benchmark]
    public void CsvHelper_Records()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteHeader<Obj>();

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteRecord(obj);
            writer.NextRecord();
        }
    }

    [Benchmark]
    public void CsvHelper_RecordsMany()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);
        writer.WriteRecords(_data);
    }

    [Benchmark]
    public void CsvHelper_Fields()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteField("Id");
        writer.WriteField("Name");
        writer.WriteField("IsEnabled");
        writer.WriteField("LastLogin");
        writer.NextRecord();

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteField(obj.Id);
            writer.WriteField(obj.Name);
            writer.WriteField(obj.IsEnabled);
            writer.WriteField(obj.LastLogin);
            writer.NextRecord();
        }
    }

    [Benchmark]
    public async Task Async_CsvHelper_Records()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteHeader<Obj>();

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteRecord(obj);
            await writer.NextRecordAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public async Task Async_CsvHelper_RecordsMany()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);
        await writer.WriteRecordsAsync(_data).ConfigureAwait(false);
    }

    [Benchmark]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    public async Task Async_CsvHelper_Fields()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteField("Id");
        writer.WriteField("Name");
        writer.WriteField("IsEnabled");
        writer.WriteField("LastLogin");
        await writer.NextRecordAsync().ConfigureAwait(false);

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteField(obj.Id);
            writer.WriteField(obj.Name);
            writer.WriteField(obj.IsEnabled);
            writer.WriteField(obj.LastLogin);
            await writer.NextRecordAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public void WriterObj()
    {
        using var writer = CsvWriter.Create(TextWriter.Null, autoFlush: true);

        writer.WriteHeader<Obj>();

        foreach (var obj in _data)
            writer.WriteRecord(obj);
    }

    [Benchmark]
    public void WriterObjTM()
    {
        using var writer = CsvWriter.Create(TextWriter.Null, autoFlush: true);

        writer.WriteHeader(ObjTypeMap.Default);

        foreach (var obj in _data)
            writer.WriteRecord(ObjTypeMap.Default, obj);
    }

    [Benchmark]
    public async Task Async_WriterObj()
    {
        await using var writer = CsvWriter.Create(TextWriter.Null, autoFlush: true);

        writer.WriteHeader<Obj>();
        await writer.NextRecordAsync();

        foreach (var obj in _data)
        {
            writer.WriteRecord(obj);
            await writer.NextRecordAsync();
        }
    }

    [Benchmark]
    public async Task Async_WriterObjTM()
    {
        await using var writer = CsvWriter.Create(TextWriter.Null, autoFlush: true);

        writer.WriteHeader(ObjTypeMap.Default);
        await writer.NextRecordAsync();

        foreach (var obj in _data)
        {
            writer.WriteRecord(ObjTypeMap.Default, obj);
            await writer.NextRecordAsync();
        }
    }

    [Benchmark(Baseline = true)]
    public void Generic()
    {
        CsvWriter.Write(TextWriter.Null, _data);
    }

    [Benchmark]
    public void Generic_TypeMap()
    {
        CsvWriter.Write(TextWriter.Null, _data, ObjTypeMap.Default);
    }

    [Benchmark]
    public Task Async_Generic()
    {
        return CsvWriter.WriteAsync(TextWriter.Null, _data);
    }

    [Benchmark]
    public Task Async_Generic_TypeMap()
    {
        return CsvWriter.WriteAsync(TextWriter.Null, _data, ObjTypeMap.Default);
    }

    [Benchmark]
    public void Generic_Fields()
    {
        var writer = CsvFieldWriter.Create(TextWriter.Null, CsvOptions<char>.Default, -1, false);

        var c1 = CsvOptions<char>.Default.GetConverter<int>();
        var c2 = CsvOptions<char>.Default.GetConverter<string>();
        var c3 = CsvOptions<char>.Default.GetConverter<bool>();
        var c4 = CsvOptions<char>.Default.GetConverter<DateTime>();

        writer.WriteText("Id");
        writer.WriteDelimiter();
        writer.WriteText("Name");
        writer.WriteDelimiter();
        writer.WriteText("IsEnabled");
        writer.WriteDelimiter();
        writer.WriteText("LastLogin");
        writer.WriteNewline();

        for (int i = 0; i < _data.Length; i++)
        {
            if (writer.Writer.NeedsFlush)
                writer.Writer.Flush();

            Obj obj = _data[i];
            writer.WriteField(c1, obj.Id);
            writer.WriteDelimiter();
            writer.WriteField(c2, obj.Name);
            writer.WriteDelimiter();
            writer.WriteField(c3, obj.IsEnabled);
            writer.WriteDelimiter();
            writer.WriteField(c4, obj.LastLogin);
            writer.WriteNewline();
        }

        writer.Writer.Complete(null);
    }

    [GlobalSetup]
    public void Setup()
    {
        _data = Enumerable
            .Range(0, Count)
            .Select(
                i => new Obj
                {
                    Id = i,
                    Name = i % 10 == 0 ? $"name,{i}" : $"name-{i}",
                    IsEnabled = i % 2 == 0,
                    LastLogin = DateTime.UnixEpoch.AddDays(i),
                })
            .ToArray();
    }

    private Obj[] _data = null!;

    public class Obj
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime LastLogin { get; set; }
    }

    [CsvTypeMap<char, Obj>]
    public partial class ObjTypeMap;
}
