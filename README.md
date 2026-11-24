# 燼淵 Ashen Abyss

一款原創的黑暗奇幻動作角色扮演遊戲，全程使用繁體中文，以 Godot 4.7（.NET／C#）製作。
溫暖世界的日爐已然熄滅；英雄帶著最後的餘燼，穿越七大區域，直抵其下的深淵。

**0.4.1 版是可遊玩的原型。** 從第一個區域到結局的主線劇情皆可完整遊玩，但仍有許多部分正在製作中（見下方〈0.4.1 版現況〉）。

## 遊戲畫面

### 七大區域與戰鬥

每一張使用不同的職業、怪物與技能組合（皆為遊戲內實際畫面，英雄身上為實際穿戴的裝備）。

<table>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/01-wastes-emberblade.jpg" alt="灰燼荒原・燼刃衛揮動雙手巨劍，在抱暖骸團、炭木人與葬甲蟲的扇形預警中硬闖" width="100%"><br><sub>灰燼荒原・燼刃衛揮動雙手巨劍，在抱暖骸團、炭木人與葬甲蟲的扇形預警中硬闖</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/02-frost-forgebody.jpg" alt="霜脊隘口・鍛身者在立凍哨的盾牆與冰爐巨軀之間出拳，腳下是鑿冰的扇形預警" width="100%"><br><sub>霜脊隘口・鍛身者在立凍哨的盾牆與冰爐巨軀之間出拳，腳下是鑿冰的扇形預警</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/03-crypt-soulbinder.jpg" alt="沉眠墓原・喚魂師率骸骨戰士與術士，在臥像騎士與托柱像的地裂預警中堅守" width="100%"><br><sub>沉眠墓原・喚魂師率骸骨戰士與術士，在臥像騎士與托柱像的地裂預警中堅守</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/04-mire-wildsoul.jpg" alt="瘴泣沼澤・荒靈行者迎擊菌冠巨軀，孢子毒池與符文預警在腳下層層疊起" width="100%"><br><sub>瘴泣沼澤・荒靈行者迎擊菌冠巨軀，孢子毒池與符文預警在腳下層層疊起</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/05-sands-pactbinder.jpg" alt="鏽砂廢都・縛淵術士召來契約巨魔，迎戰鏽尾蠍與鏡盾衛的扇形斬擊" width="100%"><br><sub>鏽砂廢都・縛淵術士召來契約巨魔，迎戰鏽尾蠍與鏡盾衛的扇形斬擊</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/06-sanctum-lampknight.jpg" alt="寂滅聖城・誓燈騎士以雙手兵器與聖焰橫掃臥像騎士與寂滅教團" width="100%"><br><sub>寂滅聖城・誓燈騎士以雙手兵器與聖焰橫掃臥像騎士與寂滅教團</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/07-abyss-hunter.jpg" alt="燼淵・影獵手在默巨與添爐巨人的烈焰預警邊緣閃身反擊" width="100%"><br><sub>燼淵・影獵手在默巨與添爐巨人的烈焰預警邊緣閃身反擊</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/08-town.jpg" alt="殘燈鎮・最後的燼火庇護所：全副武裝的誓燈騎士走過街燈映照的石板路" width="100%"><br><sub>殘燈鎮・最後的燼火庇護所：全副武裝的誓燈騎士走過街燈映照的石板路</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/09-dungeon-astromancer.jpg" alt="燼淵地城「黑曜裂谷」・星術師以連鎖閃電迎擊鍛錘役與熔爐的烈焰" width="100%"><br><sub>燼淵地城「黑曜裂谷」・星術師以連鎖閃電迎擊鍛錘役與熔爐的烈焰</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/10-boss-rimelord.jpg" alt="赫爾蒙的冰廳・鍛身者在霜骸領主・赫爾蒙的霜晶預警陣中近身纏鬥" width="100%"><br><sub>赫爾蒙的冰廳・鍛身者在霜骸領主・赫爾蒙的霜晶預警陣中近身纏鬥</sub></td>
</tr>
</table>

### 主要介面

