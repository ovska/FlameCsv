using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

// ReSharper disable InconsistentNaming

internal sealed class FlameSymbols(Compilation compilation)
{
    public bool NullableContext => compilation.Options.NullableContextOptions.AnnotationsEnabled();
    public bool AllowUnsafe => compilation is CSharpCompilation { Options.AllowUnsafe: true };

    public INamedTypeSymbol CsvOptions { get; } = GetUnboundGeneric(compilation, "FlameCsv.CsvOptions`1");
    public INamedTypeSymbol CsvConverterTTValue { get; } = GetUnboundGeneric(compilation, "FlameCsv.CsvConverter`2");
    public INamedTypeSymbol CsvConverterFactory { get; } = GetUnboundGeneric(compilation, "FlameCsv.CsvConverterFactory`1");
    public INamedTypeSymbol CsvConverterOfTAttribute { get; } = GetUnboundGeneric(compilation, "FlameCsv.Binding.Attributes.CsvConverterAttribute`2");

    public INamedTypeSymbol CsvIndexTargetAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvIndexTargetAttribute");
    public INamedTypeSymbol CsvIndexIgnoreAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvIndexIgnoreAttribute");
    public INamedTypeSymbol CsvIndexAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvIndexAttribute");
    public INamedTypeSymbol CsvHeaderIgnoreAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
    public INamedTypeSymbol CsvHeaderAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
    public INamedTypeSymbol CsvHeaderTargetAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvHeaderTargetAttribute");
    public INamedTypeSymbol CsvConstructorAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");

    private readonly Dictionary<ISymbol, INamedTypeSymbol> _optionsTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, INamedTypeSymbol> _factoryTypes = new(SymbolEqualityComparer.Default);

    public INamedTypeSymbol GetCsvOptionsType(ITypeSymbol tokenType)
    {
        if (!_optionsTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _optionsTypes[tokenType] = type = CsvOptions.OriginalDefinition.Construct(tokenType.OriginalDefinition);
        }

        return type;
    }

    public INamedTypeSymbol GetCsvConverterFactoryType(ITypeSymbol targetType)
    {
        if (!_factoryTypes.TryGetValue(targetType, out INamedTypeSymbol? type))
        {
            _factoryTypes[targetType] = type = CsvConverterFactory.ConstructedFrom.Construct(targetType.OriginalDefinition);
        }

        return type;
    }

    private static INamedTypeSymbol Get(Compilation compilation, string name)
    {
        return compilation.GetTypeByMetadataName(name)
            ?? throw new InvalidOperationException("Type not found by metadata name: " + name);
    }

    private static INamedTypeSymbol GetUnboundGeneric(Compilation compilation, string name) => Get(compilation, name).ConstructUnboundGenericType();
}
