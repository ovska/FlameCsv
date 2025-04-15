#if true
Fuzzer.LibFuzzer.Run(ScenarioRunner.Run<Parsing>);
#else
DirectoryInfo dir = new(@"..\..\..\");

// include files that start with crash- or timeout-
var files = dir
    .EnumerateFiles("crash-*", SearchOption.TopDirectoryOnly)
    .Concat(dir.EnumerateFiles("timeout-*", SearchOption.TopDirectoryOnly));

foreach (var file in files)
{
    var data = File.ReadAllBytes(file.FullName);
    ScenarioRunner.Run<Parsing>(data);
}
#endif
