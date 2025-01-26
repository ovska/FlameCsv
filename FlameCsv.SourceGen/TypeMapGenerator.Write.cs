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

        if (typeMap.Scope == CsvBindingScope.Read)
            return;

        var writableProperties = typeMap.Properties.Where(m => m.CanWrite).ToArray();

        Array.Sort(
            writableProperties,
            (b1, b2) =>
            {
                var b1Order = b1.Order;
                var b2Order = b2.Order;

                foreach (var target in typeMap.TargetAttributes)
                {
                    if (StringComparer.Ordinal.Equals(target.MemberName, b1.Name))
                    {
                        b1Order = Math.Max(b1Order, target.Order);
                    }
                    else if (StringComparer.Ordinal.Equals(target.MemberName, b2.Name))
                    {
                        b2Order = Math.Max(b2Order, target.Order);
                    }
                }

                if (b1.Order != b2.Order)
                {
                    return b2.Order.CompareTo(b1.Order);
                }

                if (b1.Order != b2.Order)
                {
                    return b2.Order.CompareTo(b1.Order);
                }

                return String.Compare(b1.Name, b2.Name, StringComparison.Ordinal);
            });

        sb.Append(@"

        protected override FlameCsv.Writing.IDematerializer<");
        sb.Append(typeMap.Token.Name);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append("> BindForWriting(FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append(@"> options)
        {
            return new Dematerializer
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

        private sealed class Dematerializer : FlameCsv.Writing.IDematerializer<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append(", ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(@">
        {");

        foreach (var property in writableProperties)
        {
            sb.Append(@"
            public required FlameCsv.CsvConverter<");
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

            public void Write(ref readonly FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token.FullyQualifiedName);
        sb.Append("> writer, ");
        sb.Append(typeMap.Type.FullyQualifiedName);
        sb.Append(@" obj)
            {");

        for (int i = 0; i < writableProperties.Length; i++)
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

            if (i < writableProperties.Length - 1)
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

            public void WriteHeader(ref readonly FlameCsv.Writing.CsvFieldWriter<");
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

        for (int i = 0; i < writableProperties.Length; i++)
        {
            var binding = writableProperties[i];

            sb.Append(@"
                writer.");
            sb.Append(method);
            sb.Append('(');
            sb.Append(binding.Names[0].ToStringLiteral());
            sb.Append(suffix);

            if (i < writableProperties.Length - 1)
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
