namespace FlameCsv.SourceGen;

// ReSharper disable InconsistentNaming
using NNW = NotNullWhenAttribute;

// ref struct to avoid accidental storage
internal readonly ref struct FlameSymbols
{
    public ITypeSymbol TokenType { get; }
    public ITypeSymbol TargetType { get; }

#if SOURCEGEN_USE_COMPILATION
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

    private readonly INamedTypeSymbol _systemDateTimeOffset;
    private readonly INamedTypeSymbol _systemTimeSpan;
    private readonly INamedTypeSymbol _systemGuid;
    private readonly INamedTypeSymbol _systemISpanParsable;
    private readonly INamedTypeSymbol _systemIUtf8SpanParsable;
    private readonly INamedTypeSymbol _systemISpanFormattable;
    private readonly INamedTypeSymbol _systemIUtf8SpanFormattable;

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

        _systemDateTimeOffset = Get(compilation, "System.DateTimeOffset");
        _systemTimeSpan = Get(compilation, "System.TimeSpan");
        _systemGuid = Get(compilation, "System.Guid");
        _systemISpanParsable = GetUnbound(compilation, "System.ISpanParsable`1");
        _systemIUtf8SpanParsable = GetUnbound(compilation, "System.IUtf8SpanParsable`1");
        _systemISpanFormattable = Get(compilation, "System.ISpanFormattable");
        _systemIUtf8SpanFormattable = Get(compilation, "System.IUtf8SpanFormattable");
    }

#else
    public FlameSymbols(ITypeSymbol tokenType, ITypeSymbol targetType)
    {
        TokenType = tokenType;
        TargetType = targetType;
    }
#endif

