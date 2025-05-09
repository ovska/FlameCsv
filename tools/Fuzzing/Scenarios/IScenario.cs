namespace FlameCsv.Fuzzing.Scenarios;

public interface IScenario
{
    static virtual bool SupportsUtf16 => false;

    static abstract void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement);

    static virtual void Run(ReadOnlyMemory<char> data, PoisonPagePlacement placement) { }
}
