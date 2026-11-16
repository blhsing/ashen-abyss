# 《燼淵 Ashen Abyss》安裝程式 / Installer

**繁體中文** ｜ [English](#english)

`AshenAbyss-<版本>-Setup.exe` 是一個小型的「網路安裝程式」（約 5 MB）：它從 GitHub 發行頁下載遊戲的 7-Zip 分割檔，依發行清單（manifest）逐一核對大小與 SHA-256，用內建的 7-Zip 程式碼解壓縮，然後建立「開始」功能表捷徑並登錄到「設定 › 應用程式」以便移除。

- 圖形化精靈（繁體中文、深色主題、終局首領「寂主・奧薩洛斯」的圖像），也可以完全以命令列無人值守安裝。
- 預設安裝到 `%LOCALAPPDATA%\Programs\AshenAbyss`，**不需要系統管理員權限**；可以改選其他資料夾。
- 下載預設使用 **系統（瀏覽器）的 Proxy 設定**（Windows 的「網際網路選項」／WinINet，包含自動設定指令碼 PAC 與自動偵測 WPAD），也可以指定 Proxy（含帳號密碼）或直接連線。
- 下載中斷時保留已下載的部分，下次執行會**接續下載**；每個檔案失敗時自動重試。
- 偵測既有的安裝（本安裝程式安裝的、或以前用指令碼安裝在 `%USERPROFILE%\Games\AshenAbyss` 並有「開始」功能表捷徑 `燼淵 Ashen Abyss.lnk` 的）並**就地升級**：保留原資料夾，只替換程式檔案。
- **存檔與設定（`%APPDATA%\AshenAbyss`）永遠不會被安裝、升級或移除動到。**

### 原始檔

| 檔案 | 用途 |
|---|---|
| `AshenAbyss.iss` | Inno Setup 6.7 指令碼（精靈、下載器、驗證、解壓縮、升級、移除） |
| `build_installer.ps1` | 建置：`powershell -ExecutionPolicy Bypass -File Y:\tools\installer\build_installer.ps1`（`-Art` 會先重新產生圖片，`-NoManifest` 不產生清單） |
| `make_manifest.ps1` | 由本機分割檔產生發行清單 `AshenAbyss-<版本>-manifest.json`（只讀取分割檔，不會修改） |
| `make_art.py` | 由圖示的渲染圖（`Y:\tools\appicon\renders\s3.png`、`icon_master.png`）產生精靈左側大圖與右上角小圖（`art\`） |
| `lang\ChineseTraditional.isl` | Inno Setup 官方繁體中文訊息檔 |
| `licenses\7-Zip-License.txt` | 7-Zip（GNU LGPL）授權聲明，會一併安裝到 `<安裝資料夾>\licenses\` |

輸出：`Y:\release_out\installer\AshenAbyss-<版本>-Setup.exe` 與 `AshenAbyss-<版本>-manifest.json`。**兩個檔案都要上傳到發行頁**（安裝程式從同一個發行版下載清單）。

### 發行清單 (manifest)

```json
{
  "format": 1, "product": "AshenAbyss", "version": "0.1.0", "tag": "v0.1.0",
  "archive": "AshenAbyss-0.1.0-win64.7z.001",
  "parts": [ { "name": "AshenAbyss-0.1.0-win64.7z.001", "size": 1992294400, "sha256": "…" }, … ],
  "download_size": 3563904556, "install_size": 4000898594, "files": 4819,
  "exe": "AshenAbyss.exe",
  "top_level": ["AshenAbyss.exe", "AshenAbyss.ico", "AshenAbyss.pck", "assets", "data_AshenAbyss_windows_x86_64"]
}
```

安裝程式不寫死分割檔數量或大小，全部來自清單。`top_level` 是封存檔最上層的項目：升級時只替換這些項目（以及上一版記錄在 `<安裝資料夾>\.installed_items` 的項目），移除時也只刪這些。

### 命令列參數

Inno Setup 內建的參數都可以使用，常用的有：

| 參數 | 說明 |
|---|---|
| `/SILENT` | 不顯示精靈頁面，只顯示進度視窗（下載、驗證、解壓縮與安裝） |
| `/VERYSILENT` | 完全不顯示任何視窗（無人值守） |
| `/SUPPRESSMSGBOXES` | 搭配 `/SILENT`、`/VERYSILENT`：不顯示訊息方塊 |
| `/DIR="資料夾"` | 安裝資料夾（預設 `%LOCALAPPDATA%\Programs\AshenAbyss`；偵測到既有安裝時預設為該資料夾）。**明確指定時一定優先** |
| `/LOG="檔案"` | 記錄檔位置。未指定時記錄檔一律寫到 `%TEMP%\Setup Log 年-月-日 #編號.txt` |
| `/TASKS="startmenuicon,desktopicon"` | 要建立的捷徑：`startmenuicon`＝「開始」功能表（預設勾選）、`desktopicon`＝桌面（預設不勾選）。`/TASKS=""` 不建立捷徑 |
| `/MERGETASKS="desktopicon"` | 在預設之外再加上桌面捷徑 |
| `/NOCANCEL`、`/NORESTART`、`/SAVEINF=`、`/LOADINF=`、`/HELP` | 見 Inno Setup 說明 |

本安裝程式自訂的參數（不分大小寫）：

| 參數 | 說明 |
|---|---|
| `/PROXY=http://主機:連接埠` | 使用指定的 HTTP Proxy（HTTPS 下載會經由 CONNECT 通道）。可寫成 `http://帳號:密碼@主機:連接埠`（特殊字元請用 `%XX` 編碼）。`/PROXY=system`＝系統設定（預設），`/PROXY=none`＝同 `/NOPROXY` |
| `/PROXYUSER=帳號`、`/PROXYPASS=密碼` | Proxy 驗證帳號密碼（Basic；NTLM／Negotiate 由 Windows 自動處理）。也可以搭配系統 Proxy 使用 |
| `/NOPROXY` | 不使用任何 Proxy，直接連線 |
| `/SOURCE="資料夾"` | 不下載，改用資料夾中已下載的分割檔（離線安裝、測試用）。資料夾內若有清單會使用並驗證 SHA-256；若沒有，會嘗試從網路取得清單，再不行就直接使用資料夾中的分割檔（記錄檔中會警告「未驗證」） |
| `/MANIFEST="檔案或網址"` | 指定發行清單（預設：`<下載來源>AshenAbyss-<版本>-manifest.json`） |
| `/BASEURL=網址` | 下載來源（預設 `https://github.com/blhsing/ashen-abyss/releases/download/v0.1.0/`） |
| `/TAG=v0.1.0` | 從指定的 GitHub 發行標籤下載（版本號取自標籤，去掉開頭的 `v`） |
| `/VERSION=0.1.0` | 指定版本號（決定清單與分割檔名稱） |
| `/DOWNLOADDIR="資料夾"` | 下載暫存資料夾（預設 `<安裝資料夾>\_download`） |
| `/KEEPPARTS` | 安裝完成後保留下載的分割檔（預設刪除） |
| `/RETRIES=n` | 每個檔案下載失敗時的重試次數（0–50，預設 5；每次間隔 3、6、9… 秒，並從中斷處接續） |
| `/NOUPGRADE` | 偵測到既有安裝時，不要預設升級它（改用 `/DIR` 或預設資料夾） |
| `/SHORTCUTNAME=名稱` | 捷徑名稱（預設 `燼淵 Ashen Abyss`；測試時用來避免動到真正的捷徑） |
| `/DETECTONLY` | 只偵測既有安裝，寫入記錄檔後結束（不做任何變更），結果見結束代碼 40–42 |
| `/DETECTLNK="捷徑"`、`/DETECTDIR="資料夾"` | 測試用：取代偵測時檢查的「開始」功能表捷徑與預設指令碼安裝資料夾 |

範例：

```bat
:: 無人值守安裝到 D:\Games\AshenAbyss，同時建立桌面捷徑，寫記錄檔
AshenAbyss-0.1.0-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /DIR="D:\Games\AshenAbyss" /MERGETASKS="desktopicon" /LOG="%TEMP%\aa_setup.log"
:: 經由公司 Proxy（需要帳號密碼）
AshenAbyss-0.1.0-Setup.exe /SILENT /PROXY=http://proxy.corp.example:8080 /PROXYUSER=alice /PROXYPASS=secret
:: 離線安裝：分割檔與清單已經在 E:\AshenAbyss-parts
AshenAbyss-0.1.0-Setup.exe /VERYSILENT /SOURCE="E:\AshenAbyss-parts"
:: 無人值守移除（存檔保留）
"%LOCALAPPDATA%\Programs\AshenAbyss\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES
```

### 結束代碼

| 代碼 | 意義 |
|---|---|
| 0 | 成功（全新安裝或升級完成）；`/DETECTONLY` 時表示沒有偵測到既有安裝 |
| 1 | 安裝程式無法初始化 |
| 2 | 使用者在安裝前取消，或中止了下載（已下載的部分會保留） |
| 3 | 進入下一步時發生嚴重錯誤（Inno Setup） |
| 4 | 安裝時發生嚴重錯誤（Inno Setup） |
| 5 | 使用者在安裝過程中取消 |
| 6 | 安裝程式被強制結束 |
| 7 | 準備安裝時失敗（其他原因） |
| 10 | 命令列參數錯誤（例如 `/PROXY=` 格式不對、`/PROXY` 與 `/NOPROXY` 同時使用） |
| 11 | 無法取得或解析發行清單 |
| 12 | 下載失敗（網路／伺服器錯誤，重試後仍失敗，或 HTTP 404 等） |
| 13 | Proxy 需要驗證或驗證失敗（HTTP 407） |
| 14 | SHA-256 或檔案大小不符（重新下載後仍不符） |
| 15 | 磁碟空間不足 |
| 16 | `/SOURCE` 資料夾缺少分割檔或大小不符 |
| 17 | 解壓縮失敗 |
| 18 | 遊戲正在執行中（安裝資料夾的檔案被占用） |
| 19 | 無法寫入安裝／下載資料夾，或無法替換程式檔案（原本的安裝會還原） |
| 40／41／42 | 只限 `/DETECTONLY`：偵測到本安裝程式安裝的版本／由「開始」功能表捷徑找到的指令碼安裝／預設資料夾 `%USERPROFILE%\Games\AshenAbyss` 中的指令碼安裝 |

移除程式 `unins000.exe`：0＝成功，1＝失敗（例如遊戲正在執行）。詳細原因都寫在記錄檔中（搜尋 `AA-RESULT`、`ERROR`）。

### 運作方式

- **Proxy**：下載器直接使用 Windows 的 WinINet。預設以 `INTERNET_OPEN_TYPE_PRECONFIG` 開啟，也就是與 Edge／「網際網路選項」相同的設定：手動 Proxy、自動設定指令碼 (PAC) 與自動偵測 (WPAD) 都有效，例外清單也一樣。指定 `/PROXY` 時改用 `INTERNET_OPEN_TYPE_PROXY`（本機位址不經過 Proxy）；`/NOPROXY` 為直接連線。Proxy 回應 407 時，若有帳號密碼會自動重送。精靈的「網路連線」頁會顯示目前的系統設定，並可「測試連線」。
- **下載**：先下載到 `<檔名>.part`，用 `Range` 標頭接續，完成後計算 SHA-256（Windows CNG），相符才改名；不符會刪除並重新下載一次。已完整下載且驗證通過的檔案不會重新下載。
- **磁碟空間**：下載前檢查：安裝資料夾所在磁碟需要「安裝大小＋64 MB」，若下載資料夾在同一磁碟還要加上尚未下載的大小。
- **解壓縮**：使用安裝程式內建的 7-Zip 程式碼（Inno Setup 的 `is7z.dll`，支援分割檔），先解壓縮到 `<安裝資料夾>\.setup-new`。
- **升級**：偵測順序：①本安裝程式在 `HKCU\…\Uninstall\{6B3E2F4A-9C1D-4E8B-A7F2-3D5C8E1B0A94}_is1` 的 `InstallLocation`；②「開始」功能表捷徑 `燼淵 Ashen Abyss.lnk` 的目標（必須是存在的 `AshenAbyss.exe`）；③預設指令碼安裝資料夾 `%USERPROFILE%\Games\AshenAbyss`。升級時先確認遊戲沒有在執行，再把舊的程式項目移到 `.setup-old`、新的移入，失敗就全部還原；成功後刪除舊檔。存檔資料夾 `%APPDATA%\AshenAbyss` 完全不會被讀寫。原本由指令碼建立的同名捷徑會被安裝程式的捷徑取代（指向同一個資料夾）。
- **移除**：刪除安裝的程式項目、捷徑與「應用程式」登錄；保留 `%APPDATA%\AshenAbyss`（存檔、設定、記錄檔）。

### 已知限制

- 沒有程式碼簽章憑證：第一次執行時 Windows SmartScreen 可能顯示「Windows 已保護您的電腦」，需要按「其他資訊 › 仍要執行」。
- Proxy 只支援 HTTP Proxy（含 CONNECT），不支援 SOCKS 或 HTTPS 協定的 Proxy。
- 同一個使用者帳號只會登錄一份安裝（「應用程式」中只有一個項目）；安裝到另一個資料夾會取代登錄的位置。
- 由安裝指令碼建立的舊安裝沒有檔案清單，升級時只替換已知的程式項目（`AshenAbyss.exe`、`.pck`、`.ico`、`assets`、`data_…`），其他檔案保留。

---

## English

`AshenAbyss-<version>-Setup.exe` is a small (~5 MB) **web installer**: it downloads the game's 7-Zip split parts from the GitHub release, checks every part's size and SHA-256 against the release manifest, extracts them with the bundled 7-Zip code, creates the Start-menu shortcut and registers an uninstaller in *Settings › Apps*.

- Graphical wizard (Traditional Chinese, dark style, art of the final boss Osaros from the app icon) or fully unattended command-line installs.
- Default folder `%LOCALAPPDATA%\Programs\AshenAbyss`, **no administrator rights**; any other folder can be chosen.
- Downloads use the **system (browser) proxy settings** by default (Windows Internet Options / WinINet, including the automatic configuration script (PAC) and auto-detect (WPAD)); a specific proxy (with credentials) or a direct connection can be chosen instead.
- Interrupted downloads **resume**; failed parts are retried.
- Detects an existing installation (made by this installer, or the earlier script-made one in `%USERPROFILE%\Games\AshenAbyss` with the Start-menu shortcut `燼淵 Ashen Abyss.lnk`) and **upgrades it in place**, replacing only the program files.
- **Saves and settings (`%APPDATA%\AshenAbyss`) are never touched by install, upgrade or uninstall.**

Build: `powershell -ExecutionPolicy Bypass -File Y:\tools\installer\build_installer.ps1` (`-Art` regenerates the images first, `-NoManifest` skips the manifest). Output: `Y:\release_out\installer\AshenAbyss-<ver>-Setup.exe` and `AshenAbyss-<ver>-manifest.json` — **upload both to the release**; the installer fetches the manifest from the same release. The manifest format is shown above; `make_manifest.ps1` builds it from the local parts (read-only). Nothing about the parts (count, names, sizes) is hard-coded in the installer.

### Command-line switches

All standard Inno Setup switches work: `/SILENT` (progress window only: download, verification, extraction, install), `/VERYSILENT` (no windows), `/SUPPRESSMSGBOXES`, `/DIR="folder"` (an explicit `/DIR` always wins over a detected installation), `/LOG="file"` (without it a log is always written to `%TEMP%\Setup Log yyyy-mm-dd #nnn.txt`), `/TASKS="startmenuicon,desktopicon"` (`startmenuicon` checked by default, `desktopicon` not; `/TASKS=""` = no shortcuts), `/MERGETASKS="desktopicon"`, `/NOCANCEL`, `/NORESTART`, `/SAVEINF=`, `/LOADINF=`, `/HELP`.

Installer-specific switches (case-insensitive):

| Switch | Meaning |
|---|---|
| `/PROXY=http://host:port` | Use this HTTP proxy (HTTPS goes through a CONNECT tunnel). `http://user:pass@host:port` is accepted (percent-encode special characters). `/PROXY=system` = system settings (default), `/PROXY=none` = `/NOPROXY` |
| `/PROXYUSER=name`, `/PROXYPASS=password` | Proxy credentials (Basic; NTLM/Negotiate are handled by Windows automatically); also usable with the system proxy |
| `/NOPROXY` | Direct connection, no proxy |
| `/SOURCE="folder"` | Install from already-downloaded parts in this folder (offline / testing). A manifest in the folder is used and verified; otherwise the manifest is fetched online; failing that, the parts in the folder are used unverified (logged as a warning) |
| `/MANIFEST="file or URL"` | Manifest to use (default `<base URL>AshenAbyss-<version>-manifest.json`) |
| `/BASEURL=url` | Download base URL (default `https://github.com/blhsing/ashen-abyss/releases/download/v0.1.0/`) |
| `/TAG=v0.1.0` | Download from this GitHub release tag (version = tag without the leading `v`) |
| `/VERSION=0.1.0` | Version (selects the manifest / part names) |
| `/DOWNLOADDIR="folder"` | Where the parts are downloaded (default `<install folder>\_download`) |
| `/KEEPPARTS` | Keep the downloaded parts after installing (default: deleted) |
| `/RETRIES=n` | Retries per part (0–50, default 5; waits 3, 6, 9… s and resumes) |
| `/NOUPGRADE` | Do not default to the detected installation's folder |
| `/SHORTCUTNAME=name` | Shortcut name (default `燼淵 Ashen Abyss`; used by tests to keep the real shortcut untouched) |
| `/DETECTONLY` | Only detect an existing installation, log it and exit without changes (exit codes 40–42) |
| `/DETECTLNK="lnk"`, `/DETECTDIR="folder"` | Testing: replace the Start-menu shortcut / default script folder checked by the detection |

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (installed or upgraded); with `/DETECTONLY`: nothing detected |
| 1 | Setup failed to initialize |
| 2 | Cancelled before installing, or the download was aborted (downloaded data is kept for resuming) |
| 3, 4 | Fatal error (Inno Setup) before / during the installation step |
| 5 | Cancelled during the installation step |
| 6 | Setup was terminated |
| 7 | Preparing to install failed (other reasons) |
| 10 | Invalid command-line switch value (e.g. malformed `/PROXY=`, `/PROXY` together with `/NOPROXY`) |
| 11 | Manifest unavailable or invalid |
| 12 | Download failed (network / server error after retries, HTTP 404 …) |
| 13 | Proxy authentication required or rejected (HTTP 407) |
| 14 | SHA-256 or size mismatch (also after downloading again) |
| 15 | Not enough disk space |
| 16 | `/SOURCE` folder is missing parts or a part has the wrong size |
| 17 | Extraction failed |
| 18 | The game is running (files in the install folder are in use) |
| 19 | Install / download folder not writable, or program files could not be replaced (the previous installation is restored) |
| 40 / 41 / 42 | `/DETECTONLY` only: found an installer-made installation / a script-made one via the Start-menu shortcut / a script-made one in the default folder `%USERPROFILE%\Games\AshenAbyss` |

Uninstaller (`unins000.exe /VERYSILENT /SUPPRESSMSGBOXES`): 0 = success, 1 = failed (e.g. the game is running). Details are always in the log (search `AA-RESULT`, `ERROR`).

### How it works

- **Proxy**: the downloader uses WinINet directly. By default it opens with `INTERNET_OPEN_TYPE_PRECONFIG`, i.e. exactly the Edge / Internet Options configuration: manual proxy, automatic configuration script (PAC), auto-detect (WPAD) and the bypass list. `/PROXY` switches to `INTERNET_OPEN_TYPE_PROXY` (local addresses bypass it); `/NOPROXY` connects directly. On HTTP 407 the request is re-sent with the given credentials. The wizard's network page shows the current system setting and has a *Test connection* button.
- **Download**: into `<name>.part`, resumed with `Range` requests; on completion the SHA-256 is computed (Windows CNG) and the file is renamed only if it matches; on a mismatch it is downloaded once more. Already complete, verified parts are not downloaded again.
- **Disk space** is checked before downloading: install size + 64 MB on the install drive, plus the bytes still to download if the download folder is on the same drive.
- **Extraction** with Inno Setup's bundled 7-Zip code (`is7z.dll`, multi-volume aware) into `<install folder>\.setup-new`.
- **Upgrade detection**: (1) this installer's `InstallLocation` in `HKCU\…\Uninstall\{6B3E2F4A-9C1D-4E8B-A7F2-3D5C8E1B0A94}_is1`; (2) the target of the Start-menu shortcut `燼淵 Ashen Abyss.lnk` (must be an existing `AshenAbyss.exe`); (3) the default script folder `%USERPROFILE%\Games\AshenAbyss`. The upgrade checks that the game is not running, moves the old program items to `.setup-old`, moves the new ones in (rolling everything back on failure) and then deletes the old ones. `%APPDATA%\AshenAbyss` is never read or written. A script-made shortcut with the same name is replaced by the installer's (same folder).
- **Uninstall** removes the installed program items, the shortcuts and the Apps entry; `%APPDATA%\AshenAbyss` (saves, settings, logs) stays.

### Known limitations

- Not code-signed: on first run Windows SmartScreen may show "Windows protected your PC" — click *More info › Run anyway*.
- HTTP proxies only (CONNECT for HTTPS); no SOCKS or HTTPS-protocol proxies.
- One registered installation per user account (one Apps entry); installing to another folder re-registers the new location.
- A script-made installation has no file list, so an upgrade replaces only the known program items (`AshenAbyss.exe`, `.pck`, `.ico`, `assets`, `data_…`) and leaves anything else in place.
