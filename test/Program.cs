using System.Buffers.Binary;
using XbdeEditor.Core;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
void Reject(Action action, string message)
{
    try { action(); }
    catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException) { checks++; return; }
    throw new InvalidOperationException(message);
}
byte[] Fixture(bool future = false)
{
    byte[] data = new byte[SaveDocument.FileSize];
    new Random(17).NextBytes(data);
    data.AsSpan(0x152318, 25).Clear();
    int[] ids = future ? [1, 7, 14, 15] : [1, 2, 8, 5, 4, 7, 6];
    data[0x152330] = (byte)ids.Length;
    for (int index = 0; index < ids.Length; index++)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x152318 + index * 2), (ushort)ids[index]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x152368 + ids[index] * 0x138), 99);
    }
    return data;
}

foreach (bool future in new[] { false, true })
{
    byte[] original = Fixture(future);
    var save = SaveDocument.Parse(original);
    Check(save.Serialize().AsSpan().SequenceEqual(original), "Round-trip altered unknown bytes.");
    save.SetResources(123, 456);
    byte[] edited = save.Serialize();
    Check(save.Money == 123 && save.Noponstones == 456, "Resource edit differs.");
    for (int offset = 0; offset < edited.Length; offset++)
        if (offset is not (>= 0x10 and < 0x14) and not (>= 0x151b40 and < 0x151b44)
            && original[offset] != edited[offset])
            throw new InvalidOperationException($"Resource editing altered byte {offset:X}.");
    Check(save.Campaign == (future ? Campaign.FutureConnected : Campaign.MainStory), "Campaign mismatch.");
    original[0] ^= 0xff;
    Check(save.Serialize()[0] != original[0], "The parser retains its caller's buffer.");
    byte[] copy = save.Serialize();
    copy[0] ^= 0xff;
    Check(copy[0] != save.Serialize()[0], "Serialization exposes mutable storage.");
}
Reject(() => SaveDocument.Parse(new byte[1688]), "System save was accepted.");
Reject(() => SaveDocument.Parse(new byte[SaveDocument.FileSize]), "Invalid party was accepted.");
byte[] bad = Fixture();
bad[0x152330] = 13;
Reject(() => SaveDocument.Parse(bad), "Oversized party was accepted.");
bad = Fixture();
BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x15231a), 1);
Reject(() => SaveDocument.Parse(bad), "Duplicate party IDs were accepted.");

string temporary = Path.Combine(Path.GetTempPath(), $"xbde-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporary);
try
{
    string source = Path.Combine(temporary, "fixture.sav");
    byte[] original = Fixture();
    File.WriteAllBytes(source, original);
    var session = SaveSession.Open(source);
    Check(!session.HasChanges, "Opening a save marks it dirty.");
    string output = Path.Combine(temporary, "copy.sav");
    session.Save(output);
    Check(File.ReadAllBytes(output).AsSpan().SequenceEqual(original), "Copy is not lossless.");
    session.Save(source);
    Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(original), "Save is not lossless.");
    Reject(() => session.Save(Path.Combine(temporary, "missing", "copy.sav")), "Saving into a missing directory succeeded.");
    File.WriteAllBytes(source, new byte[5]);
    Reject(() => session.Save(source), "An externally changed source was overwritten.");
    Check(File.ReadAllBytes(source).Length == 5, "Failed save damaged the source.");
    Check(!Directory.EnumerateFiles(temporary, ".xbde-*.tmp").Any(), "Temporary files were leaked.");

    if (args is ["--save-directory", var directory])
    {
        foreach (string realPath in Directory.EnumerateFiles(directory, "*.sav")
            .Where(path => Path.GetFileName(path).StartsWith("bfsgame") || Path.GetFileName(path).StartsWith("bfsmeria")))
        {
            byte[] before = File.ReadAllBytes(realPath);
            var realSession = SaveSession.Open(realPath);
            Check(realSession.Document.Serialize().AsSpan().SequenceEqual(before), "Real save round-trip differs.");
            string realCopy = Path.Combine(temporary, Path.GetFileName(realPath));
            realSession.Save(realCopy);
            Check(File.ReadAllBytes(realCopy).AsSpan().SequenceEqual(before), "Real save copy differs.");
            Check(File.ReadAllBytes(realPath).AsSpan().SequenceEqual(before), "Original real save changed.");
            Console.WriteLine($"{Path.GetFileName(realPath)}: {realSession.Document.Campaign}, {realSession.Document.PartyIds.Count} characters; lossless copy passed.");
        }
    }
}
finally { Directory.Delete(temporary, recursive: true); }
Console.WriteLine($"Core tests passed: {checks} checks.");
