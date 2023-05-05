namespace FlameCsv.SourceGen;

[Generator]
public partial class TypeMapGenerator : ISourceGenerator
{
    private static class Namespaces
    {
        public const string Binding = "FlameCsv.Binding";
        public const string Attributes = "FlameCsv.Binding.Attributes";
    }

    private static class Types
    {
        public const string CsvTypeMapAttribute = "CsvTypeMapAttribute";
        public const string CsvHeaderIgnoreAttribute = "CsvHeaderIgnoreAttribute";
    }

    private INamedTypeSymbol? _csvReaderOptions;
    private INamedTypeSymbol? _icsvParserFactory;
    private INamedTypeSymbol? _typeMapAttribute;
    private INamedTypeSymbol? _csvHeaderIgnoreAttribute;
    private INamedTypeSymbol? _csvHeaderAttribute;
    private INamedTypeSymbol? _csvHeaderRequiredAttribute;
    private INamedTypeSymbol? _csvParserOverrideAttribute;
    private INamedTypeSymbol? _csvParserOverrideOfTAttribute;

    private readonly List<(int id, string name)> _requiredMembers = new();

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return;

        InitAttributes(context.Compilation);

        foreach (var typeMapSymbol in GetTypeMapSymbols(context, receiver))
        {
            context.AddSource($"{typeMapSymbol.ContainingClass.Name}.G.cs", SourceText.From(CreateTypeMap(typeMapSymbol), Encoding.UTF8));
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    private string CreateTypeMap(TypeMapSymbol typeMap)
    {
        return $@"#nullable enable
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Binding;

namespace {typeMap.ContainingClass.ContainingNamespace.ToDisplayString()}
{{
    partial class {typeMap.ContainingClass.Name} : CsvTypeMap<{typeMap.TokenName}, {typeMap.ResultName}>
    {{
        protected override bool IgnoreUnparsable => {(typeMap.IgnoreUnparsable ? "true" : "false")};
        protected override bool IgnoreUnmatched => {(typeMap.IgnoreUnmatched ? "true" : "false")};
        protected override bool IgnoreDuplicate => {(typeMap.IgnoreDuplicate ? "true" : "false")};
{WriteCreateInstance(typeMap)}
        protected override TryParseHandler? BindMember(string name, CsvReaderOptions<{typeMap.TokenName}> options, ref ulong fieldMask)
        {{
{string.Join(@"
", WriteParsers(typeMap))}
            return null;
        }}

        protected override void ValidateRequiredMembers(ICollection<string> headers, ulong fieldMask)
        {{{WriteRequiredCheck(typeMap)}
        }}
    }}
}}";
    }

    private string WriteCreateInstance(TypeMapSymbol typeMap)
    {
        if (!HasCreateInstanceMethod(typeMap))
        {
            return $@"        
        protected override {typeMap.ResultName} CreateInstance() => new {typeMap.ResultName}();
";
        }

        return "";
    }

    private string WriteRequiredCheck(TypeMapSymbol typeMap)
    {
        if (_requiredMembers.Count == 0)
            return "";

        var sb = new StringBuilder(64);

        foreach (var (id, name) in _requiredMembers)
        {
            sb.Append($@"
            if (!SetFlag(ref fieldMask, {id}))
                ThrowRequiredNotRead({Stringify(name)});
");
        }

        sb.Length--;
        return sb.ToString();
    }

    private IEnumerable<string> WriteParsers(TypeMapSymbol typeMap)
    {
        int count = 0;

        foreach (var member in typeMap.Type.GetMembers())
        {
            if (member is IPropertySymbol property && IsValidProperty(property))
            {
                yield return FormatString(property, property.Type);
                AddRequiredMember(count, member);
                count++;
            }
            else if (member is IFieldSymbol field && IsValidField(field))
            {
                yield return FormatString(field, field.Type);
                AddRequiredMember(count, member);
                count++;
            }
        }

        if (count == 0)
        {
            // TODO: add diagnostic
            throw new InvalidOperationException("No writable members on type " + typeMap.Type.ToDisplayString());
        }

        string FormatString(ISymbol propertyOrField, ITypeSymbol type)
        {
            var names = string.Join(@" ||
                ", GetMemberHeaderNames(propertyOrField, typeMap).Select(n => $"options.Comparer.Equals({Stringify(n)}, name)"));

            return $@"            if ({names})
            {{
                if (SetFlag(ref fieldMask, {count}))
                    return HandleDuplicate(member: {Stringify(propertyOrField.Name)}, name, options);

                var parser = {ResolveParser(propertyOrField, type)};

                return (ref {typeMap.ResultName} instance, ReadOnlySpan<{typeMap.TokenName}> field) =>
                {{
                    if (parser.TryParse(field, out var value))
                    {{
                        instance.{propertyOrField.Name} = value;
                        return true;
                    }}
                    return false;
                }};
            }}
";
        }

        string ResolveParser(ISymbol propertyOrField, ITypeSymbol type)
        {
            foreach (var attributeData in propertyOrField.GetAttributes())
            {
                if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                    SymbolEqualityComparer.Default.Equals(typeMap.Token, attribute.TypeArguments[0]) &&
                    SymbolEqualityComparer.Default.Equals(attribute.ConstructUnboundGenericType(), _csvParserOverrideOfTAttribute))
                {
                    return GetParserInitializer(typeMap.Token, type, attribute.TypeArguments[1]);
                }
            }

            return $"options.GetParser<{type.ToDisplayString()}>()";
        }
    }

    private void AddRequiredMember(int id, ISymbol member)
    {
        foreach (var attributeData in member.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, _csvHeaderRequiredAttribute))
            {
                _requiredMembers.Add((id, member.Name));
                return;
            }
        }
    }

    private IEnumerable<TypeMapSymbol> GetTypeMapSymbols(
        GeneratorExecutionContext context,
        SyntaxReceiver receiver)
    {
        foreach (var candidate in receiver.Candidates)
        {
            var model = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(candidate, context.CancellationToken);

            if (classSymbol is not null)
            {
                foreach (var attributeData in classSymbol.GetAttributes())
                {
                    if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                        SymbolEqualityComparer.Default.Equals(attribute.ConstructUnboundGenericType(), _typeMapAttribute))
                    {
                        yield return new TypeMapSymbol(
                            containingClass: (INamedTypeSymbol)classSymbol,
                            attribute: attributeData,
                            context: context);
                    }
                }
            }
        }
    }

    private IEnumerable<string?> GetMemberHeaderNames(ISymbol symbol, TypeMapSymbol typeMap)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, _csvHeaderAttribute))
            {
                foreach (var arg in attributeData.ConstructorArguments)
                {
                    if (arg.Values.IsDefaultOrEmpty)
                    {
                        typeMap.Context.ReportDiagnostic(Diagnostics.EmptyHeaderValuesAttribute(typeMap.Type, symbol));
                        yield return symbol.Name;
                    }
                    else
                    {
                        foreach (var value in arg.Values)
                        {
                            yield return (string?)value.Value;
                        }
                    }

                    yield break;
                }
            }
        }

        yield return symbol.Name;
    }

    [MemberNotNull(nameof(_typeMapAttribute))]
    [MemberNotNull(nameof(_csvHeaderIgnoreAttribute))]
    [MemberNotNull(nameof(_csvHeaderAttribute))]
    private void InitAttributes(Compilation compilation)
    {
        _csvReaderOptions = Init("FlameCsv.CsvReaderOptions`1").ConstructUnboundGenericType();
        _typeMapAttribute = Init("FlameCsv.Binding.CsvTypeMapAttribute`2").ConstructUnboundGenericType();
        _icsvParserFactory = Init("FlameCsv.Parsers.ICsvParserFactory`1").ConstructUnboundGenericType();
        _csvHeaderIgnoreAttribute = Init("FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
        _csvHeaderAttribute = Init("FlameCsv.Binding.Attributes.CsvHeaderAttribute");
        _csvHeaderRequiredAttribute = Init("FlameCsv.Binding.Attributes.CsvHeaderRequiredAttribute");
        _csvParserOverrideAttribute = Init("FlameCsv.Binding.Attributes.CsvParserOverrideAttribute");
        _csvParserOverrideOfTAttribute = Init("FlameCsv.Binding.Attributes.CsvParserOverrideAttribute`2").ConstructUnboundGenericType();

        INamedTypeSymbol Init(string name)
        {
            return compilation.GetTypeByMetadataName(name) ?? throw new Exception($"Couldn't find type {name}");
        }
    }
}
