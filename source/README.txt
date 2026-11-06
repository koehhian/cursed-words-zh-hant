原始碼與重建

需要 Windows、PowerShell 7、Python 3，以及使用者合法安裝的 Cursed Words 4.12。
使用遊戲隨附的 Unity 程式庫與 Windows .NET Framework 編譯器，不需 Unity 專案。

1. 修改 translations.json 後執行 python export_translations.py。
2. 使用 PowerShell 7 執行：
   ./build.ps1 -GameDirectory "你的 Steam\steamapps\common\Cursed Words"
3. 上層 payload 及輸出根目錄的一鍵 EXE 會重新產生；不會直接修改安裝中的遊戲。

reviewed.json 用來標記需要在對話逐字動畫前先翻譯的原始字串。
若新增此類對話，請同步加入 reviewed.json。修改值以 translations.json 為準。
backup/Assembly-CSharp.dll 必須是相同版本未修改的原始檔。

Mono.Cecil 0.11.6：MIT 授權，見 lib/Mono.Cecil-LICENSE.txt。
離線辭典：見上層 ECDICT-LICENSE.txt。
