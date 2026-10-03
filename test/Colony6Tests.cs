using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class Colony6Tests
{
    private static readonly (int Development, int Population, int Effect, int Self, int Quest)[] Rows =
    [
        (2,4,0,51,991), (2,8,0,52,992), (3,15,0,53,993), (3,18,0,54,994), (3,27,46,55,995),
        (2,1,0,0,0), (2,1,0,96,0), (2,1,0,0,0), (3,1,0,0,0), (4,2,0,0,982),
        (1,0,0,0,984), (2,0,38,0,985), (2,0,0,0,986), (3,0,0,0,987), (5,3,0,0,998),
        (3,0,0,0,934), (3,2,39,0,990), (4,0,53,0,997), (5,0,44,0,937), (6,3,29,0,938),
        (0,0,32,0,0), (0,0,33,0,0), (0,0,34,0,0), (0,0,35,0,0), (0,0,36,0,0)
    ];

    internal static byte[] StartedFixture(byte[] bytes)
    {
        bytes = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
        bytes.AsSpan(0xcf8, 7).Clear();
        bytes[0xcf8] = 20;
        bytes[0xcf9] = 30;
        bytes[0xcfa] = 1;
        foreach (var row in Rows)
        {
            if (row.Self != 0) bytes[0xc94 + row.Self] = 0;
            if (row.Quest != 0) bytes[0x5f0 + row.Quest] = 0;
        }
        return bytes;
    }

    internal static byte[] ExpectedMaximum(byte[] before)
    {
        byte[] expected = (byte[])before.Clone();
        for (int category = 0; category < 5; category++)
        {
            for (int level = before[0xcfa + category]; level < 5; level++)
            {
                var row = Rows[category * 5 + level];
                expected[0xcf8] += (byte)row.Development;
                expected[0xcf9] += (byte)row.Population;
                if (row.Effect != 0)
                {
                    int bit = 0x278a + row.Effect - 0xa20;
                    expected[0x50 + (bit >> 3)] |= (byte)(1 << (bit & 7));
                }
                if (row.Self != 0) expected[0xc94 + row.Self] = 1;
                if (row.Quest != 0) expected[0x5f0 + row.Quest] = 200;
            }
            expected[0xcfa + category] = 5;
        }
        return expected;
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] original = StartedFixture(fixture(false));
        for (int combination = 1; combination < 1_296; combination++)
        {
            byte[] bytes = (byte[])original.Clone();
            int digits = combination;
            for (int category = 0; category < 4; category++)
            {
                bytes[0xcfa + category] = (byte)(digits % 6);
                digits /= 6;
            }
            int minimum = bytes.AsSpan(0xcfa, 4).ToArray().Min();
            bytes[0xcfe] = (byte)(combination % (minimum + 1));
            var document = SaveDocument.Parse(bytes);
            check(document.Colony6 is { CanMaximize: true }
                && document.Serialize().AsSpan().SequenceEqual(bytes), "Colony inspection changed or misidentified saved fields.");
            document.MaximizeColony6();
            byte[] expected = ExpectedMaximum(bytes);
            check(document.Colony6 is { IsMaximum: true }
                && document.Serialize().AsSpan().SequenceEqual(expected), "Reconstruction differs from the native missing upgrade rows.");
            document.MaximizeColony6();
            check(document.Serialize().AsSpan().SequenceEqual(expected), "Reconstruction added development/population twice.");
        }
        byte[] completed = (byte[])original.Clone();
        completed[0xcf8] = 100;
        completed[0xcf9] = 150;
        completed.AsSpan(0xcfa, 5).Fill(5);
        completed[0xcf4] = 2;
        var maximum = SaveDocument.Parse(completed);
        maximum.MaximizeColony6();
        check(maximum.Serialize().AsSpan().SequenceEqual(completed), "Maximum colony or its post-upgrade flags were normalized.");

        byte[] flagsAlreadySet = (byte[])original.Clone();
        foreach (var row in Rows)
        {
            if (row.Self != 0) flagsAlreadySet[0xc94 + row.Self] = 1;
            if (row.Quest != 0) flagsAlreadySet[0x5f0 + row.Quest] = 200;
        }
        var flagged = SaveDocument.Parse(flagsAlreadySet);
        flagged.MaximizeColony6();
        check(flagged.Serialize().AsSpan().SequenceEqual(ExpectedMaximum(flagsAlreadySet)),
            "Already-set flags skipped missing facility upgrades or repeated past-level increments.");
        foreach (var (offset, value) in new (int, byte)[]
        {
            (0xcfa, 6), (0xcfe, 1), (0xcf8, 255), (0xcf9, 255), (0xcf4, 2), (0x5f0 + 998, 255)
        })
        {
            byte[] invalid = (byte[])original.Clone();
            invalid[offset] = value;
            var document = SaveDocument.Parse(invalid);
            check(document.Colony6 is { CanMaximize: false }
                && document.Serialize().AsSpan().SequenceEqual(invalid), "Invalid colony was normalized during inspection.");
            reject(document.MaximizeColony6, "Invalid reconstruction state was accepted.");
            check(document.Serialize().AsSpan().SequenceEqual(invalid), "Rejected reconstruction partially changed the save.");
        }
        foreach (uint version in new uint[] { 0, 1, 6, 8, uint.MaxValue })
        {
            byte[] bytes = (byte[])original.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, version);
            var document = SaveDocument.Parse(bytes);
            check(document.Colony6 is { CanMaximize: false }, "Unverified format enabled reconstruction.");
            reject(document.MaximizeColony6, "Unverified format accepted reconstruction.");
            check(document.Serialize().AsSpan().SequenceEqual(bytes), "Protected format changed bytes.");
        }
        byte[] unstarted = (byte[])original.Clone();
        unstarted.AsSpan(0xcfa, 5).Clear();
        var early = SaveDocument.Parse(unstarted);
        reject(early.MaximizeColony6, "Unstarted reconstruction bypassed story prerequisites.");
        check(early.Serialize().AsSpan().SequenceEqual(unstarted), "Unstarted reconstruction changed bytes.");
        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = StartedFixture(fixture(future));
            if (!future) bytes[0x152330] = 1;
            var document = SaveDocument.Parse(bytes);
            check(document.Colony6 is null, "Another campaign exposes main-story reconstruction.");
            reject(document.MaximizeColony6, "Another campaign accepted reconstruction.");
            check(document.Serialize().AsSpan().SequenceEqual(bytes), "Protected campaign changed bytes.");
        }
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var document = SaveDocument.Parse(bytes);
        if (document.Campaign == Campaign.FutureConnected)
        {
            check(document.Colony6 is null && bytes.AsSpan(0xcf8, 8).IndexOfAnyExcept((byte)0) < 0,
                "Real Future Connected reconstruction is not protected.");
            return;
        }
        if (document.Colony6 is not { CanMaximize: true } state) return;
        check(state.Development == bytes[0xcf8] && state.Population == bytes[0xcf9]
            && state.Housing == bytes[0xcfa] && state.OverallLevel == bytes[0xcfe], "Real colony maps to the wrong fields.");
        document.MaximizeColony6();
        check(document.Serialize().AsSpan().SequenceEqual(ExpectedMaximum(bytes)), "Real reconstruction differs outside native write boundaries.");
        if (state.IsMaximum) check(document.Serialize().AsSpan().SequenceEqual(bytes), "Real maximum colony changed bytes.");
    }
}
