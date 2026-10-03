using System.Buffers.Binary;
using XbdeEditor.Core;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
void Reject(Action action, string message)
{
    try { action(); }
    catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException) { checks++; return; }
    throw new InvalidOperationException(message);
}
byte[] Fixture(bool future = false)
{
    byte[] data = new byte[SaveDocument.FileSize];
    new Random(17).NextBytes(data);
    data.AsSpan(0x152318, 25).Clear();
    int[] ids = future ? [1, 7, 14, 15] : [1, 2, 8, 5, 4, 7, 6];
    data[0x152330] = (byte)ids.Length;
    for (int index = 0; index < ids.Length; index++)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x152318 + index * 2), (ushort)ids[index]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x152368 + (ids[index] - 1) * 0x138), 99);
    }
    return data;
}

foreach (bool future in new[] { false, true })
{
    byte[] original = Fixture(future);
    var save = SaveDocument.Parse(original);
    Check(save.Serialize().AsSpan().SequenceEqual(original), "Round-trip altered unknown bytes.");
    save.SetResources(123, 456);
    byte[] edited = save.Serialize();
    Check(save.Money == 123 && save.Noponstones == 456, "Resource edit differs.");
    for (int offset = 0; offset < edited.Length; offset++)
        if (offset is not (>= 0x10 and < 0x14) and not (>= 0x151b40 and < 0x151b44)
            && original[offset] != edited[offset])
            throw new InvalidOperationException($"Resource editing altered byte {offset:X}.");
    Check(save.Campaign == (future ? Campaign.FutureConnected : Campaign.MainStory), "Campaign mismatch.");
    var character = save.GetCharacter(future ? 14 : 1);
    Check(character.Level == 99, "Character ID resolves to the wrong record.");
    byte[] beforeCharacterEdit = save.Serialize();
    character.SetResources(ap: 765);
    int characterOffset = 0x152368 + (character.Id - 1) * 0x138;
    byte[] afterCharacterEdit = save.Serialize();
    Check(character.AP == 765, "Character AP edit differs.");
    for (int offset = 0; offset < afterCharacterEdit.Length; offset++)
        if (!(offset >= characterOffset + 8 && offset < characterOffset + 12)
            && beforeCharacterEdit[offset] != afterCharacterEdit[offset])
            throw new InvalidOperationException($"Character edit altered byte {offset:X}.");
    foreach (uint reserve in new[] { 0u, CharacterRecord.MaximumReserveExperience })
    {
        byte[] beforeReserve = save.Serialize();
        character.SetResources(reserveExperience: reserve);
        byte[] afterReserve = save.Serialize();
        Check(character.ReserveExperience == reserve, "Reserve EXP boundary failed.");
        Check(afterReserve.AsSpan(0, characterOffset + 0xf4).SequenceEqual(beforeReserve.AsSpan(0, characterOffset + 0xf4))
            && afterReserve.AsSpan(characterOffset + 0xf8).SequenceEqual(beforeReserve.AsSpan(characterOffset + 0xf8)),
            "Reserve EXP editing changed a level, current EXP or another record.");
    }
    character.SetResources(ap: CharacterRecord.MaximumAP);
    Check(character.AP == CharacterRecord.MaximumAP, "AP maximum failed.");
    character.SetResources(ap: 0);
    Check(character.AP == 0, "AP minimum failed.");
    byte[] beforeLimits = save.Serialize();
    Reject(() => character.SetResources(ap: CharacterRecord.MaximumAP + 1, reserveExperience: 1), "AP above the game cap was accepted.");
    Reject(() => character.SetResources(ap: 1, reserveExperience: CharacterRecord.MaximumReserveExperience + 1), "Reserve EXP above the game cap was accepted.");
    Check(save.Serialize().AsSpan().SequenceEqual(beforeLimits), "A rejected resource edit partially mutated the document.");
    save.MaxAllAP();
    byte[] afterBulk = save.Serialize();
    Check(save.Characters.All(member => member.AP == CharacterRecord.MaximumAP), "Bulk AP missed a joined character.");
    int[] apOffsets = save.Characters.Select(member => 0x152368 + (member.Id - 1) * 0x138 + 8).ToArray();
    for (int offset = 0; offset < afterBulk.Length; offset++)
    {
        bool allowed = apOffsets.Any(start => offset >= start && offset < start + 4);
        if (!allowed && beforeLimits[offset] != afterBulk[offset])
            throw new InvalidOperationException($"Bulk AP changed byte {offset:X}.");
    }
    save.MaxAllAP();
    Check(save.Serialize().AsSpan().SequenceEqual(afterBulk), "Bulk AP is not idempotent.");
    byte[] beforeRejected = save.Serialize();
    Reject(() => character.SetResources(123, 1000), "Out-of-range Affinity Coins were accepted.");
    Check(save.Serialize().AsSpan().SequenceEqual(beforeRejected), "Invalid character update partially mutated AP.");
    Reject(() => save.GetCharacter(16), "Unsupported character ID was accepted.");
    Reject(() => save.GetCharacter(future ? 2 : 14), "Absent character was editable.");
    if (future)
    {
        Reject(() => character.SetResources(affinityCoins: 0), "Future Connected accepted Affinity Coins.");
    }
    else
    {
        character.SetResources(affinityCoins: 999);
        Check(character.AffinityCoins == 999, "Affinity Coin maximum failed.");
        character.SetResources(affinityCoins: 0);
        Check(character.AffinityCoins == 0, "Affinity Coin minimum failed.");
    }
    original[0] ^= 0xff;
    Check(save.Serialize()[0] != original[0], "The parser retains its caller's buffer.");
    byte[] copy = save.Serialize();
    copy[0] ^= 0xff;
    Check(copy[0] != save.Serialize()[0], "Serialization exposes mutable storage.");
}
ArtsTests.Run(Fixture, Check, Reject);
AchievementTests.Run(Fixture, Check, Reject);
SkillsTests.Run(Fixture, Check, Reject);
SkillLinksTests.Run(Fixture, Check, Reject);
AffinityTests.Run(Fixture, Check, Reject);
RegionAffinityTests.Run(Fixture, Check, Reject);
Colony6Tests.Run(Fixture, Check, Reject);
CollectopaediaTests.Run(Fixture, Check, Reject);
CollectopaediaCompletionTests.Run(Fixture, Check, Reject);
EquipmentTests.Run(Fixture, Check, Reject);
EquipmentSwitchTests.Run(Fixture, Check, Reject);
EquipmentInventoryTests.Run(Fixture, Check, Reject);
GemTests.Run(Fixture, Check, Reject);
InventoryTests.Run(Fixture, Check, Reject);
Reject(() => SaveDocument.Parse(new byte[1688]), "System save was accepted.");
Reject(() => SaveDocument.Parse(new byte[SaveDocument.FileSize]), "Invalid party was accepted.");
byte[] bad = Fixture();
bad[0x152330] = 13;
Reject(() => SaveDocument.Parse(bad), "Oversized party was accepted.");
bad = Fixture();
BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x15231a), 1);
Reject(() => SaveDocument.Parse(bad), "Duplicate party IDs were accepted.");
bad = Fixture();
BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x152368), 0);
Reject(() => SaveDocument.Parse(bad), "Character ID 1 did not map to record 0.");
bad = Fixture(true);
BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x152368 + 13 * 0x138), 0);
Reject(() => SaveDocument.Parse(bad), "Character ID 14 did not map to record 13.");

