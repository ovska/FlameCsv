using FlameCsv;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Readers;

const string data = """
id,name,isadmin,favouriteday
123,Bob,true,Friday
456,Alice,false,Sunday
789,Mallory,,Monday
""";

using var reader = new StringReader(data);
var options = new CsvTextReaderOptions
{
    Tokens = CsvTokens<char>.Unix,
    HasHeader = false,
};

await foreach (var user in CsvReader.ReadAsync<ValueUser>(reader, options))
{
    Console.WriteLine(user);
}

Console.ReadKey();

public record class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool? IsAdmin { get; set; }
    public DayOfWeek FavouriteDay { get; set; }
}

public readonly struct ValueUser
{
 [CsvIndex(0)]   public int Id { get; init; }
 [CsvIndex(1)]   public string? Name { get; init; }
 [CsvIndex(2)]   public bool? IsAdmin { get; init; }
 [CsvIndex(3)]   public DayOfWeek FavouriteDay { get; init; }
}
