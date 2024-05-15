namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void GetWriteCode(StringBuilder sb, ref readonly TypeMapSymbol typeMap)
    {
        if (typeMap.Scope == BindingScope.Read)
            return;

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"

        public override IDematerializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.Type.ToDisplayString());
        sb.Append("> GetDematerializer(CsvOptions<");
        sb.Append(typeMap.Token);
        sb.Append(@"> options)
        {
            return new Dematerializer
            {");

        var converterFactorySymbol = typeMap.Symbols.CsvConverterFactory.ConstructedFrom.Construct(typeMap.TokenSymbol);

        foreach (var binding in typeMap.Bindings.Members)
        {
            if (!binding.CanWrite)
                continue;

            sb.Append(@"
                ");
            sb.Append(binding.ConverterId);
            sb.Append(" = ");
            ResolveConverter(sb, in typeMap, binding.Symbol, binding.Type, converterFactorySymbol);
            sb.Append(',');
        }

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"
            };
        }

        private sealed class Dematerializer : IDematerializer<");
        sb.Append(typeMap.Token);
        sb.Append(", ");
        sb.Append(typeMap.Type.ToDisplayString());
        sb.Append(@">
        {");

        foreach (var binding in typeMap.Bindings.Members)
        {
            if (!binding.CanWrite)
                continue;

            sb.Append(@"
            public required CsvConverter<");
            sb.Append(typeMap.Token);
            sb.Append(", ");
            sb.Append(binding.Type.ToDisplayString());
            sb.Append("> ");
            sb.Append(binding.ConverterId);
            sb.Append(" { get; init; }");
        }

        typeMap.ThrowIfCancellationRequested();

        sb.Append(@"

            public void Write<TWriter>(CsvFieldWriter<");
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
            sb.Append(binding.ConverterId);

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

            public void WriteHeader<TWriter>(CsvFieldWriter<");
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
            sb.Append(binding.Name.ToStringLiteral());

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
