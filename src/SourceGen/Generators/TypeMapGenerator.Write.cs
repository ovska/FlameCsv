using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

#pragma warning disable RCS1146 // Use conditional access

partial class TypeMapGenerator
{
    private static void GetWriteCode(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine();

        WriteDematerializerCtor(writer, typeMap, out int writableCount);

        writer.WriteLine();

        foreach (var hasHeader in stackalloc bool[] { true, false })
        {
            if (!hasHeader)
            {
                if (typeMap.IndexesForWriting.Length == 0)
                {
                    break;
                }

                writer.WriteLine();
            }

            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine(GlobalConstants.CodeDomAttribute);
            writer.WriteLine(GlobalConstants.EditorBrowsableAttr);
            writer.Write("internal sealed partial class TypeMap");
            if (!hasHeader)
            {
                writer.Write("Index");
            }

            writer.WriteLine(
                $"Dematerializer : global::FlameCsv.Writing.IDematerializer<{typeMap.TokenName}, {typeMap.Type.FullyQualifiedName}>"
            );

            using (writer.WriteBlock())
            {
                int count = hasHeader ? writableCount : typeMap.IndexesForWriting.UnsafeArray.Count(x => x is not null);

                writer.WriteLine($"public int FieldCount => {count};");
                writer.WriteLine();

                EquatableArray<IMemberModel?> membersToWrite = (
                    hasHeader ? typeMap.AllMembers : typeMap.IndexesForWriting!
                )!;

                foreach (var property in membersToWrite.Writable())
                {
                    if (property.HasFormatConverter(typeMap))
                    {
                        writer.Write("public required ");
                        WriteConverterType(writer, typeMap.TokenName, property);
                        writer.Write(' ');
                        property.WriteConverterName(writer);
                        writer.WriteLine(" { get; init; }");
                    }
                    else if (property.IsFormattable(typeMap))
                    {
                        writer.Write("public required global::FlameCsv.Binding.CsvTypeMap.FormatConfig ");
                        property.WriteConfigPrefix(writer);
                        writer.WriteLine(" { get; init; }");
                    }
                }

                writer.WriteLine();
                cancellationToken.ThrowIfCancellationRequested();

                writer.WriteLineIf(typeMap.UnsafeCodeAllowed, GlobalConstants.SkipLocalsInitAttribute);
                writer.WriteLine(
                    $"public void Write(ref readonly global::FlameCsv.Writing.CsvFieldWriter<{typeMap.TokenName}> writer, {typeMap.Type.FullyQualifiedName} obj)"
                );

                using (writer.WriteBlock())
                {
                    bool first = true;

                    foreach (var member in membersToWrite.Writable())
                    {
                        writer.WriteLineIf(!first, "writer.WriteDelimiter();");
                        first = false;

                        if (member.HasFormatConverter(typeMap))
                        {
                            writer.Write("writer.WriteField(");
                            member.WriteConverterName(writer);
                            writer.Write(", ");
                            WriteAccessor(writer, member);
                            writer.WriteLine(");");
                        }
                        else if (member.IsInlinedString(typeMap))
                        {
                            writer.Write("writer.WriteText(");
                            WriteAccessor(writer, member);
                            writer.WriteLine(");");
                        }
                        else if (member.IsFormattable(typeMap))
                        {
                            writer.Write("global::FlameCsv.Writing.CsvFieldWritingExtensions.FormatValue(in writer, ");
                            WriteAccessor(writer, member);
                            writer.Write(", ");
                            member.WriteConfigPrefix(writer);
                            writer.Write(".Format, ");
                            member.WriteConfigPrefix(writer);
                            writer.WriteLine(".FormatProvider);");
                        }
                    }
                }

                writer.WriteLine();
                cancellationToken.ThrowIfCancellationRequested();

                writer.WriteLineIf(hasHeader && typeMap.UnsafeCodeAllowed, GlobalConstants.SkipLocalsInitAttribute);
                writer.WriteLine(
                    $"public void WriteHeader(ref readonly global::FlameCsv.Writing.CsvFieldWriter<{typeMap.TokenName}> writer)"
                );
                using (writer.WriteBlock())
                {
                    if (!hasHeader)
                    {
                        writer.WriteLine(
                            "throw new global::System.NotSupportedException(\"This instance is not configured to write a header record.\");"
                        );
                        break;
                    }

                    bool first = true;

                    foreach (var member in typeMap.AllMembers!.Writable())
                    {
                        writer.WriteLineIf(!first, "writer.WriteDelimiter();");
                        writer.Write("writer.WriteRaw(");
                        writer.Write(member.HeaderName.ToStringLiteral());
                        writer.WriteIf(typeMap.IsByte, "u8");
                        writer.WriteLine(");");
                        first = false;
                    }
                }
            }
        }

        static void WriteAccessor(IndentedTextWriter writer, IMemberModel member)
        {
            if (member is PropertyModel { ExplicitInterfaceOriginalDefinitionName: { } explicitName })
            {
                writer.Write("((");
                writer.Write(explicitName.AsSpan());
                writer.Write(")obj).");
            }
            else
            {
                writer.Write("obj.");
            }

            writer.Write(member.HeaderName);
        }
    }

    internal static void WriteDematerializerCtor(
        IndentedTextWriter writer,
        in TypeMapModel typeMap,
        out int writableCount
    )
    {
        writer.WriteLine(
            $"protected override global::FlameCsv.Writing.IDematerializer<{typeMap.TokenName}, {typeMap.Type.FullyQualifiedName}> BindForWriting(global::FlameCsv.CsvOptions<{typeMap.TokenName}> options)"
        );

        writableCount = 0;

        using (writer.WriteBlock())
        {
            writer.WriteLine("if (options.HasHeader)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("return new TypeMapDematerializer");

                using var _ = writer.WriteBlockWithSemicolon();

                foreach (var property in typeMap.AllMembers.Writable())
                {
                    if (TryWriteMember(writer, typeMap, property))
                    {
                        writableCount++;
                    }
                }
            }

            writer.WriteLine();
            if (typeMap.IndexesForWriting.Length == 0)
            {
                writer.WriteLine(
                    $"throw new global::System.NotSupportedException(\"No valid index binding configuration for type {typeMap.Type.Name}.\");"
                );
            }
            else
            {
                writer.WriteLine("return new TypeMapIndexDematerializer");
                using (writer.WriteBlockWithSemicolon())
                {
                    foreach (var property in typeMap.IndexesForWriting)
                    {
                        if (property is null)
                        {
                            continue;
                        }
                        _ = TryWriteMember(writer, typeMap, property);
                    }
                }
            }
        }

        static bool TryWriteMember(IndentedTextWriter writer, TypeMapModel typeMap, IMemberModel property)
        {
            if (property.HasFormatConverter(typeMap))
            {
                property.WriteConverterName(writer);
                writer.Write(" = ");
                WriteConverter(writer, typeMap.TokenName, property);
                writer.WriteLine(",");
                return true;
            }
            else if (property.IsFormattable(typeMap))
            {
                property.WriteConfigPrefix(writer);
                writer.Write(" = new global::FlameCsv.Binding.CsvTypeMap.FormatConfig(typeof(");
                writer.Write(property.Type.FullyQualifiedName);
                writer.WriteLine("), options),");
                return true;
            }

            return false;
        }
    }
}
