namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void GetWriteCode(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        if (typeMap.Scope == BindingScope.Read)
            return;

        var writeBindingsSorted = typeMap.Bindings.Members.Where(m => m.CanWrite).ToArray();

        Array.Sort(
            writeBindingsSorted,
            (b1, b2) => b1.Order != b2.Order
                ? b2.Order.CompareTo(b1.Order)
                : String.Compare(b1.Name, b2.Name, StringComparison.Ordinal));

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"

        public override FlameCsv.Writing.IDematerializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.Type.ToDisplayString());
        sb.Append("> GetDematerializer(FlameCsv.CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append(@"> options)
        {
            return new Dematerializer
            {");

        var converterFactorySymbol = typeMap.Symbols.CsvConverterFactory.ConstructedFrom.Construct(typeMap.TokenSymbol);

        foreach (var binding in writeBindingsSorted)
        {
            sb.Append(@"
                ");
            binding.WriteConverterId(sb);
            sb.Append(" = ");
            ResolveConverter(sb, in typeMap, binding.Symbol, binding.Type, converterFactorySymbol);
            sb.Append(',');
        }

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"
            };
        }

        private sealed class Dematerializer : FlameCsv.Writing.IDematerializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.Type.ToDisplayString());
        sb.Append(@">
        {");

        foreach (var binding in writeBindingsSorted)
        {
            sb.Append(@"
            public required FlameCsv.CsvConverter<");
            sb.Append(typeMap.Token);
            sb.Append(", ");
            sb.Append(binding.Type.ToDisplayString());
            sb.Append("> ");
            binding.WriteConverterId(sb);
            sb.Append(" { get; init; }");
        }

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"

            public void Write(ref readonly FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token);
        sb.Append("> writer, ");
        sb.Append(typeMap.Type.ToDisplayString());
        sb.Append(@" obj)
            {");

        for (int i = 0; i < writeBindingsSorted.Length; i++)
        {
            var binding = writeBindingsSorted[i];

            if (!binding.CanWrite)
                continue;

            sb.Append(@"
                writer.WriteField(");
            binding.WriteConverterId(sb);

            if (binding.IsExplicitInterfaceDefinition(typeMap.Type, out var ifaceSymbol))
            {
                sb.Append(", ((");
                sb.Append(ifaceSymbol.ToDisplayString());
                sb.Append(")obj).");
            }
            else
            {
                sb.Append(", obj.");
            }

            sb.Append(binding.Name);

            if (i < writeBindingsSorted.Length - 1)
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

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"
            }

            public void WriteHeader(ref readonly FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token);
        sb.Append(@"> writer)
            {");

        // write directly to the writer for char and byte
        string suffix = typeMap.TokenSymbol.SpecialType == SpecialType.System_Byte ? "u8" : "";
        string method = typeMap.TokenSymbol.SpecialType switch
        {
            SpecialType.System_Char or SpecialType.System_Byte => "WriteRaw",
            _ => "WriteText"
        };

        for (int i = 0; i < writeBindingsSorted.Length; i++)
        {
            var binding = writeBindingsSorted[i];

            sb.Append(@"
                writer.");
            sb.Append(method);
            sb.Append('(');
            sb.Append(binding.Names[0].ToStringLiteral());
            sb.Append(suffix);

            if (i < writeBindingsSorted.Length - 1)
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
