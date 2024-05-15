using FlameCsv.SourceGen.Bindings;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void GetReadCode(
        StringBuilder sb,
        in TypeMapSymbol typeMap)
    {
        if (typeMap.Scope == BindingScope.Write)
            return;

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"        /// <summary>
        /// Callback for parsing a single field and writing the value to the object.
        /// </summary>
        private delegate bool TryParseHandler");
        sb.Append(typeMap.ParseHandlerArgs);
        sb.Append(@";

        public override IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@"> BindMembers(ReadOnlySpan<string> headers, CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append(@"> options)
        {
            TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);
            bool anyFieldBound = false;

            for (int index = 0; index < headers.Length; index++)
            {
                string name = headers[index];
");
        WriteMatchers(sb, in typeMap);

        sb.Append(@"
                ");

        if (typeMap.IgnoreUnmatched)
        {
            sb.Append("materializer.Handlers[index] = ");
            sb.Append(typeMap.ParseHandlerArgs);
            sb.Append(" => true; // ignored");
        }
        else
        {
            sb.Append("ThrowUnmatched(name, index, options);");
        }

        sb.Append(@"
            }

            if (!anyFieldBound)
                ThrowNoFieldsBound(headers, options);
");

        WriteRequiredCheck(sb);

        sb.Append(@"
            return materializer;
        }

        public override IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@"> BindMembers(CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append("""
> options)
        {
            throw new NotSupportedException($"{GetType().FullName} does not support index binding.");
        }
""");

        WriteMissingRequiredFields(sb);

        sb.Append(@"

        private struct ParseState
        {
");

        foreach (var binding in _bindings.AllBindings)
        {
            sb.Append("            public ");
            sb.Append(binding.Type.ToDisplayString());
            sb.Append(' ');
            sb.Append(binding.Name);
            sb.Append(@";
");
        }

        sb.Append(@"        }

        private sealed class TypeMapMaterializer : IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@">
        {");

        foreach (var binding in _bindings.AllBindings)
            WriteParserMember(sb, in typeMap, binding);

        sb.Append(@"

            public readonly TryParseHandler[] Handlers;

            public TypeMapMaterializer(int length)
            {
                Handlers = new TryParseHandler[length];
            }

            public int FieldCount => Handlers.Length;

            public ");
        sb.Append(typeMap.ResultName);
        sb.Append(" Parse(ref CsvFieldReader<");
        sb.Append(typeMap.Token);
        sb.Append(@"> reader)
            {
                ParseState state = default;");
        WriteDefaultParameterValues(sb, in typeMap);
        sb.Append(@"

                int index = 0;

                while (CsvFieldReader<");
        sb.Append(typeMap.Token);
        sb.Append(">.TryReadNext(ref reader, out ReadOnlySpan<");
        sb.Append(typeMap.Token);
        sb.Append(@"> field))
                {
                    if (Handlers[index++](this, ref state, field))
                    {
                        continue;
                    }

                    reader.ThrowParseFailed(field, null);
                }

                // Ensure there were no leftover fields
                reader.EnsureFullyConsumed(fieldCount: Handlers.Length);

                // Create the value from parsed values. Required members are validated when creating the materializer,
                // optional members are assigned only if parsed to not overwrite possible default values.
                ");
        sb.Append(typeMap.ResultName);
        sb.Append(" obj = new ");
        sb.Append(typeMap.ResultName);
        WriteSetters(sb, in typeMap);
        sb.Append(@"
                return obj;
            }
        }

        ");
        WriteParserHandlers(sb, in typeMap);
    }

    private void WriteDefaultParameterValues(StringBuilder sb, in TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

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

                // write compile-time defaults for optional parameter(s) in case they don't get parsed");
            }

            sb.Append(@"
                state.");
            sb.Append(binding.Name);
            sb.Append(" = ");

            // Enum values are resolved as their underlying type so they need to be cast back to the enum type
            // e.g. DayOfWeek.Friday would be "state.arg = (System.DayOfWeek)5;"
            if (binding.Type.IsEnumOrNullableEnum())
            {
                sb.Append('(');
                sb.Append(binding.Type.ToDisplayString());
                sb.Append(')');
            }

            sb.Append(binding.DefaultValue.ToLiteral());
            sb.Append(';');
        }
    }

    private void WriteSetters(StringBuilder sb, in TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

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
                if (_symbols.Nullable)
                    sb.Append('!');
                sb.Append(",");
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
                if (_symbols.Nullable)
                    sb.Append('!');
                sb.Append(@",
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

            sb.Append("                if (null != ");
            sb.Append(binding.ConverterId);
            sb.Append(") ");

            if (binding.IsExplicitInterfaceDefinition(typeMap.Type, out var ifaceSymbol))
            {
                sb.Append("((");
                sb.Append(ifaceSymbol.ToDisplayString());
                sb.Append(")obj).");
            }
            else
            {
                sb.Append("obj.");
            }

            sb.Append(binding.Name);
            sb.Append(" = state.");
            sb.Append(binding.Name);
            if (_symbols.Nullable)
                sb.Append('!');
            sb.Append(@";
");
        }

        sb.Length--;
    }

    private void WriteRequiredCheck(StringBuilder sb)
    {
        if (_bindings.RequiredBindings.Length == 0)
        {
            sb.Append(@"
            // No check for required members, the type has none.
");
            return;
        }

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
            sb.Append(_bindings.RequiredBindings[i].ConverterId);
            sb.Append(" == null");
        }

        sb.Append(@")
                ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers, options);
");
    }

    private void WriteMissingRequiredFields(StringBuilder sb)
    {
        if (_bindings.RequiredBindings.Length == 0)
            return;

        sb.Append(@"

        private static IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)
        {");

        foreach (var b in _bindings.RequiredBindings)
        {
            sb.Append($@"
            if (materializer.{b.ConverterId} == null) yield return {b.Name.ToStringLiteral()};");
        }

        sb.Append(@"
        }");
    }

    private void WriteParserMember(StringBuilder sb, in TypeMapSymbol typeMap, IBinding binding)
    {
        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"
            public CsvConverter<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(binding.Type.ToDisplayString());
        sb.Append(_symbols.Nullable ? ">? " : "> ");
        sb.Append(binding.ConverterId);
        sb.Append(';');
    }

    private void WriteParserHandlers(
        StringBuilder sb,
        in TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        bool first = true;

        foreach (var binding in _bindings.AllBindings)
        {
            if (!first)
            {
                sb.Append(@"
        ");
            }
            else
            {
                if (_symbols.Nullable)
                {
                    sb.Append(@"#nullable disable
        ");
                }
            }

            sb.Append("private static readonly TryParseHandler ");
            sb.Append(binding.HandlerId);
            sb.Append(" = ");
            sb.Append(typeMap.ParseHandlerArgs);
            sb.Append(" => materializer.");
            sb.Append(binding.ConverterId);
            sb.Append(".TryParse(field, out state.");
            sb.Append(binding.Name);
            sb.Append(");");

            first = false;
        }

        if (_symbols.Nullable)
        {
            sb.Append(@"
        #nullable enable");
        }
    }

    private void WriteMatchers(StringBuilder sb, in TypeMapSymbol typeMap)
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

        var converterFactorySymbol = _symbols.CsvConverterFactory.ConstructedFrom.Construct(typeMap.TokenSymbol);

        HashSet<string> writtenNames = [];

        foreach (var binding in allBindingsSorted)
        {
            typeMap.ThrowIfCancellationRequested();

            sb.Append(@"
                if (");

            if (!typeMap.ThrowOnDuplicate)
            {
                // add check to ignore already handled members
                sb.Append("null == materializer.");
                sb.Append(binding.ConverterId);
                sb.Append(@" &&
                    ");
            }

            bool firstName = true;

            foreach (string name in binding.Names)
            {
                if (writtenNames.Add(name))
                    WriteComparison(name);
            }

            foreach (var attribute in typeMap.Type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(_symbols.CsvHeaderTargetAttribute, attribute.AttributeClass)
                    && binding.Name.Equals(attribute.ConstructorArguments[0].Value as string))
                {
                    foreach (var value in attribute.ConstructorArguments[1].Values)
                    {
                        if (value.Value is string name && writtenNames.Add(name))
                            WriteComparison(name);
                    }
                }
            }

            writtenNames.Clear();

            sb.Append(") // ");

            if (binding.Order == 0)
            {
                sb.Append("default order");
            }
            else
            {
                sb.Append("order: ");
                sb.Append(binding.Order);
            }

            sb.Append(@"
                {");

            if (typeMap.ThrowOnDuplicate)
            {
                sb.Append(@"
                    if (null != materializer.");
                sb.Append(binding.ConverterId);
                sb.Append(") ThrowDuplicate(");
                sb.Append(binding.Name.ToStringLiteral());
                sb.Append(@", name, headers, options);
");
            }

            sb.Append(@"
                    materializer.");
            sb.Append(binding.ConverterId);
            sb.Append(" = ");
            ResolveConverter(sb, in typeMap, binding.Symbol, binding.Type, converterFactorySymbol);
            sb.Append(@";
                    materializer.Handlers[index] = ");
            sb.Append(binding.HandlerId);
            sb.Append(@";
                    anyFieldBound = true;
                    continue;
                }
");

            void WriteComparison(string name)
            {
                if (firstName)
                {
                    firstName = false;
                }
                else
                {
                    sb.Append(@" ||
                    ");
                }

                sb.Append("options.Comparer.Equals(name, ");
                sb.Append(name.ToStringLiteral());
                sb.Append(')');
            }
        }
    }
}
