namespace XbdeEditor.Core;

public static class CharacterCatalog
{
    private static readonly string[] English = ["", "Shulk", "Reyn", "Fiora", "Dunban", "Sharla", "Riki", "Melia", "Fiora", "Dickson", "Mumkhar", "Alvis", "Dunban", "Dunban", "Kino", "Nene"];
    private static readonly string[] Chinese = ["", "修尔克", "莱恩", "菲奥伦", "丹邦", "卡露娜", "力奇", "梅莉亚", "菲奥伦", "迪克森", "穆姆卡", "阿尔维斯", "丹邦", "丹邦", "奇诺", "宁宁"];
    private static readonly string[] Traditional = ["", "修爾克", "萊恩", "菲奧倫", "丹邦", "卡露娜", "力奇", "梅莉亞", "菲奧倫", "迪克森", "穆姆卡", "阿爾維斯", "丹邦", "丹邦", "奇諾", "寧寧"];
    private static readonly string[] Japanese = ["", "シュルク", "ライン", "フィオルン", "ダンバン", "カルナ", "リキ", "メリア", "フィオルン", "ディクソン", "ムムカ", "アルヴィース", "ダンバン", "ダンバン", "キノ", "ネネ"];
    private static readonly string[] Korean = ["", "슈르크", "라인", "피오른", "단반", "카르나", "리키", "멜리아", "피오른", "딕슨", "무므카", "알비스", "단반", "단반", "키노", "네네"];

    public static string Get(int id, string language)
    {
        string[] names = language switch
        {
            "zh-Hans" => Chinese, "zh-Hant" => Traditional, "ja" => Japanese, "ko" => Korean, _ => English
        };
        return id > 0 && id < names.Length ? names[id] : $"ID {id}";
    }
}
