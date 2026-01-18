namespace FlameCsv.SourceGen;

using NNW = NotNullWhenAttribute;

// ref struct to avoid accidental storage
internal readonly ref struct FlameSymbols
{
    public ITypeSymbol TokenType { get; }
    public ITypeSymbol TargetType { get; }

    private readonly INamedTypeSymbol _options;
    private readonly INamedTypeSymbol _converter;
    private readonly INamedTypeSymbol _converterFactory;
    private readonly INamedTypeSymbol _converterOfTAttribute;
    private readonly INamedTypeSymbol _headerAttribute;
    private readonly INamedTypeSymbol _ignoreAttribute;
    private readonly INamedTypeSymbol _indexAttribute;
    private readonly INamedTypeSymbol _constructorAttribute;
    private readonly INamedTypeSymbol _orderAttribute;
    private readonly INamedTypeSymbol _requiredAttribute;
    private readonly INamedTypeSymbol _typeProxyAttribute;
    private readonly INamedTypeSymbol _ignoredIndexesAttribute;
    private readonly INamedTypeSymbol _stringPoolingAttribute;

    private readonly INamedTypeSymbol _systemDateTimeOffset;
    private readonly INamedTypeSymbol _systemTimeSpan;
    private readonly INamedTypeSymbol _systemGuid;
    private readonly INamedTypeSymbol _systemISpanParsable;
    private readonly INamedTypeSymbol _systemIUtf8SpanParsable;
    private readonly INamedTypeSymbol _systemISpanFormattable;
    private readonly INamedTypeSymbol _systemIUtf8SpanFormattable;
    private readonly INamedTypeSymbol _systemIBinaryInteger;
    private readonly INamedTypeSymbol _systemIFloatingPoint;

    public FlameSymbols(Compilation compilation, ITypeSymbol tokenType, ITypeSymbol targetType)
    {
        TokenType = tokenType;
        TargetType = targetType;
        _options = GetUnbound(compilation, "FlameCsv.CsvOptions`1").OriginalDefinition.Construct(tokenType);
        _converter = GetUnbound(compilation, "FlameCsv.CsvConverter`2");
        _converterFactory = GetUnbound(compilation, "FlameCsv.CsvConverterFactory`1")
            .OriginalDefinition.Construct(tokenType);
        _converterOfTAttribute = GetUnbound(compilation, "FlameCsv.Attributes.CsvConverterAttribute`1");
        _headerAttribute = Get(compilation, "FlameCsv.Attributes.CsvHeaderAttribute");
        _ignoreAttribute = Get(compilation, "FlameCsv.Attributes.CsvIgnoreAttribute");
        _indexAttribute = Get(compilation, "FlameCsv.Attributes.CsvIndexAttribute");
        _constructorAttribute = Get(compilation, "FlameCsv.Attributes.CsvConstructorAttribute");
        _orderAttribute = Get(compilation, "FlameCsv.Attributes.CsvOrderAttribute");
        _requiredAttribute = Get(compilation, "FlameCsv.Attributes.CsvRequiredAttribute");
        _typeProxyAttribute = Get(compilation, "FlameCsv.Attributes.CsvTypeProxyAttribute");
        _ignoredIndexesAttribute = Get(compilation, "FlameCsv.Attributes.CsvIgnoredIndexesAttribute");
        _stringPoolingAttribute = Get(compilation, "FlameCsv.Attributes.CsvStringPoolingAttribute");

        _systemDateTimeOffset = Get(compilation, "System.DateTimeOffset");
        _systemTimeSpan = Get(compilation, "System.TimeSpan");
        _systemGuid = Get(compilation, "System.Guid");
        _systemISpanParsable = GetUnbound(compilation, "System.ISpanParsable`1");
        _systemIUtf8SpanParsable = GetUnbound(compilation, "System.IUtf8SpanParsable`1");
        _systemISpanFormattable = Get(compilation, "System.ISpanFormattable");
        _systemIUtf8SpanFormattable = Get(compilation, "System.IUtf8SpanFormattable");
        _systemIBinaryInteger = GetUnbound(compilation, "System.Numerics.IBinaryInteger`1");
        _systemIFloatingPoint = GetUnbound(compilation, "System.Numerics.IFloatingPoint`1");
    }

    private static SymbolEqualityComparer Seq => SymbolEqualityComparer.Default;

    public bool IsCsvConverterTTValue([NNW(true)] ISymbol? symbol) => Seq.Equals(_converter, symbol);

    public bool IsCsvConverterOfTAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_converterOfTAttribute, symbol);

    public bool IsCsvHeaderAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_headerAttribute, symbol);

    public bool IsCsvIgnoreAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_ignoreAttribute, symbol);

    public bool IsStringPoolingAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_stringPoolingAttribute, symbol);

    public bool IsCsvIndexAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_indexAttribute, symbol);

    public bool IsCsvConstructorAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_constructorAttribute, symbol);

    public bool IsCsvOrderAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_orderAttribute, symbol);

    public bool IsCsvRequiredAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_requiredAttribute, symbol);

    public bool IsCsvOptionsOfT([NNW(true)] ISymbol? symbol) => Seq.Equals(_options, symbol);

    public bool IsCsvConverterFactoryOfT([NNW(true)] ISymbol? symbol) => Seq.Equals(_converterFactory, symbol);

    public bool IsTypeProxyAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_typeProxyAttribute, symbol);

    public bool IsIgnoredIndexesAttribute([NNW(true)] ISymbol? symbol) => Seq.Equals(_ignoredIndexesAttribute, symbol);

    public bool IsDateTimeOffset([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemDateTimeOffset, symbol);

    public bool IsTimeSpan([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemTimeSpan, symbol);

    public bool IsGuid([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemGuid, symbol);

    public bool IsISpanParsable([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemISpanParsable, symbol);

    public bool IsIUtf8SpanParsable([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemIUtf8SpanParsable, symbol);

    public bool IsISpanFormattable([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemISpanFormattable, symbol);

    public bool IsIUtf8SpanFormattable([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemIUtf8SpanFormattable, symbol);

    public bool IsIBinaryInteger([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemIBinaryInteger, symbol);

    public bool IsIFloatingPoint([NNW(true)] ISymbol? symbol) => Seq.Equals(_systemIFloatingPoint, symbol);

    private static INamedTypeSymbol Get(Compilation compilation, string name)
    {
        return compilation.GetTypeByMetadataName(name)
            ?? throw new InvalidOperationException("Type not found by metadata name: " + name);
    }

    private static INamedTypeSymbol GetUnbound(Compilation compilation, string name)
    {
        return Get(compilation, name).ConstructUnboundGenericType();
    }
}
