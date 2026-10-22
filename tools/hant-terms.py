# 繁体用语表：Windows 逐字简转繁（LCMapStringEx）之后的写法 → 繁中（台湾、港澳）习惯写法。
# 用法：python tools/hant-terms.py cs LOMAssistant/UiTraditional.cs   重新生成 C# 表
#       python tools/hant-terms.py fix 文件…                     按同一张表改繁中文案里的字样
import sys

TERMS = [
    # 逐字转换转不对的字
    ("游戲", "遊戲"), ("怎么", "怎麼"), ("什么", "什麼"), ("這么", "這麼"), ("那么", "那麼"), ("多么", "多麼"), ("么", "麼"),
    ("決斗", "決鬥"), ("戰斗", "戰鬥"), ("打斗", "打鬥"), ("斗志", "鬥志"), ("干等", "乾等"), ("備注", "備註"),
    ("沖突", "衝突"), ("圖鑒", "圖鑑"), ("閑", "閒"), ("并", "並"), ("云", "雲"), ("后", "後"), ("里", "裡"), ("日歷", "日曆"), ("建筑", "建築"), ("日志", "記錄"),
    # 電腦、介面用語
    ("遊戲目錄", "遊戲資料夾"), ("設置", "設定"), ("界面", "介面"), ("窗口", "視窗"), ("默認", "預設"), ("屏幕", "螢幕"), ("菜單", "選單"), ("鼠標", "滑鼠"),
    ("文件夾", "資料夾"), ("配置文件", "設定檔"), ("文件", "檔案"), ("運行", "執行"), ("加載", "載入"), ("內存", "記憶體"),
    ("代碼", "程式碼"), ("程序", "程式"), ("導出", "匯出"), ("導入", "匯入"), ("日誌", "記錄檔"), ("信息", "資訊"),
    ("搜索", "搜尋"), ("保存", "儲存"), ("支持", "支援"), ("手柄", "手把"), ("數據", "資料"), ("視頻", "影片"), ("鏈接", "連結"),
    ("粘貼", "貼上"), ("撤銷", "復原"), ("回車", "Enter"), ("激活", "啟用"), ("自定義", "自訂"), ("反饋", "回報"),
    ("字符", "字元"), ("聯網", "連線"), ("托盤", "系統匣"), ("質量", "品質"), ("獲取", "取得"), ("服務器", "伺服器"),
    ("網絡", "網路"), ("帖子", "貼文"), ("賬號", "帳號"), ("賬戶", "帳戶"), ("配置", "設定"),
    # 上面「里」「后」一律換了，這些詞本來就是「里」「后」，換回來
    ("萬裡", "萬里"), ("千裡", "千里"), ("公裡", "公里"), ("裡程", "里程"), ("鄰裡", "鄰里"), ("故裡", "故里"),
    ("皇後", "皇后"), ("太後", "太后"), ("王後", "王后"), ("天後", "天后"),
]


def fix(text):
    for a, b in TERMS:
        text = text.replace(a, b)
    return text


def gen_cs(path):
    rows = "\n".join(f'\t\t("{a}", "{b}"),' for a, b in TERMS)
    cs = f'''namespace LOMAssistant;

/// <summary>
/// 繁体中文用语：Windows 逐字简转繁（LCMapStringEx）以后，把转不对的字和大陆用语换成繁中习惯（遊戲、設定、介面、視窗、檔案……）。
/// 本 Mod 的界面和安装器（Installer/build-installer.sh 也编译这个文件）都用它，README、Steam 指南引用的界面字样按同一张表写。
/// 表按顺序替换，长的、更具体的写在前面。由 tools/hant-terms.py 生成，要改先改那里再重新生成。
/// </summary>
public static class UiTraditional
{{
	private static readonly (string From, string To)[] Terms =
	{{
{rows}
	}};

	public static string Fix(string text)
	{{
		if (string.IsNullOrEmpty(text))
		{{
			return text;
		}}
		foreach (var (from, to) in Terms)
		{{
			if (text.IndexOf(from, System.StringComparison.Ordinal) >= 0)
			{{
				text = text.Replace(from, to);
			}}
		}}
		return text;
	}}
}}
'''
    open(path, "w", encoding="utf-8", newline="\n").write(cs)


if __name__ == "__main__":
    if sys.argv[1] == "cs":
        gen_cs(sys.argv[2])
    else:
        for p in sys.argv[2:]:
            t = open(p, encoding="utf-8", newline="").read()
            n = fix(t)
            open(p, "w", encoding="utf-8", newline="").write(n)
            print(p, "changed" if n != t else "same")
