using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Horology;

// BenchmarkRunner.Run(
//     [
//         typeof(ReadObjects),
//         typeof(WriteObjects),
//         typeof(EnumerateBench),
//     ],
//     new Config(),
//     args);

// var b = new CsvEnumerateBench();
// b.Setup();
// b.Flame_byte();
BenchmarkRunner.Run<TokenizationBench>(new Config(), args);

file class Config : ManualConfig
{
    const int Iters = 8;

    public Config()
    {
        AddExporter(HtmlExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.BriefCompressed);
        AddExporter(
            new CsvExporter(
                CsvSeparator.Comma,
                new SummaryStyle(
                    cultureInfo: System.Globalization.CultureInfo.InvariantCulture,
                    printUnitsInHeader: true,
                    printUnitsInContent: false,
                    printZeroValuesInContent: true,
                    timeUnit: null,
                    sizeUnit: null
                )
            )
        );

        AddLogger(DefaultConfig.Instance.GetLoggers().ToArray());
        AddAnalyser(DefaultConfig.Instance.GetAnalysers().ToArray());
        AddValidator(DefaultConfig.Instance.GetValidators().ToArray());
        WithSummaryStyle(SummaryStyle.Default);

        AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);

        AddJob(
            Job.Default.WithMinWarmupCount(Iters)
                .WithMaxWarmupCount(Iters * 2)
                .WithMinIterationCount(Iters)
                .WithMaxIterationCount(Iters * 3)
                .WithMinIterationTime(TimeInterval.FromSeconds(1))
                .WithGcServer(true)
        );

#if !ARM
        AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(maxDepth: 2, printSource: true)));
#endif

        WithOptions(ConfigOptions.DisableLogFile);
    }
}
