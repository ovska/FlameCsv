using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void GetWriteCode(
        IndentedTextWriter writer,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine();
        writer.WriteLine(
            $"protected override global::FlameCsv.Writing.IDematerializer<{typeMap.Token.Name}, {typeMap.Type.FullyQualifiedName}> BindForWriting(global::FlameCsv.CsvOptions<{typeMap.Token.FullyQualifiedName}> options)");

        int writableCount = 0;

        using (writer.WriteBlock())
        {
            writer.WriteLine("return new Dematerializer");

            writer.WriteLine("{");
            writer.IncreaseIndent();

            foreach (var property in typeMap.AllMembers)
            {
                if (!property.CanWrite) continue;

                property.WriteConverterName(writer);
                writer.Write(" = ");
                WriteConverter(writer, property);
                writer.WriteLine(",");

                writableCount++;
            }

            writer.DecreaseIndent();
            writer.WriteLine("};");
        }

        writer.WriteLine();

        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine(
            $"private sealed class Dematerializer : global::FlameCsv.Writing.IDematerializer<{typeMap.Token.FullyQualifiedName}, {typeMap.Type.FullyQualifiedName}>");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"public int FieldCount => {writableCount};");
            writer.WriteLine();

            foreach (var property in typeMap.AllMembers)
            {
                if (!property.CanWrite) continue;
                writer.Write(
                    $"public required global::FlameCsv.CsvConverter<{typeMap.Token.FullyQualifiedName}, {property.Type.FullyQualifiedName}> ");
                property.WriteConverterName(writer);
                writer.WriteLine(" { get; init; }");
            }

            writer.WriteLine();
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine(
                $"public void Write(ref readonly global::FlameCsv.Writing.CsvFieldWriter<{typeMap.Token.FullyQualifiedName}> writer, {typeMap.Type.FullyQualifiedName} obj)");

            using (writer.WriteBlock())
            {
                bool first = true;

                foreach (var member in typeMap.AllMembers)
                {
                    if (member is not PropertyModel { CanWrite: true } property) continue;

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

                    writer.Write(property.Name);
                    writer.WriteLine(");");
                    first = false;
                }
            }

            writer.WriteLine();
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine(
                $"public void WriteHeader(ref readonly global::FlameCsv.Writing.CsvFieldWriter<{typeMap.Token.FullyQualifiedName}> writer)");
            using (writer.WriteBlock())
            {
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
                    if (member is not PropertyModel { CanWrite: true } property) continue;

                    writer.WriteLineIf(!first, "writer.WriteDelimiter();");
                    writer.Write($"writer.{method}(");
                    writer.Write((property.Names.IsEmpty ? property.Name : property.Names[0]).ToStringLiteral());
                    writer.WriteLine($"{suffix});");
                    first = false;
                }
            }
        }
    }
}
