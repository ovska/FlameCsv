using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class EnumConverterGenerator
{
    private static void WriteFormatMethod(
        EnumModel model,
        bool numbers,
        IndentedTextWriter writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string enumName = model.EnumType.FullyQualifiedName;

        writer.WriteLine(
            $"public override bool TryFormat(global::System.Span<{model.TokenType.Name}> destination, {enumName} value, out int charsWritten)");
        using var block = writer.WriteBlock();

        writer.WriteLine("__Unsafe.SkipInit(out charsWritten);");
        writer.WriteLine();

        if (numbers)
        {
            int? fastPathCount = null;

            if (model.ContiguousFromZeroCount > 0)
            {
                fastPathCount = Math.Min(model.ContiguousFromZeroCount, 10);
                writer.WriteLine($"if ((uint)value < {fastPathCount})");
                using (writer.WriteBlock())
                {
                    writer.WriteLine($"destination[0] = ({model.TokenType.Name})('0' + value);");
                    writer.WriteLine("charsWritten = 1;");
                    writer.WriteLine("return true;");
                }

                writer.WriteLine();
            }

            if (fastPathCount != model.UniqueValues.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                WriteFormatMatch(
                    writer,
                    in model,
                    model.Values.DistinctBy(x => x.Value).OrderBy(x => x.Value).Skip(fastPathCount ?? 0),
                    (innerWriter, value) => innerWriter.Write($"{model.EnumType.FullyQualifiedName}.{value.Name}"),
                    static (writer, value) =>
                    {
                        writer.Write("\"");
                        writer.Write(value.Value.ToString());
                        writer.Write("\"");
                    });
            }
            else
            {
                writer.WriteLine("return false;");
            }
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteFormatMatch(
                writer,
                in model,
                model.Values.DistinctBy(x => x.Value),
                (innerWriter, value) => { innerWriter.Write($"{enumName}.{value.Name}"); },
                static (writer, value) => { writer.Write((value.ExplicitName ?? value.Name).ToStringLiteral()); });
        }
    }

    private static void WriteFormatMatch<T>(
        IndentedTextWriter writer,
        in EnumModel model,
        IEnumerable<T> values,
        Action<IndentedTextWriter, T> writeCase,
        Action<IndentedTextWriter, T> writeValue)
        where T : IEquatable<T?>
    {
        writer.WriteLine("return value switch");

        writer.WriteLine("{");
        writer.IncreaseIndent();

        foreach (var value in values)
        {
            writeCase(writer, value);
            writer.Write(" => TryWriteCore(destination, ");
            writeValue(writer, value);
            if (model.TokenType.SpecialType == SpecialType.System_Byte) writer.Write("u8");
            writer.WriteLine(", out charsWritten),");
        }

        writer.WriteLine("_ => false");
        writer.DecreaseIndent();
        writer.WriteLine("};");
    }
}
