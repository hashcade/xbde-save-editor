using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class RegionAffinityTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] bytes = fixture(false);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
        var document = SaveDocument.Parse(bytes);
        check(document.CanEditRegionAffinity && document.RegionAffinities.Select(region => region.Id).SequenceEqual(new[] { 1, 2, 3, 4, 5 }),
            "Region affinity does not enumerate the five native areas.");
        check(document.Serialize().AsSpan().SequenceEqual(bytes), "Inspecting region affinity changed bytes.");
        foreach (int id in Enumerable.Range(1, 5))
        {
            foreach (int points in new[] { 0, 1, 1_999, 2_000, 3_999, 4_000, 5_999, 6_000, 7_999, 8_000, 9_999, 10_000 })
            {
                var copy = SaveDocument.Parse(bytes);
                var region = copy.GetRegionAffinity(id);
                region.SetPoints(points);
                byte[] expected = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xdf2 + id * 2), (ushort)points);
                check(copy.Serialize().AsSpan().SequenceEqual(expected), "Region edit changed story, NPCs, quests, pair affinity or achievements.");
                check(region.Points == points && region.Stars == Math.Min(points / 2_000 + 1, 5), "Region stars differ from the native thresholds.");
                region.SetStars(region.Stars);
                check(copy.Serialize().AsSpan().SequenceEqual(expected), "Selecting unchanged stars lost points inside the current tier.");
            }
            foreach (int stars in Enumerable.Range(1, 5))
            {
                var copy = SaveDocument.Parse(bytes);
                copy.GetRegionAffinity(id).SetPoints(0);
                copy.GetRegionAffinity(id).SetStars(stars);
                byte[] expected = (byte[])bytes.Clone();
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xdf2 + id * 2), (ushort)((stars - 1) * 2_000));
                check(copy.Serialize().AsSpan().SequenceEqual(expected), "Region star selection did not set the tier minimum.");
            }
        }
        foreach (int invalidId in new[] { -1, 0, 6, 7, int.MaxValue })
            reject(() => document.GetRegionAffinity(invalidId), "An unused region record was exposed for editing.");
        var first = document.GetRegionAffinity(1);
        reject(() => first.SetPoints(-1), "Negative region affinity was accepted.");
        reject(() => first.SetPoints(10_001), "Region affinity above the native cap was accepted.");
        reject(() => first.SetStars(0), "Zero-star region affinity was accepted.");
        reject(() => first.SetStars(6), "Region affinity above five stars was accepted.");
        check(document.Serialize().AsSpan().SequenceEqual(bytes), "Rejected region edits changed bytes.");
        document.MaxAllRegionAffinity();
        byte[] maximum = (byte[])bytes.Clone();
        for (int id = 1; id <= 5; id++) BinaryPrimitives.WriteUInt16LittleEndian(maximum.AsSpan(0xdf2 + id * 2), 10_000);
        check(document.RegionAffinities.All(region => region.IsMaximum && region.Stars == 5)
            && document.Serialize().AsSpan().SequenceEqual(maximum), "Bulk region maximum modified more than its five u16 fields.");
        document.MaxAllRegionAffinity();
        check(document.Serialize().AsSpan().SequenceEqual(maximum), "Region maximum is not idempotent.");
        byte[] anomalous = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(anomalous.AsSpan(0xdf4), ushort.MaxValue);
        var unusual = SaveDocument.Parse(anomalous);
        check(unusual.GetRegionAffinity(1).Points == ushort.MaxValue && unusual.GetRegionAffinity(1).Stars == 5
            && unusual.Serialize().AsSpan().SequenceEqual(anomalous), "Unusual region affinity was normalized on load.");
        unusual.GetRegionAffinity(1).SetStars(5);
        check(unusual.Serialize().AsSpan().SequenceEqual(anomalous), "Unchanged stars rewrote unusual saved points.");
        VerifyRealSave(bytes, check);
        VerifyRealSave(anomalous, check);
        foreach (uint version in new uint[] { 0, 1, 6, 8, uint.MaxValue })
        {
            byte[] unsupported = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(unsupported, version);
            var protectedSave = SaveDocument.Parse(unsupported);
            check(!protectedSave.CanEditRegionAffinity && protectedSave.RegionAffinities.Count == 5, "Unverified format is not read-only.");
            reject(protectedSave.MaxAllRegionAffinity, "Unverified format accepted bulk region edits.");
            reject(() => protectedSave.GetRegionAffinity(1).SetPoints(1), "Unverified format accepted region point edits.");
            reject(() => protectedSave.GetRegionAffinity(1).SetStars(5), "Unverified format accepted region star edits.");
            check(protectedSave.Serialize().AsSpan().SequenceEqual(unsupported), "Rejected unverified region edits changed bytes.");
        }
        foreach (bool future in new[] { false, true })
        {
            byte[] unsupported = future ? fixture(true) : (byte[])bytes.Clone();
            if (!future) unsupported[0x152330] = 1;
            var protectedSave = SaveDocument.Parse(unsupported);
            check(!protectedSave.CanEditRegionAffinity && protectedSave.RegionAffinities.Count == 0, "Another campaign exposes five main-story areas.");
            reject(protectedSave.MaxAllRegionAffinity, "Another campaign accepted bulk region editing.");
            reject(() => protectedSave.GetRegionAffinity(1), "Another campaign exposes a main-story region.");
            check(protectedSave.Serialize().AsSpan().SequenceEqual(unsupported), "Protected campaign changed bytes.");
            VerifyRealSave(unsupported, check);
        }
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        if (save.Campaign == Campaign.FutureConnected)
        {
            check(save.RegionAffinities.Count == 0 && save.Serialize().AsSpan().SequenceEqual(bytes),
                "Inspecting Future Connected exposed main-story regions or changed protected bytes.");
            return;
        }
        if (!save.CanEditRegionAffinity) return;
        check(save.RegionAffinities.Select(region => region.Id).SequenceEqual(new[] { 1, 2, 3, 4, 5 }),
            "Real main-story regions differ from the five native areas.");
        foreach (var region in save.RegionAffinities)
        {
            int points = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0xdf2 + region.Id * 2));
            check(region.Points == points && region.Stars == Math.Min(points / 2_000 + 1, 5),
                "Real region points or stars differ from the native field.");
            var copy = SaveDocument.Parse(bytes);
            copy.GetRegionAffinity(region.Id).SetPoints(1);
            byte[] expected = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xdf2 + region.Id * 2), 1);
            check(copy.Serialize().AsSpan().SequenceEqual(expected), "Real region editing changed unrelated bytes.");
        }
        check(save.Serialize().AsSpan().SequenceEqual(bytes), "Inspecting real regions changed bytes.");
        byte[] maximum = (byte[])bytes.Clone();
        for (int id = 1; id <= 5; id++) BinaryPrimitives.WriteUInt16LittleEndian(maximum.AsSpan(0xdf2 + id * 2), 10_000);
        save.MaxAllRegionAffinity();
        check(save.RegionAffinities.All(region => region.IsMaximum) && save.Serialize().AsSpan().SequenceEqual(maximum),
            "Maximum real region affinity changed unrelated bytes or missed an area.");
        save.MaxAllRegionAffinity();
        check(save.Serialize().AsSpan().SequenceEqual(maximum), "Maximum real region affinity is not idempotent.");
    }
}
