using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

partial class EnumConverterGenerator
{
    private const string MemExt = "global::System.MemoryExtensions";

    private static void WriteParseMethod(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteParseMethod));

        writer.WriteLine("if (source.IsEmpty)");
        using (writer.WriteBlock())
        {
            writer.WriteLine("__Unsafe.SkipInit(out value);");
            writer.WriteLine("return false;");
        }

        writer.WriteLine();

        writer.WriteLine($"ref {model.TokenType.Name} first = ref __MemoryMarshal.GetReference(source);");
        writer.WriteLine();

        // write the fast path if:
        // - enum is small and contiguous from 0 (implies: has no duplicate values)
        // - enum has no names or explicit names that are only 1 char
        if (
            model is { ContiguousFromZero: true, Values.Length: <= 10 }
            && model
                .Values.AsImmutableArray()
                .All(static v => v.Name.Length != 1 && v.ExplicitName is not { Length: 1 })
        )
        {
            writer.DebugLine("Fast path taken (under 10 contiguous values and no 1-char names)");
            writer.WriteLine("// Enum is small and contiguous from 0, try to use fast path");
            writer.WriteLine("if (source.Length == 1)");
            using (writer.WriteBlock())
            {
                writer.WriteLine($"value = ({model.EnumType.FullyQualifiedName})(uint)(first - '0');");
                writer.WriteLine($"return value < ({model.EnumType.FullyQualifiedName}){model.Values.Length};");
            }
        }
        else
        {
            writer.DebugLine("Fast path not taken (10 contiguous values and no 1-char names)");
            WriteNumberCheck(in model, writer, cancellationToken);
        }

        // flags enums can be valid even though the value is not defined, e.g. 1 | 2
        writer.WriteLine();
        writer.WriteIf(!model.HasFlagsAttribute, "else ");

        writer.WriteLineIf(model.HasFlagsAttribute, "// flags-enum can have a valid value outside the explicit values");
        writer.WriteLine("if (_parseStrategy.TryParse(source, out value))");
        using (writer.WriteBlock())
        {
            writer.WriteLine("return true;");
        }

        writer.WriteLine();
        writer.WriteLine("// unknown value, defer to Enum.TryParse");

        if (model.TokenType.IsByte())
        {
            writer.WriteLine("return TryParseFromUtf16(source, out value);");
        }
        else
        {
            writer.WriteLine("return global::System.Enum.TryParse(source, _ignoreCase, out value)");
            writer.IncreaseIndent();
            writer.WriteLine("&& (_allowUndefinedValues || IsDefined(value));");
            writer.DecreaseIndent();
        }
    }

    private static void WriteNumberCheck(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteNumberCheck));

        writer.WriteLine("// check if value starts with a digit");
        writer.Write("if ((uint)(first - '0') <= 9u");

        writer.WriteLine(model.Values.AsImmutableArray().Any(static v => v.Value < 0) ? " || first == '-')" : ")");

        using IndentedTextWriter.Block block1 = writer.WriteBlock();
        writer.WriteLine("switch (source.Length)");
        using IndentedTextWriter.Block block2 = writer.WriteBlock();

        IEnumerable<IGrouping<int, (string name, BigInteger value)>> entries = model
            .UniqueValues.Select(m => (name: m.ToString(), value: m))
            .GroupBy(m => m.name.Length)
            .ToList();

        foreach (var group in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteLine($"case {group.Key}:");
            using (writer.WriteBlock())
            {
                // only positive values and all values in the 0..9 range are valid
                if (!model.HasNegativeValues && group.Key == 1 && group.Count() == 10)
                {
                    writer.DebugLine("Fast path taken: all values in the 0..9 range are valid");
                    writer.WriteLine("// all values in the 0..9 range are valid");
                    writer.WriteLine($"value = ({model.EnumType.FullyQualifiedName})(uint)(first - '0');");
                    writer.WriteLine("return true;");
                    continue;
                }

                // optimized alternative for 1-length integers
                if (group.Key == 1)
                {
                    writer.DebugLine("Special case: 1-length integers");
                    writer.WriteLine("switch (first)");
                    using (writer.WriteBlock())
                    {
                        foreach (var entry in group)
                        {
                            writer.Write("case ");
                            writer.WriteIf(model.TokenType.IsByte(), "(byte)");
                            writer.Write(entry.name[0].ToCharLiteral());
                            writer.Write($": value = ({model.EnumType.FullyQualifiedName})");
                            writer.WriteIf(entry.value < 0, "(");
                            writer.Write(entry.value.ToString());
                            writer.WriteIf(entry.value < 0, ")");
                            writer.WriteLine("; return true;");
                        }
                    }

                    writer.WriteLine("break;");
                    continue;
                }

                if (group.Key == 2)
                {
                    writer.DebugLine("Special case: 2-length integers");
                    WriteSwitchTwoCharacters(in model, 0, writer, group, cancellationToken);
                    writer.WriteLine("break;");
                    continue;
                }

                writer.DebugLine("Slow path: over 2-length integers");
                writer.WriteLine("switch (first)");
                using (writer.WriteBlock())
                {
                    foreach (var firstCharGroup in group.GroupBy(g => g.name[0]))
                    {
                        writer.Write("case ");
                        writer.WriteIf(model.TokenType.IsByte(), "(byte)");
                        writer.WriteLine($"{firstCharGroup.Key.ToCharLiteral()}:");

                        using (writer.WriteBlock())
                        {
                            foreach (var entry in firstCharGroup)
                            {
                                writer.Write($"if ({MemExt}.EndsWith(source, \"{entry.name[1..]}\"");
                                writer.WriteLine(model.TokenType.IsByte() ? "u8))" : "))");
                                using (writer.WriteBlock())
                                {
                                    writer.WriteLine($"value = ({model.EnumType.FullyQualifiedName}){entry.value};");
                                    writer.WriteLine("return true;");
                                }
                            }

                            writer.WriteLine("break;");
                        }
                    }
                }

                writer.WriteLine("break;");
            }
        }
    }

    private static void WriteSwitch(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteSwitch));

        writer.WriteLine(
            $"public override bool TryParse(global::System.ReadOnlySpan<{model.TokenType.Name}> source, out {model.EnumType.FullyQualifiedName} value)"
        );
        using var block = writer.WriteBlock();

        writer.WriteLine($"ref {model.TokenType.Name} first = ref __MemoryMarshal.GetReference(source);");
        writer.WriteLine();

        var entries = GetEntries(model, ignoreCase, cancellationToken);

        writer.WriteLine("switch (source.Length)");

        bool isByte = model.TokenType.IsByte();

        using (writer.WriteBlock())
        {
            IEnumerable<IGrouping<int, Entry>> lengthGroups = isByte
                ? entries.GroupBy(e => Encoding.UTF8.GetByteCount(e.Name))
                : entries.GroupBy(e => e.Length);

            foreach (var lengthGroup in lengthGroups)
            {
                writer.WriteLine($"case {lengthGroup.Key}:");

                using (writer.WriteBlock())
                {
                    if (isByte)
                    {
                        if (!ignoreCase)
                        {
                            WriteStringMatchByteAsciiOrdinal(in model, writer, lengthGroup, cancellationToken);
                        }
                        else if (lengthGroup.All(e => e.Name.IsAscii()))
                        {
                            WriteStringMatchByte(in model, writer, ignoreCase, lengthGroup, cancellationToken);
                        }
                        else if (
                            lengthGroup.All(g =>
                                g.Name.ToLowerInvariant() == g.Name && g.Name.ToUpperInvariant() == g.Name
                            )
                        )
                        {
                            WriteStringMatchByteAsciiOrdinal(
                                in model,
                                writer,
                                lengthGroup,
                                cancellationToken,
                                forceIfChain: true
                            );
                        }
                        else
                        {
                            int len = Math.Min(32, lengthGroup.Max(e => e.Name.Length));
                            len = (int)Math.Pow(2, Math.Ceiling(Math.Log(len) / Math.Log(2)));

                            writer.DebugLine("Slow path: not ascii and not case-agnostic");
                            writer.WriteLine(
                                $"global::System.ReadOnlySpan<char> source_chars = GetChars(source, stackalloc char[{len}], out char[]? toReturn);"
                            );
                            writer.WriteLine("bool retVal = false;");
                            writer.WriteLine("__Unsafe.SkipInit(out value);");
                            writer.WriteLine();
                            WriteStringMatchChar(in model, writer, ignoreCase, lengthGroup, cancellationToken);

                            writer.WriteLine(
                                "if (toReturn is not null) global::System.Buffers.ArrayPool<char>.Shared.Return(toReturn, clearArray: true);"
                            );

                            writer.WriteLine("if (retVal) return true;");
                            writer.WriteLine("break;");
                        }
                    }
                    else
                    {
                        WriteStringMatchChar(in model, writer, ignoreCase, lengthGroup, cancellationToken);
                    }
                }
            }

            writer.WriteLine("default:");
            writer.IncreaseIndent();
            writer.WriteLine("break;");
            writer.DecreaseIndent();
        }

        writer.WriteLine();
        writer.WriteLine("__Unsafe.SkipInit(out value);");
        writer.WriteLine("return false;");
    }

    private static void WriteStringMatchChar(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteStringMatchChar));

        bool isByte = model.TokenType.IsByte();
        string sourceName = isByte ? "source_chars" : "source";

        if (entriesByLength.Count() == 1 || entriesByLength.Any(x => x.Name.ContainsSurrogates()))
        {
            writer.DebugLine("Direct comparison: single entry or contains surrogates");
            foreach (var single in entriesByLength)
            {
                writer.Write($"if ({MemExt}.Equals({sourceName}, ");
                writer.Write(single.Name.ToStringLiteral());
                writer.Write(", global::System.StringComparison.Ordinal");

                if (ignoreCase)
                {
                    writer.Write("IgnoreCase");
                }

                writer.WriteLine("))");

                using (writer.WriteBlock())
                {
                    writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{single.MemberName};");
                    writer.WriteLine(isByte ? "retVal = true;" : "return true;");
                }
            }

            writer.WriteLineIf(!isByte, "break;");
            return;
        }

        writer.Write("switch (");

        if (isByte)
        {
            if (!ignoreCase)
            {
                writer.WriteLine($"{sourceName}[0])");
            }
            else if (entriesByLength.All(e => e.Name.IsAscii()))
            {
                writer.WriteLine($"(char)({sourceName}[0] | 0x20))");
            }
            else
            {
                writer.DebugLine("Slow path: ignore case and not ascii");
                writer.WriteLine($"char.ToLowerInvariant({sourceName}[0]))");
            }
        }
        else
        {
            if (!ignoreCase)
            {
                writer.WriteLine("first)");
            }
            else if (entriesByLength.All(e => e.Name.IsAscii()))
            {
                writer.WriteLine("(char)(first | 0x20))");
            }
            else
            {
                writer.DebugLine("Slow path: ignore case and not ascii");
                writer.WriteLine("char.ToLowerInvariant(first))");
            }
        }

        using (writer.WriteBlock())
        {
            foreach (var group in entriesByLength.GroupBy(g => g.FirstChar).OrderByDescending(g => g.Count()))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = ignoreCase ? char.ToLowerInvariant(group.Key) : group.Key;

                int groupCount = group.Count();
                bool writeBreak = groupCount > 1 || group.All(e => e.Name.Length > 1);

                writer.WriteLine($"case {key.ToCharLiteral()}:");
                using (writer.WriteBlock())
                {
                    // the unrolled Equals for chars automatically does length checks
                    foreach (var entry in group)
                    {
                        if (entry.Name.Length > 1)
                        {
                            writer.Write($"if ({MemExt}.EndsWith({sourceName}, ");
                            writer.Write(entry.Name[1..].ToStringLiteral());
                            writer.Write(", global::System.StringComparison.Ordinal");

                            if (ignoreCase)
                            {
                                writer.Write("IgnoreCase");
                            }

                            writer.WriteLine("))");
                        }

                        writer.DebugLineIf(entry.Name.Length == 1, "Only one value, do a single comparison");

                        using (writer.WriteBlockIf(writeBreak))
                        {
                            writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{entry.MemberName};");
                            writer.WriteLine(isByte ? "retVal = true;" : "return true;");
                        }
                    }

                    writer.WriteLineIf(writeBreak, "break;");
                }
            }
        }

        writer.WriteLineIf(!isByte, "break;");
    }

    private static void WriteStringMatchByteAsciiOrdinal(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken,
        bool forceIfChain = false
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteStringMatchByteAsciiOrdinal));

        if (forceIfChain || entriesByLength.Count() < 3 || entriesByLength.Any(e => e.Name.ContainsSurrogates()))
        {
            writer.DebugLineIf(forceIfChain, "if-chain forced");
            writer.DebugLineIf(!forceIfChain, "< 3 values, or contains surrogates");

            if (forceIfChain)
            {
                writer.Write("// case-agnostic value");
                writer.WriteIf(entriesByLength.Count() > 1, "s");
                writer.WriteLine();
            }

            foreach (var single in entriesByLength)
            {
                writer.Write("if (source.SequenceEqual(");
                writer.Write(single.Name.ToStringLiteral());
                writer.WriteLine("u8))");

                using (writer.WriteBlock())
                {
                    writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{single.MemberName};");
                    writer.WriteLine("return true;");
                }
            }

            writer.WriteLine("break;");
            return;
        }

        writer.WriteLine("switch (first)");

        using (writer.WriteBlock())
        {
            foreach (var group in entriesByLength.GroupBy(g => g.FirstChar))
            {
                cancellationToken.ThrowIfCancellationRequested();

                writer.WriteLine($"case (byte){group.Key.ToCharLiteral()}:");

                bool writeBlock = group.Count() > 1 || group.All(e => e.Name.Length > 1);

                using (writer.WriteBlock())
                {
                    // the unrolled Equals for chars automatically does length checks
                    foreach (var entry in group)
                    {
                        if (entry.Name.Length > 1)
                        {
                            writer.Write($"if ({MemExt}.EndsWith(source, ");
                            writer.Write(entry.Name[1..].ToStringLiteral());
                            writer.WriteLine("u8))");
                        }

                        using (writer.WriteBlockIf(writeBlock))
                        {
                            writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{entry.MemberName};");
                            writer.WriteLine("return true;");
                        }
                    }

                    writer.WriteLineIf(writeBlock, "break;");
                }
            }
        }

        writer.WriteLine("break;");
    }

    private static void WriteStringMatchByte(
        ref readonly EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteStringMatchByte));

        string enumTypeFullName = model.EnumType.FullyQualifiedName;

        List<Entry> outerEntries = PooledList<Entry>.Acquire();
        outerEntries.AddRange(entriesByLength);
        WriteNestedAsciiSwitch(0, outerEntries);
        PooledList<Entry>.Release(outerEntries);
        writer.WriteLine("break;");

        void WriteNestedAsciiSwitch(int depth, List<Entry> entries)
        {
            bool isMaxDepth = depth == (entriesByLength.Key) - 1;

            if (isMaxDepth || entries.Count <= 3 || entries.TrueForAll(m => m.Name[depth] == entries[0].Name[depth]))
            {
                writer.DebugLine("Less than 3 entries, reached max depth, or all same char");

                foreach (var entry in entries)
                {
                    writer.Write("if (");
                    writer.IncreaseIndent();

                    int i = depth;

                    while (i < entry.Length)
                    {
                        if ((entry.Length - i) >= 8)
                        {
                            WriteUnrolled(8);
                            i += 8;
                        }
                        else if ((entry.Length - i) >= 4)
                        {
                            WriteUnrolled(4);
                            i += 4;
                        }
                        else
                        {
                            char compare = entry.Name[i];

                            if (ignoreCase)
                            {
                                writer.WriteIf(compare.IsAsciiLetter(), '(');
                                WriteIndexAccess(writer, i);
                                writer.Write(compare.IsAsciiLetter() ? " | 0x20) == " : " == ");
                                writer.Write(
                                    compare.IsAsciiLetter()
                                        ? char.ToLowerInvariant(compare).ToCharLiteral()
                                        : compare.ToCharLiteral()
                                );
                            }
                            else
                            {
                                WriteIndexAccess(writer, i);
                                writer.Write($" == {compare.ToCharLiteral()}");
                            }

                            i++;
                        }

                        if (i != entry.Length)
                        {
                            writer.WriteLine(" &&");
                        }
                    }

                    writer.DecreaseIndent();
                    writer.WriteLine(")");
                    using (writer.WriteBlock())
                    {
                        writer.WriteLine($"value = {enumTypeFullName}.{entry.MemberName};");
                        writer.WriteLine("return true;");
                    }

                    void WriteUnrolled(int count)
                    {
                        string type = count == 8 ? "ulong" : "uint";

                        writer.Write("__MemoryMarshal.Read<");
                        writer.Write(type);
                        writer.Write(">(");

                        Span<char> chars = stackalloc char[count];
                        entry.Name.AsSpan(i, count).CopyTo(chars);
                        foreach (ref char c in chars)
                            c = char.ToLowerInvariant(c);
                        var littleEndian = chars.ToString().ToStringLiteral();

                        writer.Write($"{littleEndian}u8)");
                        writer.Write(" == ");

                        writer.WriteIf(ignoreCase, "(");
                        writer.Write($"__Unsafe.ReadUnaligned<{type}>(ref ");

                        if (i != 0)
                        {
                            writer.Write($"__Unsafe.Add(ref first, {i}))");
                        }
                        else
                        {
                            writer.Write("first)");
                        }

                        if (ignoreCase)
                        {
                            writer.Write(" | ");

                            Span<bool> bytes = stackalloc bool[count];
                            bool allLetters = true;

                            for (int byteIndex = 0; byteIndex < count; byteIndex++)
                            {
                                bool isLetter = entry.Name[i + byteIndex].IsAsciiLetter();
                                bytes[byteIndex] = isLetter;
                                if (!isLetter)
                                    allLetters = false;
                            }

                            bytes.Reverse();

                            if (!allLetters)
                            {
                                writer.Write("(__BitConverter.IsLittleEndian ? ");
                            }

                            writer.Write("0x");

                            foreach (var b in bytes)
                                writer.Write(b ? "20" : "00");
                            writer.Write(count == 8 ? "UL" : "U");

                            if (!allLetters)
                            {
                                writer.Write(" : 0x");
                                bytes.Reverse();
                                foreach (var b in bytes)
                                    writer.Write(b ? "20" : "00");
                                writer.Write(count == 8 ? "UL)" : "U)");
                            }

                            writer.Write(")");
                        }
                    }
                }
            }
            else
            {
                writer.DebugLine("Build a deeper switch; more than 3 entries and not all same char");

                List<Entry> innerEntries = PooledList<Entry>.Acquire();

                writer.Write("switch (");
                writer.WriteIf(ignoreCase, "(");
                WriteIndexAccess(writer, depth);
                writer.WriteIf(ignoreCase, " | 0x20)");
                writer.WriteLine(")");

                using (writer.WriteBlock())
                {
                    foreach (
                        var firstChar in entries.GroupBy(e => ignoreCase ? char.ToLowerInvariant(e.Name[0]) : e.Name[0])
                    )
                    {
                        innerEntries.Clear();
                        innerEntries.AddRange(firstChar);

                        writer.WriteLine($"case {firstChar.Key.ToCharLiteral()}:");
                        using (writer.WriteBlock())
                        {
                            WriteNestedAsciiSwitch(depth + 1, innerEntries);
                            writer.WriteLine("break;");
                        }
                    }
                }

                PooledList<Entry>.Release(innerEntries);
            }
        }
    }

    private static void WriteParseSlow(IndentedTextWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteParseSlow));

        writer.WriteLine(
            "global::System.ReadOnlySpan<char> chars = GetChars(source, stackalloc char[32], out char[]? toReturn);"
        );
        writer.WriteLine(
            "bool retVal = global::System.Enum.TryParse(chars, _ignoreCase, out value) && (_allowUndefinedValues || IsDefined(value));"
        );
        writer.WriteLine(
            "if (toReturn is not null) global::System.Buffers.ArrayPool<char>.Shared.Return(toReturn, clearArray: true);"
        );
        writer.WriteLine("return retVal;");
    }

    private readonly record struct Entry(char FirstChar, string Name, string MemberName) : IComparable<Entry>
    {
        public int Length => Name.Length;

        public int CompareTo(Entry other)
        {
            int cmp = Length.CompareTo(other.Length);
            if (cmp == 0)
                cmp = FirstChar.CompareTo(other.FirstChar);
            return cmp;
        }
    }

    private static IEnumerable<Entry> GetEntries(EnumModel model, bool ignoreCase, CancellationToken cancellationToken)
    {
        foreach (var value in model.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            char firstChar = ignoreCase ? char.ToLowerInvariant(value.Name[0]) : value.Name[0];
            yield return new Entry(firstChar, value.Name, value.Name);

            if (value.ExplicitName is not null)
            {
                firstChar = ignoreCase ? char.ToLowerInvariant(value.ExplicitName[0]) : value.ExplicitName[0];
                yield return new Entry(firstChar, value.ExplicitName, value.Name);
            }
        }
    }

    private static void WriteGetChars(IndentedTextWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteGetChars));

        writer.WriteLine("global::System.Span<char> destination;");
        writer.WriteLine("int length = global::System.Text.Encoding.UTF8.GetMaxCharCount(source.Length);");
        writer.WriteLine();
        writer.WriteLine(
            "if (length <= buffer.Length || (length = global::System.Text.Encoding.UTF8.GetCharCount(source)) <= buffer.Length)"
        );
        using (writer.WriteBlock())
        {
            writer.WriteLine("destination = buffer;");
            writer.WriteLine("toReturn = null;");
        }

        writer.WriteLine("else");
        using (writer.WriteBlock())
        {
            writer.WriteLine("toReturn = global::System.Buffers.ArrayPool<char>.Shared.Rent(length);");
            writer.WriteLine("destination = toReturn;");
        }

        writer.WriteLine();
        writer.WriteLine("int written = global::System.Text.Encoding.UTF8.GetChars(source, destination);");
        writer.WriteLine("return destination.Slice(0, written);");
    }

    private static void WriteSwitchTwoCharacters(
        ref readonly EnumModel model,
        int offset,
        IndentedTextWriter writer,
        IEnumerable<(string name, BigInteger value)> values,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteSwitchTwoCharacters));

        (string type, int shift) = model.TokenType.IsByte() ? ("ushort", 8) : ("uint", 16);

        writer.Write(type);
        writer.Write(" __mask = ");
        writer.Write("__Unsafe.ReadUnaligned<");
        writer.Write(type);
        writer.Write(">(ref ");

        if (model.TokenType.IsByte())
        {
            WriteIndexAccess(writer, offset);
        }
        else
        {
            writer.Write("__Unsafe.As<char, byte>(ref ");
            WriteIndexAccess(writer, offset);
            writer.Write(")");
        }

        writer.WriteLine(");");
        writer.WriteLine("if (!__BitConverter.IsLittleEndian) __mask = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(__mask);");
        writer.WriteLine("switch (__mask)");

        using (writer.WriteBlock())
        {
            foreach (var entry in values)
            {
                writer.Write("case (");
                writer.Write(entry.name[0].ToCharLiteral());
                writer.Write(" | (");
                writer.Write(entry.name[1].ToCharLiteral());
                writer.Write($" << {shift})): value = ({model.EnumType.FullyQualifiedName})");
                writer.WriteIf(entry.value < 0, "(");
                writer.Write(entry.value.ToString());
                writer.WriteIf(entry.value < 0, ")");
                writer.WriteLine("; return true;");
            }
        }
    }

    private static void WriteIndexAccess(IndentedTextWriter writer, int depth)
    {
        if (depth == 0)
        {
            writer.Write("first");
            return;
        }

        // TODO: remove when JIT is smart enough to optimize the bounds checks
        // on net9, byte ignorecase tryparse for System.TypeCode produces 755 bytes of ASM with this (vs 934 without)
        writer.Write($"__Unsafe.Add(ref first, {depth})");
    }
}