Check(LevelProgression.ExperienceToNextLevel(1) == 40, "Level 2 EXP cost differs from the growth table.");
Check(LevelProgression.ExperienceToNextLevel(98) == 1491201, "Level 99 EXP cost differs.");
Check(LevelProgression.ExperienceToNextLevel(99) == 0, "Level cap has a next-level cost.");
Check(LevelProgression.Normalize(1, 100) == (3u, 0u), "EXP normalization failed at exact thresholds.");
Check(LevelProgression.Normalize(1, 101) == (3u, 1u), "EXP remainder was lost.");
Check(LevelProgression.Normalize(98, 1491202) == (99u, 1u), "Level 99 normalization differs.");
foreach (bool future in new[] { false, true })
{
    byte[] progressionBytes = Fixture(future);
    var progressionSave = SaveDocument.Parse(progressionBytes);
    foreach (var member in progressionSave.Characters)
    {
        int record = 0x152368 + (member.Id - 1) * 0x138;
        BinaryPrimitives.WriteUInt32LittleEndian(progressionBytes.AsSpan(record + 0xec), 99);
        BinaryPrimitives.WriteUInt32LittleEndian(progressionBytes.AsSpan(record + 0xf0), 123);
    }
    progressionSave = SaveDocument.Parse(progressionBytes);
    foreach (var member in progressionSave.Characters)
    {
        uint minimum = member.MinimumLevel;
        byte[] before = progressionSave.Serialize();
        Reject(() => member.SetProgression(level: minimum - 1), "Below-introduction level was accepted.");
        Reject(() => member.SetProgression(level: 100), "Level 100 was accepted.");
        Reject(() => member.SetProgression(level: minimum, experience: 100000000), "EXP cap was ignored.");
        Check(progressionSave.Serialize().AsSpan().SequenceEqual(before), "Rejected progression changed bytes.");
        member.SetProgression(level: minimum);
        Check(member.Level == minimum && member.Experience == 0, "Level selection did not reset EXP.");
        Check(member.ExpertLevel == 99 && member.ExpertExperience == 0, "Highest level bookkeeping differs.");
        member.SetProgression(experience: LevelProgression.ExperienceToNextLevel(minimum) + 7);
        Check(member.Level == minimum + 1 && member.Experience == 7, "Linked EXP did not advance level.");
        member.SetProgression(level: 99, experience: LevelProgression.MaximumExperience);
        Check(member.Level == 99 && member.Experience == LevelProgression.MaximumExperience, "Level cap EXP was discarded.");
        byte[] after = progressionSave.Serialize();
        int start = 0x152368 + (member.Id - 1) * 0x138;
        for (int offset = 0; offset < after.Length; offset++)
            if (!(offset >= start && offset < start + 8) && !(offset >= start + 0xec && offset < start + 0xf4)
                && after[offset] != before[offset])
                throw new InvalidOperationException($"Progression changed unrelated byte {offset:X}.");
        Check(member.CanEditProgression, "Joined playable character has no progression support.");
    }
}
byte[] ascending = Fixture();
BinaryPrimitives.WriteUInt32LittleEndian(ascending.AsSpan(0x152368), 1);
BinaryPrimitives.WriteUInt32LittleEndian(ascending.AsSpan(0x152368 + 0xec), 1);
var ascendingMember = SaveDocument.Parse(ascending).GetCharacter(1);
ascendingMember.SetProgression(level: 10);
Check(ascendingMember.ExpertLevel == 10, "New highest level was not recorded.");

