using Basic.Reference.Assemblies;
using FlameCsv.SourceGen;
using FlameCsv.SourceGen.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.Tests.SourceGen;

[Collection(typeof(MetadataCollection))]
public class DiagnosticTests(MetadataFixture fixture)
{
    private readonly MetadataReference[] _metadataReferences =
    [
        ReferenceAssemblies.SystemRuntime,
        ReferenceAssemblies.SystemRuntimeSerializationPrimitives, // needed for EnumMemberAttribute
        fixture.FlameCsvCore,
    ];

    [Fact]
    public void Should_Ensure_Partial()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            public class NotPartial;

            public class Obj
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """,
            [Descriptors.NotPartialType]
        );
    }

    [Fact]
    public void Should_Ensure_Not_File_Scoped_TypeMap()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            file partial class FileScoped;


            public class Obj
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """,
            [Descriptors.FileScopedType]
        );
    }

    [Fact]
    public void Should_Ensure_Not_File_Scoped_EnumConverter()
    {
        AssertDiagnostic(
            """
            [CsvEnumConverter<char, DayOfWeek>]
            file partial class FileScoped;
            """,
            [Descriptors.FileScopedType],
            enumGen: true
        );
    }

    [Fact]
    public void Should_Ensure_Not_File_Scoped_Target()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, FileScoped>]
            public partial class TypeMap;

            file class FileScoped { public int Id; }
            """,
            [Descriptors.FileScopedType]
        );
    }

    [Fact]
    public void Should_Ensure_Not_File_Scoped_Enum()
    {
        AssertDiagnostic(
            """
            [CsvEnumConverter<char, MyEnum>]
            partial class FileScoped;

            file enum MyEnum { Value1,  Value2 }
            """,
            [Descriptors.FileScopedType],
            enumGen: true
        );
    }

    [Fact]
    public void Should_Ensure_Enum_Token_Type()
    {
        AssertDiagnostic(
            """
            [CsvEnumConverter<int, DayOfWeek>]
            partial class FileScoped;
            """,
            [Descriptors.EnumUnsupportedToken],
            enumGen: true
        );
    }

    [Theory]
    [InlineData(true, "0value")] // starts with numeric
    [InlineData(true, "-Value2")] // starts with a hyphen
    [InlineData(true, "")] // empty string
    [InlineData(false, "Value1")] // another type's name
    [InlineData(false, "Value_1")] // another type's explicit name
    public void Should_Ensure_Enum_Explicit_Name(bool invalid, string name)
    {
        AssertDiagnostic(
            $$"""
            [CsvEnumConverter<char, MyEnum>]
            partial class FileScoped;

            enum MyEnum
            {
                [System.ComponentModel.DescriptionAttribute(Name = "Ignored")]
                [System.Runtime.Serialization.EnumMember(Value = "Value_1")]
                Value1,

                [System.Runtime.Serialization.EnumMember(Value = "{{name}}")]
                Value2
            }
            """,
            [invalid ? Descriptors.EnumInvalidExplicitName : Descriptors.EnumDuplicateName],
            enumGen: true
        );
    }

    [Fact]
    public void Should_Ensure_Enum_Flags_Validity()
    {
        AssertDiagnostic(
            """
            [CsvEnumConverter<char, MyEnum>]
            partial class FileScoped;

            [System.Flags]
            enum MyEnum
            {
                A = 0b0001,
                B = 0b0010,
                C = 0b0110, // invalid, has a bit that is only in a combination value
            }
            """,
            [Descriptors.EnumUnsupportedFlag],
            enumGen: true
        );
    }

    [Fact]
    public void Should_Validate_Targeted_Member_Existence()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            [CsvHeader("xyz", MemberName = "not_exists", IsParameter = true)]
            [CsvHeader("abc", MemberName = "not_exists", IsParameter = false)]
            class Obj
            {
                public Obj(int id) { Id = id; }
                public Obj() { }

                public int Id { get; set; }
                public string Name { get; set; }
            }
            """,
            [Descriptors.TargetMemberNotFound, Descriptors.TargetMemberNotFound]
        );
    }

    [Fact]
    public void Should_Validate_Detected_Constructor()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public Obj(int id) { }
                public Obj(int? id) { }
                public int Id { get; set; }
            }
            """,
            [Descriptors.NoValidConstructor]
        );
    }

    [Fact]
    public void Should_Validate_Targeted_Constructor()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            [CsvConstructor(ParameterTypes = new[] { typeof(string) })]
            class Obj
            {
                public Obj(int id) { }
                public Obj() { }
                public int Id { get; set; }
            }
            """,
            [Descriptors.NoMatchingConstructor]
        );
    }

    [Fact]
    public void Should_Validate_Abstract_Converter()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                [CsvConverter<AbstractConverter>]
                public int Id { get; set; }
            }

            abstract class AbstractConverter : CsvConverter<char, int>;
            """,
            [Descriptors.CsvConverterAbstract]
        );
    }

    [Fact]
    public void Should_Validate_Converter_Constructor()
    {
        AssertDiagnostic(
            $$"""
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                [CsvConverter<MyConverter>]
                public int Id { get; set; }
            }

            class MyConverter : CsvConverter<char, int>
            {
                public MyConverter(int value) { } // constructor with invalid parameter
                {{ConverterBody}}
            }
            """,
            [Descriptors.NoCsvConverterConstructor]
        );
    }

    [Fact]
    public void Should_Validate_Converter_Type()
    {
        AssertDiagnostic(
            $$"""
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                [CsvConverter<MyConverter>]
                public bool Id { get; set; }
                
                [CsvStringPooling]
                public int Value { get; set; }
            }

            class MyConverter : CsvConverter<char, int>
            {
                {{ConverterBody}}
            }
            """,
            [Descriptors.CsvConverterTypeMismatch, Descriptors.CsvConverterTypeMismatch]
        );
    }

    [Fact]
    public void Should_Validate_Readable_Members()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public int Id { get; private set; }
            }
            """,
            [Descriptors.NoReadableMembers]
        );
    }

    [Fact]
    public void Should_Validate_Writable_Members()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public Obj(int id) { Id = id; }
                private int Id { get; set; }
            }
            """,
            [Descriptors.NoWritableMembers]
        );
    }

    [Fact]
    public void Should_Not_Ignore_Required_Parameter()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public Obj([CsvIgnore] int id) { Id = id; }
                public int Id { get; set; }
            }
            """,
            [Descriptors.IgnoredParameterWithoutDefaultValue]
        );
    }

    [Fact]
    public void Should_Report_Conflicting_Order()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            [CsvOrder(2, MemberName = "Id")]
            class Obj
            {
                [CsvOrder(1)] // two different orders
                public int Id { get; set; }
            }
            """,
            [Descriptors.ConflictingConfiguration]
        );
    }

    [Fact]
    public void Should_Report_Ref_Struct_Parameter()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public Obj(Span<int> value) { }
                public int Id { get; set; }
            }
            """,
            [Descriptors.RefLikeConstructorParameter]
        );
    }

    [Fact]
    public void Should_Report_Ref_Parameter()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public Obj(ref int value) { }
                public int Id { get; set; }
            }
            """,
            [Descriptors.RefConstructorParameter]
        );
    }

    [Fact]
    public void Should_Report_Conflicting_Header()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                [CsvHeader("Name")]
                [CsvHeader("Name_")] // two different headers
                public string Name { get; set; }
            }
            """,
            [Descriptors.ConflictingConfiguration]
        );
    }

    [Fact]
    public void Should_Report_Conflicting_On_Type()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            [CsvHeader("Name", MemberName = "Name")]
            [CsvHeader("Name_", MemberName = "Name")] // two different headers
            class Obj
            {
                public string Name { get; set; }
            }
            """,
            [Descriptors.ConflictingConfiguration]
        );
    }

    [Fact]
    public void Should_Report_Conflicting_Index()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            [CsvIndex(0, MemberName = "Id")]
            class Obj
            {
                [CsvIndex(1)] // two different indexes
                public int Id { get; set; }
            }
            """,
            [Descriptors.ConflictingConfiguration]
        );
    }

    [Fact]
    public void Should_Report_Conflicting_Index_Param()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                public Obj([CsvIndex(0)] int value) { }
                [CsvIndex(0)] public int Prop1 { get; set; }
                [CsvIndex(1)] public int Prop2 { get; set; }
            }
            """,
            [Descriptors.ConflictingIndex]
        );
    }

    [Fact]
    public void Should_Report_Gap_In_Indexes()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            class Obj
            {
                [CsvIndex(0)]
                public int Prop1 { get; set; }
                
                [CsvIndex(2)] // gap in indexes
                public int Prop2 { get; set; }
            }
            """,
            [Descriptors.GapInIndex]
        );
    }

    [Fact]
    public void Should_Require_MemberName_On_Type()
    {
        AssertDiagnostic(
            """
            [CsvTypeMap<char, Obj>]
            partial class ObjTypeMap;

            [CsvHeader("value")]
            class Obj
            {
                public int Id { get; set; }
            }
            """,
            [Descriptors.NoMemberNameOnAttribute]
        );

        AssertDiagnostic(
            """
            [assembly: CsvHeader("value", TargetType = typeof(Obj))]

            [CsvTypeMap<char, Obj>(SupportsAssemblyAttributes = true)]
            partial class ObjTypeMap;

            class Obj
            {
                public int Id { get; set; }
            }
            """,
            [Descriptors.NoMemberNameOnAttribute]
        );
    }

    [Fact]
    public void Should_Require_TargetType_On_Assembly()
    {
        AssertDiagnostic(
            """
            [assembly: CsvHeader("value")]

            [CsvTypeMap<char, Obj>(SupportsAssemblyAttributes = true)]
            partial class ObjTypeMap;

            class Obj
            {
                public int Id { get; set; }
            }
            """,
            [Descriptors.NoTargetTypeOnAssembly]
        );
    }

    private void AssertDiagnostic(string source, DiagnosticDescriptor[] diagnostics, bool enumGen = false)
    {
        Array.Sort(diagnostics, (x, y) => x.Id.CompareTo(y.Id));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestProject",
            [
                CSharpSyntaxTree.ParseText(
                    $$"""
                    using System;
                    using FlameCsv;
                    using FlameCsv.Attributes;
                    using FlameCsv.Binding;

                    {{source}}
                    """,
                    cancellationToken: TestContext.Current.CancellationToken
                ),
            ],
            _metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        IIncrementalGenerator generator = enumGen ? new EnumConverterGenerator() : new TypeMapGenerator();
        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: false)
        );

        var runResult = driver
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult()
            .Results.Single();

        var reportedDiagnosticIds = runResult
            .Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Id)
            .Select(d => d.Id);

        Assert.Equal(diagnostics.Select(d => d.Id), reportedDiagnosticIds);

        Assert.Empty(runResult.GeneratedSources);
    }

    private const string ConverterBody = """
        public override bool TryFormat(Span<char> destination, int value, out int charsWritten)
        {
            throw new NotImplementedException();
        }

        public override bool TryParse(ReadOnlySpan<char> source, out int value)
        {
            throw new NotImplementedException();
        }
        """;
}
