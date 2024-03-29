﻿using CommunityToolkit.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Writing;

namespace FlameCsv;

public static partial class CsvWriter
{
    public static Task WriteToFileAsync<TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options,
        CsvContextOverride<byte> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var dematerializer = typeMap.GetDematerializer(options);
        var _context = new CsvWritingContext<byte>(options, in context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(File.OpenWrite(path), in _context),
            dematerializer,
            cancellationToken);
    }


    public static Task WriteToFileAsync<TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char> options,
        Encoding? encoding = null,
        CsvContextOverride<char> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var dematerializer = typeMap.GetDematerializer(options);
        var _context = new CsvWritingContext<char>(options, in context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(
                new StreamWriter(File.OpenWrite(path), encoding: encoding, leaveOpen: false),
                in _context),
            dematerializer,
            cancellationToken);
    }


    public static Task WriteAsync<TValue>(
        IEnumerable<TValue> values,
        PipeWriter pipeWriter,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options,
        CsvContextOverride<byte> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(pipeWriter);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var dematerializer = typeMap.GetDematerializer(options);
        var _context = new CsvWritingContext<byte>(options, in context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, in _context),
            dematerializer,
            cancellationToken);
    }


    public static Task WriteAsync<TValue>(
        IEnumerable<TValue> values,
        Stream stream,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options,
        CsvContextOverride<byte> context = default,
        int bufferSize = -1,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanWrite(stream);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var dematerializer = typeMap.GetDematerializer(options);
        var _context = new CsvWritingContext<byte>(options, in context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, in _context, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="values"></param>
    /// <param name="options"></param>
    /// <param name="context"></param>
    /// <param name="initialCapacity">Initial capacity of the string builder</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A <see cref="StringBuilder"/> containing the CSV</returns>
    public static Task<StringBuilder> WriteToStringAsync<TValue>(
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char> options,
        CsvContextOverride<char> context = default,
        int initialCapacity = 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<StringBuilder>(cancellationToken);

        return Core();

        async Task<StringBuilder> Core()
        {
            var dematerializer = typeMap.GetDematerializer(options);
            var _context = new CsvWritingContext<char>(options, in context);

            var sb = new StringBuilder(capacity: initialCapacity);
            await WriteAsyncCore(
                values,
                CsvFieldWriter.Create(new StringWriter(sb), in _context),
                dematerializer,
                cancellationToken);
            return sb;
        }
    }
}
