using FlameCsv.SourceGen.Helpers;
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

        WriteDematerializerCtor(
            writer,
            typeMap.Token.FullyQualifiedName,
            typeMap.Type.FullyQualifiedName,
            typeMap.AllMembers,
            out int writableCount);

        writer.WriteLine();

        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine(GlobalConstants.CodeDomAttribute);
        writer.WriteLine(EditorBrowsableNever);
        writer.WriteLine(
            $"internal sealed partial class TypeMapDematerializer : global::FlameCsv.Writing.IDematerializer<{typeMap.Token.FullyQualifiedName}, {typeMap.Type.FullyQualifiedName}>");

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

                    writer.Write(property.HeaderName);
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
                    writer.Write(property.HeaderName.ToStringLiteral());
                    writer.WriteLine($"{suffix});");
                    first = false;
                }
            }
        }
    }

    internal static void WriteDematerializerCtor(
        IndentedTextWriter writer,
        string token,
        string targetType,
        EquatableArray<IMemberModel> members,
        out int writableCount)
    {
        writer.WriteLine(
            $"protected override global::FlameCsv.Writing.IDematerializer<{token}, {targetType}> BindForWriting(global::FlameCsv.CsvOptions<{token}> options)");

        writableCount = 0;

        using (writer.WriteBlock())
        {
            writer.WriteLine("return new TypeMapDematerializer");

            writer.WriteLine("{");
            writer.IncreaseIndent();

            foreach (var property in members)
            {
                if (!property.CanWrite) continue;

                property.WriteConverterName(writer);
                writer.Write(" = ");
                WriteConverter(writer, token, property);
                writer.WriteLine(",");

                writableCount++;
            }

            writer.DecreaseIndent();
            writer.WriteLine("};");
        }
    }
}
