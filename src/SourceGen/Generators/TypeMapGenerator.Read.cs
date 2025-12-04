using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

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
            writer.WriteLine();
            writer.WriteLine(
                "global::System.Collections.Generic.IEqualityComparer<string> comparer = options.Comparer;"
            );
            writer.WriteLine("bool anyBound = false;");
            writer.WriteLine();
            writer.WriteLine("for (int index = 0; index < headerSpan.Length; index++)");

            using (writer.WriteBlock())
            {
                writer.WriteLine("string name = headerSpan[index];");

                WriteMatchers(writer, typeMap, cancellationToken);
                writer.WriteLine();

                writer.WriteLine("if (!options.IgnoreUnmatchedHeaders)");
                {
                    writer.WriteLine("base.ThrowUnmatched(name, index, headers);");
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
            writer.WriteLine("public int ExpectedFieldCount => expectedFieldCount;");
            writer.WriteLine();

            foreach (var member in typeMap.AllMembers.Readable())
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write("public ");
                WriteConverterType(writer, typeMap.TokenName, member);
                writer.Write(' ');
                member.WriteConverterName(writer);
                writer.WriteLine(";");
            }

            writer.WriteLine();

            foreach (var member in typeMap.AllMembers.Readable())
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write("public int ");
                member.WriteId(writer);
                writer.WriteLine(";");
            }

            writer.WriteLine();

            writer.WriteLineIf(typeMap.UnsafeCodeAllowed, GlobalConstants.SkipLocalsInitAttribute);
            writer.WriteLine(
                $"public {typeMap.Type.FullyQualifiedName} Parse(scoped ref readonly global::FlameCsv.Reading.CsvRecordRef<{typeMap.TokenName}> record)"
            );

            using (writer.WriteBlock())
            {
                writer.WriteLine("if (record.FieldCount != ExpectedFieldCount)");

                using (writer.WriteBlock())
                {
                    writer.WriteLine(
                        "global::FlameCsv.Exceptions.CsvReadException.ThrowForInvalidFieldCount(ExpectedFieldCount, in record);"
                    );
                }

                writer.WriteWithoutIndent(
                    """

                    #if RELEASE
                                global::System.Runtime.CompilerServices.Unsafe.SkipInit(out ParseState state);
                    #else
                                ParseState state = default;
                    #endif

                    """
                );

                WriteDefaultParameterValues(writer, typeMap, cancellationToken, out bool hasOptionalParameters);

                writer.WriteLine();

                foreach (var member in typeMap.AllMembers.Readable())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    writer.Write("if (");

                    if (!member.IsRequired)
                    {
                        member.WriteConverterName(writer);
                        writer.Write(" is not null && ");
                    }

                    writer.Write("!");
                    member.WriteConverterName(writer);
                    writer.Write(".TryParse(record[");
                    member.WriteId(writer);
                    writer.Write("], out state.");
                    writer.Write(member.Identifier);
                    writer.WriteLine("))");

                    using (writer.WriteBlock())
                    {
                        writer.Write("ThrowForFailedParse(");
                        member.WriteId(writer);
                        writer.WriteLine(");");
                    }

                    writer.WriteLine();
                }

#if false
                writer.WriteLine("// Required fields are guaranteed to be non-null.");
                writer.WriteLine("// Optional fields are null-checked to only write a value when one was read.");

                writer.WriteLineIf(
                    hasOptionalParameters,
                    "// Optional parameters are always passed, their default value is used when not read (see above)"
                );
#endif

                string typeToWrite = typeMap.Proxy?.FullyQualifiedName ?? typeMap.Type.FullyQualifiedName;

                writer.Write($"{typeToWrite} obj = new {typeToWrite}");

                WriteSetters(writer, typeMap, cancellationToken);

                writer.WriteLine("return obj;");
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

        writer.Write("(");

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

        writer.Write(")");

        // explicit interface implementations cannot be init only, and cannot be written in an initializer
        if (
            typeMap
                .Properties.AsImmutableArray()
                .Any(static p => p is { IsRequired: true, ExplicitInterfaceOriginalDefinitionName: null })
        )
        {
            writer.WriteLine();

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
            writer.Write("}");
        }

        writer.WriteLine(";");

        foreach (var property in typeMap.Properties)
        {
            // required already written above
            if (!property.IsParsable || property is { IsRequired: true, ExplicitInterfaceOriginalDefinitionName: null })
            {
                continue;
            }

            if (!property.IsRequired)
            {
                writer.Write("if (");
                property.WriteConverterName(writer);
                writer.Write(" is not null) ");
            }

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
            writer.WriteLine();
            writer.WriteLine("// No required fields");
            return;
        }

        writer.Write("if (");

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

            writer.WriteLine("// Ignored headers");
            writer.Write($"if (comparer.Equals(name, {enumerator.Current.ToStringLiteral()}");

            writer.IncreaseIndent();

            while (enumerator.MoveNext())
            {
                writer.WriteLine(") ||");
                writer.Write($"comparer.Equals(name, {enumerator.Current.ToStringLiteral()}");
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
            writer.Write($"comparer.Equals(name, {member.HeaderName.ToStringLiteral()})");

            foreach (string name in member.Aliases)
            {
                writer.WriteLine(" ||");
                writer.Write($"comparer.Equals(name, {name.ToStringLiteral()})");
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
                writer.Write("if (!options.IgnoreDuplicateHeaders && materializer.");
                member.WriteConverterName(writer);
                writer.WriteLine(" is not null)");
                using (writer.WriteBlock())
                {
                    writer.Write("base.ThrowDuplicate(");
                    writer.Write(member.HeaderName.ToStringLiteral());
                    writer.WriteLine(", name, headers, comparer);");
                }

                writer.WriteLine();
                writer.Write("materializer.");
                member.WriteConverterName(writer);
                writer.Write(" = ");
                WriteConverter(writer, typeMap.TokenName, member);
                writer.WriteLine(";");
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
        writer.WriteLine(")");
        writer.WriteLine("{");
        writer.IncreaseIndent();

        foreach (var member in typeMap.IndexesForReading)
        {
            member.WriteConverterName(writer);
            writer.Write(" = ");
            WriteConverter(writer, typeMap.TokenName, member);
            writer.WriteLine(",");
        }

        writer.DecreaseIndent();
        writer.WriteLine("};");

        for (int index = 0; index < typeMap.IndexesForReading.Length; index++)
        {
            IMemberModel? member = typeMap.IndexesForReading[index];
            writer.Write("materializer.");
            member.WriteId(writer);
            writer.Write(" = ");

            if (member is null)
            {
                writer.WriteLine("-1;");
                continue;
            }

            writer.Write(index.ToString());
            writer.WriteLine(";");
        }

        writer.WriteLine("return materializer;");
    }
}
