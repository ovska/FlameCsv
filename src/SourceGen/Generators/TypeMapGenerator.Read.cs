using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

#pragma warning disable RCS1146 // Use conditional access

partial class TypeMapGenerator
{
    private static void GetReadCode(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.Write("protected override global::FlameCsv.Reading.IMaterializer<");
        writer.Write(typeMap.TokenName);
        writer.Write(", ");
        writer.Write(typeMap.Type.FullyQualifiedName);
        writer.Write(
            "> BindForReading(global::System.Collections.Immutable.ImmutableArray<string> headers, global::FlameCsv.CsvOptions<"
        );
        writer.Write(typeMap.TokenName);
        writer.WriteLine("> options)");

        using (writer.WriteBlock())
        {
            writer.WriteLine("scoped global::System.ReadOnlySpan<string> headerSpan = headers.AsSpan();");
            writer.WriteLine("TypeMapMaterializer materializer = new TypeMapMaterializer(headerSpan.Length);");
            writer.WriteLine(
                "global::System.StringComparer comparer = options.IgnoreHeaderCase ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal;"
            );
            writer.WriteLine();
            writer.WriteLine("bool anyBound = false;");
            writer.WriteLine("global::System.Collections.Generic.List<int> ignoredIndexes = null;");
            writer.WriteLine();
            writer.WriteLine("for (int index = 0; index < headerSpan.Length; index++)");

            using (writer.WriteBlock())
            {
                writer.WriteLine("string name = headerSpan[index];");

                WriteMatchers(writer, typeMap, cancellationToken);
                writer.WriteLine();

                writer.WriteLine("if (!options.IgnoreUnmatchedHeaders)");
                using (writer.WriteBlock())
                {
                    writer.WriteLine("base.ThrowUnmatched(name, index, headers);");
                }
                writer.WriteLine(
                    "else if (options.ValidateQuotes >= global::FlameCsv.CsvQuoteValidation.ValidateUnreadFields)"
                );
                using (writer.WriteBlock())
                {
                    writer.WriteLine("(ignoredIndexes ??= new(headerSpan.Length - index)).Add(index);");
                }
            }

            writer.WriteLine();
            writer.WriteLine("if (!anyBound)");

            using (writer.WriteBlock())
            {
                writer.WriteLine("base.ThrowNoFieldsBound(headers);");
            }

            WriteRequiredCheck(typeMap, writer);

            writer.WriteLine();
            writer.WriteLine("if (ignoredIndexes is not null) materializer.IgnoredIndexes = [..ignoredIndexes];");
            writer.WriteLine("return materializer;");
        }

        writer.WriteLine();

        writer.WriteLine(
            $"protected override global::FlameCsv.Reading.IMaterializer<{typeMap.TokenName}, {typeMap.Type.FullyQualifiedName}> BindForReading(global::FlameCsv.CsvOptions<{typeMap.TokenName}> options)"
        );

        using (writer.WriteBlock())
        {
            if (typeMap.IndexesForReading.Length != 0)
            {
                WriteReadingIndexes(writer, typeMap, cancellationToken);
            }
            else
            {
                writer.WriteLine(
                    $"throw new global::System.NotSupportedException(\"No valid index binding configuration for type {typeMap.Type.Name}.\");"
                );
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        WriteMissingRequiredFields(typeMap, writer);

        writer.WriteLine();

        writer.WriteLineIf(typeMap.UnsafeCodeAllowed, GlobalConstants.SkipLocalsInitAttribute);
        writer.WriteLine("private struct ParseState");
        using (writer.WriteBlock())
        {
            foreach (var member in typeMap.AllMembers.Readable())
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine($"public {member.Type.FullyQualifiedName} {member.Identifier};");
            }
        }

        writer.WriteLine();

        writer.WriteLine(GlobalConstants.CodeDomAttribute);
        writer.WriteLine(GlobalConstants.EditorBrowsableAttr);
        writer.WriteLine(
            $"internal sealed partial class TypeMapMaterializer(int expectedFieldCount) : global::FlameCsv.Reading.IMaterializer<{typeMap.TokenName}, {typeMap.Type.FullyQualifiedName}>"
        );

        using (writer.WriteBlock())
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool commentWritten = false;

            foreach (var member in typeMap.AllMembers.Readable())
            {
                if (!member.HasParseConverter(typeMap))
                {
                    continue;
                }

                if (!commentWritten)
                {
                    writer.WriteLine("// converters for each member/parameter");
                    commentWritten = true;
                }

                writer.Write("public ");
                WriteConverterType(writer, typeMap.TokenName, member);
                writer.Write(' ');
                member.WriteConverterName(writer);
                writer.Write(';');
                writer.WriteIf(member.IsRequired, " // required");
                writer.WriteLine();
            }

            if (!commentWritten)
            {
                writer.WriteLine("// No converters needed for any members/parameters");
            }

            writer.WriteLine();

            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine("// field indexes in the current CSV");
            foreach (var member in typeMap.AllMembers.Readable())
            {
                writer.Write("public int ");
                member.WriteId(writer);
                writer.WriteLine(" = -1;");
            }

            writer.WriteLine();
            writer.WriteLine("public int[] IgnoredIndexes;");
            writer.WriteLine();

            writer.WriteLineIf(typeMap.UnsafeCodeAllowed, GlobalConstants.SkipLocalsInitAttribute);
            writer.WriteLine(
                $"public {typeMap.Type.FullyQualifiedName} Parse(global::FlameCsv.Reading.CsvRecordRef<{typeMap.TokenName}> record)"
            );

            using (writer.WriteBlock())
            {
                writer.WriteLine("if (record.FieldCount != expectedFieldCount)");
                using (writer.WriteBlock())
                {
                    writer.WriteLine(
                        "global::FlameCsv.Exceptions.CsvReadException.ThrowForInvalidFieldCount(expectedFieldCount, record);"
                    );
                }

                writer.WriteLine("if (IgnoredIndexes is int[] ignoredIndexes)");
                using (writer.WriteBlock())
                {
                    writer.WriteLine("record.ValidateFieldsUnsafe(ignoredIndexes);");
                }

                writer.WriteWithoutIndent(
                    """
                    #if RELEASE
                                global::System.Runtime.CompilerServices.Unsafe.SkipInit(out int __index);
                                global::System.Runtime.CompilerServices.Unsafe.SkipInit(out ParseState state);
                    #else
                                int __index;
                                ParseState state = default;
                    #endif

                    """
                );

                WriteDefaultParameterValues(writer, typeMap, cancellationToken, out bool hasOptionalParameters);

                writer.WriteLine();

                writer.DebugLine("Writing required members..");
                foreach (var member in typeMap.AllMembers.Readable())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!member.IsRequired && member is not ParameterModel)
                    {
                        writer.DebugLine($"Skipping non-required member {member.Identifier}");
                        continue;
                    }

                    writer.Write("if (");

                    if (!member.IsRequired)
                    {
                        member.WriteConverterName(writer);
                        writer.Write(" is not null && ");
                    }

                    writer.Write('!');
                    member.WriteConverterName(writer);
                    writer.Write(".TryParse(record.GetFieldUnsafe(__index = ");
                    member.WriteId(writer);
                    writer.Write("), out state.");
                    writer.Write(member.Identifier);
                    writer.WriteLine("))");

                    using (writer.WriteBlock())
                    {
                        writer.WriteLine("goto FailedParse;");
                    }

                    writer.WriteLine();
                }

                string typeToWrite = typeMap.Proxy?.FullyQualifiedName ?? typeMap.Type.FullyQualifiedName;

                writer.Write($"{typeToWrite} obj = new {typeToWrite}");

                WriteSetters(writer, typeMap, cancellationToken);

                writer.WriteLine("return obj;");

                writer.WriteLine();
                writer.WriteLine("FailedParse:");
                writer.WriteLine("ThrowForFailedParse(__index);");
                writer.WriteLine("return default; // Unreachable");
            }

