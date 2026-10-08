using System.Text.Json.Serialization;

namespace XbdeEditor.Gui.Localization;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class UiLocaleJsonContext : JsonSerializerContext { }
