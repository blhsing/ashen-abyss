### 安裝程式（Windows）

最簡單的安裝方式：從[發行頁](https://github.com/blhsing/ashen-abyss/releases/latest)下載 **`AshenAbyss-0.1.0-Setup.exe`**（約 5 MB）並執行。安裝程式會：

- 從 GitHub 下載遊戲檔案（約 3.3 GB），逐一核對 SHA-256，再解壓縮安裝（安裝後約 3.7 GB）；
- 預設安裝到 `%LOCALAPPDATA%\Programs\AshenAbyss`，**不需要系統管理員權限**，也可以選擇其他資料夾；
- 預設使用系統（瀏覽器）的 Proxy 設定，也可以在精靈中改用自訂 Proxy（含帳號密碼）或直接連線；下載中斷後再次執行會接續下載；
- 偵測到已安裝的版本時可以**就地升級**；存檔與設定（`%APPDATA%\AshenAbyss`）不會被安裝、升級或移除動到；
- 建立「開始」功能表捷徑（可選桌面捷徑），並可從「設定 › 應用程式」移除。

安裝程式沒有程式碼簽章，第一次執行時 Windows SmartScreen 可能會顯示「Windows 已保護您的電腦」，請按「其他資訊」→「仍要執行」。

**命令列（無人值守）安裝**，常用參數：

| 參數 | 說明 |
|---|---|
| `/VERYSILENT /SUPPRESSMSGBOXES` | 完全不顯示視窗（`/SILENT` 只顯示進度） |
| `/DIR="資料夾"` | 安裝資料夾 |
| `/LOG="檔案"` | 記錄檔 |
| `/MERGETASKS="desktopicon"` | 另外建立桌面捷徑 |
| `/PROXY=http://主機:連接埠`、`/PROXYUSER=`、`/PROXYPASS=` | 指定 Proxy 與驗證帳密 |
| `/NOPROXY` | 直接連線，不使用 Proxy |
| `/SOURCE="資料夾"` | 使用已下載好的分割檔離線安裝 |
| `/KEEPPARTS` | 安裝後保留下載的分割檔 |

```bat
AshenAbyss-0.1.0-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /DIR="D:\Games\AshenAbyss" /LOG="%TEMP%\aa_setup.log"
```

結束代碼：0＝成功、2＝已取消、10＝參數錯誤、11＝無法取得發行清單、12＝下載失敗、13＝Proxy 驗證失敗、14＝檔案驗證失敗、15＝磁碟空間不足、16＝本機分割檔不完整、17＝解壓縮失敗、18＝遊戲正在執行、19＝無法寫入資料夾。完整的參數與說明見 [`tools/installer/README.md`](tools/installer/README.md)。

不想用安裝程式的話，也可以自行下載所有 `AshenAbyss-0.1.0-win64.7z.*` 分割檔，用 [7-Zip](https://www.7-zip.org/) 解壓縮後執行 `AshenAbyss.exe`。
