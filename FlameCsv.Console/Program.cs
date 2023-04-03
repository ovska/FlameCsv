using FlameCsv;
using FlameCsv.Binding.Attributes;

const string data = """
id,name,isadmin,favouriteday
123,Bob,true,Friday
456,Alice,false,Sunday
789,Mallory,,Monday
""";

using var reader = new StringReader(data);
var options = new CsvTextReaderOptions
{
    Newline = "\n".AsMemory(),
    HasHeader = true,
};

await foreach (var user in CsvReader.ReadAsync<User>(reader, options))
{
    Console.WriteLine(user);
}

public record class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool? IsAdmin { get; set; }
    public DayOfWeek FavouriteDay { get; set; }
}

[CsvHeaderIgnore("whocares", Comparison = StringComparison.Ordinal)]
[CsvIndexIgnore(1)]
public readonly struct ValueUser
{
    [CsvIndex(0)] public int Id { get; init; }
    [CsvIndex(2)] public string? Name { get; init; }
    [CsvIndex(3)] public bool? IsAdmin { get; init; }
    [CsvIndex(4)] public DayOfWeek FavouriteDay { get; init; }
}
