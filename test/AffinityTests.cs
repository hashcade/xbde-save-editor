using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class AffinityTests
{
    private static int UnlockOffset(int recipient, int source) => 0x152368 + (recipient - 1) * 0x138 + 0xa4 + (source - 1) * 4;
    private static readonly int[][] Matrix =
    [
        [-1, 1, 2, 3, 4, 5, 6, 2], [1, -1, 7, 8, 9, 10, 11, 7],
        [2, 7, -1, 12, 13, 14, 15, -1], [3, 8, 12, -1, 16, 17, 18, 12],
        [4, 9, 13, 16, -1, 19, 20, 13], [5, 10, 14, 17, 19, -1, 21, 14],
        [6, 11, 15, 18, 20, 21, -1, 15], [2, 7, -1, 12, 13, 14, 15, -1]
    ];

    internal static byte[] Fixture(byte[] bytes)
    {
        SkillLinksTests.Fixture(bytes);
        bytes.AsSpan(0xe02, 42).Clear();
        for (int recipient = 1; recipient <= 8; recipient++)
            bytes.AsSpan(UnlockOffset(recipient, 1), 32).Clear();
        return bytes;
    }

    private static void ExpectEdit(byte[] expected, int index, int points)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xe00 + index * 2), (ushort)points);
        uint rank = (uint)Math.Min(points / 1_000, 4);
        for (int recipient = 1; recipient <= 8; recipient++)
            for (int source = 1; source <= 8; source++)
                if (Matrix[recipient - 1][source - 1] == index)
                {
                    int offset = UnlockOffset(recipient, source);
                    uint previous = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset));
                    BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(offset), Math.Max(previous, rank));
                }
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] bytes = Fixture(fixture(false));
        var document = SaveDocument.Parse(bytes);
        check(document.CanEditAffinity && document.Affinities.Count == 21, "Fiora aliases created a duplicate affinity pair.");
        check(document.Affinities.All(pair => pair.FirstCharacterId != 3 && pair.SecondCharacterId != 3), "Joined later Fiora was not chosen for display.");
        check(document.Serialize().AsSpan().SequenceEqual(bytes), "Inspecting affinity altered bytes.");
        for (int first = 1; first <= 8; first++)
            for (int second = 1; second <= 8; second++)
            {
                int index = Matrix[first - 1][second - 1];
                if (index < 1) reject(() => document.GetAffinity(first, second), "Self or alternate-form affinity was accepted.");
                else check(document.GetAffinity(first, second).Index == index, "Affinity index differs from the native matrix.");
            }
        foreach (var pair in document.Affinities)
            foreach (int points in new[] { 0, 999, 1_000, 1_999, 2_000, 2_999, 3_000, 3_999, 4_000, 4_999, 5_000 })
            {
                var copy = SaveDocument.Parse(bytes);
                var target = copy.GetAffinity(pair.FirstCharacterId, pair.SecondCharacterId);
                target.SetPoints(points);
                byte[] expected = (byte[])bytes.Clone();
                ExpectEdit(expected, pair.Index, points);
                check(copy.Serialize().AsSpan().SequenceEqual(expected), "Affinity editing changed another pair, links, coins or achievements.");
                int slots = Math.Min(points / 1_000, 4) + 1;
                check(target.Points == points && target.FirstUnlockedSlots == slots && target.SecondUnlockedSlots == slots,
                    "Affinity threshold or directed slot count differs.");
                target.SetPoints(points);
                check(copy.Serialize().AsSpan().SequenceEqual(expected), "Affinity assignment is not idempotent.");
            }
        var firstPair = document.GetAffinity(1, 2);
        firstPair.SetPoints(5_000);
        firstPair.SetPoints(0);
        check(firstPair.Points == 0 && firstPair.FirstUnlockedSlots == 5 && firstPair.SecondUnlockedSlots == 5,
            "Lowering affinity relocked learned link slots.");
        byte[] beforeInvalid = document.Serialize();
        reject(() => firstPair.SetPoints(-1), "Negative affinity was accepted.");
        reject(() => firstPair.SetPoints(5_001), "Affinity above the native cap was accepted.");
        reject(() => document.GetAffinity(1, 14), "Affinity with an absent character was accepted.");
        check(document.Serialize().AsSpan().SequenceEqual(beforeInvalid), "Invalid affinity partially changed bytes.");

        byte[] unusual = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(unusual.AsSpan(0xe02), 60_000);
        BinaryPrimitives.WriteUInt32LittleEndian(unusual.AsSpan(UnlockOffset(1, 2)), uint.MaxValue);
        var unusualSave = SaveDocument.Parse(unusual);
        check(unusualSave.GetAffinity(1, 2).FirstUnlockedSlots is null
            && unusualSave.Serialize().AsSpan().SequenceEqual(unusual), "Anomalous affinity or slot counts were normalized on load.");
        unusualSave.GetAffinity(1, 2).SetPoints(5_000);
        byte[] unusualExpected = (byte[])unusual.Clone();
        ExpectEdit(unusualExpected, 1, 5_000);
        check(unusualSave.Serialize().AsSpan().SequenceEqual(unusualExpected), "Maximum affinity lowered an anomalous saved unlock count.");

        document = SaveDocument.Parse(bytes);
        document.MaxAllAffinity();
        byte[] maximum = (byte[])bytes.Clone();
        for (int index = 1; index <= 21; index++) ExpectEdit(maximum, index, 5_000);
        check(document.Affinities.All(pair => pair.IsMaximum) && document.Serialize().AsSpan().SequenceEqual(maximum),
            "Bulk maximum missed a pair or changed unrelated bytes.");
        document.MaxAllAffinity();
        check(document.Serialize().AsSpan().SequenceEqual(maximum), "Bulk affinity maximum is not idempotent.");

        byte[] partial = (byte[])bytes.Clone();
        partial[0x152330] = 2;
        var partialSave = SaveDocument.Parse(partial);
        partialSave.MaxAllAffinity();
        byte[] partialExpected = (byte[])partial.Clone();
        ExpectEdit(partialExpected, 1, 5_000);
        check(partialSave.Affinities.Count == 1 && partialSave.Serialize().AsSpan().SequenceEqual(partialExpected),
            "Bulk affinity changed an unjoined pair.");
        foreach (var kind in new[] { "future", "unknown", "version" })
        {
            byte[] unsupported = kind == "future" ? fixture(true) : (byte[])bytes.Clone();
            if (kind == "unknown") unsupported[0x152330] = 1;
            if (kind == "version") BinaryPrimitives.WriteUInt32LittleEndian(unsupported, 8);
            var protectedSave = SaveDocument.Parse(unsupported);
            check(!protectedSave.CanEditAffinity, "An unsupported campaign or version allows affinity edits.");
            reject(protectedSave.MaxAllAffinity, "Unsupported bulk affinity was accepted.");
            if (kind == "version") reject(() => protectedSave.GetAffinity(1, 2).SetPoints(1), "Unverified format allows pair editing.");
            else check(protectedSave.Affinities.Count == 0, "Unsupported campaign displays native affinity pairs.");
            check(protectedSave.Serialize().AsSpan().SequenceEqual(unsupported), "Unsupported affinity editing altered bytes.");
        }
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        if (!save.CanEditAffinity) return;
        check(save.Affinities.Count == 21 && save.Affinities.All(pair => pair.IsMaximum), "Real affinity layout differs from the verified 21 maximum pairs.");
        save.MaxAllAffinity();
        check(save.Serialize().AsSpan().SequenceEqual(bytes), "Maximum real affinity changed already-unlocked records.");
        var copy = SaveDocument.Parse(bytes);
        copy.GetAffinity(1, 8).SetPoints(0);
        byte[] expected = (byte[])bytes.Clone();
        ExpectEdit(expected, 2, 0);
        check(copy.Serialize().AsSpan().SequenceEqual(expected), "Lowering real Fiora affinity changed another field or relocked slots.");
    }
}
