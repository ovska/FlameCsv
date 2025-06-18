using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Basic.Reference.Assemblies;
using FlameCsv.SourceGen.Generators;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.Tests.SourceGen;

[Collection(typeof(MetadataCollection))]
public class EnumGenTests(MetadataFixture fixture)
{
    [Fact]
    public void Test_EnumGenerator_Cacheability()
    {
        EnumModel model = GetValidModel(
            """
            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            enum TestEnum
            {
                Dog,
                Cat,
                [global::System.Runtime.Serialization.EnumMember(Value = "Zebra Animal!")]
                Zebra,
            }
            """,
            out CSharpCompilation compilation
        );

        Assert.Equal("byte", model.TokenType.Name);
        Assert.Equal("TestEnum", model.EnumType.Name);

        Assert.Equal(3, model.Values.Length);

        Assert.Equal(0, model.Values[0].Value);
        Assert.Equal("Dog", model.Values[0].Name);
        Assert.Null(model.Values[0].ExplicitName);

        Assert.Equal(1, model.Values[1].Value);
        Assert.Equal("Cat", model.Values[1].Name);
        Assert.Null(model.Values[1].ExplicitName);

        Assert.Equal(2, model.Values[2].Value);
        Assert.Equal("Zebra", model.Values[2].Name);
        Assert.Equal("Zebra Animal!", model.Values[2].ExplicitName);

        Assert.Equal([0, 1, 2], model.UniqueValues);

        EnumConverterGenerator generator = new();
        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)
        );

        // Run the generator once
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        Assert.All(
            driver
                .GetRunResult()
                .Results.Single()
                .TrackedOutputSteps.Single(x => x.Key != "FlameCsv_EnumSourceGen_Diagnostics")
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
            .TrackedSteps["FlameCsv_EnumSourceGen_Model"]
            .Single()
            .Outputs;
        (object Value, IncrementalStepRunReason Reason) output2 = Assert.Single(assemblyNameOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output2.Reason);
    }

    [Fact]
    public void Test_Very_Large_Values()
    {
        EnumModel model = GetValidModel(
            """
            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            enum TestEnum : ulong
            {
                A = ulong.MaxValue,
                B = ulong.MaxValue - 1,
            }
            """
        );

        // values are ordered from smallest to largest
        Assert.Equal("B", model.Values[0].Name);
        Assert.Equal("A", model.Values[1].Name);
        Assert.Equal(ulong.MaxValue - 1, model.Values[0].Value);
        Assert.Equal(ulong.MaxValue, model.Values[1].Value);
    }

    [Fact]
    public void Test_Very_Small_Values()
    {
        EnumModel model = GetValidModel(
            """
            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            enum TestEnum : long
            {
                A = long.MinValue,
                B = long.MinValue + 1,
            }
            """
        );

        Assert.Equal("A", model.Values[0].Name);
        Assert.Equal("B", model.Values[1].Name);
        Assert.Equal(long.MinValue, model.Values[0].Value);
        Assert.Equal(long.MinValue + 1, model.Values[1].Value);
    }

    [Fact]
    public void Test_Contiguous_From_Zero()
    {
        EnumModel model = GetValidModel(
            """
            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            enum TestEnum { A, B, C, D, E, F }
            """
        );

        Assert.True(model.ContiguousFromZero);
        Assert.Equal(6, model.ContiguousFromZeroCount); // first 6 values are contiguous
        Assert.Equal([0, 1, 2, 3, 4, 5], model.UniqueValues);
    }

    [Fact]
    public void Test_Common_Props()
    {
        EnumModel model = GetValidModel(
            """
            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            enum TestEnum { A = 1, B = 2 }
            """
        );

        Assert.True(model.InGlobalNamespace);
        Assert.Equal("<global namespace>", model.Namespace);
        Assert.False(model.HasNegativeValues);
        Assert.False(model.HasFlagsAttribute);
        Assert.False(model.ContiguousFromZero);
        Assert.Equal(0, model.ContiguousFromZeroCount); // no contiguous values from zero

        model = GetValidModel(
            """
            namespace MyNamespace;

            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            [System.Flags]
            public enum TestEnum { A = 1, B = -2 }
            """
        );

        Assert.False(model.InGlobalNamespace);
        Assert.Equal("MyNamespace", model.Namespace);
        Assert.True(model.HasNegativeValues);
        Assert.True(model.HasFlagsAttribute);
        Assert.False(model.ContiguousFromZero);
        Assert.Equal(0, model.ContiguousFromZeroCount); // no contiguous values from zero
    }

    [Fact]
    public void Test_Partially_Contiguous_From_Zero()
    {
        EnumModel model = GetValidModel(
            """
            [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
            partial class EnumConverter;

            enum TestEnum { A, B, C, D, E, F, G = 7 }
            """
        );

        Assert.False(model.ContiguousFromZero);
        Assert.Equal(6, model.ContiguousFromZeroCount);
        Assert.Equal([0, 1, 2, 3, 4, 5, 7], model.UniqueValues);
    }

    private EnumModel GetValidModel(string source, [CallerMemberName] string? assemblyName = null) =>
        GetValidModel(source, out _, assemblyName);

    private EnumModel GetValidModel(
        string source,
        out CSharpCompilation compilation,
        [CallerMemberName] string? assemblyName = null
    )
    {
        compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            [fixture.FlameCsvCore, .. Net90.References.All],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));

        var enumConverter =
            compilation.GetTypeByMetadataName("EnumConverter")
            ?? compilation.GetTypeByMetadataName("MyNamespace.EnumConverter");

        Assert.NotNull(enumConverter);
        var attribute = enumConverter.GetAttributes().Single();

        Assert.True(
            EnumModel.TryGet(
                enumConverter,
                attribute,
                TestContext.Current.CancellationToken,
                out var diagnostics,
                out var model
            )
        );
        Assert.Empty(diagnostics);

        return model;
    }
}
