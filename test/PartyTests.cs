using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class PartyTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (bool future in new[] { false, true })
        {
            var bytes = fixture(future);
            var save = SaveDocument.Parse(bytes);
            var before = save.PartyIds.ToArray();
            var reverse = before.Reverse().ToArray();
            save.ReorderParty(reverse);
            check(save.PartyIds.SequenceEqual(reverse) && save.Characters.Select(character => character.Id).SequenceEqual(reverse),
                "Party order does not preserve character identity.");
            var result = save.Serialize();
            check(result.AsSpan(0, 0x152318).SequenceEqual(bytes.AsSpan(0, 0x152318))
                && result.AsSpan(0x152318 + before.Length * 2).SequenceEqual(bytes.AsSpan(0x152318 + before.Length * 2)),
                "Party reorder changed recruitment, inactive slots or character records.");
            save.ReorderParty(reverse);
            check(save.Serialize().AsSpan().SequenceEqual(result), "Party reorder is not idempotent.");
            reject(() => save.ReorderParty([]), "Empty party accepted.");
            reject(() => save.ReorderParty(reverse.Skip(1).ToArray()), "Party member removal accepted.");
            reject(() => save.ReorderParty(reverse.Append(9).ToArray()), "Party member addition accepted.");
            reject(() => save.ReorderParty(Enumerable.Repeat(reverse[0], reverse.Length).ToArray()), "Duplicate party IDs accepted.");
            reject(() => save.ReorderParty(reverse.Select(id => id == reverse[0] ? 27 : id).ToArray()), "Party replacement accepted.");
            reject(() => save.ReorderParty(null!), "Null party order accepted.");
            check(save.Serialize().AsSpan().SequenceEqual(result), "Invalid party edit partially changed bytes.");
            save.ReorderParty(before);
            check(save.Serialize().AsSpan().SequenceEqual(bytes), "Restoring party order changed unrelated bytes.");
        }
        var unknown = fixture(false);
        unknown[0x152330] = 1;
        var ambiguous = SaveDocument.Parse(unknown);
        reject(() => ambiguous.ReorderParty([1]), "Unknown campaign permits party changes.");
        var guest = fixture(false);
        BinaryPrimitives.WriteUInt16LittleEndian(guest.AsSpan(0x15231a), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(guest.AsSpan(0x152368 + 8 * 0x138), 20);
        var guestSave = SaveDocument.Parse(guest);
        reject(() => guestSave.ReorderParty(guestSave.PartyIds.Reverse().ToArray()), "Story guest party permits reordering.");
    }
}