byte[] ambiguous = Fixture(true);
ambiguous[0x152330] = 2;
var ambiguousSave = SaveDocument.Parse(ambiguous);
Check(ambiguousSave.Campaign == Campaign.Unknown, "Shared characters incorrectly identify a campaign.");
Check(!ambiguousSave.GetCharacter(1).CanEditProgression, "Ambiguous campaign enabled progression.");
Reject(() => ambiguousSave.GetCharacter(1).SetProgression(level: 60), "Ambiguous campaign accepted a level edit.");
Reject(() => ambiguousSave.GetCharacter(1).SetResources(affinityCoins: 100),
    "An ambiguous campaign allowed main-story coin editing.");
Check(ambiguousSave.Serialize().AsSpan().SequenceEqual(ambiguous), "Ambiguous campaign parsing changed bytes.");

string temporary = Path.Combine(Path.GetTempPath(), $"xbde-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporary);
try
{
    string source = Path.Combine(temporary, "fixture.sav");
    byte[] original = Fixture();
    File.WriteAllBytes(source, original);
    var session = SaveSession.Open(source);
    Check(!session.HasChanges, "Opening a save marks it dirty.");
    string output = Path.Combine(temporary, "copy.sav");
    session.Save(output);
    Check(File.ReadAllBytes(output).AsSpan().SequenceEqual(original), "Copy is not lossless.");
    session.Save(source);
    Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(original), "Save is not lossless.");
    Reject(() => session.Save(Path.Combine(temporary, "missing", "copy.sav")), "Saving into a missing directory succeeded.");
    File.WriteAllBytes(source, new byte[5]);
    Reject(() => session.Save(source), "An externally changed source was overwritten.");
    Check(File.ReadAllBytes(source).Length == 5, "Failed save damaged the source.");
    Check(!Directory.EnumerateFiles(temporary, ".xbde-*.tmp").Any(), "Temporary files were leaked.");

    if (args is ["--save-directory", var directory])
    {
        foreach (string realPath in Directory.EnumerateFiles(directory, "*.sav")
            .Where(path => Path.GetFileName(path).StartsWith("bfsgame") || Path.GetFileName(path).StartsWith("bfsmeria")))
        {
            byte[] before = File.ReadAllBytes(realPath);
            var realSession = SaveSession.Open(realPath);
            Check(realSession.Document.Serialize().AsSpan().SequenceEqual(before), "Real save round-trip differs.");
            var achievementCopy = SaveDocument.Parse(before);
            if (achievementCopy.CanEditAchievements)
            {
                int completedCount = achievementCopy.Achievements.Count(item => item.Completed);
                achievementCopy.UnlockAllAchievements();
                Check(achievementCopy.Achievements.All(item => item.Completed), "Real achievement bulk edit missed a record.");
                if (completedCount == 200)
                    Check(achievementCopy.Serialize().AsSpan().SequenceEqual(before), "Completed real achievements were normalized.");
                Console.WriteLine($"{Path.GetFileName(realPath)}: achievements {completedCount}/200.");
            }
            else
            {
                Reject(achievementCopy.UnlockAllAchievements, "Unsupported real save accepted achievement editing.");
                Check(achievementCopy.Serialize().AsSpan().SequenceEqual(before), "Protected real achievements changed.");
            }
            SkillsTests.VerifyRealSave(before, Check);
            SkillLinksTests.VerifyRealSave(before, Check);
            AffinityTests.VerifyRealSave(before, Check);
            RegionAffinityTests.VerifyRealSave(before, Check);
            Colony6Tests.VerifyRealSave(before, Check);
            CollectopaediaTests.VerifyRealSave(before, Check);
            EquipmentTests.VerifyRealSave(before, Check);
            EquipmentSwitchTests.VerifyRealSave(before, Check);
            EquipmentInventoryTests.VerifyRealSave(before, Check);
            GemTests.VerifyRealSave(before, Check);
            InventoryTests.VerifyRealSave(before, Check);
            foreach (var actualCharacter in realSession.Document.Characters)
            {
                Check(actualCharacter.Level is >= 1 and <= 99, "Real character mapping produced an invalid level.");
                var editedReal = SaveDocument.Parse(before);
                uint changedAP = actualCharacter.AP == 12345 ? 12346u : 12345u;
                editedReal.GetCharacter(actualCharacter.Id).SetResources(ap: changedAP);
                int recordOffset = 0x152368 + (actualCharacter.Id - 1) * 0x138;
                byte[] editedBytes = editedReal.Serialize();
                Check(editedBytes.AsSpan(0, recordOffset + 8).SequenceEqual(before.AsSpan(0, recordOffset + 8))
                    && editedBytes.AsSpan(recordOffset + 12).SequenceEqual(before.AsSpan(recordOffset + 12)),
                    "Real character edit changed a neighboring record.");
                var reserveCopy = SaveDocument.Parse(before);
                reserveCopy.GetCharacter(actualCharacter.Id).SetResources(reserveExperience: CharacterRecord.MaximumReserveExperience);
                byte[] reserveBytes = reserveCopy.Serialize();
                Check(reserveBytes.AsSpan(0, recordOffset + 0xf4).SequenceEqual(before.AsSpan(0, recordOffset + 0xf4))
                    && reserveBytes.AsSpan(recordOffset + 0xf8).SequenceEqual(before.AsSpan(recordOffset + 0xf8)),
                    "Real reserve edit changed level, EXP or unrelated data.");
                var artsCopy = SaveDocument.Parse(before);
                artsCopy.GetCharacter(actualCharacter.Id).MaxArts();
                byte[] artsBytes = artsCopy.Serialize();
                Check(artsBytes.AsSpan(0, 0x1536e8).SequenceEqual(before.AsSpan(0, 0x1536e8)),
                    "Real art maximum changed resources, palette or other save sections.");
                Check(artsCopy.GetCharacter(actualCharacter.Id).Arts.Where(art => art.CanEdit)
                    .All(art => art.Level == art.MaximumLevel), "Real art maximum missed an upgradeable art.");
            }
            var learnedArtsCopy = SaveDocument.Parse(before);
            learnedArtsCopy.LearnAndMaxAllArts();
            byte[] learnedArtsBytes = learnedArtsCopy.Serialize();
            Check(learnedArtsBytes.AsSpan(0, 0x1536e8).SequenceEqual(before.AsSpan(0, 0x1536e8)),
                "Real art learning changed resources, equipment, palettes or other save sections.");
            Check(learnedArtsCopy.Characters.SelectMany(character => character.Arts).Where(art => art.IsLevelLearned)
                .All(art => art.Level == art.MaximumLevel), "Real art learning missed an ordinary art.");
            foreach (var member in realSession.Document.Characters)
                foreach (var art in member.Arts.Where(art => !art.Learned && !art.CanLearn))
                    Check(!learnedArtsCopy.GetCharacter(member.Id).GetArt(art.Id).Learned,
                        "Real art learning unlocked a protected event art.");
            learnedArtsCopy.LearnAndMaxAllArts();
            Check(learnedArtsCopy.Serialize().AsSpan().SequenceEqual(learnedArtsBytes), "Real art learning is not idempotent.");
            string realCopy = Path.Combine(temporary, Path.GetFileName(realPath));
            realSession.Save(realCopy);
            Check(File.ReadAllBytes(realCopy).AsSpan().SequenceEqual(before), "Real save copy differs.");
            Check(File.ReadAllBytes(realPath).AsSpan().SequenceEqual(before), "Original real save changed.");
            Console.WriteLine($"{Path.GetFileName(realPath)}: {realSession.Document.Campaign}, {realSession.Document.PartyIds.Count} characters; lossless copy passed.");
        }
    }
}
finally { Directory.Delete(temporary, recursive: true); }
Console.WriteLine($"Core tests passed: {checks} checks.");
