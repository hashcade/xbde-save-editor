using System.Buffers.Binary;
using XbdeEditor.Core;

internal static class SkillsTests
{
    private const int CharacterBase = 0x152368;
    private const int CharacterSize = 0x138;

    internal static void Run(Func<bool, byte[]> fixture, Action<bool, string> check, Action<Action, string> reject)
    {
        check(SkillCatalog.All.Count == 40 && SkillCatalog.All.Sum(tree => tree.Skills.Count) == 200,
            "Skill catalog is incomplete.");
        byte[] original = fixture(false);
        original[0x152330] = 8;
        for (int id = 1; id <= 8; id++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(0x152318 + (id - 1) * 2), (ushort)id);
            BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(Offset(id)), 99);
            for (int index = 1; index <= 5; index++)
            {
                Set(original, id, 0x7c, index, 123);
                Set(original, id, 0x90, index, index == 1 ? 1u : 0u);
            }
        }
        for (int flag = 1; flag <= 14; flag++) Unlock(original, flag, false);
        var save = SaveDocument.Parse(original);
        check(save.Serialize().AsSpan().SequenceEqual(original), "Skill loading changed bytes.");
        foreach (var character in save.Characters)
        {
            var trees = character.SkillTrees;
            check(trees.Count == 5, "Character skill tree ownership differs.");
            check(trees[0].Skills[0].Id == (character.Id - 1) * 25 + 1, "Skill block ownership differs.");
            check(trees.Take(3).All(tree => tree.CanEdit) && trees.Skip(3).All(tree => !tree.IsUnlocked),
                "Hidden skill flags were ignored.");
            foreach (var tree in trees.Take(3))
            {
                int minimum = tree.MinimumLearnedCount;
                byte[] before = save.Serialize();
                reject(() => tree.SetLearnedCount(minimum - 1), "Learned count below its innate minimum was accepted.");
                reject(() => tree.SetLearnedCount(6), "Six learned skills were accepted.");
                reject(() => tree.SetProgress(tree.MaximumProgress + 1), "Remaining SP crossed the next skill threshold.");
                check(save.Serialize().AsSpan().SequenceEqual(before), "Rejected skill editing changed bytes.");
                for (int learned = minimum; learned <= 5; learned++)
                {
                    tree.SetLearnedCount(learned);
                    check(tree.LearnedCount == learned && tree.Progress == 0, "Learned count did not reset remaining SP.");
                    if (learned < 5)
                    {
                        tree.SetProgress(tree.MaximumProgress);
                        check(tree.Progress == tree.Skills[learned].RequiredSP - 1, "Remaining SP limit differs.");
                        reject(() => tree.SetProgress(tree.MaximumProgress + 1), "Exact learning threshold was accepted as remainder.");
                    }
                    else reject(() => tree.SetProgress(1), "A completed tree exposed SP editing.");
                }
                check(OnlySkillFields(before, save.Serialize(), [character.Id], [tree.Index]),
                    "Single-tree editing changed another tree, selection, links or flags.");
            }
            foreach (var tree in trees.Skip(3))
            {
                byte[] before = save.Serialize();
                reject(() => tree.Maximize(), "Locked hidden tree was learned.");
                reject(() => tree.SetLearnedCount(0), "Locked hidden tree was edited.");
                check(save.Serialize().AsSpan().SequenceEqual(before), "Locked tree mutation changed bytes.");
            }
            byte[] beforeCharacter = save.Serialize();
            character.MaxSkills();
            check(OnlySkillFields(beforeCharacter, save.Serialize(), [character.Id], [1, 2, 3]),
                "Character maximum changed a locked branch or another character.");
        }
        byte[] unlocked = save.Serialize();
        for (int flag = 1; flag <= 14; flag++)
        {
            Unlock(unlocked, flag, true);
            var document = SaveDocument.Parse(unlocked);
            var expected = SkillCatalog.All.Where(tree => tree.UnlockFlag == flag).ToArray();
            check(expected.All(tree => document.GetCharacter(tree.CharacterId).GetSkillTree(tree.Index).IsUnlocked),
                "Native hidden-tree flag mapping differs.");
        }
        save = SaveDocument.Parse(unlocked);
        byte[] beforeAll = save.Serialize();
        save.MaxAllSkills();
        byte[] afterAll = save.Serialize();
        check(save.Characters.SelectMany(character => character.SkillTrees).All(tree => tree.LearnedCount == 5 && tree.Progress == 0),
            "All-character maximum missed an unlocked tree.");
        check(OnlySkillFields(beforeAll, afterAll, Enumerable.Range(1, 8), Enumerable.Range(1, 5)),
            "All-character skills modified selected trees, quest unlocks or skill links.");
        save.MaxAllSkills();
        check(save.Serialize().AsSpan().SequenceEqual(afterAll), "Skill maximum is not idempotent.");
        reject(() => save.GetCharacter(1).GetSkillTree(0), "Zero tree index was accepted.");
        reject(() => save.GetCharacter(1).GetSkillTree(6), "Sixth tree index was accepted.");

        byte[] corrupt = (byte[])original.Clone();
        Set(corrupt, 1, 0x7c, 1, uint.MaxValue);
        Set(corrupt, 1, 0x90, 1, uint.MaxValue);
        var existing = SaveDocument.Parse(corrupt);
        existing.GetCharacter(1).SetResources(ap: 1);
        check(existing.GetCharacter(1).GetSkillTree(1).Progress == uint.MaxValue,
            "Unrelated editing clamped existing SP.");
        existing.GetCharacter(1).GetSkillTree(1).Maximize();
        check(existing.GetCharacter(1).GetSkillTree(1).LearnedCount == 5,
            "Explicit skill maximum did not normalize an invalid count.");

        foreach (bool future in new[] { false, true })
        {
            byte[] bytes = fixture(future);
            if (!future) bytes[0x152330] = 1;
            var unsupported = SaveDocument.Parse(bytes);
            reject(() => unsupported.MaxAllSkills(), "Unsupported campaign exposed skill maximum.");
            reject(() => unsupported.GetCharacter(1).MaxSkills(), "Unsupported character exposed skill maximum.");
            reject(() => unsupported.GetCharacter(1).GetSkillTree(1).Maximize(), "Unsupported campaign exposed tree editing.");
            check(unsupported.Serialize().AsSpan().SequenceEqual(bytes), "Unsupported campaign changed bytes.");
            if (future) check(unsupported.GetCharacter(14).SkillTrees.Count == 0
                && unsupported.GetCharacter(15).SkillTrees.Count == 0, "Kino or Nene received another character's tree.");
        }
    }

    internal static void VerifyRealSave(byte[] bytes, Action<bool, string> check)
    {
        var save = SaveDocument.Parse(bytes);
        if (save.Campaign != Campaign.MainStory) return;
        int[] editableTrees = save.Characters.SelectMany(character => character.SkillTrees)
            .Where(tree => tree.CanEdit).Select(tree => tree.Index).Distinct().ToArray();
        save.MaxAllSkills();
        check(OnlySkillFields(bytes, save.Serialize(), save.PartyIds, editableTrees),
            "Real-save skill maximum changed unrelated fields.");
        check(save.Characters.SelectMany(character => character.SkillTrees).Where(tree => tree.CanEdit)
            .All(tree => tree.LearnedCount == 5 && tree.Progress == 0), "Real-save maximum missed a tree.");
    }

    private static int Offset(int id) => CharacterBase + (id - 1) * CharacterSize;
    private static void Set(byte[] data, int id, int field, int index, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(Offset(id) + field + (index - 1) * 4), value);

    private static void Unlock(byte[] data, int flag, bool value)
    {
        int bit = 0x2cdd + flag;
        int offset = 0x50 + (bit >> 3);
        byte mask = (byte)(1 << (bit & 7));
        data[offset] = value ? (byte)(data[offset] | mask) : (byte)(data[offset] & ~mask);
    }

    private static bool OnlySkillFields(byte[] before, byte[] after, IEnumerable<int> ids, IEnumerable<int> trees)
    {
        var allowed = ids.SelectMany(id => trees.SelectMany(index => new[] { 0x7c, 0x90 }
            .SelectMany(field => Enumerable.Range(Offset(id) + field + (index - 1) * 4, 4)))).ToHashSet();
        return Enumerable.Range(0, after.Length).All(offset => before[offset] == after[offset] || allowed.Contains(offset));
    }
}
