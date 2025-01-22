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

        sb.Append("        public override FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append("> BindMembers(ReadOnlySpan<string> headers, FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append(
            @"> options)
        {
            TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);
            bool anyFieldBound = false;

            System.Collections.Generic.IEqualityComparer<string> comparer = options.Comparer;

            for (int index = 0; index < headers.Length; index++)
            {
                string name = headers[index];
");
        WriteMatchers(sb, in typeMap);

        sb.Append(
            @"
                ");

        if (typeMap.IgnoreUnmatched)
        {
            sb.Append(
                @"// ignoring unmatched header
                materializer.Targets[index] = -1;");
        }
        else
        {
            sb.Append("base.ThrowUnmatched(name, index);");
        }

        sb.Append(
            @"
            }

            if (!anyFieldBound)
            {
                base.ThrowNoFieldsBound(headers);
            }
");

        WriteRequiredCheck(in typeMap, sb);

        sb.Append(
            @"
            return materializer;
        }

        public override FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(@"> BindMembers(FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append(
            """
            > options)
                    {
                        
            """);
        WriteIndexBinding(sb, in typeMap);
        sb.Append(
            @"
        }");

        WriteMissingRequiredFields(in typeMap, sb);

        sb.Append(
            @"

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
            sb.Append(
                @";
");
        }

        sb.Append(
            @"        }

        private sealed class TypeMapMaterializer : FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.ResultName);
        sb.Append(
            @">
        {");

        foreach (var binding in typeMap.Bindings.AllBindings)
        {
            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            WriteParserMember(sb, in typeMap, binding);
        }

        sb.Append(
            @"

            public readonly int[] Targets;

            public TypeMapMaterializer(int length)
            {
                Targets = new int[length];
            }

            public ");
        sb.Append(typeMap.ResultName);
        sb.Append(" Parse<TReader>(ref TReader reader) where TReader : FlameCsv.Reading.ICsvRecordFields<");
        sb.Append(typeMap.Token);
        sb.Append(
            @">, allows ref struct
            {
                int[] targets = Targets;

                if (targets.Length != reader.FieldCount)
                {
                    FlameCsv.Exceptions.CsvReadException.ThrowForInvalidFieldCount(expected: targets.Length, actual: reader.FieldCount);
                }

#if DEBUG
                ParseState state = default;
#else
                System.Runtime.CompilerServices.Unsafe.SkipInit(out ParseState state); // uninitialized members are never accessed
#endif");
        WriteDefaultParameterValues(sb, in typeMap);
        sb.Append(
            @"

                for (int index = 0; index < targets.Length; index++)
                {
                    ReadOnlySpan<");
        sb.Append(typeMap.Token);
        sb.Append(
            @"> @field = reader[index];

                    bool result = targets[index] switch
                    {
");

        foreach (var binding in typeMap.Bindings.AllBindings)
        {
            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            sb.Append("                        ");
            sb.Append(binding.Index);
            sb.Append(" => ");
            binding.WriteConverterId(sb);
            if (typeMap.Symbols.Nullable) sb.Append('!');
            sb.Append(".TryParse(@field, out state.");
            sb.Append(binding.Name);
            if (typeMap.Symbols.Nullable) sb.Append('!');
            sb.Append(@"),
");
        }

        sb.Append(
            @"                        0 => ThrowForInvalidTarget(index),
                        _ => true, // ignored fields fall back to this
                    };

                    if (!result)
                    {
                        ThrowForFailedParse(@field, index);
                    }
                }

                // Create the value from parsed values. Required members are validated when creating the materializer,
                // optional members are assigned only if parsed to not overwrite possible default values.
                ");
        sb.Append(typeMap.ResultName);
        sb.Append(" obj = new ");
        sb.Append(typeMap.ResultName);
        WriteSetters(sb, in typeMap);
        sb.Append(
            @"
                return obj;
            }

            [System.Diagnostics.CodeAnalysis.DoesNotReturn]
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private static bool ThrowForInvalidTarget(int index) => throw new System.Diagnostics.UnreachableException($""Converter at index {index} was uninitialized"");

            [System.Diagnostics.CodeAnalysis.DoesNotReturn]
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private void ThrowForFailedParse(ReadOnlySpan<");
        sb.Append(typeMap.Token);
        sb.Append(@"> @field, int target)
            {
");

        foreach (var binding in typeMap.Bindings.AllBindings)
        {
            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            sb.Append("                if (target == ");
            sb.Append(binding.Index);
            sb.Append(") FlameCsv.Exceptions.CsvParseException.Throw(@field, ");
            binding.WriteConverterId(sb);
            sb.Append(", ");
            sb.Append(binding.Name.ToStringLiteral());
            sb.Append(@");
");
        }

        sb.Append(@"                throw new System.Diagnostics.UnreachableException(""Invalid target: "" + target.ToString());
            }
        }

        ");
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
                sb.Append(
                    @"

                // write compile-time defaults for optional parameter(s) in case they don't get parsed");
            }

            sb.Append(
                @"
                state.");
            sb.Append(binding.Name);
            sb.Append(" = ");

            // Enum values are resolved as their underlying type, so they need to be cast back to the enum type
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
                sb.Append(
                    @"
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
            sb.Append(
                @"
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
                sb.Append(
                    @",
                ");
            }

            sb.Append('}');
        }

        sb.Append(
            @";
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
            sb.Append(
                @";
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
            sb.Append(
                @"
            // No check for required members, the type has none.
");
            return;
        }

        sb.Append(
            @"
            if (");

        for (int i = 0; i < typeMap.Bindings.RequiredBindings.Length; i++)
        {
            if (i != 0)
            {
                sb.Append(
                    @" ||
                ");
            }

            sb.Append("materializer.");
            typeMap.Bindings.RequiredBindings[i].WriteConverterId(sb);
            sb.Append(" is null");
        }

        sb.Append(
            @")
                base.ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers);
");
    }

    private void WriteMissingRequiredFields(ref readonly TypeMapSymbol typeMap, StringBuilder sb)
    {
        if (typeMap.Bindings.RequiredBindings.Length == 0)
            return;

        sb.Append(
            @"

        private static System.Collections.Generic.IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)
        {");

        foreach (var b in typeMap.Bindings.RequiredBindings)
        {
            sb.Append(
                @"
            if (materializer.");
            b.WriteConverterId(sb);
            sb.Append(" is null) yield return ");
            sb.Append(GetName(b));
            sb.Append(';');
        }

        sb.Append(
            @"
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

        sb.Append(
            @"
            public FlameCsv.CsvConverter<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(binding.Type.ToDisplayString());
        sb.Append(typeMap.Symbols.Nullable ? ">? " : "> ");
        binding.WriteConverterId(sb);
        sb.Append(';');
    }

    private void WriteMatchers(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        var allBindingsSorted = typeMap.Bindings.AllBindings.ToArray();

        Array.Sort(
            allBindingsSorted,
            (b1, b2) =>
            {
                if (b1.Order != b2.Order)
                {
                    return b2.Order.CompareTo(b1.Order);
                }

                if ((b1 is ParameterBinding) != (b2 is ParameterBinding))
                {
                    return (b2 is ParameterBinding).CompareTo(b1 is ParameterBinding);
                }

                if (b1.IsRequired != b2.IsRequired)
                {
                    return b2.IsRequired.CompareTo(b1.IsRequired);
                }

                return String.Compare(b1.Name, b2.Name, StringComparison.Ordinal);
            });

        var converterFactorySymbol = typeMap.Symbols.CsvConverterFactory.ConstructedFrom.Construct(typeMap.TokenSymbol);

        HashSet<string> writtenNames = [];

        foreach (var binding in allBindingsSorted)
        {
            typeMap.ThrowIfCancellationRequested();

            if (!binding.CanRead || binding.Scope == BindingScope.Write)
                continue;

            sb.Append(
                @"
                if (");

            if (!typeMap.ThrowOnDuplicate)
            {
                // add check to ignore already handled members
                sb.Append("materializer.");
                binding.WriteConverterId(sb);
                sb.Append(
                    @" is null &&
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
                if (SymbolEqualityComparer.Default.Equals(
                        typeMap.Symbols.CsvHeaderTargetAttribute,
                        attribute.AttributeClass) &&
                    binding.Name.Equals(attribute.ConstructorArguments[0].Value as string))
                {
                    foreach (var value in attribute.ConstructorArguments[1].Values)
                    {
                        if (value.Value is string name && writtenNames.Add(name))
                            WriteComparison(name);
                    }
                }
            }

            writtenNames.Clear();

            sb.Append(")");

            if (binding.Order != 0)
            {
                sb.Append(" // order: ");
                sb.Append(binding.Order);
            }

            sb.Append(
                @"
                {");

            if (typeMap.ThrowOnDuplicate)
            {
                sb.Append(
                    @"
                    if (materializer.");
                binding.WriteConverterId(sb);
                sb.Append(" is not null) base.ThrowDuplicate(");
                sb.Append(binding.Name.ToStringLiteral());
                sb.Append(
                    @", name, headers);
");
            }

            sb.Append(
                @"
                    materializer.");
            binding.WriteConverterId(sb);
            sb.Append(" = ");
            ResolveConverter(sb, in typeMap, binding.Symbol, binding.Type, converterFactorySymbol);
            sb.Append(
                @";
                    materializer.Targets[index] = ");
            binding.WriteIndex(sb);
            sb.Append(
                @";
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
                    sb.Append(
                        @" ||
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
            sb.Append(
                "throw new System.NotSupportedException(GetType().FullName + \" does not support index binding: \" + ");
            sb.Append(error.ToStringLiteral());
            sb.Append(");");
            return;
        }

        sb.Append("TypeMapMaterializer materializer = new TypeMapMaterializer(");
        sb.Append(bindings.Count);
        sb.Append(
            @");
");

        foreach (var binding in bindings)
        {
            sb.Append("            materializer.Targets[");
            sb.Append(binding.Index);

            if (binding.Symbol is not null)
            {
                sb.Append("] = @s__Handler_");
                sb.Append(binding.Symbol.Name);
                sb.Append(
                    @";
");
            }
            else
            {
                sb.Append(
                    @"] = static (TypeMapMaterializer materializer, ref ParseState state, ReadOnlySpan<char> field) => true;
");
            }
        }
    }
}
