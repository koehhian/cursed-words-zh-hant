# 開發與重建

需要 Windows、PowerShell 7、Python 3、Windows .NET Framework 編譯器，以及自己安裝的 Cursed Words 4.12。Mono.Cecil 0.11.6 隨 source/lib 提供。

開發用原始遊戲 DLL 放在 `backup/Assembly-CSharp.dll`（自行從相容的原遊戲或 ZhTW-Backup 複製；不可提交）。修改 `source/translations.json` 後執行 `python source/export_translations.py`，再在 PowerShell 7 執行：

```powershell
./source/build.ps1 -GameDirectory "C:/Program Files (x86)/Steam/steamapps/common/Cursed Words"
```

建置會產生本機 payload、差異資料與一鍵 EXE。EXE 輸出到 distribution/。完整遊戲 DLL、backup、installer-resources 不可提交或隨公開 ZIP 散布。

`source/binary_delta.py` 產生 CWD1 差異指令：從玩家原檔複製片段，以及寫入變更資料。安裝器檢查原始雜湊與重建結果雜湊。payload 中只公開原創 helper、翻譯與 MIT 詞庫；原遊戲輸入及中間產物只保留本機。

多音字／注音不是本專案功能。拼字的字母、英文單字及字塊規則應保留；數字與問號須沿用遊戲選定的 HistoricWord，不能另外推算。
