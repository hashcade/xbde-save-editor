using System.Globalization;

namespace XbdeEditor.Core;

public sealed record ArtDefinition(int Id, int CharacterId, string Name, bool IsTalent, int Order)
{
    public int? LinkedArtId => Id switch
    {
        97 => 103, 98 => 104, 99 => 105, 100 => 111, 101 => 112, 102 => 113, _ => null
    };
    public bool IsMonado => Id is >= 3 and <= 10;
    public bool HasMasterBook => !IsTalent && !IsMonado && CharacterId is not (3 or 14 or 15);
}

public static class ArtCatalog
{
    public static IReadOnlyList<ArtDefinition> All { get; } = Load();

    private static IReadOnlyList<ArtDefinition> Load()
    {
        using var stream = typeof(ArtCatalog).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.arts.tsv")
            ?? throw new InvalidDataException("Missing art catalog.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var arts = new List<ArtDefinition>();
        while (reader.ReadLine() is { } line)
        {
            string[] fields = line.Split('\t');
            int Number(int index) => int.Parse(fields[index], CultureInfo.InvariantCulture);
            arts.Add(new ArtDefinition(Number(0), Number(1), fields[2], Number(3) == 1, Number(4)));
        }
        if (arts.Select(art => art.Id).Distinct().Count() != arts.Count || arts.Any(art => art.Id is < 1 or > 188))
            throw new InvalidDataException("Invalid art catalog IDs.");
        return arts.AsReadOnly();
    }
}
