# 安裝、使用與還原

支援 Steam Windows Cursed Words 4.12。從 Releases 下載 EXE 或 Windows ZIP；EXE 未數位簽署，SHA256SUMS.txt 可核對下載檔。一般玩家無需 Python 或 PowerShell 7。

關閉遊戲後執行安裝器，選擇包含 `Cursed Words.exe` 的資料夾，按「一鍵中文化」。預設位置為 Steam 的 `steamapps/common/Cursed Words`。安裝器先核對版本，再備份原始 DLL，從本機原始檔套用差異補丁，核對結果後安裝。

送出英文單字後，右上角浮窗顯示釋義，保留到下次送出。拖曳標題列移動；「－」縮小；F8 顯示／隱藏；長詞條可以捲動。未收錄時會顯示提示。學英文時請保留遊戲拼字詞庫的英語設定。

要移除時，先關閉遊戲，再開相同 EXE 按「還原英文」。保留遊戲中的 `Cursed Words_Data/ZhTW-Backup`，才能還原原始 DLL。還原會停用中文化與辭典，翻譯／詞庫檔案可能仍保留於磁碟。

遇到版本不符，請停止操作並取得適用新版補丁；Steam 驗證遊戲檔案可恢復官方程式。回報問題請附遊戲版本、完整錯誤訊息、出現位置及畫面，不要上傳存檔或原遊戲 DLL。
