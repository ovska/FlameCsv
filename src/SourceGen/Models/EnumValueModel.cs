using FlameCsv.SourceGen.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct EnumValueModel : IComparable<EnumValueModel>
{
    public string Name { get; }
    public string? ExplicitName { get; }
    public BigInteger Value { get; }
    public string DisplayName => ExplicitName ?? Name;

    public EnumValueModel(IFieldSymbol enumValue, bool isUnsigned, List<Diagnostic> diagnostics)
    {
        Name = enumValue.Name;
        Value = isUnsigned
            ? new BigInteger(Convert.ToUInt64(enumValue.ConstantValue))
            : new BigInteger(Convert.ToInt64(enumValue.ConstantValue));

        foreach (var attribute in enumValue.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.Serialization.EnumMemberAttribute")
            {
                continue;
            }

            // position of the named argument "Value"; should not be necessary but just in case
            int valueArgumentIndex = 0;

            // Check named arguments
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (
                    string.Equals(namedArg.Key, "Value", StringComparison.Ordinal)
                    && namedArg.Value.Value is string value
                )
                {
                    ExplicitName = value;
                    break;
                }

                valueArgumentIndex++;
            }

            if (HasInvalidExplicitName)
            {
                Location? location = (attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax)
                    ?.ArgumentList?.Arguments.ElementAtOrDefault(valueArgumentIndex)
                    ?.GetLocation();

                diagnostics.Add(
                    Diagnostics.EnumInvalidExplicitName(
                        enumValue.ContainingSymbol,
                        enumValue,
                        location ?? attribute.GetLocation(),
                        ExplicitName!
                    )
                );
            }

            break;
        }
    }

    public int CompareTo(EnumValueModel other)
    {
        int cmp = Value.CompareTo(other.Value);
        if (cmp == 0)
            cmp = StringComparer.Ordinal.Compare(Name, other.Name); // fields cannot have the same name
        return cmp;
    }

    public bool HasInvalidExplicitName =>
        ExplicitName switch
        {
            null => false,
            "" => true,
            _ => ExplicitName[0].IsAsciiNumeric(),
        };

    public bool HasValidExplicitName => ExplicitName is not null && !HasInvalidExplicitName;
}