#if SOURCEGEN_USE_COMPILATION
    private static readonly SymbolEqualityComparer _seq = SymbolEqualityComparer.Default;

    public bool IsCsvConverterTTValue([NNW(true)] ISymbol? symbol) => _seq.Equals(_converter, symbol);
    public bool IsCsvConverterOfTAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_converterOfTAttribute, symbol);
    public bool IsCsvHeaderAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_headerAttribute, symbol);
    public bool IsCsvIgnoreAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_ignoreAttribute, symbol);
    public bool IsCsvIndexAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_indexAttribute, symbol);
    public bool IsCsvConstructorAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_constructorAttribute, symbol);
    public bool IsCsvOrderAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_orderAttribute, symbol);
    public bool IsCsvRequiredAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_requiredAttribute, symbol);
    public bool IsCsvOptionsOfT([NNW(true)] ISymbol? symbol) => _seq.Equals(_options, symbol);
    public bool IsCsvConverterFactoryOfT([NNW(true)] ISymbol? symbol) => _seq.Equals(_converterFactory, symbol);
    public bool IsTypeProxyAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_typeProxyAttribute, symbol);
    public bool IsIgnoredIndexesAttribute([NNW(true)] ISymbol? symbol) => _seq.Equals(_ignoredIndexesAttribute, symbol);

    public bool IsDateTimeOffset([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemDateTimeOffset, symbol);
    public bool IsTimeSpan([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemTimeSpan, symbol);
    public bool IsGuid([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemGuid, symbol);
    public bool IsISpanParsable([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemISpanParsable, symbol);
    public bool IsIUtf8SpanParsable([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemIUtf8SpanParsable, symbol);
    public bool IsISpanFormattable([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemISpanFormattable, symbol);
    public bool IsIUtf8SpanFormattable([NNW(true)] ISymbol? symbol) => _seq.Equals(_systemIUtf8SpanFormattable, symbol);

    private static INamedTypeSymbol Get(Compilation compilation, string name)
    {
        return compilation.GetTypeByMetadataName(name) ??
            throw new InvalidOperationException("Type not found by metadata name: " + name);
    }

    private static INamedTypeSymbol GetUnbound(Compilation compilation, string name)
    {
        return Get(compilation, name).ConstructUnboundGenericType();
    }
#else
    // ReSharper disable MemberCanBeMadeStatic.Global
    public bool IsCsvConverterTTValue([NNW(true)] ISymbol? symbol) => _converterTTValue.IsEqual(symbol);
    public bool IsCsvConverterOfTAttribute([NNW(true)] ISymbol? symbol) => _converterOfTAttribute.IsEqual(symbol);
    public bool IsCsvHeaderAttribute([NNW(true)] ISymbol? symbol) => _headerAttribute.IsEqual(symbol);
    public bool IsCsvIgnoreAttribute([NNW(true)] ISymbol? symbol) => _ignoreAttribute.IsEqual(symbol);
    public bool IsCsvIndexAttribute([NNW(true)] ISymbol? symbol) => _indexAttribute.IsEqual(symbol);
    public bool IsCsvConstructorAttribute([NNW(true)] ISymbol? symbol) => _constructorAttribute.IsEqual(symbol);
    public bool IsCsvOrderAttribute([NNW(true)] ISymbol? symbol) => _orderAttribute.IsEqual(symbol);
    public bool IsCsvRequiredAttribute([NNW(true)] ISymbol? symbol) => _requiredAttribute.IsEqual(symbol);
    public bool IsTypeProxyAttribute([NNW(true)] ISymbol? symbol) => _typeProxyAttribute.IsEqual(symbol);
    public bool IsIgnoredIndexesAttribute([NNW(true)] ISymbol? symbol) => _ignoredIndexesAttribute.IsEqual(symbol);

    public bool IsDateTimeOffset([NNW(true)] ISymbol? symbol) => _systemDateTimeOffset.IsEqual(symbol);
    public bool IsTimeSpan([NNW(true)] ISymbol? symbol) => _systemTimeSpan.IsEqual(symbol);
    public bool IsGuid([NNW(true)] ISymbol? symbol) => _systemGuid.IsEqual(symbol);
    public bool IsISpanParsable([NNW(true)] ISymbol? symbol) => _systemISpanParsable.IsEqual(symbol);
    public bool IsIUtf8SpanParsable([NNW(true)] ISymbol? symbol) => _systemIUtf8SpanParsable.IsEqual(symbol);
    public bool IsISpanFormattable([NNW(true)] ISymbol? symbol) => _systemISpanFormattable.IsEqual(symbol);
    public bool IsIUtf8SpanFormattable([NNW(true)] ISymbol? symbol) => _systemIUtf8SpanFormattable.IsEqual(symbol);

    private static readonly TypeData _converterTTValue = new("FlameCsv.CsvConverter", 2);
    private static readonly TypeData _converterOfTAttribute = new("FlameCsv.Attributes.CsvConverterAttribute", 1);
    private static readonly TypeData _headerAttribute = new("FlameCsv.Attributes.CsvHeaderAttribute");
    private static readonly TypeData _ignoreAttribute = new("FlameCsv.Attributes.CsvIgnoreAttribute");
    private static readonly TypeData _indexAttribute = new("FlameCsv.Attributes.CsvIndexAttribute");
    private static readonly TypeData _constructorAttribute = new("FlameCsv.Attributes.CsvConstructorAttribute");
    private static readonly TypeData _orderAttribute = new("FlameCsv.Attributes.CsvOrderAttribute");
    private static readonly TypeData _requiredAttribute = new("FlameCsv.Attributes.CsvRequiredAttribute");
    private static readonly TypeData _typeProxyAttribute = new("FlameCsv.Attributes.CsvTypeProxyAttribute");
    private static readonly TypeData _ignoredIndexesAttribute = new("FlameCsv.Attributes.CsvIgnoredIndexesAttribute");

    private static readonly TypeData _systemDateTimeOffset = new("System.DateTimeOffset");
    private static readonly TypeData _systemTimeSpan = new("System.TimeSpan");
    private static readonly TypeData _systemGuid = new("System.Guid");
    private static readonly TypeData _systemISpanParsable = new("System.ISpanParsable", 1);
    private static readonly TypeData _systemIUtf8SpanParsable = new("System.IUtf8SpanParsable", 1);
    private static readonly TypeData _systemISpanFormattable = new("System.ISpanFormattable");
    private static readonly TypeData _systemIUtf8SpanFormattable = new("System.IUtf8SpanFormattable");

    public bool IsCsvOptionsOfT(ITypeSymbol symbol)
    {
        return IsGenericTypeOfT(TokenType, symbol, "FlameCsv.CsvOptions<");
    }

    public bool IsCsvConverterFactoryOfT(ITypeSymbol symbol)
    {
        return IsGenericTypeOfT(TokenType, symbol, "FlameCsv.CsvConverterFactory<");
    }

    private static bool IsGenericTypeOfT(ITypeSymbol tokenType, ITypeSymbol symbol, string prefix)
    {
        if (symbol.SpecialType is SpecialType.System_Object || symbol is INamedTypeSymbol { Arity: not 1 })
        {
            return false;
        }

        string tokenString = tokenType.ToDisplayString();
        string symbolString = symbol.ToDisplayString();

        return
            symbolString.Length == tokenString.Length + prefix.Length + 1 &&
            symbolString.StartsWith(prefix) &&
            symbolString.IndexOf(tokenString, StringComparison.Ordinal) == prefix.Length &&
            symbolString[prefix.Length + tokenString.Length] == '>';
    }
    // ReSharper restore MemberCanBeMadeStatic.Global

    internal readonly struct TypeData
    {
        private int Arity { get; }
        private string Name { get; }
        private string Namespace { get; }

        public TypeData(string fullName, int genericParameterCount = 0)
        {
            int namespaceIndex = fullName.LastIndexOf('.');

            Arity = genericParameterCount;
            Name = fullName[(namespaceIndex + 1)..];
            Namespace = namespaceIndex == -1 ? "" : fullName[..namespaceIndex];
        }

        public bool IsEqual([NNW(true)] ISymbol? symbol)
        {
            if (symbol is not null)
            {
                if (symbol.Name == Name && (Arity == 0 || symbol is INamedTypeSymbol namedType && namedType.Arity == Arity))
                {
                    return symbol.ContainingNamespace.ToDisplayString() == Namespace;
                }
            }

            return false;
        }
    }
#endif
}

