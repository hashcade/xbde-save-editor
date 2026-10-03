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
        reject(() => unknown.GetCharacter(1).GetArt(17).Learn(), "Ambiguous campaign allowed art learning.");
        reject(unknown.LearnAndMaxAllArts, "Ambiguous campaign allowed global art learning.");
        check(unknown.Serialize().AsSpan().SequenceEqual(ambiguous), "Rejected art learning changed bytes.");
        byte[] guestBytes = fixture(false);
        guestBytes[0x152318] = 9;
        guestBytes.AsSpan(0x152368 + 8 * 0x138, 4).Clear();
        guestBytes[0x152368 + 8 * 0x138] = 1;
        var withGuest = SaveDocument.Parse(guestBytes);
        reject(withGuest.LearnAndMaxAllArts, "Unsupported guest was included in global art learning.");
        check(withGuest.Serialize().AsSpan().SequenceEqual(guestBytes), "Guest rejection partially edited other characters.");
        byte[] futureGuestBytes = fixture(true);
        futureGuestBytes[0x152318] = 27;
        var withFutureGuest = SaveDocument.Parse(futureGuestBytes);
        check(!withFutureGuest.CanLearnAndMaxAllArts, "An unmapped Future Connected guest passed the bulk preflight.");
        reject(withFutureGuest.LearnAndMaxAllArts, "An unmapped guest was silently excluded from global art learning.");
        check(withFutureGuest.Serialize().AsSpan().SequenceEqual(futureGuestBytes), "Unmapped guest rejection partially edited the party.");
        TestLearning(fixture, check, reject);
    }

    private static void TestLearning(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        const int table = 0x1536e8;
        foreach (bool future in new[] { false, true })
        {
            byte[] original = fixture(future);
            original.AsSpan(table, 188 * 2).Clear();
            foreach (var definition in ArtCatalog.All)
            {
                original[table + (definition.Id - 1) * 2 + 1] = 0x80;
                if (definition.IsTalent) original[table + (definition.Id - 1) * 2] = 1;
            }
            var save = SaveDocument.Parse(original);
            var art = save.GetCharacter(1).GetArt(17);
            check(art.CanLearn && art.IsLevelLearned && art.LearnLevel > 0 && !art.RequiresEvent,
                "Ordinary learning metadata is missing.");
            reject(() => art.Learn(0), "Art learning accepted level zero.");
            reject(() => art.Learn(art.MaximumLevel + 1), "Art learning exceeded the campaign cap.");
            reject(() => save.GetCharacter(1).GetArt(5).Learn(), "Learning bypassed a Monado event.");
            reject(() => save.GetCharacter(1).GetArt(1).Learn(), "Learning changed a talent art.");
            reject(() => save.GetCharacter(7).GetArt(118).Learn(), "Learning bypassed Mind Blast's quest.");
            if (!future) reject(() => save.GetCharacter(8).GetArt(143).Learn(), "Learning bypassed Final Cross's event.");
            check(save.Serialize().AsSpan().SequenceEqual(original), "Rejected learning changed the save.");
            art.Learn();
            var single = save.Serialize();
            int offset = table + (art.Id - 1) * 2;
            check(art.Level == 1 && art.ManualFlags == 0x80 && art.CanEdit && !art.CanLearn,
                "Single learning failed to preserve unknown flags or enable editing.");
            check(Enumerable.Range(0, single.Length).All(index => original[index] == single[index] || index == offset),
                "Single learning changed unrelated fields.");
            reject(() => art.Learn(), "Already learned art was learned twice.");
            save.GetCharacter(1).LearnAndMaxArts();
            check(art.Level == (future ? 10 : 12) && art.ManualFlags == (future ? 0x83 : 0x87),
                "Character learning/maximum differs from the campaign cap.");
            check(!save.GetCharacter(7).GetArt(103).Learned, "Character learning affected another character.");
            save.LearnAndMaxAllArts();
            byte[] all = save.Serialize();
            var editable = save.Characters.SelectMany(character => character.Arts).Where(item => item.CanEdit).ToArray();
            check(editable.All(item => item.Level == item.MaximumLevel)
                && save.Characters.SelectMany(character => character.Arts).All(item => !item.CanLearn),
                "Global learning/maximum missed an ordinary art.");
            check(!save.GetCharacter(1).GetArt(5).Learned && !save.GetCharacter(7).GetArt(118).Learned
                && save.GetCharacter(1).GetArt(1).Level == 1, "Global learning changed protected event or talent arts.");
            if (!future) check(!save.GetCharacter(8).GetArt(143).Learned, "Global learning unlocked Final Cross.");
            var allowed = editable.SelectMany(item => new[] { table + (item.Id - 1) * 2, table + (item.Id - 1) * 2 + 1 })
                .Concat(ArtCatalog.All.Where(item => item.LinkedArtId is { } id && editable.Any(source => source.Id == id))
                    .Select(item => table + (item.Id - 1) * 2)).ToHashSet();
            check(Enumerable.Range(0, all.Length).All(index => original[index] == all[index] || allowed.Contains(index)),
                "Global learning changed palettes, AP, story flags, absent characters or unrelated art records.");
            foreach (var discharge in ArtCatalog.All.Where(item => item.LinkedArtId is not null))
                check(all[table + (discharge.Id - 1) * 2] == save.GetCharacter(7).GetArt(discharge.LinkedArtId!.Value).Level
                    && all[table + (discharge.Id - 1) * 2 + 1] == 0x80, "Learning broke discharge linkage or its flags.");
            save.LearnAndMaxAllArts();
            check(save.Serialize().AsSpan().SequenceEqual(all), "Global art learning is not idempotent.");
        }
    }
}
