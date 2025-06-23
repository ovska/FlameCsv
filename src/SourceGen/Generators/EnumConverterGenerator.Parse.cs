using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

partial class EnumConverterGenerator
{
    private const string MemExt = "global::System.MemoryExtensions";

    private static void WriteParseMethod(
        EnumModel model,
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

        writer.WriteLine($"ref {model.Token} first = ref __MemoryMarshal.GetReference(source);");
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
            writer.WriteLine("// Enum is small and contiguous from 0, and has no 1 length names; try to use fast path");
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
            WriteNumberCheck(model, writer, cancellationToken);
        }

        // flags enums can be valid even though the value is not defined, e.g. 1 | 2
        writer.WriteIf(!model.HasFlagsAttribute, "else ");

        writer.WriteLineIf(model.HasFlagsAttribute);
        writer.WriteLineIf(model.HasFlagsAttribute, "// flags-enum can have a valid value outside the explicit values");
        writer.WriteLine("if (_parseStrategy.TryParse(source, out value))");
        using (writer.WriteBlock())
        {
            writer.WriteLine("return true;");
        }

        writer.WriteLine();
        writer.WriteLine("// unknown value, defer to Enum.TryParse");

        if (model.IsByte)
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
        EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteNumberCheck));

        writer.WriteLine("// check if value starts with a digit");
        writer.Write("if ((uint)(first - '0') <= 9u");
        writer.WriteIf(model.Values.AsImmutableArray().Any(static v => v.Value < 0), " || first == '-'");
        writer.WriteLine(")");

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
                        foreach ((string name, BigInteger value) in group)
                        {
                            writer.Write("case ");
                            writer.WriteIf(model.IsByte, "(byte)");
                            writer.Write(name[0].ToCharLiteral());
                            writer.Write($": value = ({model.EnumType.FullyQualifiedName})");
                            writer.WriteIf(value < 0, "(");
                            writer.Write(value.ToString());
                            writer.WriteIf(value < 0, ")");
                            writer.WriteLine("; return true;");
                        }
                    }

                    writer.WriteLine("break;");
                    continue;
                }

                if (group.Key == 2)
                {
                    writer.DebugLine("Special case: 2-length integers");
                    WriteSwitchTwoCharacters(model, 0, writer, group, cancellationToken);
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
                        writer.WriteIf(model.IsByte, "(byte)");
                        writer.WriteLine($"{firstCharGroup.Key.ToCharLiteral()}:");

                        using (writer.WriteBlock())
                        {
                            foreach ((string name, BigInteger value) in firstCharGroup)
                            {
                                writer.Write($"if ({MemExt}.EndsWith(source, \"{name[1..]}\"");
                                writer.WriteLine(model.IsByte ? "u8))" : "))");
                                using (writer.WriteBlock())
                                {
                                    writer.WriteLine($"value = ({model.EnumType.FullyQualifiedName}){value};");
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
        EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteSwitch));

        writer.WriteLine(
            $"public override bool TryParse(global::System.ReadOnlySpan<{model.Token}> source, out {model.EnumType.FullyQualifiedName} value)"
        );
        using var block = writer.WriteBlock();

        writer.WriteLine($"ref {model.Token} first = ref __MemoryMarshal.GetReference(source);");
        writer.WriteLine();

        var entries = GetEntries(model, ignoreCase, cancellationToken);

        writer.WriteLine("switch (source.Length)");

        bool isByte = model.IsByte;

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
                            // case-sensitive comparisons always use SequenceEqual (autovectorized by JIT)
                            WriteStringMatchByteAsciiOrdinal(model, writer, lengthGroup, cancellationToken);
                        }
                        else if (lengthGroup.All(e => e.Name.IsAscii()))
                        {
                            // all entries are ASCII, so we can use a fast path
                            WriteStringMatchByteIgnoreCase(model, writer, lengthGroup, cancellationToken);
                        }
                        else
                        {
                            int groupLength = lengthGroup.Count();

                            List<Entry> caseAgnosticValues = PooledList<Entry>.Acquire();
                            List<Entry> complexValues = PooledList<Entry>.Acquire();

                            foreach (var entry in lengthGroup)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                (entry.Name.IsCaseAgnostic() ? caseAgnosticValues : complexValues).Add(entry);
                            }

                            if (caseAgnosticValues.Count() != 0)
                            {
                                bool allHandled = complexValues.Count == 0;

                                // all entries are case-agnostic
                                WriteStringMatchByteAsciiOrdinal(
                                    model,
                                    writer,
                                    caseAgnosticValues.AsGrouping(lengthGroup.Key),
                                    cancellationToken,
                                    caseAgnostic: true,
                                    writeTrailingBreak: allHandled
                                );

                                writer.WriteLineIf(!allHandled);
                            }

                            if (complexValues.Count != 0)
                            {
                                var complexGroup = complexValues.AsGrouping(lengthGroup.Key);

                                if (complexGroup.All(e => e.Name.IsAscii()))
                                {
                                    writer.DebugLine("Remaining values are all ASCII, use fast path");
                                    WriteStringMatchByteIgnoreCase(model, writer, complexGroup, cancellationToken);
                                }
                                else
                                {
                                    // there are non-case agnostic non-ascii entries, use the slowest path

                                    // get the max length of a valid entry
                                    int stackallocLength = Math.Min(128, lengthGroup.Max(e => e.Name.Length));

                                    writer.DebugLine("Slow path: not ascii and not case-agnostic");
                                    writer.WriteLine(
                                        $"global::System.ReadOnlySpan<char> source_chars = GetChars(source, stackalloc char[{stackallocLength}], out char[]? toReturn);"
                                    );
                                    writer.WriteLine("bool retVal = false;");
                                    writer.WriteLine("__Unsafe.SkipInit(out value);");
                                    writer.WriteLine();
                                    WriteStringMatchChar(model, writer, ignoreCase, complexGroup, cancellationToken);

                                    writer.WriteLine(
                                        "if (toReturn is not null) global::System.Buffers.ArrayPool<char>.Shared.Return(toReturn, clearArray: true);"
                                    );

                                    writer.WriteLine("if (retVal) return true;");
                                    writer.WriteLine("break;");
                                }
                            }

                            PooledList<Entry>.Release(caseAgnosticValues);
                            PooledList<Entry>.Release(complexValues);
                        }
                    }
                    else
                    {
                        WriteStringMatchChar(model, writer, ignoreCase, lengthGroup, cancellationToken);
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
        EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteStringMatchChar));

        bool isByte = model.IsByte;
        string sourceName = isByte ? "source_chars" : "source";

        // up to 3 entries provides marginal performance benefits before falling back to the switch
        if (entriesByLength.Count() <= 3 || entriesByLength.Any(x => x.Name.ContainsSurrogates()))
        {
            writer.DebugLine("Direct comparison: <= 3 entries, or any of the values contain surrogates");
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

        string firstItem = isByte ? "source[0]" : "first";

        if (
            !ignoreCase
            || entriesByLength.All(e => char.ToLowerInvariant(e.Name[0]) == char.ToUpperInvariant(e.Name[0]))
        )
        {
            // if all first chars are case-agnostic, we can use the simple switch path
            writer.WriteLine($"switch ({firstItem})");
        }
        else if (entriesByLength.All(e => e.Name[0].IsAsciiLetter()))
        {
            // if all first chars are ascii letters, we can use bitwise lowercase
            writer.WriteLine($"switch ((char)({firstItem} | 0x20))");
        }
        else
        {
            writer.DebugLine("Slow path: ignore case and not ascii");
            writer.WriteLine($"switch (char.ToLowerInvariant({firstItem}))");
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
        EnumModel model,
        IndentedTextWriter writer,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken,
        bool caseAgnostic = false,
        bool writeTrailingBreak = true
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteStringMatchByteAsciiOrdinal));

        if (caseAgnostic || entriesByLength.Count() < 3 || entriesByLength.Any(e => e.Name.ContainsSurrogates()))
        {
            writer.DebugLineIf(caseAgnostic, "if-chain forced");
            writer.DebugLineIf(!caseAgnostic, "< 3 values, or contains surrogates");

            writer.WriteLineIf(caseAgnostic, "// case-agnostic value(s)");

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

            writer.WriteLineIf(writeTrailingBreak, "break;");
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

        writer.WriteLineIf(writeTrailingBreak, "break;");
    }

    private static void WriteStringMatchByteIgnoreCase(
        EnumModel model,
        IndentedTextWriter writer,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteStringMatchByteIgnoreCase));

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

                    int offset = depth;

                    while (offset < entry.Length)
                    {
                        int remaining = entry.Length - offset;

                        if (remaining >= 16)
                        {
                            writer.WriteLine("(__Vector128.IsHardwareAccelerated");
                            writer.IncreaseIndent();
                            writer.Write("? (");
                            WriteByteAsciiLowercaseCheckVectorized(writer, entry, offset);
                            writer.WriteLine(")");
                            writer.Write(": (");
                            WriteByteAsciiLowercaseCheck(writer, entry, offset, 8);
                            writer.Write(" && ");
                            WriteByteAsciiLowercaseCheck(writer, entry, offset + 8, 8);
                            writer.DecreaseIndent();
                            writer.WriteLine();
                            writer.Write("))");
                            offset += 16;
                        }
                        else if (remaining >= 8)
                        {
                            WriteByteAsciiLowercaseCheck(writer, entry, offset, 8);
                            offset += 8;
                        }
                        else if (remaining >= 4)
                        {
                            WriteByteAsciiLowercaseCheck(writer, entry, offset, 4);
                            offset += 4;
                        }
                        else
                        {
                            char compare = entry.Name[offset];
                            bool isLetter = compare.IsAsciiLetter();

                            writer.WriteIf(isLetter, '(');
                            WriteIndexAccess(writer, offset);
                            writer.Write(isLetter ? " | 0x20) == " : " == ");
                            writer.Write(char.ToLowerInvariant(compare).ToCharLiteral());

                            offset++;
                        }

                        writer.WriteLineIf(offset != entry.Length, " &&");
                    }

                    writer.DecreaseIndent();
                    writer.WriteLine(")");
                    using (writer.WriteBlock())
                    {
                        writer.WriteLine($"value = {enumTypeFullName}.{entry.MemberName};");
                        writer.WriteLine("return true;");
                    }
                }
            }
            else
            {
                writer.DebugLine("Build a deeper switch; more than 3 entries and not all same char");

                List<Entry> innerEntries = PooledList<Entry>.Acquire();

                writer.Write("switch ((");
                WriteIndexAccess(writer, depth);
                writer.WriteLine(" | 0x20))");

                using (writer.WriteBlock())
                {
                    foreach (var firstChar in entries.GroupBy(e => char.ToLowerInvariant(e.Name[0])))
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
        EnumModel model,
        int offset,
        IndentedTextWriter writer,
        IEnumerable<(string name, BigInteger value)> values,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.DebugLine(nameof(WriteSwitchTwoCharacters));

        (string type, int shift) = model.IsByte ? ("ushort", 8) : ("uint", 16);

        writer.Write(type);
        writer.Write(" __mask = ");
        writer.Write("__Unsafe.ReadUnaligned<"); // TODO: MemoryMarshal.Read in .NET 10 should have fixed bounds checks
        writer.Write(type);
        writer.Write(">(ref ");

        if (model.IsByte)
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
        writer.WriteLine(
            "if (!__BitConverter.IsLittleEndian) __mask = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(__mask);"
        );
        writer.WriteLine("switch (__mask)");

        using (writer.WriteBlock())
        {
            foreach ((string name, BigInteger value) in values)
            {
                writer.Write("case (");
                writer.Write(name[0].ToCharLiteral());
                writer.Write(" | (");
                writer.Write(name[1].ToCharLiteral());
                writer.Write($" << {shift})): value = ({model.EnumType.FullyQualifiedName})");
                writer.WriteIf(value < 0, "(");
                writer.Write(value.ToString());
                writer.WriteIf(value < 0, ")");
                writer.WriteLine("; return true;");
            }
        }
    }

    private static void WriteIndexAccess(IndentedTextWriter writer, int offset)
    {
        if (offset == 0)
        {
            writer.Write("first");
            return;
        }

        // TODO: remove when JIT is smart enough to optimize the bounds checks with switch/case
        // e.g.: on net9, byte ignorecase tryparse for System.TypeCode produces 755 vs 934 bytes of ASM
        // should be: source[offset]
        writer.Write($"__Unsafe.Add(ref first, {offset})");
    }

    /// <summary>
    /// Writes a case-insensitive check for <paramref name="width"/> bytes at a time against the entry's name at
    /// <paramref name="offset"/>.
    /// </summary>
    /// <param name="writer">Writer</param>
    /// <param name="entry">Entry to write</param>
    /// <param name="offset">Offset from the start of the input</param>
    /// <param name="width">Number of characters checked</param>
    private static void WriteByteAsciiLowercaseCheck(IndentedTextWriter writer, in Entry entry, int offset, int width)
    {
        string type = width == 8 ? "ulong" : "uint";

        writer.Write("__MemoryMarshal.Read<");
        writer.Write(type);
        writer.Write(">(");
        writer.Write(entry.Name.Substring(offset, width).ToLowerInvariant().ToStringLiteral());
        writer.Write("u8) == ");

        writer.Write($"(__Unsafe.ReadUnaligned<{type}>(ref ");

        writer.WriteIf(offset != 0, $"__Unsafe.Add(ref first, {offset}))");
        writer.WriteIf(offset == 0, "first)");

        writer.Write(" | ");

        Span<bool> bytes = stackalloc bool[width];
        bool isSymmetric = GetMaskLittleEndian(bytes, entry.Name, offset, out _);

        if (!isSymmetric)
        {
            writer.Write("(__BitConverter.IsLittleEndian ? ");
        }

        writer.Write("0x");

        foreach (var b in bytes)
            writer.Write(b ? "20" : "00");
        writer.Write(width == 8 ? "UL" : "U");

        if (!isSymmetric)
        {
            writer.Write(" : 0x");
            bytes.Reverse();
            foreach (var b in bytes)
                writer.Write(b ? "20" : "00");
            writer.Write(width == 8 ? "UL)" : "U)");
        }

        writer.Write(")");
    }

    /// <summary>
    /// Writes a case-insensitive check for <paramref name="width"/> bytes at a time against the entry's name at
    /// <paramref name="offset"/>.
    /// </summary>
    /// <param name="writer">Writer</param>
    /// <param name="entry">Entry to write</param>
    /// <param name="offset">Offset from the start of the input</param>
    /// <param name="width">Number of characters checked</param>
    private static int WriteByteAsciiLowercaseCheckVectorized(IndentedTextWriter writer, in Entry entry, int offset)
    {
        const int width = 128 / 8;

        writer.Write($"__Vector128.LoadUnsafe(in ");
        writer.Write(entry.Name.Substring(offset, width).ToLowerInvariant().ToStringLiteral());
        writer.Write("u8[0]) == ");

        Span<bool> bytes = stackalloc bool[width];
        _ = GetMaskLittleEndian(bytes, entry.Name, offset, out bool? allSame);

        writer.WriteIf(allSame is null or true, "(");
        writer.Write($"__Vector128.LoadUnsafe(in first");
        writer.WriteIf(offset != 0, $", {offset}");
        writer.Write(")");

        if (allSame is null)
        {
            writer.Write($" | __Vector128.Create(");

            for (int i = 0; i < width; i++)
            {
                writer.Write(bytes[i] ? "0x20" : "0x00");

                if (i < width - 1)
                {
                    writer.Write(", ");
                }
            }

            writer.Write(")");
        }
        else if (allSame.Value == true)
        {
            writer.Write($" | __Vector128.Create((byte)0x20))");
        }

        return 128 / 8;
    }

    /// <summary>
    /// Returns mask for the given <paramref name="value"/> in little-endian format,
    /// where values are <c>true</c> for bytes that are case-sensitive.
    /// </summary>
    /// <param name="span"></param>
    /// <param name="value"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static bool GetMaskLittleEndian(Span<bool> span, string value, int offset, out bool? allSame)
    {
        int trueCount = 0;
        int falseCount = 0;

        for (int byteIndex = 0; byteIndex < span.Length; byteIndex++)
        {
            bool isLetter = value[offset + byteIndex].IsAsciiLetter();
            span[byteIndex] = isLetter;

            if (isLetter)
            {
                trueCount++;
            }
            else
            {
                falseCount++;
            }
        }

        allSame =
            trueCount == 0 ? false
            : falseCount == 0 ? true
            : null;

        // if all values are the same, we are guaranteed to be symmetric
        if (allSame.HasValue)
        {
            return true;
        }

        span.Reverse();

        for (int i = 0; i < span.Length / 2; i++)
        {
            if (span[i] != span[span.Length - 1 - i])
            {
                return false;
            }
        }
        return true;
    }
}
