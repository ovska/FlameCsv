namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void GetWriteCode(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        if (typeMap.Scope == BindingScope.Read)
            return;

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

        int bindingCount = 0;

        foreach (var binding in typeMap.Bindings.Members)
        {
            if (!binding.CanWrite)
                continue;

            bindingCount++;
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
        {
            public int FieldCount => ");
        sb.Append(bindingCount);
        sb.Append(';');

        foreach (var binding in typeMap.Bindings.Members)
        {
            if (!binding.CanWrite)
                continue;

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

            public void Write<TWriter>(FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token);
        sb.Append(", TWriter> writer, ");
        sb.Append(typeMap.Type.ToDisplayString());
        sb.Append(" obj) where TWriter : struct, System.Buffers.IBufferWriter<");
        sb.Append(typeMap.Token);
        sb.Append(@">
            {");

        for (int i = 0; i < typeMap.Bindings.Members.Length; i++)
        {
            var binding = typeMap.Bindings.Members[i];

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

            if (i < typeMap.Bindings.Members.Length - 1)
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

            public void WriteHeader<TWriter>(FlameCsv.Writing.CsvFieldWriter<");
        sb.Append(typeMap.Token);
        sb.Append(", TWriter> writer) where TWriter : struct, System.Buffers.IBufferWriter<");
        sb.Append(typeMap.Token);
        sb.Append(@">
            {");

        for (int i = 0; i < typeMap.Bindings.Members.Length; i++)
        {
            var binding = typeMap.Bindings.Members[i];

            if (!binding.CanWrite)
                continue;

            sb.Append(@"
                writer.WriteText(");
            sb.Append(binding.Names[0].ToStringLiteral());

            if (i < typeMap.Bindings.Members.Length - 1)
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
