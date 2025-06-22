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

        // uses a built-in conversion (SpanFormattable/Parsable)
        bool builtinConversion = false;

        if (wrapInNullable)
        {
            writer.Write("options.Aot.GetOrCreateNullable<");
            writer.Write(member.Type.FullyQualifiedName.AsSpan()[..^1]); // trim out the nullability question mark
            writer.Write(">(static options => ");
        }

        if (@override is null)
        {
            builtinConversion = TryGetBuiltinConversion(token, member, out string? builtinMethodName);

            if (builtinConversion)
            {
                writer.Write($"options.Aot.GetOrCreate<{member.Type.FullyQualifiedName}>(static options => ");
                writer.Write($"global::FlameCsv.Converters.ConverterCreationExtensions.{builtinMethodName}<");
                writer.Write(member.Type.FullyQualifiedName);
                writer.Write(">(options)");
            }
            else
            {
                writer.Write(
                    member.Type.IsEnumOrNullableEnum ? "options.Aot.GetOrCreateEnum<" : "options.Aot.GetConverter<"
                );

                // need to trim out the question mark for nullable types
                Range range = member.Type.SpecialType == SpecialType.System_Nullable_T ? (..^1) : (..);
                writer.Write(member.Type.FullyQualifiedName.AsSpan()[range]);

                writer.Write(">()");
            }
        }
        else
        {
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
        }

        if (wrapInNullable || builtinConversion)
        {
            writer.Write(", canCache: ");

            // explicit converter override, don't cache this one
            if (@override is { WrapInNullable: true })
            {
                writer.Write("false");
            }
            else if (member.Type.SpecialType == SpecialType.System_Nullable_T)
            {
                writer.Write("true");
            }

            writer.Write(")");
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

        if (token != "byte" || (status & BuiltinConvertable.Utf8Any) == 0)
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
