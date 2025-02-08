using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    internal static void WriteConverter(IndentedTextWriter writer, string token, IMemberModel member)
    {
        if (member.IsIgnored)
        {
            writer.Write($"global::FlameCsv.Binding.CsvIgnored.Converter<{token}, {member.Type.FullyQualifiedName}>()");
            return;
        }

        bool wrapInNullable =
            member.OverriddenConverter?.WrapInNullable ??
            member.Type.SpecialType == SpecialType.System_Nullable_T;

        if (wrapInNullable)
        {
            writer.Write("options.Aot.GetOrCreateNullable<");
            writer.Write(member.Type.FullyQualifiedName.AsSpan()[..^1]); // trim out the nullability question mark
            writer.Write(">(static options => ");
        }

        if (member.OverriddenConverter is not { } converter)
        {
            writer.Write(
                member.Type.IsEnumOrNullableEnum ? "options.Aot.GetOrCreateEnum<" : "options.Aot.GetConverter<");

            Range range = member.Type.SpecialType == SpecialType.System_Nullable_T ? (..^1) : (..);
            writer.Write(member.Type.FullyQualifiedName.AsSpan()[range]);

            writer.Write(">()");
        }
        else
        {
            if (converter.IsFactory)
            {
                writer.Write($"(global::FlameCsv.CsvConverter<{token}, {member.Type.FullyQualifiedName}>)");
            }

            writer.Write("new ");
            writer.Write(converter.ConverterType.FullyQualifiedName);
            writer.Write(converter.ConstructorArguments == ConstructorArgumentType.Options ? "(options)" : "()");

            if (converter.IsFactory)
            {
                writer.Write(".Create(typeof(");
                writer.Write(member.Type.FullyQualifiedName);
                writer.Write("), options)");
            }
        }

        if (wrapInNullable) writer.Write(")");
    }

    internal static SortedDictionary<int, IMemberModel?>? TryGetIndexBindings(
        bool write,
        TypeMapModel model,
        ref AnalysisCollector collector)
    {
        SortedDictionary<int, IMemberModel?>? dict = null;

        foreach (var ignored in model.IgnoredIndexes)
        {
            dict ??= MemberDictPool.Acquire();
            dict[ignored] = null;
        }

        foreach (var member in model.AllMembers)
        {
            if (write ? !member.CanWrite : !member.CanRead) continue;
            if (member.Index is not { } index) continue;

            dict ??= MemberDictPool.Acquire();

            // first on this index
            if (!dict.TryGetValue(index, out IMemberModel? existing))
            {
                dict[index] = member.IsIgnored ? null : member;
                continue;
            }

            if (member.IsIgnored)
            {
                // current is ignored, don't touch existing
                continue;
            }

            // check if existing is ignored, overwrite with current
            if (existing is null)
            {
                dict[index] = member;
                continue;
            }

            // conflicting bindings
            // TODO: diagnostic
            MemberDictPool.Release(dict);
            return null;
        }

        // check indexes for gaps
        if (dict is not null)
        {
            int lastIndex = -1;
            foreach (var kvp in dict)
            {
                if (kvp.Key != lastIndex + 1)
                {
                    // gap in the indexes
                    // TODO: diagnostic
                    MemberDictPool.Release(dict);
                    return null;
                }

                lastIndex = kvp.Key;
            }
        }

        return dict;
    }

    private static void WriteDefaultInstance(IndentedTextWriter writer, TypeMapModel typeMap)
    {
        writer.WriteLine("/// <summary>");
        writer.WriteLine("/// Returns a thread-safe instance of the typemap with default options.");
        writer.WriteLine("/// </summary>");

        writer.WriteLine("/// <remarks>");
        writer.Write("/// Unmatched headers ");
        writer.Write(typeMap.IgnoreUnmatched ? "are ignored." : "cause an exception.");
        writer.WriteLine("<br/>");
        writer.Write("/// Duplicate headers ");
        writer.Write(typeMap.ThrowOnDuplicate ? "cause an exception." : "are ignored.");
        writer.WriteLine("<br/>");
        writer.Write("/// De/materializer caching ");
        writer.WriteLine(typeMap.NoCaching ? "is disabled." : "is enabled.");
        writer.WriteLine("/// </remarks>");

        writer.WriteLine(
            $"public static {typeMap.TypeMap.FullyQualifiedName} Default {{ get; }} = new {typeMap.TypeMap.FullyQualifiedName}()");
        writer.WriteLine("{");
        writer.IncreaseIndent();
        writer.Write("IgnoreUnmatched = ");
        writer.WriteLine(typeMap.IgnoreUnmatched ? "true," : "false,");
        writer.Write("ThrowOnDuplicate = ");
        writer.WriteLine(typeMap.ThrowOnDuplicate ? "true," : "false,");
        writer.Write("NoCaching = ");
        writer.WriteLine(typeMap.NoCaching ? "true," : "false,");
        writer.DecreaseIndent();
        writer.WriteLine("};");
        writer.WriteLine();
    }
}
