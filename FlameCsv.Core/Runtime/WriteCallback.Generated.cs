﻿#if false
// <auto-generated />
using FlameCsv.Writers;

namespace FlameCsv;

internal static class WriteTestGen<T, TWriter>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    private static void WriteHeader<TTuple>(CsvWriteOperation<T, TWriter> writer, CsvWriterOptions<T> options)
        where TTuple : struct, System.Runtime.CompilerServices.ITuple
	{
        if (options.WriteHeader)
        {
            int count = new TTuple().Length;
            int current = 0;
            
            while (current < count)
			{
                if (current != 0) writer.WriteDelimiter();
                writer.WriteString($"Item{++current}");
		    }

            writer.WriteNewline();
        }
	}

    public static async Task WriteAsync<T0, T1>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();
        var formatter10 = options.GetFormatter<T10>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteDelimiter();
            writer.WriteValue(formatter10, record.Item11);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();
        var formatter10 = options.GetFormatter<T10>();
        var formatter11 = options.GetFormatter<T11>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteDelimiter();
            writer.WriteValue(formatter10, record.Item11);
            writer.WriteDelimiter();
            writer.WriteValue(formatter11, record.Item12);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();
        var formatter10 = options.GetFormatter<T10>();
        var formatter11 = options.GetFormatter<T11>();
        var formatter12 = options.GetFormatter<T12>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteDelimiter();
            writer.WriteValue(formatter10, record.Item11);
            writer.WriteDelimiter();
            writer.WriteValue(formatter11, record.Item12);
            writer.WriteDelimiter();
            writer.WriteValue(formatter12, record.Item13);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();
        var formatter10 = options.GetFormatter<T10>();
        var formatter11 = options.GetFormatter<T11>();
        var formatter12 = options.GetFormatter<T12>();
        var formatter13 = options.GetFormatter<T13>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteDelimiter();
            writer.WriteValue(formatter10, record.Item11);
            writer.WriteDelimiter();
            writer.WriteValue(formatter11, record.Item12);
            writer.WriteDelimiter();
            writer.WriteValue(formatter12, record.Item13);
            writer.WriteDelimiter();
            writer.WriteValue(formatter13, record.Item14);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();
        var formatter10 = options.GetFormatter<T10>();
        var formatter11 = options.GetFormatter<T11>();
        var formatter12 = options.GetFormatter<T12>();
        var formatter13 = options.GetFormatter<T13>();
        var formatter14 = options.GetFormatter<T14>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteDelimiter();
            writer.WriteValue(formatter10, record.Item11);
            writer.WriteDelimiter();
            writer.WriteValue(formatter11, record.Item12);
            writer.WriteDelimiter();
            writer.WriteValue(formatter12, record.Item13);
            writer.WriteDelimiter();
            writer.WriteValue(formatter13, record.Item14);
            writer.WriteDelimiter();
            writer.WriteValue(formatter14, record.Item15);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

    public static async Task WriteAsync<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(
        CsvWriteOperation<T, TWriter> writer,
        CsvWriterOptions<T> options,
        IAsyncEnumerable<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)>(writer, options);

        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);

        if (!await enumerator.MoveNextAsync())
			return;

        var formatter0 = options.GetFormatter<T0>();
        var formatter1 = options.GetFormatter<T1>();
        var formatter2 = options.GetFormatter<T2>();
        var formatter3 = options.GetFormatter<T3>();
        var formatter4 = options.GetFormatter<T4>();
        var formatter5 = options.GetFormatter<T5>();
        var formatter6 = options.GetFormatter<T6>();
        var formatter7 = options.GetFormatter<T7>();
        var formatter8 = options.GetFormatter<T8>();
        var formatter9 = options.GetFormatter<T9>();
        var formatter10 = options.GetFormatter<T10>();
        var formatter11 = options.GetFormatter<T11>();
        var formatter12 = options.GetFormatter<T12>();
        var formatter13 = options.GetFormatter<T13>();
        var formatter14 = options.GetFormatter<T14>();
        var formatter15 = options.GetFormatter<T15>();

        do
        {
            if (writer.NeedsFlush)
                await writer.FlushAsync(cancellationToken);

            var record = enumerator.Current;
            writer.WriteValue(formatter0, record.Item1);
            writer.WriteDelimiter();
            writer.WriteValue(formatter1, record.Item2);
            writer.WriteDelimiter();
            writer.WriteValue(formatter2, record.Item3);
            writer.WriteDelimiter();
            writer.WriteValue(formatter3, record.Item4);
            writer.WriteDelimiter();
            writer.WriteValue(formatter4, record.Item5);
            writer.WriteDelimiter();
            writer.WriteValue(formatter5, record.Item6);
            writer.WriteDelimiter();
            writer.WriteValue(formatter6, record.Item7);
            writer.WriteDelimiter();
            writer.WriteValue(formatter7, record.Item8);
            writer.WriteDelimiter();
            writer.WriteValue(formatter8, record.Item9);
            writer.WriteDelimiter();
            writer.WriteValue(formatter9, record.Item10);
            writer.WriteDelimiter();
            writer.WriteValue(formatter10, record.Item11);
            writer.WriteDelimiter();
            writer.WriteValue(formatter11, record.Item12);
            writer.WriteDelimiter();
            writer.WriteValue(formatter12, record.Item13);
            writer.WriteDelimiter();
            writer.WriteValue(formatter13, record.Item14);
            writer.WriteDelimiter();
            writer.WriteValue(formatter14, record.Item15);
            writer.WriteDelimiter();
            writer.WriteValue(formatter15, record.Item16);
            writer.WriteNewline();
        }
        while (await enumerator.MoveNextAsync());
    }

}
#endif