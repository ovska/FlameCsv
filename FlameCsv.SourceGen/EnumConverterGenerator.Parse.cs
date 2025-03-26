using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class EnumConverterGenerator
{
    private static void WriteParseMethod(
        in EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteLine("if (source.IsEmpty)");
        using (writer.WriteBlock())
        {
            writer.WriteLine("value = default;");
            writer.WriteLine("return false;");
        }

        writer.WriteLine();

        writer.WriteLine($"ref {model.TokenType.Name} first = ref __MemoryMarshal.GetReference(source);");
        writer.WriteLine();

        // write the fast path if:
        // - enum is small and contiguous from 0 (implies: has no duplicate values)
        // - enum has no names or explicit names that are only 1 char
        bool writeLabel = false;

        if (model is { ContiguousFromZero: true, Values.Length: <= 10 } &&
            model.Values.AsImmutableArray().All(static v => v.Name.Length != 1 && v.ExplicitName is not { Length: 1 }))
        {
            writeLabel = false;

            writer.WriteLine("// Enum is small and contiguous from 0, use fast path");
            writer.WriteLine("if (source.Length == 1)");
            using (writer.WriteBlock())
            {
                writer.WriteLine($"value = ({model.EnumType.FullyQualifiedName})(uint)(first - '0');");
                writer.WriteLine($"return value < ({model.EnumType.FullyQualifiedName}){model.Values.Length};");
            }
        }
        else
        {
            WriteNumberCheck(in model, writer, cancellationToken);
        }

        writer.WriteLine();
        writer.WriteLine(
            model.TokenType.IsByte()
                ? "// case-sensitivity is in separate paths so we can use specialized fast-path ignorecase-checks"
                : "// case-sensitivity is in separate paths so JIT can unroll comparisons with constant StringComparison");
        writer.WriteLine("if (_ignoreCase)");
        using (writer.WriteBlock())
        {
            WriteSwitch(in model, writer, ignoreCase: true, cancellationToken);
        }

        writer.WriteLine("else // case-sensitive");
        using (writer.WriteBlock())
        {
            WriteSwitch(in model, writer, ignoreCase: false, cancellationToken);
        }

        writer.WriteLine();

        writer.WriteLine("// not a known value");
        if (writeLabel)
        {
            writer.DecreaseIndent();
            writer.WriteLine("Fallback:");
            writer.IncreaseIndent();
        }

        if (model.TokenType.SpecialType == SpecialType.System_Byte)
        {
            writer.WriteLine("throw new NotImplementedException();");
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
        in EnumModel model,
        IndentedTextWriter writer,
        CancellationToken cancellationToken)
    {
        writer.WriteLine("// check if it's a number");
        writer.Write("if ((uint)(first - '0') <= 9u");

        writer.WriteLine(
            model.Values.AsImmutableArray().Any(static v => v.Value < 0)
                ? " || first == '-')"
                : ")");

        var entries = model.UniqueValues.Select(m => (name: m.ToString(), value: m)).GroupBy(m => m.name.Length);

        using (writer.WriteBlock())
        {
            writer.WriteLine("switch (source.Length)");
            using (writer.WriteBlock())
            {
                foreach (var group in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    writer.WriteLine($"case {group.Key}:");
                    using (writer.WriteBlock())
                    {
                        // only positive values and all values in the 0..9 range are valid
                        if (!model.HasNegativeValues && group.Key == 1 && group.Count() == 10)
                        {
                            writer.WriteLine("// all values in the 0..9 range are valid");
                            writer.WriteLine($"value = ({model.EnumType.FullyQualifiedName})(uint)(first - '0');");
                            writer.WriteLine("return true;");
                            continue;
                        }

                        // optimized alternative for 1-length integers
                        if (group.Key == 1)
                        {
                            writer.WriteLine("switch (first)");
                            using (writer.WriteBlock())
                            {
                                foreach (var entry in group)
                                {
                                    writer.Write("case ");
                                    if (model.TokenType.SpecialType is SpecialType.System_Byte) writer.Write("(byte)");
                                    writer.Write(entry.name[0].ToCharLiteral());
                                    writer.WriteLine(
                                        $": value = ({model.EnumType.FullyQualifiedName}){entry.value}; return true;");
                                }
                            }

                            writer.WriteLine("break;");
                            continue;
                        }

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
                                    if (group.Key == 2)
                                    {
                                        writer.WriteLine("switch (source[1])");
                                        using (writer.WriteBlock())
                                        {
                                            foreach (var entry in firstCharGroup)
                                            {
                                                writer.Write("case ");
                                                writer.WriteIf(model.TokenType.IsByte(), "(byte)");
                                                writer.Write(entry.name[1].ToCharLiteral());
                                                writer.WriteLine(
                                                    $": value = ({model.EnumType.FullyQualifiedName}){entry.value}; return true;");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        writer.WriteLine(
                                            $"global::System.ReadOnlySpan<{model.TokenType.Name}> tail = source.Slice(1);");

                                        foreach (var entry in firstCharGroup)
                                        {
                                            writer.Write($"if (tail.SequenceEqual(\"{entry.name[1..]}\"");
                                            writer.WriteLine(model.TokenType.IsByte() ? "u8))" : "))");
                                            using (writer.WriteBlock())
                                            {
                                                writer.WriteLine(
                                                    $"value = ({model.EnumType.FullyQualifiedName}){entry.value};");
                                                writer.WriteLine("return true;");
                                            }
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
        }
    }

    private static void WriteSwitch(
        in EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entries = GetEntries(model, ignoreCase, cancellationToken);

        writer.WriteLine("switch (source.Length)");

        bool isByte = model.TokenType.SpecialType == SpecialType.System_Byte;

        using (writer.WriteBlock())
        {
            foreach (var lengthGroup in entries.GroupBy(e => e.Length))
            {
                writer.WriteLine($"case {lengthGroup.Key}:");

                using (writer.WriteBlock())
                {
                    if (isByte)
                    {
                        if (!ignoreCase && lengthGroup.All(e => e.Name.IsAscii()))
                        {
                            WriteStringMatchByteAsciiOrdinal(
                                in model,
                                writer,
                                lengthGroup,
                                cancellationToken);
                        }
                        else
                        {
                            WriteStringMatchByte(model, writer, ignoreCase, lengthGroup, cancellationToken);
                        }
                    }
                    else
                    {
                        WriteStringMatchChar(
                            in model,
                            writer,
                            ignoreCase,
                            lengthGroup,
                            cancellationToken);
                    }
                }
            }

            writer.WriteLine("default:");
            writer.IncreaseIndent();
            writer.WriteLine("break;");
            writer.DecreaseIndent();
        }
    }

    private static void WriteStringMatchChar(
        in EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (entriesByLength.Count() == 1)
        {
            var single = entriesByLength.Single();

            writer.Write("if (source.Equals(");
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
                writer.WriteLine("return true;");
            }

            writer.WriteLine("break;");
            return;
        }

        writer.Write("switch (");

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
            writer.WriteLine("char.ToLowerInvariant(first))");
        }

        using (writer.WriteBlock())
        {
            foreach (var group in entriesByLength.GroupBy(g => g.FirstChar).OrderByDescending(g => g.Count()))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = ignoreCase
                    ? char.ToLowerInvariant(group.Key)
                    : group.Key;

                writer.WriteLine($"case {key.ToCharLiteral()}:");
                using (writer.WriteBlock())
                {
                    // the unrolled Equals for chars automatically does length checks
                    foreach (var entry in group)
                    {
                        if (entry.Name.Length > 1)
                        {
                            writer.Write("if (source.EndsWith(");
                            writer.Write(entry.Name[1..].ToStringLiteral());
                            writer.Write(", global::System.StringComparison.Ordinal");

                            if (ignoreCase)
                            {
                                writer.Write("IgnoreCase");
                            }

                            writer.WriteLine("))");
                        }

                        using (writer.WriteBlock())
                        {
                            writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{entry.MemberName};");
                            writer.WriteLine("return true;");
                        }
                    }

                    writer.WriteLine("break;");
                }
            }
        }

        writer.WriteLine("break;");
    }

    private static void WriteStringMatchByteAsciiOrdinal(
        in EnumModel model,
        IndentedTextWriter writer,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // TODO: profile and optimize this limit
        if (entriesByLength.Count() < 3)
        {
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
                using (writer.WriteBlock())
                {
                    bool first = true;

                    // the unrolled Equals for chars automatically does length checks
                    foreach (var entry in group)
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            writer.Write("else ");
                        }

                        if (entry.Name.Length > 1)
                        {
                            writer.Write("if (source.EndsWith(");
                            writer.Write(entry.Name[1..].ToStringLiteral());
                            writer.WriteLine("u8))");
                        }

                        using (writer.WriteBlock())
                        {
                            writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{entry.MemberName};");
                            writer.WriteLine("return true;");
                        }
                    }

                    writer.WriteLine("break;");
                }
            }
        }

        writer.WriteLine("break;");
    }

    private static void WriteStringMatchByte(
        EnumModel model,
        IndentedTextWriter writer,
        bool ignoreCase,
        IGrouping<int, Entry> entriesByLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool allAscii = entriesByLength.All(e => e.Name.IsAscii());

        if (allAscii)
        {
            List<Entry> entries = PooledList<Entry>.Acquire();
            entries.AddRange(entriesByLength);
            WriteNestedAsciiSwitch(0, entries);
            PooledList<Entry>.Release(entries);
            writer.WriteLine("break;");
            return;
        }

        foreach (var entry in entriesByLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.Write("if (global::System.Text.Ascii.Equals");
            if (ignoreCase) writer.Write("IgnoreCase");
            writer.Write("(source, ");
            writer.Write(entry.Name.ToStringLiteral());
            writer.WriteLine("u8))");

            using (writer.WriteBlock())
            {
                writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{entry.MemberName};");
                writer.WriteLine("return true;");
            }
        }

        writer.WriteLine("break;");

        void WriteNestedAsciiSwitch(int depth, List<Entry> entries)
        {
            if (entries.Count <= 3 ||
                depth == (entriesByLength.Key) - 1 ||
                entries.TrueForAll(m => m.Name[depth] == entries[0].Name[depth]))
            {
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
                                writer.Write(
                                    compare.IsAsciiLetter()
                                        ? $"(source[{i}] | 0x20) == {char.ToLowerInvariant(compare).ToCharLiteral()}"
                                        : $" source[{i}]         == {compare.ToCharLiteral()}");
                            }
                            else
                            {
                                writer.Write($"source[{i}] == {compare.ToCharLiteral()}");
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
                        writer.WriteLine($"value = {model.EnumType.FullyQualifiedName}.{entry.MemberName};");
                        writer.WriteLine("return true;");
                    }

                    void WriteUnrolled(int count)
                    {
                        string type = count == 8 ? "ulong" : "uint";

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
                            writer.Write(" | 0x");

                            for (int byteIndex = 0; byteIndex < count; byteIndex++)
                            {
                                writer.Write(entry.Name[i + byteIndex].IsAsciiLetter() ? "20" : "00");
                            }

                            writer.Write(count == 8 ? "UL)" : "U)");
                        }

                        writer.Write(" == ");

                        writer.Write("__MemoryMarshal.Read<");
                        writer.Write(type);
                        writer.Write(">(");

                        Span<char> chars = stackalloc char[count];
                        entry.Name.AsSpan(i, count).CopyTo(chars);
                        foreach (ref char c in chars) c = char.ToLowerInvariant(c);

                        var bigEndian = chars.ToString().ToStringLiteral();
                        chars.Reverse();
                        var littleEndian = chars.ToString().ToStringLiteral();

                        writer.Write($"__BitConverter.IsLittleEndian ? {littleEndian}u8 : {bigEndian}u8)");
                    }
                }
            }
            else
            {
                List<Entry> innerEntries = PooledList<Entry>.Acquire();

                writer.Write("switch (");
                writer.WriteIf(ignoreCase, "(");
                writer.Write($"source[{depth}]");
                writer.WriteIf(ignoreCase, " | 0x20)");
                writer.WriteLine(")");

                using (writer.WriteBlock())
                {
                    foreach (var firstChar in entries.GroupBy(
                                 e => ignoreCase ? char.ToLowerInvariant(e.Name[0]) : e.Name[0]))
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

    private readonly record struct Entry(int Length, char FirstChar, string Name, string MemberName)
        : IComparable<Entry>
    {
        public int CompareTo(Entry other)
        {
            int cmp = Length.CompareTo(other.Length);
            if (cmp == 0) cmp = FirstChar.CompareTo(other.FirstChar);
            return cmp;
        }
    }

    private static IEnumerable<Entry> GetEntries(
        EnumModel model,
        bool ignoreCase,
        CancellationToken cancellationToken)
    {
        foreach (var value in model.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            char firstChar = ignoreCase ? char.ToLowerInvariant(value.Name[0]) : value.Name[0];
            yield return new Entry(value.Name.Length, firstChar, value.Name, value.Name);

            if (value.ExplicitName is not null)
            {
                firstChar = ignoreCase ? char.ToLowerInvariant(value.ExplicitName[0]) : value.ExplicitName[0];
                yield return new Entry(value.ExplicitName.Length, firstChar, value.ExplicitName, value.Name);
            }
        }
    }
}
