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
            return _assemblyMetadata.GetReference();
        }
    }

    private readonly AssemblyMetadata _assemblyMetadata;
    private bool _disposed;

    public MetadataFixture()
    {
        // Initialize FlameCsvCore reference using AssemblyMetadata.CreateFromFile
        string assemblyPath = typeof(CsvTypeMapAttribute<,>).Assembly.Location;
        _assemblyMetadata = AssemblyMetadata.CreateFromFile(assemblyPath);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _assemblyMetadata.Dispose();
    }
}
