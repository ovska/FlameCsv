using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

partial class EnumConverterGenerator
{
    private static void WriteFormatMethod(
        EnumModel model,
        bool numbers,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine($"{nameof(WriteFormatMethod)}, numbers: {numbers}");

        string enumName = model.EnumType.FullyQualifiedName;

        writer.WriteLine(
            $"public override global::System.Buffers.OperationStatus TryFormat(global::System.Span<{model.Token}> destination, {enumName} value, out int charsWritten)"
        );
        using var block = writer.WriteBlock();

        writer.WriteLine("__Unsafe.SkipInit(out charsWritten);");
        writer.WriteLine();
        writer.WriteLine($"ref {model.Token} dst = ref destination[0];");
        writer.WriteLine();

        if (numbers)
        {
            int? fastPathCount = null;

            if (model.ContiguousFromZeroCount > 0)
            {
                writer.DebugLine("Fast path, has values contiguous from zero");

                fastPathCount = Math.Min(model.ContiguousFromZeroCount, 10);
                writer.Write("if ((");
                writer.Write(
                    model.UnderlyingType.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64
                        ? "ulong"
                        : "uint"
                );
                writer.WriteLine($")value < {fastPathCount})");
                using (writer.WriteBlock())
                {
                    writer.WriteLine($"dst = ({model.Token})('0' + (uint)value);");
                    writer.WriteLine("charsWritten = 1;");
                    writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
                }

                writer.WriteLine();
            }

            if (fastPathCount != model.UniqueValues.Length)
            {
                writer.DebugLine("Not all values are contiguous from zero");

                cancellationToken.ThrowIfCancellationRequested();

                var numericValues = model
                    .Values.DistinctBy(x => x.Value)
                    .OrderBy(x => x.Value)
                    .Skip(fastPathCount ?? 0)
                    .ToList();

                HashSet<int> distinctLengths = [.. numericValues.Select(v => v.Value.ToString().Length)];
                bool skipLengthCheck = distinctLengths.Count == 1;

                if (skipLengthCheck)
                {
                    writer.DebugLine("All values have the same # of digits, skipping length checks inside the switch");
                    writer.WriteLine($"if (destination.Length >= {distinctLengths.First()})");
                }

                using (writer.WriteBlockIf(skipLengthCheck))
                {
                    WriteFormatMatch(
                        writer,
                        model,
                        cancellationToken,
                        numericValues,
                        static value => value.Value.ToString(),
                        skipLengthCheck
                    );
                }

                if (skipLengthCheck)
                {
                    writer.WriteLine("else");
                    using (writer.WriteBlock())
                    {
                        writer.WriteLine("return global::System.Buffers.OperationStatus.DestinationTooSmall;");
                    }
                }
            }
            else
            {
                writer.DebugLine("All values are contiguous from zero");
                writer.WriteLine("// unknown value");
                writer.WriteLine("return global::System.Buffers.OperationStatus.InvalidData;");
            }
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteFormatMatch(
                writer,
                model,
                cancellationToken,
                model.Values.DistinctBy(x => x.Value),
                static value => value.DisplayName,
                skipLengthCheck: false
            );
        }
    }

