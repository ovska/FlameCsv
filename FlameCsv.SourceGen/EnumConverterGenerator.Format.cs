using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class EnumConverterGenerator
{
    private static void WriteFormatMethod(
        EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string enumName = model.EnumType.FullyQualifiedName;

        writer.WriteLine("__Unsafe.SkipInit(out charsWritten);");
        writer.WriteLine("if (destination.IsEmpty) return false;");
        writer.WriteLine();

        writer.WriteLine("if (_writeNumbers)");
        using (writer.WriteBlock())
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
            }

            if (fastPathCount != model.UniqueValues.Length)
            {
                writer.WriteLineIf(model.ContiguousFromZeroCount > 0);

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
        }

        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine("else");
        using (writer.WriteBlock())
        {
            WriteFormatMatch(
                writer,
                in model,
                model.Values.DistinctBy(x => x.Value),
                (innerWriter, value) => { innerWriter.Write($"{enumName}.{value.Name}"); },
                static (writer, value) => { writer.Write((value.ExplicitName ?? value.Name).ToStringLiteral()); });
        }

        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine();
        writer.WriteLine("// not a known value");

        if (model.TokenType.SpecialType is SpecialType.System_Byte)
        {
            // TODO: simplify when Enum implements IUtf8Formattable
            writer.WriteLine(
                "var handler = new global::System.Text.Unicode.Utf8.TryWriteInterpolatedStringHandler(" +
                "0, 1, destination, _provider, out bool shouldAppend);");
            writer.WriteLine("if (shouldAppend)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("handler.AppendFormatted(value, _format);");
                writer.WriteLine(
                    "return global::System.Text.Unicode.Utf8.TryWrite(destination, ref handler, out charsWritten);");
            }

            writer.WriteLine();
            writer.WriteLine("charsWritten = 0;");
            writer.WriteLine("return false;");
        }
        else
        {
            writer.WriteLine(
                "return ((global::System.ISpanFormattable)value).TryFormat(destination, out charsWritten, _format, _provider);");
        }

        // TODO: investigate perf in unrolled assignments for short inputs
        writer.WriteLine();
        writer.WriteLine(AggressiveInlining);
        writer.WriteLine("static bool TryWriteCore(");
        writer.IncreaseIndent();
        writer.WriteLine($"global::System.Span<{model.TokenType.Name}> destination,");
        writer.WriteLine($"global::System.ReadOnlySpan<{model.TokenType.Name}> value,");
        writer.WriteLine("out int charsWritten)");
        writer.DecreaseIndent();
        using (writer.WriteBlock())
        {
            writer.WriteLine("if (value.Length == 1)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("if (destination.Length >= 1)");
                using (writer.WriteBlock())
                {
                    writer.WriteLine("destination[0] = value[0];");
                    writer.WriteLine("charsWritten = 1;");
                    writer.WriteLine("return true;");
                }
            }

            writer.WriteLine("else if (value.Length == 2)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("if (destination.Length >= 2)");
                using (writer.WriteBlock())
                {
                    writer.WriteLine("destination[0] = value[0];");
                    writer.WriteLine("destination[1] = value[1];");
                    writer.WriteLine("charsWritten = 2;");
                    writer.WriteLine("return true;");
                }
            }

            writer.WriteLine("else if (destination.Length >= value.Length)");
            using (writer.WriteBlock())
            {
                writer.WriteLine("value.CopyTo(destination);");
                writer.WriteLine("charsWritten = value.Length;");
                writer.WriteLine("return true;");
            }

            writer.WriteLine();
            writer.WriteLine("__Unsafe.SkipInit(out charsWritten);");
            writer.WriteLine("return false;");
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
        writer.WriteLine("bool retVal = value switch");

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

        writer.WriteLine();
        writer.WriteLine("if (retVal)");
        using (writer.WriteBlock())
        {
            writer.WriteLine("return true;");
        }
    }
}
