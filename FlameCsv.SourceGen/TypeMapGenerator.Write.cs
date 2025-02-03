using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void GetWriteCode(
        StringBuilder sb,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        sb.Append(@"

        protected override global::FlameCsv.Writing.IDematerializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append("> BindForWriting(global::FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append(
            @"> options)
        {
            ");

        int writableCount = 0;
        foreach (var member in typeMap.AllMembers) if (member.CanWrite) writableCount++;

        if (writableCount == 0)
        {
            sb.Append(@"throw new global::System.NotSupportedException(""Type has no writable properties"");
        }
");

            return;
        }

        sb.Append(@"return new Dematerializer
            {");

        foreach (var property in typeMap.AllMembers)
        {
            if (!property.CanWrite) continue;

            sb.Append(@"
                ");
            property.WriteConverterName(sb);
            sb.Append(" = ");
            WriteConverter(sb, property);
            sb.Append(',');
        }

        cancellationToken.ThrowIfCancellationRequested();

        sb.Append(@"
            };
        }

        private sealed class Dematerializer : global::FlameCsv.Writing.IDematerializer<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(@">
        {
            public int FieldCount => ");
        sb.Append(writableCount);
        sb.Append(';');

        foreach (var property in typeMap.AllMembers)
        {
            if (!property.CanWrite) continue;

            sb.Append(@"
            public required global::FlameCsv.CsvConverter<");
            sb.Append(typeMap.Token.FullyQualifiedName);
            sb.Append(", ");
            sb.Append(property.Type.FullyQualifiedName);
            sb.Append("> ");
            property.WriteConverterName(sb);
            sb.Append(" { get; init; }");
        }

        cancellationToken.ThrowIfCancellationRequested();

        sb.Append(@"

            public void Write(ref readonly global::FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append("> writer, ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(@" obj)
            {");

        bool first = true;

        foreach (var member in typeMap.AllMembers)
        {
            if (member is not PropertyModel { CanWrite: true } property) continue;

            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(@"
                writer.WriteDelimiter();");
            }

            sb.Append(@"
                writer.WriteField(");
            member.WriteConverterName(sb);

            if (!string.IsNullOrEmpty(property.ExplicitInterfaceOriginalDefinitionName))
            {
                sb.Append(", ((");
                sb.Append(property.ExplicitInterfaceOriginalDefinitionName);
                sb.Append(")obj).");
            }
            else
            {
                sb.Append(", obj.");
            }

            sb.Append(property.ActualName);
            sb.Append(");");
        }

        cancellationToken.ThrowIfCancellationRequested();

        sb.Append(@"
            }

            public void WriteHeader(ref readonly global::FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append(@"> writer)
            {");

        // write directly to the writer for char and byte
        (string suffix, string method) = typeMap.Token.SpecialType switch
        {
            SpecialType.System_Char => ("", "WriteRaw"),
            SpecialType.System_Byte => ("u8", "WriteRaw"),
            _ => ("", "WriteText"),
        };

        first = true;

        foreach (var member in typeMap.AllMembers)
        {
            if (!member.CanWrite) continue;

            cancellationToken.ThrowIfCancellationRequested();

            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(@"
                writer.WriteDelimiter();");
            }

            sb.Append(@"
                writer.");
            sb.Append(method);
            sb.Append('(');
            sb.Append((member.Names.IsEmpty ? member.ActualName : member.Names[0]).ToStringLiteral());
            sb.Append(suffix);
            sb.Append(");");
        }

        sb.Append(@"
            }
        }");
    }
}
