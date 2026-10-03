namespace XbdeEditor.Core;

public sealed class SaveSession
{
    private byte[] _original;

    private SaveSession(string sourcePath, byte[] original, SaveDocument document)
    {
        SourcePath = sourcePath;
        _original = original;
        Document = document;
    }

    public string SourcePath { get; }
    public SaveDocument Document { get; }
    public bool HasChanges => !Document.Serialize().AsSpan().SequenceEqual(_original);

    public static SaveSession Open(string path)
    {
        string fullPath = Path.GetFullPath(path);
        byte[] original = File.ReadAllBytes(fullPath);
        return new SaveSession(fullPath, original, SaveDocument.Parse(original));
    }

    public void Save(string destination)
    {
        string fullPath = Path.GetFullPath(destination);
        byte[] bytes = Document.Serialize();
        _ = SaveDocument.Parse(bytes);
        bool sameSource = string.Equals(fullPath, SourcePath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        if (sameSource && !File.ReadAllBytes(SourcePath).AsSpan().SequenceEqual(_original))
            throw new IOException("The source save changed on disk. Reopen it before saving.");
        string directory = Path.GetDirectoryName(fullPath)!;
        string temporary = Path.Combine(directory, $".xbde-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
            if (sameSource) _original = bytes;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
