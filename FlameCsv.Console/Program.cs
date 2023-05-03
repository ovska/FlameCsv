using System.Globalization;
using FlameCsv;
//using CsvHelper;
//using CsvHelper.Configuration;

//id,name,isadmin,favouriteday
const string data = """
123,Bob,true,Friday
456,Alice,false,Sunday
789,Mallory,,Monday
""";

//var textWriter = new StringWriter();
//var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";"};
//var csvwriter = new CsvWriter(textWriter, config);
//csvwriter.WriteRecord(data);

using var reader = new StringReader(data);
var options = new CsvTextReaderOptions
{
    Newline = "\n",
    HasHeader = true,
};

await foreach (var user in CsvReader.ReadRecordsAsync(
    reader,
    options,
    (int id, string? name, bool? isAdmin, DayOfWeek dof) => new User
    {
        Id = id,
        Name = name,
        IsAdmin = isAdmin,
        FavouriteDay = dof,
    }))
{
    Console.WriteLine(user);
}

Console.ReadLine();

public record class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool? IsAdmin { get; set; }
    public DayOfWeek FavouriteDay { get; set; }
}

//[CsvHeaderIgnore("whocares", Comparison = StringComparison.Ordinal)]
//[CsvIndexIgnore(1)]
//public readonly struct ValueUser
//{
//    [CsvIndex(0)] public int Id { get; init; }
//    [CsvIndex(2)] public string? Name { get; init; }
//    [CsvIndex(3)] public bool? IsAdmin { get; init; }
//    [CsvIndex(4)] public DayOfWeek FavouriteDay { get; init; }
//}
