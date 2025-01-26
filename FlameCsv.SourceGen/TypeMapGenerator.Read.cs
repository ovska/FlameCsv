using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void GetReadCode(
        StringBuilder sb,
        FlameSymbols symbols,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        if (typeMap.Scope == CsvBindingScope.Write)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        sb.Append("        protected override FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append("> BindForReading(ReadOnlySpan<string> headers, FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @"> options)
        {
            TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);

            System.Collections.Generic.IEqualityComparer<string> comparer = options.Comparer;

            for (int index = 0; index < headers.Length; index++)
            {
                string name = headers[index];
");
        WriteMatchers(sb, symbols, typeMap, cancellationToken);

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

            if (!global::System.MemoryExtensions.ContainsAnyInRange(materializer.Targets, @s__MinIndex, @s__MaxIndex))
            {
                base.ThrowNoFieldsBound(headers);
            }
");

        WriteRequiredCheck(typeMap, sb);

        sb.Append(
            @"
            return materializer;
        }

        protected override FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(@"> BindForReading(FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            """
            > options)
                    {
                        throw new System.NotSupportedException();
                    }
            """);

        WriteMissingRequiredFields(typeMap, sb);

        sb.Append(
            @"

        private struct ParseState
        {
");

        foreach (var member in typeMap.PropertiesAndParameters)
        {
            if (!member.CanRead || member.Scope == CsvBindingScope.Write)
                continue;

            sb.Append("            public ");
            sb.Append(member.Type.FullyQualifiedName);
            sb.Append(' ');
            sb.Append(member.Name);
            sb.Append(
                @";
");
        }

        sb.Append(
            @"        }

        private sealed class TypeMapMaterializer : FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(
            @">
        {");

        foreach (var member in typeMap.PropertiesAndParameters)
        {
            if (!member.CanRead || member.Scope == CsvBindingScope.Write)
                continue;

            cancellationToken.ThrowIfCancellationRequested();

            sb.Append(
                @"
            public FlameCsv.CsvConverter<");
            sb.Append(typeMap.Token.Name);
            sb.Append(", ");
            sb.Append(member.Type.FullyQualifiedName);
            sb.Append(symbols.NullableContext ? ">? " : "> ");
            sb.Append(member.ConverterPrefix);
            sb.Append(member.Name);
            sb.Append(';');
        }

        sb.Append(
            @"

            public readonly int[] Targets;

            public TypeMapMaterializer(int length)
            {
                Targets = new int[length];
            }

            public ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(" Parse<TReader>(ref TReader reader) where TReader : FlameCsv.Reading.ICsvRecordFields<");
        sb.Append(typeMap.Token.Name);
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
        WriteDefaultParameterValues(sb, typeMap, cancellationToken);
        sb.Append(
            @"

                for (int index = 0; index < targets.Length; index++)
                {
                    ReadOnlySpan<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @"> @field = reader[index];

                    bool result = targets[index] switch
                    {
");

        foreach (var member in typeMap.PropertiesAndParameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!member.CanRead || member.Scope == CsvBindingScope.Write)
                continue;

            sb.Append("                        ");
            sb.Append(member.IndexPrefix);
            sb.Append(member.Name);
            sb.Append(" => ");
            sb.Append(member.ConverterPrefix);
            sb.Append(member.Name);
            if (symbols.NullableContext) sb.Append('!');
            sb.Append(".TryParse(@field, out state.");
            sb.Append(member.Name);
            if (symbols.NullableContext) sb.Append('!');
            sb.Append(
                @"),
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
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(" obj = new ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        WriteSetters(sb, symbols, typeMap, cancellationToken);
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
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @"> @field, int target)
            {
");

        foreach (var member in typeMap.PropertiesAndParameters)
        {
            if (!member.CanRead || member.Scope == CsvBindingScope.Write)
                continue;

            sb.Append("                if (target == ");
            sb.Append(member.IndexPrefix);
            sb.Append(member.Name);
            sb.Append(") FlameCsv.Exceptions.CsvParseException.Throw(@field, ");
            sb.Append(member.ConverterPrefix);
            sb.Append(member.Name);
            sb.Append("!, ");
            sb.Append(member.Name.ToStringLiteral());
            sb.Append(
                @");
");
        }

        sb.Append(
            @"                throw new System.Diagnostics.UnreachableException(""Invalid target: "" + target.ToString());
            }
        }

        ");
    }

    private static void WriteDefaultParameterValues(
        StringBuilder sb,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (typeMap.Parameters is null) return;

        bool commentWritten = false;

        foreach (var parameter in typeMap.Parameters)
        {
            // check if parameter can be omitted at all
            if (!parameter.HasDefaultValue ||
                parameter.IsRequiredByAttribute ||
                // Don't write common default values
                parameter.DefaultValue is null or false or 0 or 0u or 0L or 0D)
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
            sb.Append(parameter.Name);
            sb.Append(" = ");

            // Enum values are resolved as their underlying type, so they need to be cast back to the enum type
            // e.g. DayOfWeek.Friday would be "state.arg = (System.DayOfWeek)5;"
            if (parameter.ParameterType is { IsEnum: true } or { UnderlyingNullableType.IsEnum: true })
            {
                sb.Append('(');
                sb.Append(parameter.ParameterType.FullyQualifiedName);
                sb.Append(')');
            }

            sb.Append(parameter.DefaultValue.ToLiteral());
            sb.Append(';');
        }
    }

    private static void WriteSetters(
        StringBuilder sb,
        FlameSymbols symbols,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        sb.Append('(');

        if (typeMap.Parameters is { Count: > 0 } parameters)
        {
            foreach (var parameter in parameters)
            {
                sb.Append(
                    @"
                    ");
                sb.Append(parameter.Name);
                sb.Append(": ");

                if (parameter.RefKind is RefKind.In or RefKind.RefReadOnlyParameter)
                {
                    sb.Append("in ");
                }

                sb.Append("state.");
                sb.Append(parameter.Name);
                if (symbols.NullableContext)
                    sb.Append('!');
                sb.Append(",");
            }

            sb.Length--;
        }

        sb.Append(')');

        if (typeMap.Properties.Exists(static p => p.IsRequired))
        {
            sb.Append(
                @"
                {
                ");

            foreach (var property in typeMap.Properties)
            {
                if (!property.IsRequired) continue;

                sb.Append("    ");
                sb.Append(property.Name);
                sb.Append(" = state.");
                sb.Append(property.Name);
                if (symbols.NullableContext)
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

        foreach (var property in typeMap.Properties)
        {
            if (property.IsRequired)
                continue; // already handled

            if (!property.CanRead)
                continue;

            sb.Append("                if (");
            sb.Append(property.ConverterPrefix);
            sb.Append(property.Name);
            sb.Append(" is not null) ");

            if (property.ExplicitInterfaceOriginalDefinition is { } iface)
            {
                sb.Append("((");
                sb.Append(iface.FullyQualifiedName);
                sb.Append(")obj).");
            }
            else
            {
                sb.Append("obj.");
            }

            sb.Append(property.Name);
            sb.Append(" = state.");
            sb.Append(property.Name);
            if (symbols.NullableContext)
                sb.Append('!');
            sb.Append(
                @";
");
        }

        sb.Length--;
    }

    private static void WriteRequiredCheck(
        TypeMapModel typeMap,
        StringBuilder sb)
    {
        if (!typeMap.HasRequiredMembers)
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

        bool first = true;

        foreach (var member in typeMap.PropertiesAndParameters)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(
                    @" ||
                ");
            }

            sb.Append("materializer.");
            sb.Append(member.ConverterPrefix);
            sb.Append(member.Name);
            sb.Append(" is null");
        }

        sb.Append(
            @")
                base.ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers);
");
    }

    private static void WriteMissingRequiredFields(TypeMapModel typeMap, StringBuilder sb)
    {
        if (!typeMap.HasRequiredMembers)
            return;

        sb.Append(
            @"

        private static System.Collections.Generic.IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)
        {");

        foreach (var member in typeMap.GetSortedReadableMembers())
        {
            sb.Append(
                @"
            if (materializer.");
            sb.Append(member.ConverterPrefix);
            sb.Append(member.Name);
            sb.Append(" is null) yield return ");
            sb.Append(member.Name.ToStringLiteral());
            sb.Append(';');
        }

        sb.Append(
            @"
        }");
    }


    private static void WriteMatchers(
        StringBuilder sb,
        FlameSymbols symbols,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        HashSet<string> writtenNames = [];

        foreach (var member in typeMap.GetSortedReadableMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            sb.Append(
                @"
                if (");

            if (!typeMap.ThrowOnDuplicate)
            {
                // add check to ignore already handled members
                sb.Append("materializer.");
                sb.Append(member.ConverterPrefix);
                sb.Append(member.Name);
                sb.Append(
                    @" is null &&
                    ");
            }

            bool firstName = true;

            foreach (string name in member.Names)
            {
                if (writtenNames.Add(name))
                    WriteComparison(name);
            }

            if (member is PropertyModel)
            {
                foreach (var attribute in typeMap.TargetAttributes)
                {
                    if (StringComparer.Ordinal.Equals(attribute.MemberName, member.Name))
                    {
                        foreach (var name in attribute.Names)
                        {
                            if (writtenNames.Add(name)) WriteComparison(name);
                        }
                    }
                }
            }

            writtenNames.Clear();

            sb.Append(")");

            if (member.Order != 0)
            {
                sb.Append(" // order: ");
                sb.Append(member.Order);
            }

            sb.Append(
                @"
                {");

            if (typeMap.ThrowOnDuplicate)
            {
                sb.Append(
                    @"
                    if (materializer.");
                sb.Append(member.ConverterPrefix);
                sb.Append(member.Name);
                sb.Append(" is not null) base.ThrowDuplicate(");
                sb.Append(member.Name.ToStringLiteral());
                sb.Append(
                    @", name, headers);
");
            }

            sb.Append(
                @"
                    materializer.");
            sb.Append(member.ConverterPrefix);
            sb.Append(member.Name);
            sb.Append(" = ");
            WriteConverter(sb, symbols, member);
            sb.Append(
                @";
                    materializer.Targets[index] = ");
            sb.Append(member.IndexPrefix);
            sb.Append(member.Name);
            sb.Append(
                @";
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
}
