using System.Collections.Immutable;
using System.Reflection;
using FlameCsv.SourceGen.Generators;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.Tests.SourceGen;

[Collection(typeof(MetadataCollection))]
public class TypeMapTests(MetadataFixture fixture)
{
    private readonly MetadataReference[] _metadataReferences =
    [
        ReferenceAssemblies.SystemRuntime,
        fixture.FlameCsvCore,
    ];

    [Fact]
    public void Generated_Code_Should_Compile_For_Builtin_And_Enum_Members()
    {
        // no using directives: all generated references must be fully qualified
        const string source = """
            namespace Gen;

            public enum Color { Red, Green, Blue }

            [System.Flags]
            public enum Perms { None = 0, Read = 1, Write = 2 }

            // UTF-16 parsable and formattable only
            public readonly struct Code : System.ISpanParsable<Code>, System.ISpanFormattable
            {
                public static Code Parse(string s, System.IFormatProvider? p) => default;
                public static bool TryParse(string? s, System.IFormatProvider? p, out Code r) { r = default; return true; }
                public static Code Parse(System.ReadOnlySpan<char> s, System.IFormatProvider? p) => default;
                public static bool TryParse(System.ReadOnlySpan<char> s, System.IFormatProvider? p, out Code r) { r = default; return true; }
                public string ToString(string? f, System.IFormatProvider? p) => "";
                public bool TryFormat(System.Span<char> d, out int w, System.ReadOnlySpan<char> f, System.IFormatProvider? p) { w = 0; return true; }
            }

            public class Rec
            {
                public int Id { get; set; }
                public Color Color { get; set; }
                public Color? MaybeColor { get; set; }
                public Perms Perms { get; set; }
                public System.DateOnly Date { get; set; }
                public System.DateOnly? MaybeDate { get; set; }
                public System.TimeOnly Time { get; set; }
                public System.Int128 Big { get; set; }
                public System.Half Half { get; set; }
                public System.Numerics.BigInteger BigInt { get; set; }
                public Code Code { get; set; }
                public Code? MaybeCode { get; set; }
            }

            public class CtorRec(Color color, Color? maybeColor, System.DateOnly date, System.DateOnly? maybeDate, Code code)
            {
                public Color Color { get; } = color;
                public Color? MaybeColor { get; } = maybeColor;
                public System.DateOnly Date { get; } = date;
                public System.DateOnly? MaybeDate { get; } = maybeDate;
                public Code Code { get; } = code;
            }

            [FlameCsv.Attributes.CsvTypeMap<char, Rec>] partial class RecCharMap;
            [FlameCsv.Attributes.CsvTypeMap<byte, Rec>] partial class RecByteMap;
            [FlameCsv.Attributes.CsvTypeMap<char, CtorRec>] partial class CtorRecCharMap;
            [FlameCsv.Attributes.CsvTypeMap<byte, CtorRec>] partial class CtorRecByteMap;
            """;

        CSharpCompilation compilation = CSharpCompilation.Create(
            nameof(Generated_Code_Should_Compile_For_Builtin_And_Enum_Members),
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            [fixture.FlameCsvCore, .. ReferenceAssemblies.All],
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TypeMapGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation output,
            out ImmutableArray<Diagnostic> generatorDiagnostics,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(generatorDiagnostics);
        Assert.Equal(compilation.SyntaxTrees.Length + 4, output.SyntaxTrees.Count());
        Assert.Empty(
            output
                .GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
        );

        // enums never use the builtin span conversion, even when they implement the span interfaces
        string byteMap = output.SyntaxTrees.Single(t => Path.GetFileName(t.FilePath) == "RecByteMap.G.cs").ToString();
        Assert.Contains("options.Aot.GetOrCreateEnum<global::Gen.Color>()", byteMap);
        Assert.Contains("options.Aot.GetOrCreateEnum<global::Gen.Perms>()", byteMap);
        Assert.DoesNotContain("CreateUtf8Transcoded<global::Gen.Color>", byteMap);
    }

    [Fact]
    public void Test_Cacheability()
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestProject",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    [FlameCsv.Attributes.CsvTypeMap<char, TestClass>]
                    partial class TestTypeMap;

                    public class TestClass
                    {
                        public int Id { get; set; }
                        public string? Name { get; set; }
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken
                ),
            ],
            _metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        TypeMapGenerator generator = new();
        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)
        );

        // Run the generator once
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        // Assert.Equal(
        //     IncrementalStepRunReason.New,
        //     driver.GetRunResult().Results.Single().TrackedOutputSteps.Single(x => x.Key != "FlameCsv_Diagnostics").Value.Single().Outputs.Single().Reason);

        Assert.All(
            driver
                .GetRunResult()
                .Results.Single()
                .TrackedOutputSteps.Single(x => x.Key != "FlameCsv_Diagnostics")
                .Value,
            step => step.Outputs.All(x => x.Reason == IncrementalStepRunReason.New)
        );

        // Update the compilation and rerun the generator
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// dummy", cancellationToken: TestContext.Current.CancellationToken)
        );
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Assert the driver doesn't recompute the output
        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        IEnumerable<(object Value, IncrementalStepRunReason Reason)> allOutputs = result
            .TrackedOutputSteps.SelectMany(outputStep => outputStep.Value)
            .SelectMany(output => output.Outputs);

        Assert.All(allOutputs, output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));

        // Assert the driver use the cached result from typemap
        ImmutableArray<(object Value, IncrementalStepRunReason Reason)> assemblyNameOutputs = result
            .TrackedSteps["FlameCsv_TypeMap"]
            .Single()
            .Outputs;
        (object Value, IncrementalStepRunReason Reason) output2 = Assert.Single(assemblyNameOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output2.Reason);
    }

    [Fact]
    public void Should_Be_Equatable()
    {
        HashSet<Type> handled = [];
        AssertEquatable(typeof(TypeMapModel), handled);
        AssertEquatable(typeof(EnumModel), handled);

        static bool AssertEquatable(Type type, HashSet<Type> handled)
        {
            while (handled.Add(type))
            {
                if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                {
                    break;
                }

                if (Nullable.GetUnderlyingType(type) is { } underlyingType)
                {
                    type = underlyingType;
                    continue;
                }

                if (!type.IsAssignableTo(typeof(IEquatable<>).MakeGenericType(type)))
                {
                    Assert.Fail($"{type.FullName} is not primitive and does not implement IEquatable<{type.FullName}>");
                    return false;
                }

                // don't check properties of IEnumerable types, we checked it implements IEquatable<> already
                if (type.IsAssignableTo(typeof(System.Collections.IEnumerable)))
                {
                    break;
                }

                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (member is PropertyInfo property)
                    {
                        if (!AssertEquatable(property.PropertyType, handled))
                        {
                            Assert.Fail($"{type.FullName}.{member.Name} is not equatable");
                            return false;
                        }
                    }
                    else if (member is FieldInfo field)
                    {
                        if (!AssertEquatable(field.FieldType, handled))
                        {
                            Assert.Fail($"{type.FullName}.{member.Name} is not equatable");
                            return false;
                        }
                    }
                }

                break;
            }

            return true;
        }
    }

    [Fact]
    public void Should_Generate_Indexes()
    {
        const string source = """
            using FlameCsv.Attributes;

            [CsvTypeMap<char, PartialObj>]
            partial class TypeMap;

            [CsvIgnoredIndexes(2)]
            record PartialObj
            {
                [CsvIndex(0)]
                public int Id { get; set; }

                [CsvIndex(1)]
                public string? Name { get; set; }

                [CsvIgnore]
                public bool IsEnabled { get; set; }

                [CsvIndex(3)]
                public System.DateTime LastLogin { get; set; }

                [CsvIndex(4)]
                public System.Guid Token { get; set; }
            }
            """;

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestProject",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            _metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // ensure no errors
        Assert.Empty(
            compilation
                .GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Severity == DiagnosticSeverity.Error)
        );

        TypeMapGenerator generator = new();
        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)
        );

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        Assert.Empty(
            driver.GetRunResult().Results.Single().Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
        );

        Assert.All(
            driver
                .GetRunResult()
                .Results.Single()
                .TrackedOutputSteps.Single(x => x.Key != "FlameCsv_Diagnostics")
                .Value,
            step => step.Outputs.All(x => x.Reason == IncrementalStepRunReason.New)
        );
    }

