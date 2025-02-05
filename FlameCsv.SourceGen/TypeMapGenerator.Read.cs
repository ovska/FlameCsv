using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void GetReadCode(
        StringBuilder sb,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        sb.Append("        protected override global::FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append("> BindForReading(scoped global::System.ReadOnlySpan<string> headers, global::FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @"> options)
        {
            TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);

            global::System.Collections.Generic.IEqualityComparer<string> comparer = options.Comparer;

            for (int index = 0; index < headers.Length; index++)
            {
                string name = headers[index];
");
        WriteMatchers(sb, typeMap, cancellationToken);

        sb.Append(
            @"
                ");

        if (typeMap.IgnoreUnmatched)
        {
            sb.Append(
                @"// Unmatched fields are ignored
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

        protected override global::FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(@"> BindForReading(global::FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            """
            > options)
                    {
                        throw new global::System.NotSupportedException("Index binding is not yet supported for the source generator.");
                    }
            """);

        cancellationToken.ThrowIfCancellationRequested();

        WriteMissingRequiredFields(typeMap, sb);

        sb.Append(
            @"

        private struct ParseState
        {
");

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;
            cancellationToken.ThrowIfCancellationRequested();

            sb.Append("            public ");
            sb.Append(member.Type.FullyQualifiedName);
            sb.Append(' ');
            sb.Append(member.Identifier);
            sb.Append(
                @";
");
        }

        sb.Append(
            @"        }

        private sealed class TypeMapMaterializer : global::FlameCsv.Reading.IMaterializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(
            @">
        {");

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;
            cancellationToken.ThrowIfCancellationRequested();

            sb.Append(
                @"
            public global::FlameCsv.CsvConverter<");
            sb.Append(typeMap.Token.Name);
            sb.Append(", ");
            sb.Append(member.Type.FullyQualifiedName);
            sb.Append("> ");
            member.WriteConverterName(sb);
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
        sb.Append(" Parse<TReader>(ref TReader reader) where TReader : global::FlameCsv.Reading.ICsvRecordFields<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @">, allows ref struct
            {
                int[] targets = Targets;

                if (targets.Length != reader.FieldCount)
                {
                    global::FlameCsv.Exceptions.CsvReadException.ThrowForInvalidFieldCount(expected: targets.Length, actual: reader.FieldCount);
                }

#if RELEASE
                global::System.Runtime.CompilerServices.Unsafe.SkipInit(out ParseState state);
#else
                ParseState state = default;
#endif");
        WriteDefaultParameterValues(sb, typeMap, cancellationToken, out bool hasOptionalParameters);
        sb.Append(
            @"

                for (int target = 0; target < targets.Length; target++)
                {
                    global::System.ReadOnlySpan<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @"> @field = reader[target];

                    bool result = targets[target] switch
                    {
");

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;

            cancellationToken.ThrowIfCancellationRequested();

            sb.Append("                        ");
            member.WriteId(sb);
            sb.Append(" => ");
            member.WriteConverterName(sb);
            sb.Append(".TryParse(@field, out state.");
            sb.Append(member.Identifier);
            sb.Append(
                @"),
");
        }

        sb.Append(
            @"                        0 => ThrowForInvalidTarget(target), // Should never happen
                        _ => true, // Ignored fields have target set to -1
                    };

                    if (!result)
                    {
                        ThrowForFailedParse(@field, target);
                    }
                }

                // Required fields are guaranteed to be non-null.
                // Optional fields are null-checked to only write a value when one was read.
                ");

        if (hasOptionalParameters)
        {
            sb.Append(@"// Optional parameters are always passed, their default value is used when not read (see above)
                ");
        }

        sb.Append((typeMap.Proxy ?? typeMap.Type).FullyQualifiedName);
        sb.Append(" obj = new ");
        sb.Append((typeMap.Proxy ?? typeMap.Type).FullyQualifiedName);
        WriteSetters(sb, typeMap, cancellationToken);
        sb.Append(
            @"
                return obj;
            }

            [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private static bool ThrowForInvalidTarget(int target) => throw new global::System.Diagnostics.UnreachableException($""Converter {target} was uninitialized"");

            [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private void ThrowForFailedParse(scoped global::System.ReadOnlySpan<");
        sb.Append(typeMap.Token.Name);
        sb.Append(
            @"> @field, int target)
            {
");

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;
            cancellationToken.ThrowIfCancellationRequested();

            sb.Append("                if (target == ");
            member.WriteId(sb);
            sb.Append(") global::FlameCsv.Exceptions.CsvParseException.Throw(@field, ");
            member.WriteConverterName(sb);
            sb.Append("!, ");
            sb.Append(member.Name.ToStringLiteral());
            sb.Append(
                @");
");
        }

        sb.Append(
            @"                throw new global::System.Diagnostics.UnreachableException(""Invalid target: "" + target.ToString());
            }
        }

        ");
    }

    private static void WriteDefaultParameterValues(
        StringBuilder sb,
        TypeMapModel typeMap,
        CancellationToken cancellationToken,
        out bool hasOptionalParameters)
    {
        cancellationToken.ThrowIfCancellationRequested();

        hasOptionalParameters = false;
        if (typeMap.Parameters.IsEmpty) return;

        foreach (var parameter in typeMap.Parameters)
        {
            // check if parameter can be omitted at all
            if (!parameter.HasDefaultValue ||
                parameter.IsRequiredByAttribute)
            {
                continue;
            }

            if (!hasOptionalParameters)
            {
                hasOptionalParameters = true;
                sb.Append(
                    @"

                // Default values of optional parameters (have a default value and are not required by attribute");
            }

            sb.Append(
                @"
                state.");
            sb.Append(parameter.Identifier);
            sb.Append(" = ");

            // Enum values are resolved as their underlying type, so they need to be cast back to the enum type
            // e.g. DayOfWeek.Friday would be "state.arg = (System.DayOfWeek)5;"
            if (parameter.ParameterType.IsEnumOrNullableEnum)
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
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        sb.Append('(');

        if (!typeMap.Parameters.IsEmpty)
        {
            foreach (var parameter in typeMap.Parameters)
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
                sb.Append(parameter.Identifier);
                sb.Append(",");
            }

            sb.Length--;
        }

        sb.Append(')');

        if (typeMap.Properties.AsImmutableArray().Any(static p => p.IsRequired))
        {
            sb.Append(
                @"
                {
                ");

            foreach (var property in typeMap.Properties)
            {
                if (!property.IsRequired) continue;

                sb.Append("    ");
                sb.Append(property.Identifier);
                sb.Append(" = state.");
                sb.Append(property.Identifier);
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
            property.WriteConverterName(sb);
            sb.Append($" is not null) ");

            if (!string.IsNullOrEmpty(property.ExplicitInterfaceOriginalDefinitionName))
            {
                sb.Append("((");
                sb.Append(property.ExplicitInterfaceOriginalDefinitionName);
                sb.Append(")obj).");
            }
            else
            {
                sb.Append("obj.");
            }

            sb.Append(property.Name);
            sb.Append(" = state.");
            sb.Append(property.Identifier);
            sb.Append(
                @";
");
        }

        sb.Length--;
    }

    private static void WriteRequiredCheck(TypeMapModel typeMap, StringBuilder sb)
    {
        if (!typeMap.HasRequiredMembers)
        {
            sb.Append(
                @"
            // No required fields
");
            return;
        }

        sb.Append(
            @"
            if (");

        bool first = true;

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead || !member.IsRequired) continue;

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
            member.WriteConverterName(sb);
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

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;

            sb.Append(
                @"
            if (materializer.");
            member.WriteConverterName(sb);
            sb.Append(" is null) yield return ");
            sb.Append(member.Identifier.ToStringLiteral());
            sb.Append(';');
        }

        sb.Append(
            @"
        }");
    }


    private static void WriteMatchers(
        StringBuilder sb,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        if (!typeMap.IgnoredHeaders.IsEmpty)
        {
            sb.Append(
                @"
                // Ignored headers
                if (comparer.Equals(name, ");
            sb.Append(typeMap.IgnoredHeaders[0].ToStringLiteral());

            for (int i = 1; i < typeMap.IgnoredHeaders.Length; i++)
            {
                sb.Append(
                    @") ||
                    comparer.Equals(name, ");
                sb.Append(typeMap.IgnoredHeaders[i].ToStringLiteral());
            }

            sb.Append(
                @"))
                {
                    materializer.Targets[index] = 0;
                    continue;
                }
");
        }


        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;

            cancellationToken.ThrowIfCancellationRequested();

            sb.Append(
                @"
                if (");

            if (!typeMap.ThrowOnDuplicate)
            {
                // add check to ignore already handled members
                sb.Append("materializer.");
                member.WriteConverterName(sb);
                sb.Append(
                    @" is null &&
                    ");
            }

            bool firstName = true;

            if (member.Names.IsEmpty)
            {
                WriteComparison(member.Name);
            }
            else
            {
                foreach (string name in member.Names)
                {
                    WriteComparison(name);
                }
            }

            sb.Append(")");

            if (member.Order != 0)
            {
                sb.Append(" // Explicit order: ");
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
                member.WriteConverterName(sb);
                sb.Append(" is not null) base.ThrowDuplicate(");
                sb.Append(member.Name.ToStringLiteral());
                sb.Append(
                    @", name, headers);
");
            }

            sb.Append(
                @"
                    materializer.");
            member.WriteConverterName(sb);
            sb.Append(" = ");
            WriteConverter(sb, member);
            sb.Append(
                @";
                    materializer.Targets[index] = ");
            member.WriteId(sb);
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
