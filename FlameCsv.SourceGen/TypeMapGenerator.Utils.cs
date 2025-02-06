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
            writer.Write("options.GetOrCreateNullable<");
            writer.Write(member.Type.FullyQualifiedName.AsSpan()[..^1]); // trim out the nullability question mark
            writer.Write(">(static options => ");
        }

        if (member.OverriddenConverter is not { } converter)
        {
            writer.Write(member.Type.IsEnumOrNullableEnum ? "options.GetOrCreateEnum<" : "options.GetConverter<");

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
}