#if false // debugging
    [Fact]
    public void Diagnostics_Should_Equate()
    {
        Diagnostic d1 = GetDiagnostic();
        Diagnostic d2 = GetDiagnostic();
        Assert.Equal(d1, d2);

        static Diagnostic GetDiagnostic()
        {
            var compilation = CSharpCompilation.Create(
                nameof(Diagnostics_Should_Equate),
                [
                    CSharpSyntaxTree.ParseText(
                        """
                        class TestClass 
                        {
                            public int Id { get; set; }
                            public string? Name { get; set; }
                        }
                        """)
                ],
                [ReferenceAssemblies.SystemRuntime],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());
            var namedTypeSymbol = semanticModel.GetDeclaredSymbol(
                semanticModel
                    .SyntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Single(c => c.Identifier.Text == "TestClass"))!;

            return Diagnostics.NoReadableMembers(namedTypeSymbol);
        }
    }

    [Fact]
    public void A_Test()
    {
        var compilation = CSharpCompilation.Create(
            "TestProject",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using FlameCsv.Binding;
                    using FlameCsv.Binding.Attributes;
                    using FlameCsv.Converters;
                    using System;

                    namespace TestiCompile;

                    public static partial class TypeMapBindingTests
                    {
                        [CsvTypeField(memberName: "Id", "__id__")]
                        [CsvTypeField(memberName: "dof", "doeeeef", IsParameter = true)]
                        [CsvTypeField(memberName: "Xyzz", "aaaaasd", IsRequired = false)]
                        private class _Obj : ISomething
                        {
                            [CsvConstructor]
                            public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Sunday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
                            {
                                Name = name;
                            }
                        
                            public int Id { get; set; }
                            public string? Name { get; set; }
                        
                            [CsvConverter<char, EnumTextConverter<DayOfWeek>>]
                            public DayOfWeek DOF { get; set; }
                        
                            public int? NullableInt { get; set; }
                            public DayOfWeek? NullableDOF { get; set; }
                        
                            bool ISomething.Xyzz { get; set; }
                        }
                        
                        [CsvTypeMap<char, _Obj>]
                        private partial class Test;
                        
                        private interface ISomething
                        {
                            bool Xyzz { get; set; }
                        }
                    }
                    """)
            ],
            _metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeMapGenerator();
        var sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        // Run the generator once
        driver = driver.RunGenerators(compilation);
        Assert.Equal(
            IncrementalStepRunReason.New,
            driver.GetRunResult().Results.Single().TrackedOutputSteps.Single().Value.Single().Outputs.Single().Reason);

        // var fromFirstRun = (TypeMapModel)driver
        //     .GetRunResult()
        //     .Results.Single()
        //     .TrackedSteps["FlameCsv_TypeMap"]
        //     .Single()
        //     .Outputs.Single()
        //     .Item1;

        // Update the compilation and rerun the generator
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("// dummy"));
        driver = driver.RunGenerators(compilation);

        // Assert the driver doesn't recompute the output
        var result = driver.GetRunResult().Results.Single();
        var allOutputs = result
            .TrackedOutputSteps.SelectMany(outputStep => outputStep.Value)
            .SelectMany(output => output.Outputs);
    }
#endif
}
