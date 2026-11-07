using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

partial class TypeMapGenerator
{
    private static void WriteConverterType(IndentedTextWriter writer, string token, IMemberModel member)
    {
        if (member.IsIgnored)
        {
            writer.Write($"global::FlameCsv.Binding.CsvIgnored.Converter<{token}, {member.Type.FullyQualifiedName}>");
        }
        else if (member.OverriddenConverter is StringPoolingConverterModel)
        {
            writer.Write(StringPoolingConverterModel.GetName(token));
        }
        else if (member.OverriddenConverter is ConverterModel { IsFactory: false, WrapInNullable: false } converter)
        {
            writer.Write(converter.ConverterType.FullyQualifiedName);
        }
        else
        {
            writer.Write($"global::FlameCsv.CsvConverter<{token}, {member.Type.FullyQualifiedName}>");
        }
    }

    private static void WriteConverter(IndentedTextWriter writer, string token, IMemberModel member)
    {
        if (member.IsIgnored)
        {
            writer.Write($"global::FlameCsv.Binding.CsvIgnored.Converter<{token}, {member.Type.FullyQualifiedName}>()");
            return;
        }

        if (member.OverriddenConverter is StringPoolingConverterModel)
        {
            writer.Write(StringPoolingConverterModel.GetName(token));
            writer.Write(".Instance");
            return;
        }

        var @override = member.OverriddenConverter as ConverterModel;

        // converter is for T but member is T?, or the member is T? struct and we may need to wrap it in a NullableConverter
        bool wrapInNullable = @override?.WrapInNullable ?? member.Type.SpecialType == SpecialType.System_Nullable_T;

        // the converted value type; trims out the question mark for nullable types
        string valueType =
            member.Type.SpecialType == SpecialType.System_Nullable_T
                ? member.Type.FullyQualifiedName[..^1]
                : member.Type.FullyQualifiedName;

        if (wrapInNullable)
        {
            writer.Write("options.Aot.GetOrCreateNullable<");
            writer.Write(valueType);
            writer.Write(">(static options => ");
        }

        if (@override is null)
        {
            if (member.Type.IsEnumOrNullableEnum)
            {
                writer.Write($"options.Aot.GetOrCreateEnum<{valueType}>()");
            }
            else if (TryGetBuiltinConversion(token, member, out string? builtinMethodName))
            {
                // uses a built-in conversion (SpanFormattable/Parsable)
                writer.Write($"options.Aot.GetOrCreate<{valueType}>(static options => ");
                writer.Write($"global::FlameCsv.Converters.ConverterCreationExtensions.{builtinMethodName}<");
                writer.Write(valueType);
                writer.Write(">(options))");
            }
            else
            {
                writer.Write($"options.Aot.GetConverter<{valueType}>()");
            }
        }
        else
        {
            if (!wrapInNullable)
            {
                writer.Write(
                    $"options.Aot.GetOrCreateOverridden<{member.Type.FullyQualifiedName}, {@override.ConverterType.FullyQualifiedName}>(static options => "
                );
            }

            // converter override, create manually
            if (@override.IsFactory)
            {
                writer.Write($"(global::FlameCsv.CsvConverter<{token}, {member.Type.FullyQualifiedName}>)");
            }

            writer.Write("new ");
            writer.Write(@override.ConverterType.FullyQualifiedName);
            writer.Write(@override.ConstructorArguments == ConstructorArgumentType.Options ? "(options)" : "()");

            if (@override.IsFactory)
            {
                writer.Write(".Create(typeof(");
                writer.Write(member.Type.FullyQualifiedName);
                writer.Write("), options)");
            }

            if (!wrapInNullable)
            {
                writer.Write(", identifier: ");
                member.WriteOverrideId(writer);
                writer.Write(')');
            }
        }

        if (wrapInNullable)
        {
            writer.Write(", identifier: ");

            // explicit converter override
            if (@override is { WrapInNullable: true })
            {
                member.WriteOverrideId(writer);
            }
            else if (member.Type.SpecialType == SpecialType.System_Nullable_T)
            {
                writer.Write("global::System.Guid.Empty");
            }

            writer.Write(')');
        }
    }

    private static bool TryGetBuiltinConversion(
        string token,
        IMemberModel member,
        [NotNullWhen(true)] out string? methodName
    )
    {
        var status = member.Convertability;

        if (token == "char")
        {
            if ((status & BuiltinConvertable.Both) == BuiltinConvertable.Both)
            {
                methodName = "CreateUtf16";
                return true;
            }

            methodName = null;
            return false;
        }

        // UTF-8 conversion needs both parsing and formatting, natively or by transcoding from UTF-16
        if (
            token != "byte"
            || (status & (BuiltinConvertable.Utf8Parsable | BuiltinConvertable.Parsable)) == 0
            || (status & (BuiltinConvertable.Utf8Formattable | BuiltinConvertable.Formattable)) == 0
        )
        {
            methodName = null;
            return false;
        }

        bool nativeParse = (status & BuiltinConvertable.Utf8Parsable) == BuiltinConvertable.Utf8Parsable;
        bool nativeFormat = (status & BuiltinConvertable.Utf8Formattable) == BuiltinConvertable.Utf8Formattable;

        methodName = (nativeParse, nativeFormat) switch
        {
            (true, true) => "CreateUtf8",
            (true, false) => "CreateUtf8Parsable",
            (false, true) => "CreateUtf8Formattable",
            (false, false) => "CreateUtf8Transcoded",
        };

        return true;
    }
}
