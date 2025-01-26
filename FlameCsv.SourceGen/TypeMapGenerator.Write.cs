using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void GetWriteCode(StringBuilder sb,
        FlameSymbols symbols,
        TypeMapModel typeMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<PropertyModel> writableProperties = typeMap.GetSortedWritableProperties();

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

        if (writableProperties.Count == 0 || typeMap.Scope == CsvBindingScope.Read)
        {
            sb.Append("throw new global::System.NotSupportedException(\"");

            sb.Append(
                writableProperties.Count == 0
                    ? "Type has no writable properties."
                    : "This type map is read-only.");

            sb.Append(@""");
        }
");

            return;
        }

        sb.Append(@"return new Dematerializer
            {");

        foreach (var property in writableProperties)
        {
            sb.Append(@"
                ");
            sb.Append(property.ConverterPrefix);
            sb.Append(property.Name);
            sb.Append(" = ");
            WriteConverter(sb, symbols, property);
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
        {");

        foreach (var property in writableProperties)
        {
            sb.Append(@"
            public required global::FlameCsv.CsvConverter<");
            sb.Append(typeMap.Token.FullyQualifiedName);
            sb.Append(", ");
            sb.Append(property.Type.FullyQualifiedName);
            sb.Append("> ");
            sb.Append(property.ConverterPrefix);
            sb.Append(property.Name);
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

        for (int i = 0; i < writableProperties.Count; i++)
        {
            var property = writableProperties[i];

            sb.Append(@"
                writer.WriteField(");
            sb.Append(property.ConverterPrefix);
            sb.Append(property.Name);

            if (property.ExplicitInterfaceOriginalDefinition is { } iface)
            {
                sb.Append(", ((");
                sb.Append(iface.FullyQualifiedName);
                sb.Append(")obj).");
            }
            else
            {
                sb.Append(", obj.");
            }

            sb.Append(property.Name);

            if (i < writableProperties.Count - 1)
            {
                sb.Append(@");
                writer.WriteDelimiter();");
            }
            else
            {
                sb.Append(@");
                writer.WriteNewline();");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        sb.Append(@"
            }

            public void WriteHeader(ref readonly global::FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append(@"> writer)
            {");

        // write directly to the writer for char and byte
        string suffix = typeMap.Token.SpecialType == SpecialType.System_Byte ? "u8" : "";
        string method = typeMap.Token.SpecialType switch
        {
            SpecialType.System_Char or SpecialType.System_Byte => "WriteRaw",
            _ => "WriteText"
        };

        for (int i = 0; i < writableProperties.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var binding = writableProperties[i];

            sb.Append(@"
                writer.");
            sb.Append(method);
            sb.Append('(');
            sb.Append(binding.Names[0].ToStringLiteral());
            sb.Append(suffix);

            if (i < writableProperties.Count - 1)
            {
                sb.Append(@");
                writer.WriteDelimiter();");
            }
            else
            {
                sb.Append(@");
                writer.WriteNewline();");
            }
        }

        sb.Append(@"
            }
        }");
    }
}
