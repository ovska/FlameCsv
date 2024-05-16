using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

namespace FlameCsv;

public abstract class CsvWriter<T> : IDisposable, IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Current column index. If not zero, delimiter is written before any fields.
    /// </summary>
    public abstract int ColumnIndex { get; }

    /// <summary>
    /// Current line in the CSV. Starts from 1.
    /// </summary>
    public abstract int LineIndex { get; }

    internal CsvWriter()
    {
    }

    [RUF(Messages.CompiledExpressions)] public abstract void WriteHeader<[DAM(Messages.ReflectionBound)] TRecord>();
    public abstract void WriteHeader<TRecord>(CsvTypeMap<T, TRecord> typeMap);
    [RUF(Messages.CompiledExpressions)] public abstract ValueTask WriteHeaderAsync<[DAM(Messages.ReflectionBound)] TRecord>(CancellationToken cancellationToken = default);
    public abstract ValueTask WriteHeaderAsync<TRecord>(CsvTypeMap<T, TRecord> typeMap, CancellationToken cancellationToken = default);

    [RUF(Messages.CompiledExpressions)] public abstract void WriteRecord<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value);
    public abstract void WriteRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value);
    [RUF(Messages.CompiledExpressions)] public abstract ValueTask WriteRecordAsync<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value, CancellationToken cancellationToken = default);
    public abstract ValueTask WriteRecordAsync<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value, CancellationToken cancellationToken = default);

    public abstract void WriteRaw(scoped ReadOnlySpan<T> value);
    public abstract ValueTask WriteRawAsync(scoped ReadOnlySpan<T> value, CancellationToken cancellationToken = default);
    public abstract void WriteField<TField>([AllowNull] TField value);
    public abstract ValueTask WriteFieldAsync<TField>([AllowNull] TField value, CancellationToken cancellationToken);
    public abstract void WriteField(ReadOnlySpan<char> text, bool skipEscaping = false);
    public abstract ValueTask WriteFieldAsync(ReadOnlySpan<char> text, bool skipEscaping = false, CancellationToken cancellationToken = default);
    public abstract void NextRecord();
    public abstract ValueTask NextRecordAsync(CancellationToken cancellationToken = default);
    public abstract void Flush();
    public abstract ValueTask FlushAsync(CancellationToken cancellationToken = default);

    public abstract void Dispose();
    public abstract ValueTask DisposeAsync();
}
