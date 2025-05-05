## Windows

Place `libfuzzer-dotnet-windows.exe` in the project directory. See: https://github.com/Metalnem/libfuzzer-dotnet

```pwsh
.\fuzz-libfuzzer.ps1 `
    -libFuzzer '.\libfuzzer-dotnet-windows.exe' `
    -project FlameCsv.Fuzzing.csproj `
    -corpus Testcases `
    -dict .\Dictionaries\csv.dict
```
