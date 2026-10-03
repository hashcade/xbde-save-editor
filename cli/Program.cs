using System.Text.Json;
using XbdeEditor.Core;

try
{
    if (args is ["--version"])
    {
        Console.WriteLine(typeof(SaveDocument).Assembly.GetName().Version?.ToString(3));
        return 0;
    }
    if (args is [] or ["--help"])
    {
        Console.WriteLine("XBDE Save Editor\n\ninspect <save>\ncopy <save> <output>\nresources <save> <output> [--money N] [--noponstones N]\ncharacter <save> <output> <id> [--ap N] [--coins N] [--reserve-exp N]\nprogression <save> <output> <id> [--level N] [--exp N]\nmax-ap <save> <output>\narts <save> <character-id>\nart <save> <output> <character-id> <art-id> --level N\nmax-art <save> <output> <character-id> <art-id>\nmax-arts <save> <output> <character-id>\nskills <save> <character-id>\nskill-tree <save> <output> <character-id> <tree-index> --learned N\nskill-tree <save> <output> <character-id> <tree-index> --sp N\nmax-skill-tree <save> <output> <character-id> <tree-index>\nmax-skills <save> <output> <character-id>\nmax-all-skills <save> <output>\nequipment <save> <character-id>\nequip <save> <output> <character-id> <Weapon|Head|Torso|Arms|Legs|Feet> <inventory-index>\nequipment-gem <save> <output> <character-id> <Weapon|Head|Torso|Arms|Legs|Feet> <socket> <gem-index|none>\n--version");
        Console.WriteLine("gems <save>\ngem <save> <output> <gem-index> [--effect N] [--rank N] [--value N]\nmax-gem <save> <output> <gem-index>");
        Console.WriteLine("inventory <save> <Collectables|Materials|KeyItems|ArtManuals>\nitem <save> <output> <kind> <index> --quantity N\nadd-item <save> <output> <item-id> --quantity N\ndelete-item <save> <output> <kind> <index>\nmax-items <save> <output> <kind>\nadd-gem <save> <output> --effect N --rank N --value N\ndelete-gem <save> <output> <gem-index>");
        Console.WriteLine("party-order <save> <output> <character-id> <character-id> ...");
        return 0;
    }
    if (args is ["party-order", var partySource, var partyOutput, .. var orderedIds])
    {
        var session = SaveSession.Open(partySource);
        session.Document.ReorderParty(orderedIds.Select(ParseId).ToArray());
        session.Save(partyOutput);
        return 0;
    }
    if (args is ["inventory", var inventorySource, var inventoryKind])
    {
        var document = SaveSession.Open(inventorySource).Document;
        var kind = ParseInventoryKind(inventoryKind);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = kind.ToString(), Capacity = InventoryCatalog.Capacity,
            Items = document.Inventory(kind).Select(item => new { item.Index, item.ItemId, item.Name, item.Quantity, item.Favorite, item.CanEdit }),
            Catalog = InventoryCatalog.Definitions.Where(item => item.Kind == kind)
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["add-item", var addItemSource, var addItemOutput, var addItemId, "--quantity", var addItemQuantity])
    {
        var session = SaveSession.Open(addItemSource);
        session.Document.AddInventoryItem(ParseId(addItemId), ParseId(addItemQuantity));
        session.Save(addItemOutput);
        return 0;
    }
    if (args is ["item", var itemSource, var itemOutput, var itemKind, var stackIndex, "--quantity", var itemQuantity])
    {
        var session = SaveSession.Open(itemSource);
        session.Document.GetInventoryItem(ParseInventoryKind(itemKind), ParseId(stackIndex)).SetQuantity(ParseId(itemQuantity));
        session.Save(itemOutput);
        return 0;
    }
    if (args is ["delete-item", var deleteItemSource, var deleteItemOutput, var deleteItemKind, var deleteItemIndex])
    {
        var session = SaveSession.Open(deleteItemSource);
        session.Document.GetInventoryItem(ParseInventoryKind(deleteItemKind), ParseId(deleteItemIndex)).Delete();
        session.Save(deleteItemOutput);
        return 0;
    }
    if (args is ["max-items", var maxItemsSource, var maxItemsOutput, var maxItemsKind])
    {
        var session = SaveSession.Open(maxItemsSource);
        session.Document.MaxInventoryQuantities(ParseInventoryKind(maxItemsKind));
        session.Save(maxItemsOutput);
        return 0;
    }
    if (args is ["add-gem", var addGemSource, var addGemOutput, .. var addGemArguments])
    {
        var newGemOptions = ParseOptions(addGemArguments, "--effect", "--rank", "--value");
        if (newGemOptions.Count != 3 || newGemOptions.Values.Any(value => value > ushort.MaxValue))
            throw new ArgumentException("Specify gem effect, rank and value.");
        var session = SaveSession.Open(addGemSource);
        session.Document.AddGem((int)newGemOptions["--effect"], (int)newGemOptions["--rank"], (int)newGemOptions["--value"]);
        session.Save(addGemOutput);
        return 0;
    }
    if (args is ["delete-gem", var deleteGemSource, var deleteGemOutput, var deleteGemIndex])
    {
        var session = SaveSession.Open(deleteGemSource);
        session.Document.GetGem(ParseId(deleteGemIndex)).Delete();
        session.Save(deleteGemOutput);
        return 0;
    }
    if (args is ["inspect", var source])
    {
        var session = SaveSession.Open(source);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            File = Path.GetFileName(session.SourcePath),
            session.Document.Campaign,
            Size = SaveDocument.FileSize,
            session.Document.Sha256,
            session.Document.PartyIds,
            session.Document.Money,
            session.Document.Noponstones,
            Characters = session.Document.Characters.Select(character => new
            {
                character.Id, Name = CharacterCatalog.Get(character.Id, "en"),
                character.Level, character.Experience, character.AP, character.AffinityCoins,
                character.ExpertLevel, character.ExpertExperience, character.ReserveExperience
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["skills", var skillsSource, var skillsCharacter])
    {
        var character = SaveSession.Open(skillsSource).Document.GetCharacter(ParseId(skillsCharacter));
        Console.WriteLine(JsonSerializer.Serialize(character.SkillTrees.Select(tree => new
        {
            tree.Index, tree.Name, tree.IsUnlocked, tree.CanEdit, tree.LearnedCount,
            tree.Progress, tree.MaximumProgress, tree.Skills
        }), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["equipment", var equipmentSource, var equipmentCharacter])
    {
        var character = SaveSession.Open(equipmentSource).Document.GetCharacter(ParseId(equipmentCharacter));
        Console.WriteLine(JsonSerializer.Serialize(character.Equipment.Select(equipment => new
        {
            Slot = equipment.Slot.ToString(), equipment.Index, equipment.ItemId, equipment.Name, equipment.Exists,
            equipment.CanEdit, equipment.CanSwitch, equipment.GemSlotCount,
            AvailableItems = equipment.AvailableItems.Select(item => new { item.Index, item.ItemId, item.Name, item.GemSlotCount }),
            Sockets = equipment.GemSockets.Select(socket => new
            {
                socket.Index, socket.Name, socket.GemIndex, socket.FixedItemId, socket.CanEdit,
                AvailableGems = socket.CanEdit ? socket.AvailableGems.Select(gem => new { gem.Index, gem.Name, gem.Rank, gem.Value, gem.Strength, gem.Chance, gem.Label }) : []
            })
        }), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["gems", var gemsSource])
    {
        var document = SaveSession.Open(gemsSource).Document;
        Console.WriteLine(JsonSerializer.Serialize(document.Gems.Where(gem => !gem.IsCylinder).Select(gem => new
        {
            gem.Index, gem.Name, gem.EffectId, gem.Rank, gem.Strength, gem.Chance, gem.CanEdit, gem.HasValidValue,
            Minimum = gem.Definition?.Minimum, Maximum = gem.Definition?.Maximum, gem.Label
        }), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["max-gem", var maxGemSource, var maxGemOutput, var maxGemIndex])
    {
        var session = SaveSession.Open(maxGemSource);
        session.Document.GetGem(ParseId(maxGemIndex)).Maximize();
        session.Save(maxGemOutput);
        return 0;
    }
    if (args is ["gem", var editGemSource, var editGemOutput, var editGemIndex, .. var gemArguments])
    {
        var gemOptions = ParseOptions(gemArguments, "--effect", "--rank", "--value");
        if (gemOptions.Count == 0 || gemOptions.Values.Any(value => value > ushort.MaxValue))
            throw new ArgumentException("Specify a valid gem effect, rank or value.");
        var session = SaveSession.Open(editGemSource);
        var gem = session.Document.GetGem(ParseId(editGemIndex));
        int effect = (int)gemOptions.GetValueOrDefault("--effect", (uint)gem.EffectId);
        int rank = (int)gemOptions.GetValueOrDefault("--rank", (uint)gem.Rank);
        int strength = (int)gemOptions.GetValueOrDefault("--value", (uint)gem.Strength);
        gem.Set(effect, rank, strength);
        session.Save(editGemOutput);
        return 0;
    }
    if (args is ["equip", var equipSource, var equipOutput, var equipCharacter, var equipSlot, var itemIndex])
    {
        if (!Enum.TryParse<EquipmentSlot>(equipSlot, true, out var slot) || !Enum.IsDefined(slot))
            throw new ArgumentException("Unrecognized equipment slot.");
        var session = SaveSession.Open(equipSource);
        session.Document.GetCharacter(ParseId(equipCharacter)).GetEquipment(slot).Equip(ParseId(itemIndex));
        session.Save(equipOutput);
        return 0;
    }
    if (args is ["equipment-gem", var gemSource, var gemOutput, var gemCharacter, var equipmentSlot, var socketIndex, var gemChoice])
    {
        if (!Enum.TryParse<EquipmentSlot>(equipmentSlot, true, out var slot) || !Enum.IsDefined(slot))
            throw new ArgumentException("Choose Weapon, Head, Torso, Arms, Legs or Feet.");
        var session = SaveSession.Open(gemSource);
        var equipment = session.Document.GetCharacter(ParseId(gemCharacter)).GetEquipment(slot);
        int socket = ParseId(socketIndex);
        if (socket < 1 || socket > equipment.GemSlotCount) throw new ArgumentOutOfRangeException(nameof(socket));
        int? gem = gemChoice == "none" ? null : ParseId(gemChoice);
        equipment.SetGems(equipment.GemSockets.Select(record => record.Index == socket ? gem : record.GemIndex).ToArray());
        session.Save(gemOutput);
        return 0;
    }
    if (args is ["max-all-skills", var allSkillsSource, var allSkillsOutput])
    {
        var session = SaveSession.Open(allSkillsSource);
        session.Document.MaxAllSkills();
        session.Save(allSkillsOutput);
        return 0;
    }
    if (args is ["max-skills", var maxSkillsSource, var maxSkillsOutput, var maxSkillsCharacter])
    {
        var session = SaveSession.Open(maxSkillsSource);
        session.Document.GetCharacter(ParseId(maxSkillsCharacter)).MaxSkills();
        session.Save(maxSkillsOutput);
        return 0;
    }
    if (args is ["max-skill-tree", var maxTreeSource, var maxTreeOutput, var maxTreeCharacter, var maxTreeIndex])
    {
        var session = SaveSession.Open(maxTreeSource);
        session.Document.GetCharacter(ParseId(maxTreeCharacter)).GetSkillTree(ParseId(maxTreeIndex)).Maximize();
        session.Save(maxTreeOutput);
        return 0;
    }
    if (args is ["skill-tree", var skillSource, var skillOutput, var skillCharacter, var skillIndex, var field, var amount])
    {
        var session = SaveSession.Open(skillSource);
        var tree = session.Document.GetCharacter(ParseId(skillCharacter)).GetSkillTree(ParseId(skillIndex));
        var skillOptions = ParseOptions([field, amount], "--learned", "--sp");
        uint value = skillOptions[field];
        if (field == "--learned")
        {
            if (value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            tree.SetLearnedCount((int)value);
        }
        else tree.SetProgress(value);
        session.Save(skillOutput);
        return 0;
    }
    if (args is ["arts", var artsSource, var artsCharacter])
    {
        var character = SaveSession.Open(artsSource).Document.GetCharacter(ParseId(artsCharacter));
        Console.WriteLine(JsonSerializer.Serialize(character.Arts.Select(art => new
        {
            art.Id, art.Name, art.Level, art.Learned, art.IsTalent, art.CanEdit, art.MaximumLevel, art.UnlockedMaximum
        }), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args is ["max-arts", var allArtsSource, var allArtsOutput, var allArtsCharacter])
    {
        var session = SaveSession.Open(allArtsSource);
        session.Document.GetCharacter(ParseId(allArtsCharacter)).MaxArts();
        session.Save(allArtsOutput);
        return 0;
    }
    if (args is ["max-art", var maxArtSource, var maxArtOutput, var maxArtCharacter, var maxArtId])
    {
        var session = SaveSession.Open(maxArtSource);
        var art = session.Document.GetCharacter(ParseId(maxArtCharacter)).GetArt(ParseId(maxArtId));
        art.SetLevel(art.MaximumLevel);
        session.Save(maxArtOutput);
        return 0;
    }
    if (args is ["art", var artSource, var artOutput, var artCharacter, var artId, .. var artFields])
    {
        var session = SaveSession.Open(artSource);
        var artOptions = ParseOptions(artFields, "--level");
        uint level = artOptions["--level"];
        if (level > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(level));
        session.Document.GetCharacter(ParseId(artCharacter)).GetArt(ParseId(artId)).SetLevel((int)level);
        session.Save(artOutput);
        return 0;
    }
    if (args is ["copy", var input, var output])
    {
        var session = SaveSession.Open(input);
        if (Path.GetFullPath(input) == Path.GetFullPath(output))
            throw new ArgumentException("Copy requires a different destination.");
        session.Save(output);
        return 0;
    }
    if (args is ["resources", var resourceSource, var resourceOutput, .. var options])
    {
        var session = SaveSession.Open(resourceSource);
        uint money = session.Document.Money;
        uint noponstones = session.Document.Noponstones;
        foreach (var (name, value) in ParseOptions(options, "--money", "--noponstones"))
        {
            switch (name)
            {
                case "--money": money = value; break;
                case "--noponstones": noponstones = value; break;
            }
        }
        session.Document.SetResources(money, noponstones);
        session.Save(resourceOutput);
        return 0;
    }
    if (args is ["max-ap", var apSource, var apOutput])
    {
        var session = SaveSession.Open(apSource);
        session.Document.MaxAllAP();
        session.Save(apOutput);
        return 0;
    }
    if (args is ["progression", var progressionSource, var progressionOutput, var progressionId, .. var progressionFields])
    {
        var session = SaveSession.Open(progressionSource);
        if (!int.TryParse(progressionId, out int id)) throw new ArgumentException("Character ID must be an integer.");
        var progressionOptions = ParseOptions(progressionFields, "--level", "--exp");
        session.Document.GetCharacter(id).SetProgression(
            level: progressionOptions.TryGetValue("--level", out uint level) ? level : null,
            experience: progressionOptions.TryGetValue("--exp", out uint experience) ? experience : null);
        session.Save(progressionOutput);
        return 0;
    }
    if (args is ["character", var characterSource, var characterOutput, var characterId, .. var fields])
    {
        var session = SaveSession.Open(characterSource);
        if (!int.TryParse(characterId, out int id)) throw new ArgumentException("Character ID must be an integer.");
        var character = session.Document.GetCharacter(id);
        uint? ap = null;
        uint? coins = null;
        uint? reserveExperience = null;
        foreach (var (name, value) in ParseOptions(fields, "--ap", "--coins", "--reserve-exp"))
        {
            switch (name)
            {
                case "--ap": ap = value; break;
                case "--coins": coins = value; break;
                case "--reserve-exp": reserveExperience = value; break;
            }
        }
        character.SetResources(ap, coins, reserveExperience);
        session.Save(characterOutput);
        return 0;
    }
    throw new ArgumentException("Unknown command. Run --help for usage.");
}
catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static int ParseId(string value) => int.TryParse(value, out int id) ? id
    : throw new ArgumentException("ID must be an integer.");

static InventoryKind ParseInventoryKind(string value) => Enum.TryParse<InventoryKind>(value, true, out var kind) && Enum.IsDefined(kind)
    ? kind : throw new ArgumentException("Choose Collectables, Materials, KeyItems or ArtManuals.");

static IReadOnlyDictionary<string, uint> ParseOptions(string[] options, params string[] allowed)
{
    if (options.Length == 0 || options.Length % 2 != 0)
        throw new ArgumentException("Provide one or more option/value pairs.");
    var values = new Dictionary<string, uint>();
    for (int index = 0; index < options.Length; index += 2)
    {
        string name = options[index];
        if (!allowed.Contains(name)) throw new ArgumentException($"Unknown option: {name}");
        if (!uint.TryParse(options[index + 1], out uint value) || !values.TryAdd(name, value))
            throw new ArgumentException("Values must be unique options containing unsigned 32-bit integers.");
    }
    return values;
}