<table>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/11-title.jpg" alt="標題畫面：寂主・奧薩洛斯自陰影中低頭怒視" width="100%"><br><sub>標題畫面：寂主・奧薩洛斯自陰影中低頭怒視</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/12-class-select.jpg" alt="擇定持燼者：燼刃衛的招牌造型，附可用武器、招牌技能與武器指南" width="100%"><br><sub>擇定持燼者：燼刃衛的招牌造型，附可用武器、招牌技能與武器指南</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/13-inventory.jpg" alt="行囊：即時 3D 角色預覽（誓燈騎士，模組化盔甲與劍盾）與 3D 物品圖示" width="100%"><br><sub>行囊：即時 3D 角色預覽（誓燈騎士，模組化盔甲與劍盾）與 3D 物品圖示</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/14-skill-tree.jpg" alt="技能樹：誓燈騎士的技能分支與技能配置列" width="100%"><br><sub>技能樹：誓燈騎士的技能分支與技能配置列</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/15-paragon.jpg" alt="燼紋盤：點亮燼紋節點、嵌入燼符" width="100%"><br><sub>燼紋盤：點亮燼紋節點、嵌入燼符</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/16-world-map.jpg" alt="世界地圖：瑟蘭的七大地域，名望、首領與主線下一步的標記" width="100%"><br><sub>世界地圖：瑟蘭的七大地域，名望、首領與主線下一步的標記</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/17-blacksmith.jpg" alt="鐵匠布蘭・淬火：為傳奇連枷挑選淬火配方" width="100%"><br><sub>鐵匠布蘭・淬火：為傳奇連枷挑選淬火配方</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/18-dialogue.jpg" alt="殘燈鎮・誓燈騎士與守燈長老艾爾溫的對話" width="100%"><br><sub>殘燈鎮・誓燈騎士與守燈長老艾爾溫的對話</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/19-quest-log.jpg" alt="任務日誌：第一幕〈將熄的火盆〉與灰燼荒原的委託" width="100%"><br><sub>任務日誌：第一幕〈將熄的火盆〉與灰燼荒原的委託</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/20-world-tier.jpg" alt="世界階級：冒險者、老練者、夢魘、煉獄四階難度" width="100%"><br><sub>世界階級：冒險者、老練者、夢魘、煉獄四階難度</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/21-settings.jpg" alt="設定：顯示、音量、遊戲與操作說明四個分頁" width="100%"><br><sub>設定：顯示、音量、遊戲與操作說明四個分頁</sub></td>
<td width="50%"></td>
</tr>
</table>

### 0.4.0 新功能

<table>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/22-weapon-guide.jpg" alt="武器指南：每種武器的數值、優劣與比較（誓燈騎士・長柄刀）" width="100%"><br><sub>武器指南：每種武器的數值、優劣與比較（誓燈騎士・長柄刀）</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/23-golem-book.jpg" alt="亡者名冊：喚魂師的骸骨戰士、骸骨術士與魔像三條線，鐵魔像由裝備鑄造" width="100%"><br><sub>亡者名冊：喚魂師的骸骨戰士、骸骨術士與魔像三條線，鐵魔像由裝備鑄造</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/24-golem-battle.jpg" alt="鏽砂廢都・喚魂師的鐵魔像手持鑄入的巨斧，與骸骨大軍圍攻鏽尾蠍" width="100%"><br><sub>鏽砂廢都・喚魂師的鐵魔像手持鑄入的巨斧，與骸骨大軍圍攻鏽尾蠍</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/25-gear-sets.jpg" alt="裝備組：雙手與單手＋副手兩組配裝，F1–F6 一鍵切換" width="100%"><br><sub>裝備組：雙手與單手＋副手兩組配裝，F1–F6 一鍵切換</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/26-bag-filter-marks.jpg" alt="行囊篩選：「全部非升級」一鍵標記回收；符合件數與批次按鈕固定在篩選面板下方" width="100%"><br><sub>行囊篩選：「全部非升級」一鍵標記回收；符合件數與批次按鈕固定在篩選面板下方</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/27-item-tooltip.jpg" alt="物品說明：與目前裝備並排比較，升級判定與每秒傷害、攻速等武器數值" width="100%"><br><sub>物品說明：與目前裝備並排比較，升級判定與每秒傷害、攻速等武器數值</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/28-ember-ladder.jpg" alt="燼階：八段燼憶依序排成階梯，擊敗一階才開啟下一階" width="100%"><br><sub>燼階：八段燼憶依序排成階梯，擊敗一階才開啟下一階</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/29-shared-stash.jpg" alt="共用儲藏箱：所有角色共用，分為四個分頁" width="100%"><br><sub>共用儲藏箱：所有角色共用，分為四個分頁</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/0.4.0/30-pinnacle-boss.jpg" alt="燼階第六階・燼刃衛在終止式・賽倫的同心圓預警中硬撼巔峰首領" width="100%"><br><sub>燼階第六階・燼刃衛在終止式・賽倫的同心圓預警中硬撼巔峰首領</sub></td>
<td width="50%"></td>
</tr>
</table>