    private static void WriteFormatMatch(
        IndentedTextWriter writer,
        EnumModel model,
        CancellationToken cancellationToken,
        IEnumerable<EnumValueModel> values,
        Func<EnumValueModel, string> getValue,
        bool skipLengthCheck
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteFormatMatch));

        writer.WriteLine("switch (value)");

        using (writer.WriteBlock())
        {
            foreach (var value in values)
            {
                writer.WriteLine($"case {model.EnumType.FullyQualifiedName}.{value.Name}:");
                using (writer.WriteBlock())
                {
                    string formattedValue = getValue(value);

                    if (TryWriteDirectFormat(writer, model, cancellationToken, formattedValue, skipLengthCheck))
                    {
                        continue;
                    }

                    writer.Write($"if ({formattedValue.ToStringLiteral()}");
                    writer.WriteIf(model.IsByte, "u8");
                    writer.WriteLine(".TryCopyTo(destination))");
                    using (writer.WriteBlock())
                    {
                        writer.Write("charsWritten = ");
                        writer.Write(
                            (
                                model.IsByte ? Encoding.UTF8.GetByteCount(formattedValue) : formattedValue.Length
                            ).ToString()
                        );
                        writer.WriteLine(";");
                        writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
                    }

                    writer.WriteLine("return global::System.Buffers.OperationStatus.DestinationTooSmall;");
                }
            }
        }

        writer.WriteLine();
        writer.WriteLine("// unknown value");
        writer.WriteLine("return global::System.Buffers.OperationStatus.InvalidData;");
    }

    private static bool TryWriteDirectFormat(
        IndentedTextWriter writer,
        EnumModel model,
        CancellationToken cancellationToken,
        string value,
        bool skipLengthCheck
    )
    {
        if (!value.IsAscii())
        {
            writer.DebugLine("Cannot unroll formatting; value is not ASCII");
            return false;
        }

        if (value.Length == 1)
        {
            writer.DebugLine("Formatting directly; value is 1 char");
            writer.Write("dst = ");
            writer.WriteIf(model.IsByte, "(byte)");
            writer.WriteLine($"{value[0].ToCharLiteral()};");
            writer.WriteLine("charsWritten = 1;");
            writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
            return true;
        }

        // don't unroll too long values; TryCopyTo should produce better codegen
        // TODO: profile and revisit in .NET 10 if TryCopyTo performance improves further
        int threshold = model.IsByte ? 8 : 4;
        if (value.Length > threshold)
        {
            writer.DebugLine($"Not unrolling; value is too long ({value.Length} > {threshold})");
            return false;
        }

        writer.DebugLine($"Unrolling direct format (skipLengthCheck: {skipLengthCheck})");

        writer.WriteLineIf(!skipLengthCheck, $"if (destination.Length >= {value.Length})");

        using (writer.WriteBlockIf(!skipLengthCheck))
        {
            // jit optimizes multiple Unsafe.Add writes to a single call for bytes, but not for chars
            if (model.IsByte)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    writer.WriteLine($"destination[{i}] = (byte){value[i].ToCharLiteral()};");
                }
            }
            else
            {
                int remaining = value.Length;
                int offset = 0;

                while (remaining >= 2)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // write 4 or 2 chars at a time
                    (int size, string type) = remaining >= 4 ? (4, "ulong") : (2, "uint");

                    writer.Write($"__Unsafe.As<char, {type}>(ref ");
                    writer.WriteIf(offset == 0, "dst");
                    writer.WriteIf(offset > 0, $"__Unsafe.Add(ref dst, {offset})");
                    writer.Write($") = __Unsafe.As<char, {type}>(ref __MemoryMarshal.GetReference<char>(");
                    writer.Write(value.Substring(offset, size).ToStringLiteral());
                    writer.WriteLine("));");

                    offset += size;
                    remaining -= size;
                }

                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    writer.Write($"__Unsafe.Add(ref dst, {offset})");
                    writer.Write(" = ");
                    writer.WriteLine($"{value[offset].ToCharLiteral()};");
                    offset++;
                    remaining--;
                }
            }

            writer.WriteLine($"charsWritten = {value.Length};");
            writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
        }

        writer.WriteLineIf(!skipLengthCheck, "return global::System.Buffers.OperationStatus.DestinationTooSmall;");
        return true;
    }

    private static void WriteFlagsFormat(
        EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteFlagsFormat));

        string enumName = model.EnumType.FullyQualifiedName;

        writer.WriteLine(
            $"public override global::System.Buffers.OperationStatus TryFormat(global::System.Span<{model.Token}> destination, {enumName} value, out int charsWritten)"
        );
        using var block = writer.WriteBlock();
        writer.WriteLine(
            $"return (({model.UnderlyingType.FullyQualifiedName})value).TryFormat(destination, out charsWritten)"
        );
        writer.IncreaseIndent();
        writer.WriteLine(" ? global::System.Buffers.OperationStatus.Done");
        writer.WriteLine(" : global::System.Buffers.OperationStatus.DestinationTooSmall;");
        writer.DecreaseIndent();
    }
}
