using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Reading;

public sealed class InvalidFormatTests
{
    [Fact]
    public void Should_Throw_On_Invalid_Format()
    {
        const string data = "Id,Name,Age\n1,\"B\"o\"b\",30\n2,Alice,45\n3,Charlie,25,true\n";
        Assert.Throws<CsvFormatException>(() => CsvReader.Read<User>(data).ToList());
    }
}

file sealed class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}
