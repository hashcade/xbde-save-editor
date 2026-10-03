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
        Console.WriteLine("XBDE Save Editor\n\ninspect <save>\ncopy <save> <output>\n--version");
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
            session.Document.PartyIds
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
    throw new ArgumentException("Unknown command. Run --help for usage.");
}
catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
