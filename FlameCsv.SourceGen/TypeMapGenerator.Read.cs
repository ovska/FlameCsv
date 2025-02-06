using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void GetReadCode(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.Write("protected override global::FlameCsv.Reading.IMaterializer<");
        writer.Write(typeMap.Token.FullyQualifiedName);
        writer.Write(", ");
        writer.Write(typeMap.Type.FullyQualifiedName);
        writer.Write(
            "> BindForReading(scoped global::System.ReadOnlySpan<string> headers, global::FlameCsv.CsvOptions<");
        writer.Write(typeMap.Token.FullyQualifiedName);
        writer.WriteLine("> options)");

        using (writer.WriteBlock())
        {
            writer.WriteLine("TypeMapMaterializer materializer = new TypeMapMaterializer(headers.Length);");
            writer.WriteLine();
            writer.WriteLine(
                "global::System.Collections.Generic.IEqualityComparer<string> comparer = options.Comparer;");
            writer.WriteLine();
            writer.WriteLine("for (int index = 0; index < headers.Length; index++)");

            using (writer.WriteBlock())
            {
                writer.WriteLine("string name = headers[index];");

                WriteMatchers(writer, typeMap, cancellationToken);
                writer.WriteLine();

                if (typeMap.IgnoreUnmatched)
                {
                    writer.WriteLine("// Unmatched fields are ignored");
                    writer.WriteLine("materializer.Targets[index] = -1;");
                }
                else
                {
                    writer.WriteLine("base.ThrowUnmatched(name, index);");
                }
            }

            writer.WriteLine();
            writer.WriteLine(
                "if (!global::System.MemoryExtensions.ContainsAnyInRange(materializer.Targets, @s__MinIndex, @s__MaxIndex))");

            using (writer.WriteBlock())
            {
                writer.WriteLine("base.ThrowNoFieldsBound(headers);");
            }

            WriteRequiredCheck(typeMap, writer);

            writer.WriteLine();
            writer.WriteLine("return materializer;");
        }

        writer.WriteLine();

        writer.WriteLine(
            $"protected override global::FlameCsv.Reading.IMaterializer<{typeMap.Token.FullyQualifiedName}, {typeMap.Type.FullyQualifiedName}> BindForReading(global::FlameCsv.CsvOptions<{typeMap.Token.FullyQualifiedName}> options)");

        using (writer.WriteBlock())
        {
            writer.WriteLine(
                "throw new global::System.NotSupportedException(\"Index binding is not yet supported for the source generator.\");");
        }

        cancellationToken.ThrowIfCancellationRequested();

        WriteMissingRequiredFields(typeMap, writer);

        writer.WriteLine();
        writer.WriteLine("private struct ParseState");
        using (writer.WriteBlock())
        {
            foreach (var member in typeMap.AllMembers)
            {
                if (!member.CanRead) continue;
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine($"public {member.Type.FullyQualifiedName} {member.Identifier};");
            }
        }

        writer.WriteLine();

        writer.WriteLine(
            $"private sealed class TypeMapMaterializer : global::FlameCsv.Reading.IMaterializer<{typeMap.Token.FullyQualifiedName}, {typeMap.Type.FullyQualifiedName}>");

        using (writer.WriteBlock())
        {
            foreach (var member in typeMap.AllMembers)
            {
                if (!member.CanRead) continue;
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write(
                    $"public global::FlameCsv.CsvConverter<{typeMap.Token.FullyQualifiedName}, {member.Type.FullyQualifiedName}> ");
                member.WriteConverterName(writer);
                writer.WriteLine(";");
            }


            writer.WriteLine();
            writer.WriteLine("public readonly int[] Targets;");
            writer.WriteLine();
            writer.WriteLine("public TypeMapMaterializer(int length)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("Targets = new int[length];");
            }

            writer.WriteLine();

            writer.WriteLine(
                $"public {typeMap.Type.FullyQualifiedName} Parse<TReader>(ref TReader reader) where TReader : global::FlameCsv.Reading.ICsvRecordFields<{typeMap.Token.FullyQualifiedName}>, allows ref struct");

            using (writer.WriteBlock())
            {
                writer.WriteLine("int[] targets = Targets;");
                writer.WriteLine();
                writer.WriteLine("if (targets.Length != reader.FieldCount)");

                using (writer.WriteBlock())
                {
                    writer.WriteLine(
                        "global::FlameCsv.Exceptions.CsvReadException.ThrowForInvalidFieldCount(expected: targets.Length, actual: reader.FieldCount);");
                }

                writer.WriteWithoutIndent(
                    """

                    #if RELEASE
                                global::System.Runtime.CompilerServices.Unsafe.SkipInit(out ParseState state);
                    #else
                                ParseState state = default;
                    #endif

                    """);


                WriteDefaultParameterValues(writer, typeMap, cancellationToken, out bool hasOptionalParameters);

                writer.WriteLine("for (int target = 0; target < targets.Length; target++)");
                using (writer.WriteBlock())
                {
                    writer.WriteLine(
                        $"global::System.ReadOnlySpan<{typeMap.Token.FullyQualifiedName}> @field = reader[target];");
                    writer.WriteLine();
                    writer.WriteLine("bool result = targets[target] switch");
                    writer.WriteLine("{");
                    writer.IncreaseIndent();

                    foreach (var member in typeMap.AllMembers)
                    {
                        if (!member.CanRead) continue;

                        cancellationToken.ThrowIfCancellationRequested();

                        member.WriteId(writer);
                        writer.Write(" => ");
                        member.WriteConverterName(writer);
                        writer.Write(".TryParse(@field, out state.");
                        writer.Write(member.Identifier);
                        writer.WriteLine("),");
                    }

                    writer.WriteLine("0 => ThrowForInvalidTarget(target), // Should never happen");
                    writer.WriteLine("_ => true, // Ignored fields have target set to -1");

                    writer.DecreaseIndent();
                    writer.WriteLine("};");

                    writer.WriteLine();

                    writer.WriteLine("if (!result)");
                    using (writer.WriteBlock())
                    {
                        writer.WriteLine("ThrowForFailedParse(@field, target);");
                    }
                }

                writer.WriteLine();
                writer.WriteLine("// Required fields are guaranteed to be non-null.");
                writer.WriteLine("// Optional fields are null-checked to only write a value when one was read.");

                writer.WriteLineIf(
                    hasOptionalParameters,
                    "// Optional parameters are always passed, their default value is used when not read (see above)");

                string typeToWrite = typeMap.Proxy?.FullyQualifiedName ?? typeMap.Type.FullyQualifiedName;

                writer.Write($"{typeToWrite} obj = new {typeToWrite}");

                WriteSetters(writer, typeMap, cancellationToken);

                writer.WriteLine("return obj;");
            }

            const string doesNotReturnAttr = "[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]";
            const string noInliningAttr
                = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";

            writer.WriteLine();
            writer.WriteLine(doesNotReturnAttr);
            writer.WriteLine(noInliningAttr);
            writer.WriteLine(
                "private static bool ThrowForInvalidTarget(int target) => throw new global::System.Diagnostics.UnreachableException($\"Converter {target} was uninitialized\");");
            writer.WriteLine();
            writer.WriteLine(doesNotReturnAttr);
            writer.WriteLine(noInliningAttr);
            writer.WriteLine(
                $"private void ThrowForFailedParse(scoped global::System.ReadOnlySpan<{typeMap.Token.FullyQualifiedName}> field, int target)");

            using (writer.WriteBlock())
            {
                foreach (var member in typeMap.AllMembers)
                {
                    if (!member.CanRead) continue;
                    cancellationToken.ThrowIfCancellationRequested();

                    writer.Write("if (target == ");
                    member.WriteId(writer);
                    writer.Write(") global::FlameCsv.Exceptions.CsvParseException.Throw(@field, ");
                    member.WriteConverterName(writer);
                    writer.WriteLine($", {member.Name.ToStringLiteral()});");
                }

                writer.WriteLine(
                    "throw new global::System.Diagnostics.UnreachableException(\"Invalid target: \" + target.ToString());");
            }
        }
    }

    private static void WriteDefaultParameterValues(
        IndentedTextWriter writer,
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
                writer.WriteLine();
                writer.WriteLine("// Default values of parameters that are not marked as required");
            }

            writer.Write($"state.{parameter.Identifier} = ");

            // Enum values are resolved as their underlying type, so they need to be cast back to the enum type
            // e.g. DayOfWeek.Friday would be "state.arg = (System.DayOfWeek)5;"
            if (parameter.ParameterType.IsEnumOrNullableEnum)
            {
                writer.Write($"({parameter.ParameterType.FullyQualifiedName})");
            }

            writer.WriteLine($"{parameter.DefaultValue.ToLiteral()};");
        }

        if (hasOptionalParameters) writer.WriteLine();
    }

    private static void WriteSetters(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.Write("(");

        if (!typeMap.Parameters.IsEmpty)
        {
            writer.IncreaseIndent();

            for (int index = 0; index < typeMap.Parameters.Length; index++)
            {
                ParameterModel parameter = typeMap.Parameters[index];
                writer.WriteLine();
                writer.Write(parameter.Name);
                writer.Write(": ");
                writer.WriteIf(
                    parameter.RefKind is RefKind.In or RefKind.RefReadOnlyParameter,
                    "in ");

                writer.Write($"state.{parameter.Identifier}");
                writer.WriteIf(index < typeMap.Parameters.Length - 1, ",");
            }

            writer.DecreaseIndent();
        }

        writer.Write(")");

        if (typeMap.Properties.AsImmutableArray().Any(static p => p.IsRequired))
        {
            writer.WriteLine();

            writer.WriteLine("{");
            writer.IncreaseIndent();

            foreach (var property in typeMap.Properties)
            {
                writer.WriteLineIf(
                    property.IsRequired,
                    $"{property.Identifier} = state.{property.Identifier},");
            }

            writer.DecreaseIndent();
            writer.Write("}");
        }

        writer.WriteLine(";");

        foreach (var property in typeMap.Properties)
        {
            // required already written above
            if (property.IsRequired || !property.CanRead)
                continue;

            writer.Write("if (");
            property.WriteConverterName(writer);
            writer.Write(" is not null) ");

            if (!string.IsNullOrEmpty(property.ExplicitInterfaceOriginalDefinitionName))
            {
                writer.Write($"(({property.ExplicitInterfaceOriginalDefinitionName})obj).");
            }
            else
            {
                writer.Write("obj.");
            }

            writer.Write(property.Name);
            writer.Write(" = state.");
            writer.Write(property.Identifier);
            writer.WriteLine(";");
        }
    }

    private static void WriteRequiredCheck(TypeMapModel typeMap, IndentedTextWriter writer)
    {
        if (!typeMap.HasRequiredMembers)
        {
            writer.WriteLine();
            writer.WriteLine("// No required fields");
            return;
        }

        writer.Write("if (");

        bool first = true;

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead || !member.IsRequired) continue;

            writer.WriteLineIf(!first, " ||");
            writer.Write("materializer.");
            member.WriteConverterName(writer);
            writer.Write(" is null");
            first = false;
        }

        writer.WriteLine(")");
        using (writer.WriteBlock())
        {
            writer.WriteLine("base.ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers);");
        }
    }

    private static void WriteMissingRequiredFields(TypeMapModel typeMap, IndentedTextWriter writer)
    {
        if (!typeMap.HasRequiredMembers)
            return;

        writer.WriteLine();
        writer.WriteLine(
            "private static System.Collections.Generic.IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)");

        using (writer.WriteBlock())
        {
            foreach (var member in typeMap.AllMembers)
            {
                if (!member.CanRead || !member.IsRequired) continue;

                writer.Write("if (materializer.");
                member.WriteConverterName(writer);
                writer.WriteLine($" is null) yield return {member.Identifier.ToStringLiteral()};");
            }
        }
    }

    private static void WriteMatchers(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        if (!typeMap.IgnoredHeaders.IsEmpty)
        {
            writer.WriteLine("// Ignored headers");
            writer.Write($"if (comparer.Equals(name, {typeMap.IgnoredHeaders[0].ToStringLiteral()}");

            writer.IncreaseIndent();

            for (int i = 1; i < typeMap.IgnoredHeaders.Length; i++)
            {
                writer.WriteLine(") ||");
                writer.Write($"comparer.Equals(name, {typeMap.IgnoredHeaders[i].ToStringLiteral()}");
            }

            writer.DecreaseIndent();
            writer.WriteLine("))");

            using (writer.WriteBlock())
            {
                writer.WriteLine("materializer.Targets[index] = -1;");
                writer.WriteLine("continue");
            }
        }

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanRead) continue;

            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine();
            writer.Write("if (");
            writer.IncreaseIndent();

            if (!typeMap.ThrowOnDuplicate)
            {
                // add check to ignore already handled members
                writer.Write("materializer.");
                member.WriteConverterName(writer);
                writer.WriteLine(" is null &&");
            }

            if (member.Names.IsEmpty)
            {
                writer.Write($"comparer.Equals(name, {member.Name.ToStringLiteral()})");
            }
            else
            {
                bool firstName = true;

                foreach (string name in member.Names)
                {
                    writer.WriteIf(!firstName, " ||");
                    writer.Write($"comparer.Equals(name, {name.ToStringLiteral()})");
                    firstName = false;
                }
            }

            writer.DecreaseIndent();

            writer.Write(")");

            if (member.Order.HasValue)
            {
                writer.Write($" // Explicit order: {member.Order}");
            }

            writer.WriteLine();

            using (writer.WriteBlock())
            {
                if (typeMap.ThrowOnDuplicate)
                {
                    writer.Write("if (materializer.");
                    member.WriteConverterName(writer);
                    writer.Write(" is not null) base.ThrowDuplicate(");
                    writer.Write(member.Name.ToStringLiteral());
                    writer.WriteLine(", name, headers);");
                    writer.WriteLine();
                }

                writer.Write("materializer.");
                member.WriteConverterName(writer);
                writer.Write(" = ");
                WriteConverter(writer, typeMap.Token.FullyQualifiedName, member);
                writer.WriteLine(";");
                writer.Write("materializer.Targets[index] = ");
                member.WriteId(writer);
                writer.WriteLine(";");
                writer.WriteLine("continue;");
            }
        }
    }
}