## 遊玩原型（Windows 10／11，64 位元）

### 安裝程式（Windows）

最簡單的安裝方式：從[發行頁](https://github.com/blhsing/ashen-abyss/releases/latest)下載 **`AshenAbyss-0.4.1-Setup.exe`**（約 5 MB）並執行。安裝程式會：

- 從 GitHub 下載遊戲檔案（約 3.9 GB），逐一核對 SHA-256，再解壓縮安裝（安裝後約 6.0 GB）；
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
AshenAbyss-0.4.1-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /DIR="D:\Games\AshenAbyss" /LOG="%TEMP%\aa_setup.log"
```

結束代碼：0＝成功、2＝已取消、10＝參數錯誤、11＝無法取得發行清單、12＝下載失敗、13＝Proxy 驗證失敗、14＝檔案驗證失敗、15＝磁碟空間不足、16＝本機分割檔不完整、17＝解壓縮失敗、18＝遊戲正在執行、19＝無法寫入資料夾。完整的參數與說明見 [`tools/installer/README.md`](tools/installer/README.md)。

不想用安裝程式的話，也可以自行下載所有 `AshenAbyss-0.4.1-win64.7z.*` 分割檔，用 [7-Zip](https://www.7-zip.org/) 解壓縮後執行 `AshenAbyss.exe`。

### 手動安裝

1. 從 [最新發行頁](../../releases/latest)下載遊戲本體的所有分割檔：`AshenAbyss-0.4.1-win64.7z.001`、`.002`……（GitHub 限制每個發行檔案不得超過 2 GB，因此遊戲以分割壓縮檔提供）。
2. 將所有分割檔放在同一個資料夾，用 [7-Zip](https://www.7-zip.org/) 開啟 `.001` 檔（它會自動接合各分割檔）。
3. 解壓縮後執行 `AshenAbyss.exe`。遊戲以全螢幕啟動，同一時間只會執行一份。

**顯示卡需求：** 支援 Vulkan 1.3 的顯示卡（NVIDIA GeForce GTX 10 系列或更新、AMD Radeon RX 5000／Radeon 680M 或更新、Intel Arc／Iris Xe），並安裝最新驅動程式。若 Vulkan 無法啟動，遊戲會自動改用 Direct3D 12。具備雙顯示卡的筆電上，遊戲會要求 Windows 使用高效能顯示卡。建議 8 GB 以上系統記憶體及 4 GB 以上顯示記憶體；顯示卡較弱時可在「設定 → 畫質」降低負擔。

**檔案存放位置**

| 項目 | 位置 |
|---|---|
| 存檔（8 個欄位）與設定 | `%APPDATA%\AshenAbyss\saves`、`%APPDATA%\AshenAbyss\settings.json` |
| 共用儲藏箱（所有角色共用；專家模式另一個） | `%APPDATA%\AshenAbyss\saves\stash.json`、`stash_hc.json`（各有 `.bak` 備份） |
| 紀錄檔（回報問題時請附上） | `%APPDATA%\AshenAbyss\logs` |
| 截圖（PrtSc 或 F12；設定「截圖方式」為存成檔案時） | `圖片\AshenAbyss` |

## 操作方式

| 按鍵 | 功能 |
|---|---|
| 滑鼠左鍵 | 移動／攻擊／交談／拾取 |
| W／A／S／D | 直接移動；施放遠程攻擊與法術時也能邊走邊出手（速度較慢） |
| 按住 Shift | 原地出手（不追向目標，施法時也站定） |
| 滑鼠右鍵、1–4 | 技能（在技能頁面中配置） |
| 空白鍵 | 閃避 |
| Q | 藥水 |
| I 或 C | 行囊與角色 |
| K／P | 技能／燼紋 |
| M 或 Tab／J | 地圖／任務日誌 |
| 按住 Alt | 顯示所有掉落物名稱 |
| Esc | 關閉面板／暫停選單 |
| PrtSc 或 F12 | 擷取畫面（預設複製到剪貼簿） |
| F11 | 切換全螢幕 |
| F8 | AI 代打：由 AI 接手遊玩；點擊地面或按移動鍵即可接管 |
| F1–F6／E | 載入裝備組 1–6／在最近兩組間快速切換 |
| G | 武器指南（指向武器或副手時開啟該武器的說明） |
| 行囊中：Shift＋左鍵或 R／X | 標記回收／標記丟棄 · L 鎖定 · F 篩選 · Ctrl＋左鍵丟在地上 |

切換到其他視窗（Alt-Tab）時遊戲會自動暫停。點擊攻擊範圍外的敵人時，英雄會跑到敵人面前再出手。

**邊走邊打：** 遠程攻擊與法術（箭矢、法術投射物、連鎖閃電、地面法術、詛咒、陷阱、魔像號令）出手時，仍可用 W/A/S/D 或按住滑鼠左鍵移動；移動速度降為 65%，邊走邊出手的傷害為 90%，箭矢與法術仍飛向瞄準的目標或地點。按住攻擊鍵瞄準敵人、同時按移動鍵後退，就能邊退邊射。近戰攻擊、衝鋒、跳躍、旋風斬與增益技能仍會站定。可在「設定 → 遊戲 → 施法時可移動」關閉。

**顯示頭盔：** 行囊中英雄預覽左上角的「頭盔：顯示／隱藏」可只隱藏頭盔外觀（屬性照常計算），讓英雄的臉露出來；每位英雄各自保存。

**展示模式：** 從主選單選擇「展示模式」，或在標題畫面閒置一分鐘，遊戲會不斷切換不同的職業、技能、怪物、首領、裝備與場景，展示最激烈的戰鬥畫面；按任意鍵返回。

## 從原始碼建置

本儲存庫包含程式碼、資料、著色器、場景、工具與文件。二進位素材（模型、材質、音訊、字型、圖示）不放在儲存庫中，而是附在每個發行版本裡。

1. 安裝 [Godot 4.7.2 .NET](https://godotengine.org/) 編輯器與 .NET 10 SDK。
2. 複製（clone）本儲存庫。
3. 從發行頁下載 `AshenAbyss-0.4.1-assets.7z.001`（及之後的分割檔，如有），解壓縮到儲存庫根目錄（會填入 `game/assets/…`；對應的 `.import` 檔已在儲存庫中）。
4. 用編輯器開啟 `game/project.godot`（第一次匯入需要數分鐘），或執行 `tools\import.ps1` 後再執行 `tools\run.ps1`。
5. `tools\install.ps1` 會匯出發行版本，並安裝到本機、建立開始功能表捷徑。

`tools\` 中的腳本需要專案位於較短的路徑：它們會以 `subst` 將專案對應為 `Y:` 磁碟機（避開 Windows 的路徑長度限制）。程式架構、測試參數與工具腳本的說明請見 `ARCHITECTURE.md`。

## 0.4.1 版現況

已完成：8 種職業、7 大區域與一座城鎮、14 座支線地城（含具名首領）、含 7 位區域首領的主線劇情、完整怪物圖鑑（129 種怪物與 28 種地區變體）、28 名稀有菁英、7 位要塞首領、4 位世界首領與燼階（8 位巔峰首領）、依實際裝備組成的 3D 英雄外觀、主手／副手／雙手／雙持武器系統（17 種武器與 5 種副手）與武器指南、3D 物品圖示與掉落模型、裝備比較、行囊篩選與批次處理、6 組裝備組、共用儲藏箱、喚魂師的亡者名冊與三種魔像（含鐵魔像鑄造）、帶詞綴的裝備、傳奇威能、鑲嵌與工藝、技能樹、燼紋盤、世界階級、深淵裂隙、8 個存檔欄位與「從離開的地方繼續」、地圖任務目標標記、依元素裝飾的地面法陣、點擊攻擊、情境滑鼠游標、展示模式、遠程攻擊與法術邊走邊放、可隱藏的頭盔外觀、AI 代打（閃避預警、邊退邊射、拾取與管理裝備，八個職業皆已打完劇情）、行囊中的即時 3D 角色、黑暗光影、AI 生成音樂（19 首）與台灣口音配音（207 句全部完成）。

製作中（美術）：影獵手與縛淵術士的部分護甲外觀、裸身皮膚質感、長袍與披布的腿部穿模、雙手武器待機與奔跑、拉弓與左手攻擊動作、後退與側移動作、魔像與骸骨術士的正式模型、主題傳送門、華麗介面邊框。已知問題列於 `QA_NOTES.md`。

## 致謝與授權

第三方素材與工具及其授權條款列於 [`credits/`](credits/)（CC0 材質與模型、SIL OFL 字型、附署名的 CC BY 3.0 圖示、MIT／Apache-2.0 生成模型）。音樂與配音皆以開放模型在本機生成；遊戲中沒有任何聲音模仿真實人物。

本遊戲自有的程式碼與內容尚未選定授權條款；在加入授權之前，保留一切權利。

本作品為獨立專案，與其他任何遊戲或發行商均無關聯，亦未獲其背書。
