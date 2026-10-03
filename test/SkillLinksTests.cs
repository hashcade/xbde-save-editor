using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class SkillLinksTests
{
    private static int Offset(int id) => 0x152368 + (id - 1) * 0x138;
    internal static byte[] Fixture(byte[] bytes)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 7);
        bytes[0x152330] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0xdf0), 390);
        for (int id = 1; id <= 8; id++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x152318 + (id - 1) * 2), (ushort)id);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Offset(id)), 99);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Offset(id) + 0xc), 999);
            bytes.AsSpan(Offset(id) + 0xc4, 40).Clear();
            for (int tree = 0; tree < 5; tree++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Offset(id) + 0x90 + tree * 4), 5);
            for (int source = 0; source < 8; source++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(Offset(id) + 0xa4 + source * 4), 4);
        }
        for (int flag = 1; flag <= 14; flag++)
        {
            int bit = 0x2cdd + flag;
            bytes[0x50 + (bit >> 3)] |= (byte)(1 << (bit & 7));
        }
        return bytes;
    }

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] bytes = Fixture(fixture(false));
        var save = SaveDocument.Parse(bytes);
        check(save.CanEditSkillLinks && save.Serialize().AsSpan().SequenceEqual(bytes), "Inspecting skill links changed bytes.");
        foreach (var character in save.Characters)
        {
            check(character.SkillLinks.Count == 30, "Unavailable, self or alternate Fiora link group was included.");
            foreach (var link in character.SkillLinks)
            {
                check(link.IsUnlocked && link.CanEdit && link.Choices.Count > 0, "An unlocked shape has no learned choices.");
                foreach (var skill in link.Choices)
                {
                    byte[] before = save.Serialize();
                    link.SetSkill(skill.Id);
                    int offset = Offset(character.Id) + 0xc4 + (link.SourceCharacterId - 1) * 5 + link.Index - 1;
                    var expected = (byte[])before.Clone();
                    expected[offset] = (byte)skill.Id;
                    check(save.Serialize().AsSpan().SequenceEqual(expected), "Skill linking wrote outside its one-byte slot.");
                    check(link.SkillId == skill.Id && link.Skill == skill && character.LinkedSkillCoinCost == skill.AffinityCoins,
                        "Linked skill ID, shape or coin cost differs.");
                    check(link.Choices.All(choice => choice.CharacterId == link.SourceCharacterId && choice.Shape == link.Shape),
                        "Link choice belongs to a different source or shape.");
                    link.SetSkill(skill.Id);
                    check(save.Serialize().AsSpan().SequenceEqual(expected), "Reassigning the same skill is not idempotent.");
                    link.SetSkill(0);
                    check(save.Serialize().AsSpan().SequenceEqual(before), "Clearing a link changed coins, slot unlocks or other fields.");
                }
                byte[] unchanged = save.Serialize();
                int wrongShape = SkillCatalog.All.Where(tree => tree.CharacterId == link.SourceCharacterId)
                    .SelectMany(tree => tree.Skills).First(skill => skill.Shape != link.Shape).Id;
                reject(() => link.SetSkill(wrongShape), "Wrong shape was linked.");
                reject(() => link.SetSkill(201), "Unrecognized skill ID was linked.");
                reject(() => link.SetSkill(-1), "Negative skill ID was linked.");
                reject(() => link.SetSkill((character.Id - 1) * 25 + 1), "Another source's skill was linked.");
                check(save.Serialize().AsSpan().SequenceEqual(unchanged), "Rejected link assignment changed bytes.");
            }
        }
        var character1 = save.GetCharacter(1);
        reject(() => character1.GetSkillLink(1, 1), "Self skill linking was allowed.");
        reject(() => character1.GetSkillLink(2, 0), "Slot zero was allowed.");
        reject(() => character1.GetSkillLink(2, 6), "A sixth link slot was allowed.");
        reject(() => save.GetCharacter(3).GetSkillLink(8, 1), "Fiora was allowed to link her alternate form's skills.");

        var sourceSlots = character1.SkillLinks.Where(link => link.SourceCharacterId == 2).ToArray();
        var repeatedShape = sourceSlots.GroupBy(link => link.Shape).First(group => group.Count() > 1).ToArray();
        int repeatedSkill = repeatedShape[0].Choices[0].Id;
        repeatedShape[0].SetSkill(repeatedSkill);
        byte[] beforeDuplicate = save.Serialize();
        reject(() => repeatedShape[1].SetSkill(repeatedSkill), "Duplicate linked skill was accepted.");
        check(save.Serialize().AsSpan().SequenceEqual(beforeDuplicate), "Duplicate rejection changed bytes.");
        repeatedShape[0].SetSkill(0);

        var slot = character1.GetSkillLink(2, 1);
        var candidate = slot.Choices[0];
        character1.SetResources(affinityCoins: (uint)candidate.AffinityCoins - 1);
        byte[] poor = save.Serialize();
        reject(() => slot.SetSkill(candidate.Id), "A link exceeded available coins.");
        check(save.Serialize().AsSpan().SequenceEqual(poor), "Insufficient coins changed bytes.");
        character1.SetResources(affinityCoins: (uint)candidate.AffinityCoins);
        slot.SetSkill(candidate.Id);
        character1.SetResources(affinityCoins: 0);
        check(character1.LinkedSkillCoinCost == candidate.AffinityCoins, "Existing overspent links were normalized.");
        slot.SetSkill(candidate.Id);
        slot.SetSkill(0);

        byte[] locked = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(locked.AsSpan(Offset(1) + 0xa8), 0);
        var lockedSave = SaveDocument.Parse(locked);
        check(lockedSave.GetCharacter(1).GetSkillLink(2, 1).IsUnlocked
            && !lockedSave.GetCharacter(1).GetSkillLink(2, 2).IsUnlocked, "Slot count was treated as a literal unlocked-slot total.");
        reject(() => lockedSave.GetCharacter(1).GetSkillLink(2, 2).SetSkill(0), "Locked slot was edited.");
        BinaryPrimitives.WriteUInt32LittleEndian(locked.AsSpan(Offset(2) + 0x90 + (candidate.TreeIndex - 1) * 4), 0);
        var unlearned = SaveDocument.Parse(locked);
        reject(() => unlearned.GetCharacter(1).GetSkillLink(2, 1).SetSkill(candidate.Id), "Unlearned skill was linked.");
        int hiddenBit = 0x2cdd + 4;
        locked[0x50 + (hiddenBit >> 3)] &= (byte)~(1 << (hiddenBit & 7));
        var hidden = SaveDocument.Parse(locked);
        check(hidden.GetCharacter(1).SkillLinks.SelectMany(link => link.Choices).All(skill => skill.CharacterId != 2 || skill.TreeIndex != 5),
            "A locked hidden branch exposed its skills for linking.");

        byte[] corrupt = (byte[])bytes.Clone();
        corrupt[Offset(1) + 0xc4 + 5] = 255;
        var unknown = SaveDocument.Parse(corrupt);
        check(unknown.GetCharacter(1).LinkedSkillCoinCost is null && unknown.Serialize().AsSpan().SequenceEqual(corrupt),
            "Unrecognized link was normalized on inspection.");
        reject(() => unknown.GetCharacter(1).GetSkillLink(2, 2).SetSkill(26), "Unknown used coin cost allowed another link.");
        unknown.GetCharacter(1).GetSkillLink(2, 1).SetSkill(0);
        check(unknown.GetCharacter(1).LinkedSkillCoinCost == 0, "Explicit removal failed to repair an unknown link.");

        foreach (bool future in new[] { false, true })
        {
            byte[] unsupported = fixture(future);
            if (!future) unsupported[0x152330] = 1;
            var document = SaveDocument.Parse(unsupported);
            check(!document.CanEditSkillLinks && document.GetCharacter(1).SkillLinks.Count == 0,
                "Unsupported campaign exposed skill-link editing.");
            reject(() => document.GetCharacter(1).GetSkillLink(2, 1), "Absent source character was linked.");
            check(document.Serialize().AsSpan().SequenceEqual(unsupported), "Unsupported inspection changed bytes.");
        }
        var otherVersion = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(otherVersion, 8);
        var protectedSave = SaveDocument.Parse(otherVersion);
        reject(() => protectedSave.GetCharacter(1).GetSkillLink(2, 1).SetSkill(candidate.Id), "Unverified format allowed skill links.");
        foreach (var (story, sources) in new (int, int[])[]
        {
            (0, [2]), (11, [2, 3]), (42, [2]), (69, [2, 5]), (100, [2, 4, 5]),
            (128, [2, 4, 5, 7]), (137, [2, 4, 5, 6, 7]), (273, [2, 4, 5, 6, 7, 8])
        })
        {
            byte[] storySave = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt16LittleEndian(storySave.AsSpan(0xdf0), (ushort)story);
            storySave[0x5eb] &= 0xef;
            var document = SaveDocument.Parse(storySave);
            check(document.GetCharacter(1).SkillLinks.Select(link => link.SourceCharacterId).Distinct().SequenceEqual(sources),
                "Native story availability for linked sources differs.");
            storySave[0x5eb] |= 0x10;
            check(SaveDocument.Parse(storySave).GetCharacter(1).SkillLinks.Select(link => link.SourceCharacterId).Distinct()
                .SequenceEqual(new[] { 2, 4, 5, 6, 7, 8 }), "Source availability override was ignored.");
        }
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        if (!save.CanEditSkillLinks) return;
        foreach (var character in save.Characters)
        {
            check(character.LinkedSkillCoinCost is not null, "Real links contain an unresolved skill ID.");
            foreach (var link in character.SkillLinks.Where(link => link.SkillId != 0))
            {
                check(link.IsUnlocked && link.Skill?.CharacterId == link.SourceCharacterId && link.Skill.Shape == link.Shape,
                    "Real skill-link shape, source or unlock mapping differs.");
                var copy = SaveDocument.Parse(bytes);
                var copyLink = copy.GetCharacter(character.Id).GetSkillLink(link.SourceCharacterId, link.Index);
                copyLink.SetSkill(link.SkillId);
                check(copy.Serialize().AsSpan().SequenceEqual(bytes), "Real link reassignment changed bytes.");
                copyLink.SetSkill(0);
                var expected = (byte[])bytes.Clone();
                expected[Offset(character.Id) + 0xc4 + (link.SourceCharacterId - 1) * 5 + link.Index - 1] = 0;
                check(copy.Serialize().AsSpan().SequenceEqual(expected), "Real link removal wrote outside its byte.");
            }
        }
    }
}
