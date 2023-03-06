using FlameCsv;
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
    HasHeader = true,
};

await foreach (var user in CsvReader.ReadAsync<User>(reader, options))
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

