using System.Text.Json;
using XbdeEditor.Core;

try
{
    if (args is ["--version"])
    {
        Console.WriteLine(typeof(SaveDocument).Assembly.GetName().Version?.ToString(3));
        return 0;
    }
    if (args is [] or ["--help"])
    {
        Console.WriteLine("XBDE Save Editor\n\ninspect <save>\ncopy <save> <output>\nresources <save> <output> [--money N] [--noponstones N]\ncharacter <save> <output> <id> [--ap N] [--coins N]\n--version");
        return 0;
    }
    if (args is ["inspect", var source])
    {
        var session = SaveSession.Open(source);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            File = Path.GetFileName(session.SourcePath),
            session.Document.Campaign,
            Size = SaveDocument.FileSize,
            session.Document.Sha256,
            session.Document.PartyIds,
            session.Document.Money,
            session.Document.Noponstones,
            Characters = session.Document.Characters.Select(character => new
            {
                character.Id, Name = CharacterCatalog.Get(character.Id, "en"),
                character.Level, character.Experience, character.AP, character.AffinityCoins,
                character.ExpertLevel, character.ExpertExperience, character.ReserveExperience
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["copy", var input, var output])
    {
        var session = SaveSession.Open(input);
        if (Path.GetFullPath(input) == Path.GetFullPath(output))
            throw new ArgumentException("Copy requires a different destination.");
        session.Save(output);
        return 0;
    }
    if (args is ["resources", var resourceSource, var resourceOutput, .. var options])
    {
        var session = SaveSession.Open(resourceSource);
        uint money = session.Document.Money;
        uint noponstones = session.Document.Noponstones;
        foreach (var (name, value) in ParseOptions(options, "--money", "--noponstones"))
        {
            switch (name)
            {
                case "--money": money = value; break;
                case "--noponstones": noponstones = value; break;
            }
        }
        session.Document.SetResources(money, noponstones);
        session.Save(resourceOutput);
        return 0;
    }
    if (args is ["character", var characterSource, var characterOutput, var characterId, .. var fields])
    {
        var session = SaveSession.Open(characterSource);
        if (!int.TryParse(characterId, out int id)) throw new ArgumentException("Character ID must be an integer.");
        var character = session.Document.GetCharacter(id);
        uint? ap = null;
        uint? coins = null;
        foreach (var (name, value) in ParseOptions(fields, "--ap", "--coins"))
        {
            switch (name)
            {
                case "--ap": ap = value; break;
                case "--coins": coins = value; break;
            }
        }
        character.SetResources(ap, coins);
        session.Save(characterOutput);
        return 0;
    }
    throw new ArgumentException("Unknown command. Run --help for usage.");
}
catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static IReadOnlyDictionary<string, uint> ParseOptions(string[] options, params string[] allowed)
{
    if (options.Length == 0 || options.Length % 2 != 0)
        throw new ArgumentException("Provide one or more option/value pairs.");
    var values = new Dictionary<string, uint>();
    for (int index = 0; index < options.Length; index += 2)
    {
        string name = options[index];
        if (!allowed.Contains(name)) throw new ArgumentException($"Unknown option: {name}");
        if (!uint.TryParse(options[index + 1], out uint value) || !values.TryAdd(name, value))
            throw new ArgumentException("Values must be unique options containing unsigned 32-bit integers.");
    }
    return values;
}
