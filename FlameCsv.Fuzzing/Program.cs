#if true
Fuzzer.LibFuzzer.Run(ScenarioRunner.Run<Converters>);
#else
DirectoryInfo dir = new(@"..\..\..\");

// include files that start with crash- or timeout-
var files = dir
    .EnumerateFiles("crash-*", SearchOption.AllDirectories)
    .Concat(dir.EnumerateFiles("timeout-*", SearchOption.AllDirectories));

foreach (var file in files)
{
    var data = File.ReadAllBytes(file.FullName);
    ScenarioRunner.Run<Unescape>(data);
}
#endif
