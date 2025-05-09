using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

partial class TypeMapGenerator
{
    private static void GetWriteCode(
        IndentedTextWriter writer,
        ref readonly TypeMapModel typeMap,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine();

        WriteDematerializerCtor(writer, in typeMap, out int writableCount);

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
            writer.WriteLine(EditorBrowsableNever);
            writer.Write("internal sealed partial class TypeMap");
            if (!hasHeader)
            {
                writer.Write("Index");
            }

            writer.WriteLine(
                $"Dematerializer : global::FlameCsv.Writing.IDematerializer<{typeMap.Token.FullyQualifiedName}, {typeMap.Type.FullyQualifiedName}>"
            );

            using (writer.WriteBlock())
            {
                int count = hasHeader ? writableCount : typeMap.IndexesForWriting.Length;

                writer.WriteLine($"public int FieldCount => {count};");
                writer.WriteLine();

                var membersToWrite = hasHeader ? typeMap.AllMembers : typeMap.IndexesForWriting;
                foreach (var property in membersToWrite)
                {
                    if (!property.CanWrite)
                        continue;
                    writer.Write(
                        $"public required global::FlameCsv.CsvConverter<{typeMap.Token.FullyQualifiedName}, {property.Type.FullyQualifiedName}> "
                    );
                    property.WriteConverterName(writer);
                    writer.WriteLine(" { get; init; }");
                }

                writer.WriteLine();
                cancellationToken.ThrowIfCancellationRequested();

                writer.WriteLine(
                    $"public void Write(ref readonly global::FlameCsv.Writing.CsvFieldWriter<{typeMap.Token.FullyQualifiedName}> writer, {typeMap.Type.FullyQualifiedName} obj)"
                );

                using (writer.WriteBlock())
                {
                    bool first = true;

                    foreach (var member in membersToWrite)
                    {
                        if (member is not PropertyModel { CanWrite: true } property)
                            continue;

                        writer.WriteLineIf(!first, "writer.WriteDelimiter();");
                        writer.Write("writer.WriteField(");
                        property.WriteConverterName(writer);

                        if (!string.IsNullOrEmpty(property.ExplicitInterfaceOriginalDefinitionName))
                        {
                            writer.Write(", ((");
                            writer.Write(property.ExplicitInterfaceOriginalDefinitionName.AsSpan());
                            writer.Write(")obj).");
                        }
                        else
                        {
                            writer.Write(", obj.");
                        }

                        writer.Write(property.HeaderName);
                        writer.WriteLine(");");
                        first = false;
                    }
                }

                writer.WriteLine();
                cancellationToken.ThrowIfCancellationRequested();

                writer.WriteLine(
                    $"public void WriteHeader(ref readonly global::FlameCsv.Writing.CsvFieldWriter<{typeMap.Token.FullyQualifiedName}> writer)"
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

                    // write directly to the writer for char and byte
                    (string suffix, string method) = typeMap.Token.SpecialType switch
                    {
                        SpecialType.System_Char => ("", "WriteRaw"),
                        SpecialType.System_Byte => ("u8", "WriteRaw"),
                        _ => ("", "WriteText"),
                    };

                    foreach (var member in typeMap.AllMembers)
                    {
                        if (member is not PropertyModel { CanWrite: true } property)
                            continue;

                        writer.WriteLineIf(!first, "writer.WriteDelimiter();");
                        writer.Write($"writer.{method}(");
                        writer.Write(property.HeaderName.ToStringLiteral());
                        writer.WriteLine($"{suffix});");
                        first = false;
                    }
                }
            }
        }
    }

    internal static void WriteDematerializerCtor(
        IndentedTextWriter writer,
        in TypeMapModel typeMap,
        out int writableCount
    )
    {
        writer.WriteLine(
            $"protected override global::FlameCsv.Writing.IDematerializer<{typeMap.Token.Name}, {typeMap.Type.FullyQualifiedName}> BindForWriting(global::FlameCsv.CsvOptions<{typeMap.Token.Name}> options)"
        );

        writableCount = 0;

        using (writer.WriteBlock())
        {
            writer.WriteLine("if (options.HasHeader)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("return new TypeMapDematerializer");

                writer.WriteLine("{");
                writer.IncreaseIndent();

                foreach (var property in typeMap.AllMembers)
                {
                    if (!property.CanWrite)
                        continue;

                    property.WriteConverterName(writer);
                    writer.Write(" = ");
                    WriteConverter(writer, typeMap.Token.Name, property);
                    writer.WriteLine(",");

                    writableCount++;
                }

                writer.DecreaseIndent();
                writer.WriteLine("};");
            }

            writer.WriteLine();
            if (typeMap.IndexesForWriting.Length == 0)
            {
                writer.WriteLine(
                    "throw new global::System.NotSupportedException(\"No valid index binding configuration.\");"
                );
            }
            else
            {
                writer.WriteLine("return new TypeMapIndexDematerializer");

                writer.WriteLine("{");
                writer.IncreaseIndent();

                foreach (var property in typeMap.IndexesForWriting)
                {
                    property.WriteConverterName(writer);
                    writer.Write(" = ");
                    WriteConverter(writer, typeMap.Token.Name, property);
                    writer.WriteLine(",");
                }

                writer.DecreaseIndent();
                writer.WriteLine("};");
            }
        }
    }
}
