using BenchmarkDotNet.Configs;
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

BenchmarkRunner.Run<TokenizationBench>(new Config(), args);

namespace FlameCsv.Benchmark
{
    file class Config : ManualConfig
    {
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
                Job.Default.WithMinWarmupCount(24)
                    .WithMaxWarmupCount(64)
                    .WithMinIterationCount(24)
                    .WithMaxIterationCount(128)
                    .WithMinIterationTime(TimeInterval.FromSeconds(1))
                    .WithGcServer(true)
            );

            WithOptions(ConfigOptions.DisableLogFile);
        }
    }
}
