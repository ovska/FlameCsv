using FlameCsv.SourceGen.Bindings;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private string GetReadCode(in TypeMapSymbol typeMap)
    {
        return @$"        /// <summary>
        /// Callback for parsing a single field and writing the value to the object.
        /// </summary>
        private delegate bool TryParseHandler{typeMap.HandlerArgs};

        protected override IMaterializer<{typeMap.Token}, {typeMap.ResultName}> BindMembers(
            ReadOnlySpan<string> headers,
            bool exposeContent,
            CsvOptions<{typeMap.Token}> options)
        {{
            TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);
            bool anyFieldBound = false;

            for (int index = 0; index < headers.Length; index++)
            {{
                string name = headers[index];

{string.Join(@"
", WriteMatchers(typeMap))}
                {(typeMap.IgnoreUnmatched
                    ? $"materializer.Handlers[index] = {typeMap.HandlerArgs} => true; // ignored"
                    : "ThrowUnmatched(name, index, exposeContent);")}
            }}

            if (!anyFieldBound)
                ThrowNoFieldsBound(headers, exposeContent);
{WriteRequiredCheck()}
            return materializer;
        }}

        protected override IMaterializer<{typeMap.Token}, {typeMap.ResultName}> BindMembers(
            bool exposeContent,
            CsvOptions<{typeMap.Token}> options)
        {{
            throw new NotSupportedException(""{typeMap.ContainingClass.MetadataName} does not support index binding."");
        }}{WriteMissingRequiredFields()}

        private struct ParseState
        {{
            
        {string.Join(@"
            ", _bindings.AllBindings.Select(b => $"public {b.Type.ToDisplayString()} {b.Name};"))}
        }}

        private struct TypeMapMaterializer : IMaterializer<{typeMap.Token}, {typeMap.ResultName}>
        {{
            {string.Join(@"
            ", WriteParserMembers(typeMap))}

            public readonly TryParseHandler[] Handlers;

            public TypeMapMaterializer(int length)
            {{
                Handlers = new TryParseHandler[length];
            }}

            public int FieldCount => Handlers.Length;

            public {typeMap.ResultName} Parse<TReader>(ref TReader reader) where TReader : ICsvFieldReader<{typeMap.Token}>
            {{
                // If possible, throw early if there are an invalid amount of fields
                reader.TryEnsureFieldCount(fieldCount: Handlers.Length);

                ParseState state = default;{WriteDefaultParameterValues()}
                
                int index = 0;

                while (reader.TryReadNext(out ReadOnlyMemory<{typeMap.Token}> field))
                {{
                    if (Handlers[index++](ref this, ref state, field.Span))
                    {{
                        continue;
                    }}

                    reader.ThrowParseFailed(field, null);
                }}

                // Ensure there were no leftover fields
                reader.EnsureFullyConsumed(fieldCount: Handlers.Length);

                // Create the value from parsed values. Required members are validated when creating the materializer,
                // optional members are assigned only if parsed to not overwrite possible default values.
                {typeMap.ResultName} obj = new {typeMap.ResultName}{WriteSetters(typeMap)}
                return obj;
            }}
        }}

        {string.Join(@"

        ", WriteParserHandlers(typeMap))}"; 
    }

    private string WriteDefaultParameterValues()
    {
        // we always write these; they are always compile time constants
        if (_bindings.Parameters.Length == 0)
            return "";

        var sb = new StringBuilder(64);

        sb.Append(@"
");

        bool commentWritten = false;

        foreach (var binding in _bindings.Parameters)
        {
            // Don't write common default values
            if (binding.IsRequired ||
                binding.DefaultValue is null or false or 0 or 0u or 0L or 0D)
            {
                continue;
            }

            if (!commentWritten)
            {
                commentWritten = true;
                sb.Append(@"
                // Preassign compile time defaults for optional parameter(s) in case they don't get parsed
");
            }

            sb.Append("                state.");
            sb.Append(binding.Name);
            sb.Append(" = ");

            // Enum values are resolved as their underlying type so they need to be cast back to the enum type
            if (binding.Type.IsEnumOrNullableEnum())
            {
                sb.Append('(');
                sb.Append(binding.Type.ToDisplayString());
                sb.Append(')');
            }

            sb.Append(binding.DefaultValue.ToLiteral());
            sb.Append(@";
");
        }

        sb.Length--;

        return sb.ToString();
    }

    private string WriteSetters(TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        var sb = new StringBuilder(256);

        sb.Append('(');

        if (_bindings.Parameters.Length != 0)
        {
            foreach (var binding in _bindings.Parameters)
            {
                sb.Append(@"
                    ");
                sb.Append(binding.ParameterName);
                sb.Append(": ");

                if (binding.HasInModifier)
                {
                    sb.Append("in ");
                }

                sb.Append("state.");
                sb.Append(binding.Name);
                sb.Append("!,");
            }

            sb.Length--;
        }

        sb.Append(')');

        if (_bindings.RequiredMembers.Length != 0)
        {
            sb.Append(@"
                {
                ");

            foreach (var binding in _bindings.RequiredMembers)
            {
                sb.Append("    ");
                sb.Append(binding.Name);
                sb.Append(" = state.");
                sb.Append(binding.Name);
                sb.Append(@"!,
                ");
            }

            sb.Append('}');
        }

        sb.Append(@";
");

        foreach (var binding in _bindings.Members.OrderBy(b => b.IsRequired))
        {
            if (binding.IsRequired)
                continue; // already handled

            sb.Append("                if (");
            sb.Append(binding.ParserId);
            sb.Append(" != null) ");

            if (binding.Symbol.ContainingType.TypeKind == TypeKind.Interface &&
                !SymbolEqualityComparer.Default.Equals(binding.Symbol.ContainingType, typeMap.Type))
            {
                sb.Append("((");
                sb.Append(binding.Symbol.OriginalDefinition.ContainingType.ToDisplayString());
                sb.Append(")obj).");
            }
            else
            {
                sb.Append("obj.");
            }

            sb.Append(binding.Name);
            sb.Append(" = state.");
            sb.Append(binding.Name);
            sb.Append(@"!;
");
        }

        sb.Length--;
        return sb.ToString();
    }

    private string WriteRequiredCheck()
    {
        if (_bindings.RequiredBindings.Length == 0)
            return @"
            // No check for required members, the type has none.
";

        var sb = new StringBuilder(128);

        sb.Append(@"
            if (");

        for (int i = 0; i < _bindings.RequiredBindings.Length; i++)
        {
            if (i != 0)
            {
                sb.Append(@" ||
                ");
            }

            sb.Append("materializer.");
            sb.Append(_bindings.RequiredBindings[i].ParserId);
            sb.Append(" == null");
        }

        return sb.Append(@")
                ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers, exposeContent);
").ToString();
    }

    private string WriteMissingRequiredFields()
    {
        if (_bindings.RequiredBindings.Length == 0)
            return "";

        var sb = new StringBuilder(128);

        sb.Append(@"

        private static IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)
        {");

        foreach (var b in _bindings.RequiredBindings)
        {
            sb.Append($@"
            if (materializer.{b.ParserId} == null) yield return {b.Name.ToStringLiteral()};");
        }

        sb.Append(@"
        }");

        return sb.ToString();
    }

    private IEnumerable<string> WriteParserMembers(TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        foreach (var binding in _bindings.Members)
        {
            yield return $"public CsvConverter<{typeMap.Token}, {binding.Type.ToDisplayString()}>? {binding.ParserId};";
        }

        foreach (var binding in _bindings.Parameters)
        {
            yield return $"public CsvConverter<{typeMap.Token}, {binding.Type.ToDisplayString()}>? {binding.ParserId};";
        }
    }

    private IEnumerable<string> WriteParserHandlers(TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        foreach (var binding in _bindings.AllBindings)
        {
            yield return $@"private static readonly TryParseHandler {binding.HandlerId} = {typeMap.HandlerArgs} =>
        {{
            if (materializer.{binding.ParserId}!.TryParse(field, out var result))
            {{
                state.{binding.Name} = result;
                return true;
            }}
            return false;
        }};";
        }
    }

    private IEnumerable<string> WriteMatchers(TypeMapSymbol typeMap)
    {
        var allBindingsSorted = _bindings.AllBindings.ToArray();

        Array.Sort(allBindingsSorted, (b1, b2) =>
        {
            if (b1.Order != b2.Order)
            {
                return b2.Order.CompareTo(b1.Order);
            }
            else if ((b1 is ParameterBinding) != (b2 is ParameterBinding))
            {
                return (b2 is ParameterBinding).CompareTo(b1 is ParameterBinding);
            }
            else
            {
                return b2.IsRequired.CompareTo(b1.IsRequired);
            }
        });

        foreach (var binding in allBindingsSorted)
        {
            typeMap.ThrowIfCancellationRequested();

            string skipDuplicate = "";
            string checkDuplicate = "";

            if (!typeMap.ThrowOnDuplicate)
            {
                skipDuplicate = $@"materializer.{binding.ParserId} == null &&
                    ";
            }
            else
            {
                checkDuplicate = $@"
                    if (materializer.{binding.ParserId} != null) ThrowDuplicate({binding.Name.ToStringLiteral()}, name, headers, exposeContent);
";
            }

            var names = string.Join(@" ||
                    ", binding.Names.Select(n => $"options.Comparer.Equals(name, {n.ToStringLiteral()})"));

            yield return $@"                if ({skipDuplicate}{names}) {(binding.Order == 0 ? "// default order" : $"// order: {binding.Order}")}
                {{{checkDuplicate}
                    materializer.{binding.ParserId} = {ResolveParser(binding.Symbol, binding.Type)};
                    materializer.Handlers[index] = {binding.HandlerId};
                    anyFieldBound = true;
                    continue;
                }}
";
        }

        string ResolveParser(ISymbol propertyOrField, ITypeSymbol type)
        {
            foreach (var attributeData in propertyOrField.GetAttributes())
            {
                if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                    SymbolEqualityComparer.Default.Equals(typeMap.TokenSymbol, attribute.TypeArguments[0]) &&
                    SymbolEqualityComparer.Default.Equals(attribute.ConstructUnboundGenericType(), _symbols.CsvConverterOfTAttribute))
                {
                    return GetParserInitializer(typeMap.TokenSymbol, type, attribute.TypeArguments[1]);
                }
            }

            type = type.UnwrapNullable(out bool isNullable);

            string typeName = type.ToDisplayString();

            string converter = TryGetEnumConverter(type, out var converterInit)
                ? converterInit!
                : $"options.GetConverter<{typeName}>()";

            if (!isNullable)
                return converter;

            return $"new NullableConverter<{typeMap.TokenSymbol}, {typeName}>(" +
                $"{converter}, options.GetNullToken(typeof({typeName})))";
        }

        bool TryGetEnumConverter(ITypeSymbol type, out string? converterInit)
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                converterInit = typeMap.TokenSymbol.SpecialType switch
                {
                    SpecialType.System_Char => "EnumTextConverter",
                    SpecialType.System_Byte => "EnumByteConverter",
                    _ => null,
                };

                if (converterInit != null)
                {
                    converterInit = $"new {converterInit}<{type.ToDisplayString()}>(options)";
                    return true;
                }
            }

            converterInit = null;
            return false;
        }
    }
}
