using System.Globalization;

namespace XbdeEditor.Core;

public sealed record SkillDefinition(int Id, string Name, uint RequiredSP);

public sealed record SkillTreeDefinition(int CharacterId, int Index, string Name, IReadOnlyList<SkillDefinition> Skills)
{
    public int UnlockFlag => Index <= 3 ? 0 : ((CharacterId == 8 ? 3 : CharacterId) - 1) * 2 + Index - 3;
}

public static class SkillCatalog
{
    public static IReadOnlyList<SkillTreeDefinition> All { get; } = Load();

    private static IReadOnlyList<SkillTreeDefinition> Load()
    {
        var skills = ReadRows("skills.tsv").Select(fields => new SkillDefinition(
            Number(fields[0]), fields[1], uint.Parse(fields[2], CultureInfo.InvariantCulture))).ToArray();
        var trees = ReadRows("skill-trees.tsv").Select(fields =>
        {
            int character = Number(fields[0]);
            int index = Number(fields[1]);
            var nodes = skills.Where(skill => (skill.Id - 1) / 25 + 1 == character
                && (skill.Id - 1) % 25 / 5 + 1 == index).ToArray();
            return new SkillTreeDefinition(character, index, fields[2], Array.AsReadOnly(nodes));
        }).ToArray();
        if (!skills.Select(skill => skill.Id).SequenceEqual(Enumerable.Range(1, 200))
            || trees.Length != 40 || trees.Any(tree => tree.Skills.Count != 5)
            || trees.Select(tree => (tree.CharacterId, tree.Index)).Distinct().Count() != 40)
            throw new InvalidDataException("Invalid skill tree catalog.");
        return Array.AsReadOnly(trees);
    }

    private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static IEnumerable<string[]> ReadRows(string filename)
    {
        using var stream = typeof(SkillCatalog).Assembly.GetManifestResourceStream($"XbdeEditor.Core.Data.{filename}")
            ?? throw new InvalidDataException($"Missing skill catalog: {filename}");
        using var reader = new StreamReader(stream);
        reader.ReadLine();
        while (reader.ReadLine() is { } line) yield return line.Split('\t');
    }
}
