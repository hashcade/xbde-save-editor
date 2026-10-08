using System.Text.Json;
using Avalonia;

namespace XbdeEditor.Gui.Localization;

public static class UiLanguage
{
    public static IReadOnlyDictionary<string, string> Languages { get; } = new Dictionary<string, string>
    {
        ["en"] = "English", ["zh-Hans"] = "简体中文", ["zh-Hant"] = "繁體中文",
        ["ja"] = "日本語", ["ko"] = "한국어", ["de"] = "Deutsch", ["fr"] = "Français",
        ["es"] = "Español", ["it"] = "Italiano"
    };
    public static string Current { get; private set; } = "en";
    private static IReadOnlyDictionary<string, string> _strings = Read("en");

    public static IReadOnlyDictionary<string, string> Read(string language)
    {
        if (!Languages.ContainsKey(language)) throw new ArgumentException("Unsupported UI language.");
        using var stream = typeof(UiLanguage).Assembly.GetManifestResourceStream($"XbdeEditor.Gui.Localization.{language}.json")
            ?? throw new InvalidDataException($"Missing language resource: {language}");
        return JsonSerializer.Deserialize(stream, UiLocaleJsonContext.Default.DictionaryStringString)
            ?? throw new InvalidDataException($"Invalid language resource: {language}");
    }

    public static void Apply(string language)
    {
        var strings = Read(language);
        if (!strings.Keys.Order().SequenceEqual(Read("en").Keys.Order()))
            throw new InvalidDataException("Language resource keys differ from English.");
        foreach (var (key, value) in strings) Application.Current!.Resources[key] = value;
        _strings = strings;
        Current = language;
    }

    public static string Get(string key) => _strings[key];
}
