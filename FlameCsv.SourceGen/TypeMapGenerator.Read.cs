using FlameCsv.SourceGen.Bindings;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void GetReadCode(
        StringBuilder sb,
        ref readonly TypeMapSymbol typeMap)
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

        public override FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@"> BindMembers(ReadOnlySpan<string> headers, FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append(@"> options)
        {
            TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);
            bool anyFieldBound = false;

            System.Collections.Generic.IEqualityComparer<string> comparer = options.Comparer;

            for (int index = 0; index < headers.Length; index++)
            {
                string name = headers[index];
");
        WriteMatchers(sb, in typeMap);

        sb.Append(@"
                ");

        if (typeMap.IgnoreUnmatched)
        {
            sb.Append(@"// ignoring unmatched header
                materializer.Handlers[index] = static ");
            sb.Append(typeMap.ParseHandlerArgs);
            sb.Append(" => true;");
        }
        else
        {
            sb.Append("base.ThrowUnmatched(name, index, options.AllowContentInExceptions);");
        }

        sb.Append(@"
            }

            if (!anyFieldBound)
            {
                base.ThrowNoFieldsBound(headers, options.AllowContentInExceptions);
            }
");

        WriteRequiredCheck(in typeMap, sb);

        sb.Append(@"
            return materializer;
        }

        public override FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@"> BindMembers(FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append("""
> options)
        {
            
""");
        WriteIndexBinding(sb, in typeMap);
        sb.Append(@"
        }");

        WriteMissingRequiredFields(in typeMap, sb);

        sb.Append(@"

        private struct ParseState
        {
");

        foreach (var binding in typeMap.Bindings.AllBindings)
        {
            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            sb.Append("            public ");
            sb.Append(binding.Type.ToDisplayString());
            sb.Append(' ');
            sb.Append(binding.Name);
            sb.Append(@";
");
        }

        sb.Append(@"        }

        private sealed class TypeMapMaterializer : FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@">
        {");

        foreach (var binding in typeMap.Bindings.AllBindings)
        {
            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            WriteParserMember(sb, in typeMap, binding);
        }

        sb.Append(@"

            public readonly TryParseHandler[] Handlers;

            public TypeMapMaterializer(int length)
            {
                Handlers = new TryParseHandler[length];
            }

            public int FieldCount => Handlers.Length;

            public ");
        sb.Append(typeMap.ResultName);
        sb.Append(" Parse<TReader>(ref TReader reader) where TReader : FlameCsv.Reading.ICsvFieldReader<");
        sb.Append(typeMap.Token);
        sb.Append(@">, allows ref struct
            {
                ParseState state = default;");
        WriteDefaultParameterValues(sb, in typeMap);
        sb.Append(@"

                int index = 0;

                while (reader.MoveNext())
                {
                    ReadOnlySpan<");
        sb.Append(typeMap.Token);
        sb.Append(@"> field = reader.Current;

                    if (!Handlers[index++](this, ref state, field))
                    {
                        FlameCsv.Exceptions.CsvParseException.Throw(reader.Options, field);
                    }
                }

                if (index < FieldCount)
                {
                    FlameCsv.Exceptions.CsvReadException.ThrowForPrematureEOF(FieldCount, reader.Options, reader.Record);
                }

                reader.Dispose();

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

    private void WriteDefaultParameterValues(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        bool commentWritten = false;

        foreach (var binding in typeMap.Bindings.Parameters)
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

    private void WriteSetters(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        sb.Append('(');

        if (typeMap.Bindings.Parameters.Length != 0)
        {
            foreach (var binding in typeMap.Bindings.Parameters)
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
                if (typeMap.Symbols.Nullable)
                    sb.Append('!');
                sb.Append(",");
            }

            sb.Length--;
        }

        sb.Append(')');

        if (typeMap.Bindings.RequiredMembers.Length != 0)
        {
            sb.Append(@"
                {
                ");

            foreach (var binding in typeMap.Bindings.RequiredMembers)
            {
                sb.Append("    ");
                sb.Append(binding.Name);
                sb.Append(" = state.");
                sb.Append(binding.Name);
                if (typeMap.Symbols.Nullable)
                    sb.Append('!');
                sb.Append(@",
                ");
            }

            sb.Append('}');
        }

        sb.Append(@";
");

        foreach (var binding in typeMap.Bindings.Members)
        {
            if (binding.IsRequired)
                continue; // already handled

            if (!binding.CanRead)
                continue;

            sb.Append("                if (");
            binding.WriteConverterId(sb);
            sb.Append(" is not null) ");

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
            if (typeMap.Symbols.Nullable)
                sb.Append('!');
            sb.Append(@";
");
        }

        sb.Length--;
    }

    private void WriteRequiredCheck(
        ref readonly TypeMapSymbol typeMap,
        StringBuilder sb)
    {
        if (typeMap.Bindings.RequiredBindings.Length == 0)
        {
            sb.Append(@"
            // No check for required members, the type has none.
");
            return;
        }

        sb.Append(@"
            if (");

        for (int i = 0; i < typeMap.Bindings.RequiredBindings.Length; i++)
        {
            if (i != 0)
            {
                sb.Append(@" ||
                ");
            }

            sb.Append("materializer.");
            typeMap.Bindings.RequiredBindings[i].WriteConverterId(sb);
            sb.Append(" is null");
        }

        sb.Append(@")
                base.ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers, options.AllowContentInExceptions);
");
    }

    private void WriteMissingRequiredFields(ref readonly TypeMapSymbol typeMap, StringBuilder sb)
    {
        if (typeMap.Bindings.RequiredBindings.Length == 0)
            return;

        sb.Append(@"

        private static System.Collections.Generic.IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)
        {");

        foreach (var b in typeMap.Bindings.RequiredBindings)
        {
            sb.Append(@"
            if (materializer.");
            b.WriteConverterId(sb);
            sb.Append(" is null) yield return ");
            sb.Append(GetName(b));
            sb.Append(';');
        }

        sb.Append(@"
        }");

        static string GetName(IBinding binding)
        {
            string name = binding is ParameterBinding
                ? binding.Name.Substring(3)
                : binding.Name;
            return name.ToStringLiteral();
        }
    }

    private void WriteParserMember(StringBuilder sb, ref readonly TypeMapSymbol typeMap, IBinding binding)
    {
        typeMap.ThrowIfCancellationRequested();

        if (!binding.CanRead)
            return;

        sb.Append(@"
            public FlameCsv.CsvConverter<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(binding.Type.ToDisplayString());
        sb.Append(typeMap.Symbols.Nullable ? ">? " : "> ");
        binding.WriteConverterId(sb);
        sb.Append(';');
    }

    private void WriteParserHandlers(
        StringBuilder sb,
        ref readonly TypeMapSymbol typeMap)
    {
        typeMap.ThrowIfCancellationRequested();

        bool first = true;

        foreach (var binding in typeMap.Bindings.AllBindings)
        {
            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            if (!first)
            {
                sb.Append(@"
        ");
            }
            else
            {
                if (typeMap.Symbols.Nullable)
                {
                    sb.Append(@"#nullable disable
        ");
                }
            }

            sb.Append("private static readonly TryParseHandler ");
            binding.WriteHandlerId(sb);
            sb.Append(" = ");
            sb.Append(typeMap.ParseHandlerArgs);
            sb.Append(" => materializer.");
            binding.WriteConverterId(sb);
            sb.Append(".TryParse(field, out state.");
            sb.Append(binding.Name);
            sb.Append(");");

            first = false;
        }

        if (typeMap.Symbols.Nullable)
        {
            sb.Append(@"
        #nullable enable");
        }
    }

    private void WriteMatchers(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        var allBindingsSorted = typeMap.Bindings.AllBindings.ToArray();

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

        var converterFactorySymbol = typeMap.Symbols.CsvConverterFactory.ConstructedFrom.Construct(typeMap.TokenSymbol);

        HashSet<string> writtenNames = [];

        foreach (var binding in allBindingsSorted)
        {
            typeMap.ThrowIfCancellationRequested();

            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            sb.Append(@"
                if (");

            if (!typeMap.ThrowOnDuplicate)
            {
                // add check to ignore already handled members
                sb.Append("materializer.");
                binding.WriteConverterId(sb);
                sb.Append(@" is null &&
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
                if (SymbolEqualityComparer.Default.Equals(typeMap.Symbols.CsvHeaderTargetAttribute, attribute.AttributeClass)
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
                    if (materializer.");
                binding.WriteConverterId(sb);
                sb.Append(" is not null) base.ThrowDuplicate(");
                sb.Append(binding.Name.ToStringLiteral());
                sb.Append(@", name, headers, options.AllowContentInExceptions);
");
            }

            sb.Append(@"
                    materializer.");
            binding.WriteConverterId(sb);
            sb.Append(" = ");
            ResolveConverter(sb, in typeMap, binding.Symbol, binding.Type, converterFactorySymbol);
            sb.Append(@";
                    materializer.Handlers[index] = ");
            binding.WriteHandlerId(sb);
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

                sb.Append("comparer.Equals(name, ");
                sb.Append(name.ToStringLiteral());
                sb.Append(')');
            }
        }
    }

    private void WriteIndexBinding(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        List<IndexBinding>? bindings;
        string? error;

        if (typeMap.Bindings.Parameters.Length == 0)
        {
            bindings = null;
            error = "No suitable constructor found";
        }
        else
        {
            (bindings, error) = IndexAttributeBinder.TryGetIndexBindings(typeMap.Type, in typeMap.Symbols, false);
        }

        if (bindings is null || error is not null)
        {
            sb.Append("throw new System.NotSupportedException(GetType().FullName + \" does not support index binding: \" + ");
            sb.Append(error.ToStringLiteral());
            sb.Append(");");
            return;
        }

        sb.Append("TypeMapMaterializer materializer = new TypeMapMaterializer(");
        sb.Append(bindings.Count);
        sb.Append(@");
");

        foreach (var binding in bindings)
        {
            sb.Append("            materializer.Handlers[");
            sb.Append(binding.Index);

            if (binding.Symbol is not null)
            {
                sb.Append("] = @s__Handler_");
                sb.Append(binding.Symbol.Name);
                sb.Append(@";
");
            }
            else
            {
                sb.Append(@"] = static (TypeMapMaterializer materializer, ref ParseState state, ReadOnlySpan<char> field) => true;
");
            }
        }
    }
}
