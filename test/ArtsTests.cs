using XbdeEditor.Core;

internal static class ArtsTests
{
    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        const int table = 0x1536e8;
        foreach (bool future in new[] { false, true })
        {
            byte[] original = fixture(future);
            original.AsSpan(table, 188 * 2).Clear();
            foreach (var definition in ArtCatalog.All)
            {
                original[table + (definition.Id - 1) * 2] = 1;
                original[table + (definition.Id - 1) * 2 + 1] = 0x80;
            }
            original[table + 4] = 2;
            original[table + 5] = 0x83;
            original[table + 32] = 0; // Battle Soul remains unlearned.
            var save = SaveDocument.Parse(original);
            check(save.Serialize().AsSpan().SequenceEqual(original), "Art loading changed unknown flags.");
            var shulk = save.GetCharacter(1);
            var slash = shulk.GetArt(12);
            check(slash.MaximumLevel == (future ? 10 : 12), "Campaign art cap differs.");
            foreach ((int level, int flags) in new[] { (1, 0x80), (4, 0x80), (5, 0x81), (7, 0x81), (8, 0x83), (10, 0x83) })
            {
                slash.SetLevel(level);
                check(slash.Level == level && slash.ManualFlags == flags, "Art level/manual threshold differs.");
            }
            slash.SetLevel(slash.MaximumLevel);
            check(slash.ManualFlags == (future ? 0x83 : 0x87), "Master book flag differs.");
            slash.SetLevel(1);
            check(slash.ManualFlags == (future ? 0x83 : 0x87), "Lowering an art removed a learned manual.");
            byte[] beforeRejected = save.Serialize();
            reject(() => slash.SetLevel(0), "Zero level unlocked or deleted an art.");
            reject(() => slash.SetLevel(slash.MaximumLevel + 1), "Art above its cap was accepted.");
            reject(() => shulk.GetArt(1).SetLevel(2), "Fixed talent art was editable.");
            reject(() => shulk.GetArt(17).SetLevel(1), "An unlearned art was unlocked.");
            reject(() => shulk.GetArt(20), "Another character's art was accepted.");
            reject(() => shulk.GetArt(189), "Art outside the saved table was accepted.");
            check(save.Serialize().AsSpan().SequenceEqual(beforeRejected), "Rejected art edit mutated bytes.");
            var monado = shulk.GetArt(3);
            check(monado.MaximumLevel == 10, "Monado maximum differs.");
            reject(() => monado.SetLevel(11), "Monado accepted a master level.");
            monado.SetLevel(10);
            check(monado.ManualFlags == 0x83, "Monado edit changed story permission.");
            check(shulk.GetArt(5).MaximumLevel == 4, "Locked Monado limit was ignored.");
            reject(() => shulk.GetArt(5).SetLevel(5), "Monado edit bypassed story permission.");
            var melia = save.GetCharacter(7);
            reject(() => melia.GetArt(97), "Derived discharge was directly editable.");
            foreach (var discharge in ArtCatalog.All.Where(art => art.LinkedArtId is not null))
            {
                var summon = melia.GetArt(discharge.LinkedArtId!.Value);
                byte[] before = save.Serialize();
                summon.SetLevel(summon.MaximumLevel);
                byte[] after = save.Serialize();
                int start = table + (summon.Id - 1) * 2;
                int linked = table + (discharge.Id - 1) * 2;
                check(after[linked] == summon.Level && after[linked + 1] == before[linked + 1], "Discharge linkage/flags differ.");
                check(Enumerable.Range(0, after.Length).All(offset => before[offset] == after[offset]
                    || offset == start || offset == start + 1 || offset == linked), "Summon edit changed unrelated bytes.");
            }
            foreach (var character in save.Characters)
            {
                byte[] before = save.Serialize();
                character.MaxArts();
                byte[] after = save.Serialize();
                var editable = character.Arts.Where(art => art.CanEdit).ToArray();
                check(editable.All(art => art.Level == art.MaximumLevel), "Character art maximum missed an art.");
                var allowed = editable.SelectMany(art => new[] { table + (art.Id - 1) * 2, table + (art.Id - 1) * 2 + 1 })
                    .Concat(ArtCatalog.All.Where(art => art.LinkedArtId is { } id && editable.Any(source => source.Id == id))
                        .Select(art => table + (art.Id - 1) * 2)).ToHashSet();
                check(Enumerable.Range(0, after.Length).All(offset => before[offset] == after[offset] || allowed.Contains(offset)),
                    "Bulk art editing altered AP, palette, story or other characters.");
                character.MaxArts();
                check(save.Serialize().AsSpan().SequenceEqual(after), "Bulk art maximum is not idempotent.");
            }
            check(!shulk.GetArt(17).Learned && shulk.GetArt(1).Level == 1, "Bulk arts unlocked a missing art or altered a talent.");
            if (future)
            {
                check(save.GetCharacter(14).GetArt(155).MaximumLevel == 10, "Kino cap differs.");
                check(save.GetCharacter(15).GetArt(172).MaximumLevel == 10, "Nene cap differs.");
                reject(() => shulk.GetArt(4).SetLevel(1), "Future Connected accepted Monado Enchant.");
            }
        }
        byte[] early = fixture(false);
        early[0x152330] = 3;
        early[0x15231c] = 3;
        early[0x15231d] = 0;
        early.AsSpan(0x1525d8, 4).Clear();
        early[0x1525d8] = 9;
        early[table + 72] = 1;
        var earlyFiora = SaveDocument.Parse(early).GetCharacter(3).GetArt(37);
        check(earlyFiora.MaximumLevel == 10, "Early Fiora received unavailable master books.");
        earlyFiora.SetLevel(10);
        reject(() => earlyFiora.SetLevel(11), "Early Fiora accepted a master level.");
        byte[] ambiguous = fixture(false);
        ambiguous[0x152330] = 1;
        ambiguous[table + 22] = 255;
        var unknown = SaveDocument.Parse(ambiguous);
        check(unknown.GetCharacter(1).GetArt(12).Level == 255, "Existing high art level was clamped.");
        reject(() => unknown.GetCharacter(1).MaxArts(), "Ambiguous campaign exposed art editing.");
        check(unknown.Serialize().AsSpan().SequenceEqual(ambiguous), "Ambiguous campaign edit changed bytes.");
    }
}
