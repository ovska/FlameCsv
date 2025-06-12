using FlameCsv.Attributes;
using Microsoft.CodeAnalysis;

namespace FlameCsv.Tests.SourceGen;

[CollectionDefinition(nameof(MetadataCollection))]
public class MetadataCollection : ICollectionFixture<MetadataFixture>;

public class MetadataFixture : IDisposable
{
    public MetadataReference FlameCsvCore
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _assemblyMetadata.Value.GetReference();
        }
    }

    private readonly Lazy<AssemblyMetadata> _assemblyMetadata = new Lazy<AssemblyMetadata>(static () =>
        AssemblyMetadata.CreateFromFile(typeof(CsvTypeMapAttribute<,>).Assembly.Location)
    );

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_assemblyMetadata.IsValueCreated)
        {
            _assemblyMetadata.Value.Dispose();
        }
    }
}
