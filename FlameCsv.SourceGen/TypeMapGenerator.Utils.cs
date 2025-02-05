using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static void WriteConverter(IndentedTextWriter writer, IMemberModel member)
    {
        bool wrapInNullable = member.OverriddenConverter?.WrapInNullable ??
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

            Range range = member.Type.SpecialType == SpecialType.System_Nullable_T ? ..^1 : Range.All;
            writer.Write(member.Type.FullyQualifiedName.AsSpan()[range]);

            writer.Write(">()");
        }
        else
        {
            writer.Write("new ");
            writer.Write(converter.ConverterType.FullyQualifiedName);
            writer.Write("(");
            writer.Write(
                converter.ConstructorArguments switch
                {
                    ConstructorArgumentType.Options => "options",
                    _ => "",
                });
            writer.Write(")");

            if (converter.IsFactory)
            {
                writer.Write(".Create<");
                writer.Write(member.Type.FullyQualifiedName);
                writer.Write(">(options)");
            }
        }

        if (wrapInNullable) writer.Write(")");
    }
}
