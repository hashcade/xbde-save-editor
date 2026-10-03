namespace XbdeEditor.Core;

public static class SkillLinkCatalog
{
    private static readonly IReadOnlyDictionary<(int Character, int Source), SkillShape[]> Shapes = Load();

    public static SkillShape GetShape(int characterId, int sourceCharacterId, int index)
    {
        if (index is < 1 or > 5 || !Shapes.TryGetValue((characterId, sourceCharacterId), out var slots))
            throw new ArgumentOutOfRangeException(nameof(index), "Unrecognized skill-link pairing or slot.");
        return slots[index - 1];
    }

    private static IReadOnlyDictionary<(int, int), SkillShape[]> Load()
    {
        using var stream = typeof(SkillLinkCatalog).Assembly.GetManifestResourceStream("XbdeEditor.Core.Data.skill-link-shapes.tsv")
            ?? throw new InvalidDataException("Missing skill-link shape catalog.");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        var shapes = new Dictionary<(int, int), SkillShape[]>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            if (fields.Length != 7) throw new InvalidDataException("Invalid skill-link shape row.");
            int character = int.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture);
            int source = int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);
            if (character is < 1 or > 8 || source is < 1 or > 8)
                throw new InvalidDataException("Invalid skill-link character.");
            if (character == source)
            {
                if (fields.Skip(2).Any(shape => shape != "None")) throw new InvalidDataException("Unexpected self-link shape.");
                continue;
            }
            var slots = fields.Skip(2).Select(Enum.Parse<SkillShape>).ToArray();
            if (slots.Any(shape => !Enum.IsDefined(shape))) throw new InvalidDataException("Invalid skill-link shape.");
            shapes.Add((character, source), slots);
        }
        if (shapes.Count != 56) throw new InvalidDataException("Incomplete skill-link shape catalog.");
        return shapes;
    }
}
