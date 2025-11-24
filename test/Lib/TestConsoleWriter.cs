using System.Text;

namespace FlameCsv.Tests;

public static class TestConsoleWriter
{
    public static void RedirectToTestOutput()
    {
        Console.SetOut(new TestOutputWriter());
    }

    private sealed class TestOutputWriter : TextWriter
    {
        public override Encoding Encoding => throw new NotImplementedException();

        public override void WriteLine() => TestContext.Current.TestOutputHelper?.WriteLine("");

        public override void WriteLine(string? value) => TestContext.Current.TestOutputHelper?.WriteLine(value ?? "");

        public override void Write(string? value) => TestContext.Current.TestOutputHelper?.Write(value ?? "");

        public override void Write(ReadOnlySpan<char> buffer) => base.Write(buffer);
    }
}
