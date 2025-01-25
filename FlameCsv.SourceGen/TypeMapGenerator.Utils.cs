using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void WriteConverter(
        StringBuilder sb,
        FlameSymbols symbols,
        IMemberModel member)
    {
        bool wrapInNullable = member.OverriddenConverter?.WrapInNullable ??
            member.Type.SpecialType == SpecialType.System_Nullable_T;

        if (wrapInNullable)
        {
            sb.Append("options.GetOrCreateNullable<");
            sb.Append(member.Type.FullyQualifiedName);
            sb.Length--; // trim out the nullability question mark
            sb.Append(">(");
        }

        if (member.OverriddenConverter is not { } converter)
        {
            sb.Append(member.Type.IsEnumOrNullableEnum ? "options.GetOrCreateEnum<" : "options.GetConverter<");
            sb.Append(member.Type.FullyQualifiedName);
            if (member.Type.SpecialType == SpecialType.System_Nullable_T) sb.Length--;
            sb.Append(">()");
        }
        else
        {
            sb.Append("new ");
            sb.Append(converter.ConverterType.FullyQualifiedName);
            sb.Append('(');
            sb.Append(
                converter.ConstructorArguments switch
                {
                    ConstructorArgumentType.Options => "options",
                    _ => "",
                });
            sb.Append(')');

            if (converter.IsFactory)
            {
                sb.Append(".Create<");
                sb.Append(member.Type.FullyQualifiedName);
                sb.Append(">(options)");
            }
        }

        if (wrapInNullable) sb.Append(')');
        if (symbols.NullableContext) sb.Append('!');
    }
}
