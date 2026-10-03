using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class CollectopaediaTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        check(CollectopaediaCatalog.All.Count == 328
            && CollectopaediaCatalog.All.All(entry => !string.IsNullOrWhiteSpace(entry.Name)),
            "Collectopaedia item references do not resolve to the inventory catalog.");
        check(CollectopaediaCatalog.Rewards.Count == 133
            && CollectopaediaCatalog.Rewards[0].ItemId == 283
            && CollectopaediaCatalog.Rewards[^1].ItemId == 3859, "Collectopaedia reward rows differ from the native table.");
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = fixture(future);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
            bytes.AsSpan(0x1490a8, 48).Clear();
            int[] ids = future ? Enumerable.Range(319, 28).ToArray() : Enumerable.Range(1, 300).ToArray();
            var document = SaveDocument.Parse(bytes);
            check(document.CanInspectCollectopaedia && document.Collectopaedia.Select(entry => entry.Id).SequenceEqual(ids),
                "Collectopaedia mixed campaigns or exposed placeholder entries.");
            check(document.Collectopaedia.All(entry => !entry.IsRegistered), "Empty Collectopaedia flags were read as registered.");
            foreach (int id in ids)
            {
                byte[] one = (byte[])bytes.Clone();
                int bit = 0x19e9 + id;
                int wordOffset = 0x148d6c + 4 * (bit >> 5);
                BinaryPrimitives.WriteUInt32LittleEndian(one.AsSpan(wordOffset), 1u << ((id + 9) & 31));
                var isolated = SaveDocument.Parse(one);
                check(isolated.Collectopaedia.Where(entry => entry.IsRegistered).Select(entry => entry.Id).SequenceEqual(new[] { id }),
                    $"Collectopaedia ID {id} reads an adjacent bit or word.");
                check(isolated.Serialize().AsSpan().SequenceEqual(one), "Reading Collectopaedia normalized or changed the save.");
            }
            foreach (int id in ids)
            {
                int bit = 0x19e9 + id;
                bytes[0x148d6c + (bit >> 3)] |= (byte)(1 << (bit & 7));
            }
            document = SaveDocument.Parse(bytes);
            check(document.Collectopaedia.All(entry => entry.IsRegistered), "Full Collectopaedia missed a completion bit.");
            check(document.Collectopaedia.Select(entry => entry.MapId).Distinct().Count() == (future ? 2 : 21),
                "Collectopaedia page grouping differs from the native map IDs.");
            foreach (int id in new[] { -1, 0, 301, 318, 347, 349, int.MaxValue, future ? 1 : 319 })
                reject(() => document.GetCollectopaediaEntry(id), "Collectopaedia exposed a placeholder or another campaign's item.");
            check(document.Serialize().AsSpan().SequenceEqual(bytes), "Collectopaedia inspection changed inventory or flags.");
            foreach (uint version in new uint[] { 0, 6, 8, uint.MaxValue })
            {
                byte[] unknown = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(unknown, version);
                var unsupported = SaveDocument.Parse(unknown);
                check(!unsupported.CanInspectCollectopaedia && unsupported.Collectopaedia.Count == 0,
                    "Collectopaedia interpreted an unverified save version.");
                reject(() => unsupported.GetCollectopaediaEntry(ids[0]), "Unverified layout exposed a registration flag.");
                check(unsupported.Serialize().AsSpan().SequenceEqual(unknown), "Unverified Collectopaedia inspection changed bytes.");
            }
        }
        byte[] ambiguous = fixture(true);
        BinaryPrimitives.WriteUInt32LittleEndian(ambiguous, 7);
        ambiguous[0x152330] = 2;
        var unidentified = SaveDocument.Parse(ambiguous);
        check(!unidentified.CanInspectCollectopaedia && unidentified.Collectopaedia.Count == 0,
            "An unidentified campaign exposed a guessed Collectopaedia catalog.");
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var document = SaveDocument.Parse(bytes);
        if (!document.CanInspectCollectopaedia) return;
        foreach (var entry in document.Collectopaedia)
        {
            int bit = 0x19e9 + entry.Id;
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x148d6c + 4 * (bit >> 5)));
            check(entry.IsRegistered == ((word & (1u << ((entry.Id + 9) & 31))) != 0),
                "Real Collectopaedia differs from the native word and mask calculation.");
        }
        check(document.Serialize().AsSpan().SequenceEqual(bytes), "Inspecting a real Collectopaedia changed the save.");
        Console.WriteLine($"Collectopaedia: {document.Collectopaedia.Count(entry => entry.IsRegistered)}/{document.Collectopaedia.Count} registered.");
    }
}