            writer.WriteLine();
            writer.WriteLine(GlobalConstants.NoInliningAttr);
            writer.WriteLine("private void ThrowForFailedParse(int index)");

            using (writer.WriteBlock())
            {
                foreach (var member in typeMap.AllMembers.Readable())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    writer.Write("if (index == ");
                    member.WriteId(writer);
                    writer.Write(
                        $") global::FlameCsv.Exceptions.CsvParseException.Throw(index, typeof({member.Type.FullyQualifiedName}), "
                    );
                    member.WriteConverterName(writer);
                    writer.WriteLine($", {member.HeaderName.ToStringLiteral()});");
                }

                writer.WriteLine(
                    "throw new global::System.Diagnostics.UnreachableException(\"Invalid target index: \" + index.ToString());"
                );
            }
        }
    }

    private static void WriteDefaultParameterValues(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken,
        out bool hasOptionalParameters
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        hasOptionalParameters = false;
        if (typeMap.Parameters.IsEmpty)
            return;

        foreach (var parameter in typeMap.Parameters)
        {
            // check if parameter can be omitted at all
            if (!parameter.HasDefaultValue || parameter.IsRequiredByAttribute)
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

        if (hasOptionalParameters)
            writer.WriteLine();
    }

    private static void WriteSetters(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.Write('(');

        if (!typeMap.Parameters.IsEmpty)
        {
            writer.IncreaseIndent();

            for (int index = 0; index < typeMap.Parameters.Length; index++)
            {
                ParameterModel parameter = typeMap.Parameters[index];
                writer.WriteLine();
                writer.Write(parameter.HeaderName);
                writer.Write(": ");
                writer.WriteIf(parameter.RefKind is RefKind.In or RefKind.RefReadOnlyParameter, "in ");

                writer.Write($"state.{parameter.Identifier}");
                writer.WriteIf(index < typeMap.Parameters.Length - 1, ",");
            }

            writer.DecreaseIndent();
        }

        writer.Write(')');

        // explicit interface implementations cannot be init only, and cannot be written in an initializer
        if (
            typeMap
                .Properties.AsImmutableArray()
                .Any(static p => p is { IsRequired: true, ExplicitInterfaceOriginalDefinitionName: null })
        )
        {
            writer.WriteLine();
            writer.DebugLine("Using explicit assignments for required properties");

            writer.WriteLine("{");
            writer.IncreaseIndent();

            foreach (var property in typeMap.Properties)
            {
                writer.WriteLineIf(
                    property is { IsRequired: true, ExplicitInterfaceOriginalDefinitionName: null },
                    $"{property.Identifier} = state.{property.Identifier},"
                );
            }

            writer.DecreaseIndent();
            writer.Write('}');
        }

        writer.WriteLine(";");
        foreach (var property in typeMap.Properties)
        {
            if (property.IsRequired && property.ExplicitInterfaceOriginalDefinitionName is not null)
            {
                WriteSetter(property, writer);
            }
        }

        foreach (var property in typeMap.Properties)
        {
            // required already written above
            if (!property.IsParsable || property.IsRequired)
            {
                continue;
            }

            writer.WriteLine();

            writer.Write("if (");
            property.WriteConverterName(writer);
            writer.WriteLine(" is not null)");

            using var _ = writer.WriteBlock();

            writer.Write("if (!");
            property.WriteConverterName(writer);
            writer.Write(".TryParse(record.GetFieldUnsafe(__index = ");
            property.WriteId(writer);
            writer.Write("), out state.");
            writer.Write(property.Identifier);
            writer.WriteLine("))");

            using (writer.WriteBlock())
            {
                writer.WriteLine("goto FailedParse;");
            }

            WriteSetter(property, writer);
        }

        static void WriteSetter(PropertyModel property, IndentedTextWriter writer)
        {
            if (!string.IsNullOrEmpty(property.ExplicitInterfaceOriginalDefinitionName))
            {
                writer.Write($"(({property.ExplicitInterfaceOriginalDefinitionName})obj).");
            }
            else
            {
                writer.Write("obj.");
            }

            writer.Write(property.HeaderName);
            writer.Write(" = state.");
            writer.Write(property.Identifier);
            writer.WriteLine(";");
        }
    }

    private static void WriteRequiredCheck(TypeMapModel typeMap, IndentedTextWriter writer)
    {
        if (!typeMap.HasRequiredMembers)
        {
            writer.DebugLine("No required members in this type");
            return;
        }

        writer.Write("if (");
        writer.IncreaseIndent();

        bool first = true;

        foreach (var member in typeMap.AllMembers.Where(m => m.IsParsable && m.IsRequired))
        {
            writer.WriteLineIf(!first, " ||");
            writer.Write("materializer.");
            member.WriteConverterName(writer);
            writer.Write(" is null");
            first = false;
        }

        writer.WriteLine(")");
        writer.DecreaseIndent();
        using (writer.WriteBlock())
        {
            writer.WriteLine("base.ThrowRequiredNotRead(GetMissingRequiredFields(materializer), headers);");
        }
    }

    private static void WriteMissingRequiredFields(TypeMapModel typeMap, IndentedTextWriter writer)
    {
        if (!typeMap.HasRequiredMembers)
        {
            return;
        }

        writer.WriteLine();
        writer.WriteLine(
            "private static System.Collections.Generic.IEnumerable<string> GetMissingRequiredFields(TypeMapMaterializer materializer)"
        );

        using (writer.WriteBlock())
        {
            foreach (var member in typeMap.AllMembers.Where(m => m.IsParsable && m.IsRequired))
            {
                writer.Write("if (materializer.");
                member.WriteConverterName(writer);
                writer.WriteLine($" is null) yield return {member.Identifier.ToStringLiteral()};");
            }
        }
    }

    private static void WriteMatchers(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken
    )
    {
        HashSet<string>? ignoredSet = null;

        foreach (var member in typeMap.AllMembers.Where(m => m.IsIgnored))
        {
            ignoredSet ??= PooledSet<string>.Acquire();
            ignoredSet.Add(member.HeaderName);
            foreach (var name in member.Aliases)
            {
                ignoredSet.Add(name);
            }
        }

        if (ignoredSet is { Count: > 0 })
        {
            using var enumerator = ignoredSet.GetEnumerator();
            _ = enumerator.MoveNext(); // must succeed

            writer.WriteLine();
            writer.WriteLine("// Ignored headers");
            writer.Write($"if (comparer.Equals({enumerator.Current.ToStringLiteral()}, name");

            writer.IncreaseIndent();

            while (enumerator.MoveNext())
            {
                writer.WriteLine(") ||");
                writer.Write($"comparer.Equals({enumerator.Current.ToStringLiteral()}, name");
            }

            writer.DecreaseIndent();
            writer.WriteLine("))");

            using (writer.WriteBlock())
            {
                writer.WriteLine("continue;");
            }
        }

        PooledSet<string>.Release(ignoredSet);

        foreach (var member in typeMap.AllMembers.Readable())
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine();
            writer.Write("if (");
            writer.IncreaseIndent();
            writer.Write($"comparer.Equals({member.HeaderName.ToStringLiteral()}, name)");

            foreach (string name in member.Aliases)
            {
                writer.WriteLine(" ||");
                writer.Write($"comparer.Equals({name.ToStringLiteral()}, name)");
            }

            writer.DecreaseIndent();

            writer.Write(')');

            if (member.Order.HasValue)
            {
                writer.Write($" // Explicit order: {member.Order}");
            }

            writer.WriteLine();

            using (writer.WriteBlock())
            {
                writer.Write("if (!options.IgnoreDuplicateHeaders && materializer.");
                member.WriteId(writer);
                writer.WriteLine(" >= 0)");
                using (writer.WriteBlock())
                {
                    writer.Write("base.ThrowDuplicate(");
                    writer.Write(member.HeaderName.ToStringLiteral());
                    writer.WriteLine(", name, headers);");
                }

                writer.WriteLine();

                if (member.HasParseConverter(typeMap))
                {
                    writer.Write("materializer.");
                    member.WriteConverterName(writer);
                    writer.Write(" = ");
                    WriteConverter(writer, typeMap.TokenName, member);
                    writer.WriteLine(";");
                }

                writer.Write("materializer.");
                member.WriteId(writer);
                writer.WriteLine(" = index;");
                writer.WriteLine("anyBound = true;");
                writer.WriteLine("continue;");
            }
        }
    }

    private static void WriteReadingIndexes(
        IndentedTextWriter writer,
        in TypeMapModel typeMap,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.Write("TypeMapMaterializer materializer = new TypeMapMaterializer(");
        writer.Write(typeMap.IndexesForReading.Length.ToString());
        writer.Write(")");

        IndentedTextWriter.Block block = default;

        foreach (var member in typeMap.IndexesForReading)
        {
            if (member is not null && member.HasParseConverter(typeMap))
            {
                if (block.writer is null)
                {
                    writer.WriteLine();
                    block = writer.WriteBlockWithSemicolon();
                }

                member.WriteConverterName(writer);
                writer.Write(" = ");
                WriteConverter(writer, typeMap.TokenName, member);
                writer.WriteLine(",");
            }
        }

        if (block.writer is null)
        {
            writer.WriteLine(";");
        }

        block.Dispose();

        for (int index = 0; index < typeMap.IndexesForReading.Length; index++)
        {
            IMemberModel? member = typeMap.IndexesForReading[index];

            if (member is null)
            {
                continue;
            }

            writer.Write("materializer.");
            member.WriteId(writer);
            writer.Write(" = ");

            writer.Write(index.ToString());
            writer.WriteLine(";");
        }

        writer.WriteLine("return materializer;");
    }
}
