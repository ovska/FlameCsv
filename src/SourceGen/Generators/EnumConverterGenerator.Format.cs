using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

partial class EnumConverterGenerator
{
    private static void WriteFormatMethod(
        ref readonly EnumModel model,
        bool numbers,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine($"{nameof(WriteFormatMethod)}, numbers: {numbers}");

        string enumName = model.EnumType.FullyQualifiedName;

        writer.WriteLine(
            $"public override global::System.Buffers.OperationStatus TryFormat(global::System.Span<{model.TokenType.Name}> destination, {enumName} value, out int charsWritten)"
        );
        using var block = writer.WriteBlock();

        writer.WriteLine("__Unsafe.SkipInit(out charsWritten);");
        writer.WriteLine();
        writer.WriteLine($"ref {model.TokenType.Name} dst = ref destination[0];");
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
                        : "uint");
                writer.WriteLine($")value < {fastPathCount})");
                using (writer.WriteBlock())
                {
                    writer.WriteLine($"dst = ({model.TokenType.Name})('0' + (uint)value);");
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

                // all are 2 digits long?
                bool skipLengthCheck = numericValues.All(v => v.Value < 100 && v.Value > -10);

                if (skipLengthCheck)
                {
                    // writer.WriteLine($"if (value > {model.EnumType.FullyQualifiedName}.{model.Values[^1].Name})");
                    // using (writer.WriteBlock())
                    // {
                    //     writer.WriteLine("return global::System.Buffers.OperationStatus.InvalidData;");
                    // }

                    // writer.WriteLine();
                    writer.WriteLine("if (destination.Length >= 2)");
                }

                using (writer.WriteBlockIf(skipLengthCheck))
                {
                    WriteFormatMatch(
                        writer,
                        in model,
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
                writer.WriteLine("return global::System.Buffers.OperationStatus.InvalidData;");
            }
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteFormatMatch(
                writer,
                in model,
                cancellationToken,
                model.Values.DistinctBy(x => x.Value),
                static value => value.DisplayName,
                skipLengthCheck: false
            );
        }
    }

    private static void WriteFormatMatch(
        IndentedTextWriter writer,
        ref readonly EnumModel model,
        CancellationToken cancellationToken,
        IEnumerable<EnumValueModel> values,
        Func<EnumValueModel, string> getValue,
        bool skipLengthCheck
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteFormatMatch));

        writer.WriteLine("switch (value)");

        using var block = writer.WriteBlock();

        foreach (var value in values)
        {
            writer.WriteLine($"case {model.EnumType.FullyQualifiedName}.{value.Name}:");
            using (writer.WriteBlock())
            {
                string formattedValue = getValue(value);

                if (TryWriteDirectFormat(writer, in model, cancellationToken, formattedValue, skipLengthCheck))
                {
                    continue;
                }

                writer.Write($"if ({formattedValue.ToStringLiteral()}");
                writer.WriteIf(model.TokenType.IsByte(), "u8");
                writer.WriteLine(".TryCopyTo(destination))");
                using (writer.WriteBlock())
                {
                    writer.Write("charsWritten = ");
                    writer.Write(
                        (
                            model.TokenType.IsByte()
                                ? Encoding.UTF8.GetByteCount(formattedValue)
                                : formattedValue.Length
                        ).ToString()
                    );
                    writer.WriteLine(";");
                    writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
                }

                writer.WriteLine("return global::System.Buffers.OperationStatus.DestinationTooSmall;");
            }
        }

        // ReSharper disable once DisposeOnUsingVariable
        block.Dispose();

        writer.WriteLine();
        writer.WriteLine("return global::System.Buffers.OperationStatus.InvalidData;");
    }

    private static bool TryWriteDirectFormat(
        IndentedTextWriter writer,
        ref readonly EnumModel model,
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
            writer.WriteIf(model.TokenType.IsByte(), "(byte)");
            writer.WriteLine($"{value[0].ToCharLiteral()};");
            writer.WriteLine("charsWritten = 1;");
            writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
            return true;
        }

        int max = model.TokenType.IsByte() ? (sizeof(long)) + 1 : (sizeof(long) / 2) + 1;
        if (value.Length > max)
        {
            writer.DebugLine("Cannot unroll formatting; value is too long");
            return false;
        }

        writer.DebugLine($"Unrolling direct format (skipLengthCheck: {skipLengthCheck})");

        writer.WriteLineIf(!skipLengthCheck, $"if (destination.Length >= {value.Length})");

        int remaining = value.Length;
        int offset = 0;

        using (writer.WriteBlockIf(!skipLengthCheck))
        {
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // jit optimizes multiple Unsafe.Add writes to a single call for bytes, but not for chars
                // so write the unaligned writes directly
                if (remaining >= 2)
                {
                    (int size, string type) = GetTypeAndSize(remaining, in model);

                    if (model.TokenType.IsByte())
                    {
                        writer.Write("__Unsafe.WriteUnaligned(ref ");
                        writer.WriteIf(offset != 0, $"__Unsafe.Add(ref dst, {offset})");
                        writer.WriteIf(offset == 0, "dst");
                        writer.Write($", __MemoryMarshal.Read<{type}>(");
                        writer.Write(value.Substring(offset, size).ToStringLiteral());
                        writer.WriteLine("u8));");
                        offset += size;
                        remaining -= size;
                    }
                    else
                    {
                        writer.Write($"__Unsafe.As<char, {type}>(ref ");
                        writer.WriteIf(offset != 0, "Unsafe.Add(ref dst, offset)");
                        writer.WriteIf(offset == 0, "dst");
                        writer.Write($") = __Unsafe.As<char, {type}>(ref __MemoryMarshal.GetReference(");
                        writer.Write(value.Substring(offset, size).ToStringLiteral());
                        writer.WriteLine(".AsSpan()));");
                        offset += size;
                        remaining -= size;
                    }

                    continue;
                }

                writer.WriteIf(offset != 0, $"__Unsafe.Add(ref dst, {offset})");
                writer.WriteIf(offset == 0, "dst");
                writer.Write(" = ");
                writer.WriteIf(model.TokenType.IsByte(), "(byte)");
                writer.WriteLine($"{value[offset].ToCharLiteral()};");
                offset++;
                remaining--;
            }

            writer.WriteLine($"charsWritten = {value.Length};");
            writer.WriteLine("return global::System.Buffers.OperationStatus.Done;");
        }

        writer.WriteLineIf(!skipLengthCheck, "return global::System.Buffers.OperationStatus.DestinationTooSmall;");
        return true;
    }

    private static void WriteFlagsFormat(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteFlagsFormat));

        string enumName = model.EnumType.FullyQualifiedName;

        writer.WriteLine(
            $"public override global::System.Buffers.OperationStatus TryFormat(global::System.Span<{model.TokenType.Name}> destination, {enumName} value, out int charsWritten)"
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

    private static (int size, string type) GetTypeAndSize(int remaining, ref readonly EnumModel model)
    {
        bool isByte = model.TokenType.IsByte();

        return remaining switch
        {
            >= 8 when isByte => (8, "ulong"),
            >= 4 => isByte ? (4, "uint") : (4, "ulong"),
            >= 2 => isByte ? (2, "ushort") : (2, "uint"),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }
}
