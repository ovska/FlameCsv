using System.Collections.Immutable;
using Basic.Reference.Assemblies;
using FlameCsv.SourceGen;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.Tests.SourceGen;

[Collection(typeof(MetadataCollection))]
public class EnumGenTests(MetadataFixture fixture)
{
    [Fact]
    public void Test_EnumGenerator()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_EnumGenerator),
            [
                CSharpSyntaxTree.ParseText(
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
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [fixture.FlameCsvCore, .. Net90.References.All],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));

        var enumConverter = compilation.GetTypeByMetadataName("EnumConverter")!;
        var attribute = enumConverter.GetAttributes().Single();

        Assert.True(
            EnumModel.TryGet(
                enumConverter,
                attribute,
                TestContext.Current.CancellationToken,
                out var diagnostics,
                out var model));
        Assert.Empty(diagnostics);

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
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        // Run the generator once
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        Assert.All(
            driver
                .GetRunResult()
                .Results.Single()
                .TrackedOutputSteps.Single(x => x.Key != "FlameCsv_EnumSourceGen_Diagnostics")
                .Value,
            step => step.Outputs.All(x => x.Reason == IncrementalStepRunReason.New));

        // Update the compilation and rerun the generator
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// dummy", cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Assert the driver doesn't recompute the output
        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        IEnumerable<(object Value, IncrementalStepRunReason Reason)> allOutputs = result
            .TrackedOutputSteps.SelectMany(outputStep => outputStep.Value)
            .SelectMany(output => output.Outputs);

        Assert.All(allOutputs, output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));

        // Assert the driver use the cached result from typemap
        ImmutableArray<(object Value, IncrementalStepRunReason Reason)> assemblyNameOutputs
            = result.TrackedSteps["FlameCsv_EnumSourceGen_Model"].Single().Outputs;
        (object Value, IncrementalStepRunReason Reason) output2 = Assert.Single(assemblyNameOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output2.Reason);
    }
}
