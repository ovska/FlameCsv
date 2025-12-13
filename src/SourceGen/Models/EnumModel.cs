using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal sealed record EnumModel
{
    public static EnumModel? TryGet(
        ISymbol converterSymbol,
        AttributeData attributeData,
        bool unsafeCode,
        CancellationToken cancellationToken,
        out ImmutableArray<Diagnostic> diagnostics
    )
    {
        List<Diagnostic> diagList = PooledList<Diagnostic>.Acquire();

        if (
            converterSymbol is INamedTypeSymbol converterType
            && attributeData.AttributeClass
                is {
                    TypeArguments: [
                        { } tokenType,
                        INamedTypeSymbol { CanBeReferencedByName: true, EnumUnderlyingType: not null } enumType,
                    ]
                }
        )
        {
            if (tokenType.SpecialType is not (SpecialType.System_Byte or SpecialType.System_Char))
            {
                diagList.Add(Diagnostics.EnumUnsupportedToken(converterSymbol, attributeData, tokenType));
            }

            Diagnostics.EnsurePartial(converterType, cancellationToken, diagList);
            Diagnostics.CheckIfFileScoped(converterType, cancellationToken, diagList);
            Diagnostics.CheckIfFileScoped(enumType, cancellationToken, diagList);

            EnumModel model = new EnumModel(
                enumType: enumType,
                isByte: tokenType.SpecialType == SpecialType.System_Byte,
                unsafeCode: unsafeCode,
                converterType: converterType,
                cancellationToken,
                diagList
            );

            if (model.Values.Length == 0)
            {
                diagList.Add(Diagnostics.EnumNoValues(enumType));
            }

            if (diagList.Count == 0)
            {
                diagnostics = [];
                PooledList<Diagnostic>.Release(diagList);
                return model;
            }
        }

        diagnostics = [.. diagList];
        PooledList<Diagnostic>.Release(diagList);
        return null;
    }

    public bool IsByte => (_config & Config.IsByte) != 0;
    public string Token => IsByte ? "byte" : "char";

    public string ConverterTypeName { get; }
    public TypeRef EnumType { get; }
    public string UnderlyingType { get; }
    public EquatableArray<BigInteger> UniqueValues { get; }
    public EquatableArray<EnumValueModel> Values { get; }
    public bool HasFlagsAttribute => (_config & Config.HasFlagsAttribute) != 0;
    public bool ContiguousFromZero => (_config & Config.ContiguousFromZero) != 0;
    public int ContiguousFromZeroCount { get; }
    public EquatableArray<NestedType> WrappingTypes { get; }
    public bool HasExplicitNames => (_config & Config.HasExplicitNames) != 0;
    public bool HasNegativeValues => (_config & Config.HasNegativeValues) != 0;

    private readonly Config _config;

    /// <summary>
    /// Whether the typemap is in the global namespace.
    /// </summary>
    public bool InGlobalNamespace => (_config & Config.InGlobalNamespace) != 0;

    public bool UnsafeCode => (_config & Config.UnsafeCode) != 0;

    /// <summary>
    /// Namespace of the typemap.
    /// </summary>
    /// <seealso cref="InGlobalNamespace"/>
    public string Namespace { get; }

    private EnumModel(
        INamedTypeSymbol enumType,
        bool isByte,
        bool unsafeCode,
        ITypeSymbol converterType,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics
    )
    {
        _config = default;

        if (isByte)
        {
            _config |= Config.IsByte;
        }

        ConverterTypeName = converterType.Name;
        EnumType = new TypeRef(enumType);
        UnderlyingType = enumType.EnumUnderlyingType!.ToDisplayString();

        List<EnumValueModel> values = PooledList<EnumValueModel>.Acquire();
        HashSet<BigInteger> uniqueValues = PooledSet<BigInteger>.Acquire();

        bool isUnsigned =
            enumType.EnumUnderlyingType?.SpecialType
                is SpecialType.System_Byte
                    or SpecialType.System_UInt16
                    or SpecialType.System_UInt32
                    or SpecialType.System_UInt64
                    or SpecialType.System_UIntPtr;

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } enumValue)
            {
                var value = new EnumValueModel(enumValue, isUnsigned, diagnostics);
                values.Add(value);
                uniqueValues.Add(value.Value);
            }
        }

        values.Sort();
        Values = values.ToEquatableArrayAndFree();
        UniqueValues = uniqueValues.ToEquatableArrayAndFree();
        Array.Sort(UniqueValues.UnsafeArray!);

        int contiguousCount = 0;
        bool isContiguous = true;

        foreach (ref readonly var value in Values)
        {
            if (value.Value != contiguousCount)
            {
                isContiguous = false;
                break;
            }

            contiguousCount++;
        }

        if (isContiguous)
        {
            _config |= Config.ContiguousFromZero;
        }

        ContiguousFromZeroCount = contiguousCount;

        WrappingTypes = NestedType.Create(converterType, cancellationToken, diagnostics);
        Namespace = converterType.ContainingNamespace.ToDisplayString();

        if (converterType.ContainingNamespace.IsGlobalNamespace)
        {
            _config |= Config.InGlobalNamespace;
        }

        if (unsafeCode)
        {
            _config |= Config.UnsafeCode;
        }

        if (UniqueValues.AsImmutableArray().Any(v => v < BigInteger.Zero))
        {
            _config |= Config.HasNegativeValues;
        }

        if (Values.AsImmutableArray().Any(v => v.HasValidExplicitName))
        {
            _config |= Config.HasExplicitNames;
            CheckExplicitNameDuplicates(enumType, diagnostics);
        }

        if (enumType.GetAttributes().Any(a => a is { AttributeClass.Name: "FlagsAttribute" }))
        {
            _config |= Config.HasFlagsAttribute;
            CheckFlags(enumType, diagnostics);
        }
    }

    private void CheckExplicitNameDuplicates(INamedTypeSymbol enumType, List<Diagnostic> diagnostics)
    {
        HashSet<string>? handled = null; // report only one diagnostic per duplicate name

        foreach (ref readonly EnumValueModel value in Values)
        {
            if (!value.HasValidExplicitName)
            {
                // If the explicit name is null or invalid, we don't need to check for duplicates
                continue;
            }

            foreach (ref readonly EnumValueModel innerValue in Values)
            {
                if (value == innerValue)
                {
                    continue;
                }

                if (value.ExplicitName == innerValue.Name || value.ExplicitName == innerValue.ExplicitName)
                {
                    if (!(handled ??= PooledSet<string>.Acquire()).Add(value.ExplicitName!))
                    {
                        // Already handled this explicit name
                        continue;
                    }

                    string invalidName = value.Name;
                    Location? location = GetMemberLocation(enumType, invalidName);

                    diagnostics.Add(
                        Diagnostics.EnumDuplicateName(
                            enumType,
                            value.Name,
                            location ?? enumType.Locations.FirstOrDefault(),
                            value.ExplicitName!
                        )
                    );
                }
            }
        }

        PooledSet<string>.Release(handled);
    }

    private void CheckFlags(INamedTypeSymbol enumType, List<Diagnostic> diagnostics)
    {
        ulong loneBits = 0;

        foreach (ref readonly EnumValueModel value in Values)
        {
            var uint64 = value.Value.AsUInt64Bits();

            if (HasOnlyOneBitSet(uint64))
            {
                loneBits |= uint64;
            }
        }

        foreach (ref readonly EnumValueModel value in Values)
        {
            var uint64 = value.Value.AsUInt64Bits();

            if (uint64 != 0 && !HasOnlyOneBitSet(uint64) && (uint64 & ~loneBits) != 0)
            {
                // This value has a bit set that is also set in another value
                diagnostics.Add(
                    Diagnostics.EnumUnsupportedFlag(enumType, value.Name, GetMemberLocation(enumType, value.Name))
                );
            }
        }

        static bool HasOnlyOneBitSet(ulong value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }
    }

    private static Location? GetMemberLocation(INamedTypeSymbol enumType, string name)
    {
        return enumType
            .GetMembers()
            .FirstOrDefault(m => m is IFieldSymbol f && f.Name == name)
            ?.DeclaringSyntaxReferences.FirstOrDefault()
            ?.GetSyntax()
            .GetLocation();
    }

    [Flags]
    private enum Config : byte
    {
        HasFlagsAttribute = 1 << 0,
        ContiguousFromZero = 1 << 1,
        HasExplicitNames = 1 << 2,
        HasNegativeValues = 1 << 3,
        UnsafeCode = 1 << 4,
        InGlobalNamespace = 1 << 5,
        IsByte = 1 << 6,
    }
}
