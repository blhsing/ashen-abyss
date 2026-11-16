# 燼淵 Ashen Abyss

一款原創的黑暗奇幻動作角色扮演遊戲，全程使用繁體中文，以 Godot 4.7（.NET／C#）製作。
溫暖世界的日爐已然熄滅；英雄帶著最後的餘燼，穿越七大區域，直抵其下的深淵。

**0.1.0 版是可遊玩的原型。** 從第一個區域到結局的主線劇情皆可完整遊玩，但仍有許多部分正在製作中（見下方〈0.1.0 版現況〉）。

## 遊戲畫面

### 七大區域與戰鬥

每一張使用不同的職業、怪物與技能組合（皆為遊戲內實際畫面）。

<table>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/01-wastes-emberblade.jpg" alt="灰燼荒原・燼刃衛以旋風斬殺出骸骨兵的重圍" width="100%"><br><sub>灰燼荒原・燼刃衛以旋風斬殺出骸骨兵的重圍</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/02-frost-astromancer.jpg" alt="霜脊隘口・星術師以霜槍與霜環迎擊霜骸擲矛手，風雪中冰晶四濺" width="100%"><br><sub>霜脊隘口・星術師以霜槍與霜環迎擊霜骸擲矛手，風雪中冰晶四濺</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/03-crypt-soulbinder.jpg" alt="沉眠墓原・喚魂師以凋零吞沒墮落騎士與寂滅教徒，戰利品「銳利的刻紋金戒」落在一旁" width="100%"><br><sub>沉眠墓原・喚魂師以凋零吞沒墮落騎士與寂滅教徒，戰利品「銳利的刻紋金戒」落在一旁</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/04-mire-hunter.jpg" alt="瘴泣沼澤・影獵手擲下煙幕，在沼巫與沼澤魔物的圍攻中反擊" width="100%"><br><sub>瘴泣沼澤・影獵手擲下煙幕，在沼巫與沼澤魔物的圍攻中反擊</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/05-sands-forgebody.jpg" alt="鏽砂廢都・鍛身者以連拳與崩山掌獨戰乾屍與砂中湧出的亡軍" width="100%"><br><sub>鏽砂廢都・鍛身者以連拳與崩山掌獨戰乾屍與砂中湧出的亡軍</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/06-sanctum-lampknight.jpg" alt="寂滅聖城・誓燈騎士以守燈光環化腳下為聖地，聖焰焚燒寂滅教團" width="100%"><br><sub>寂滅聖城・誓燈騎士以守燈光環化腳下為聖地，聖焰焚燒寂滅教團</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/07-abyss-wildsoul.jpg" alt="燼淵・荒靈行者以熊靈重擊硬撼燼魔群，縛魂在遠處游移" width="100%"><br><sub>燼淵・荒靈行者以熊靈重擊硬撼燼魔群，縛魂在遠處游移</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/08-town.jpg" alt="殘燈鎮・最後的燼火庇護所：誓燈騎士與鎮民老葛交談，街燈映照石板路" width="100%"><br><sub>殘燈鎮・最後的燼火庇護所：誓燈騎士與鎮民老葛交談，街燈映照石板路</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/09-dungeon-pactbinder.jpg" alt="燼淵地城「黑曜裂谷」・縛淵術士以淵門洞開召來淵門巨魔，衝入燼魔群" width="100%"><br><sub>燼淵地城「黑曜裂谷」・縛淵術士以淵門洞開召來淵門巨魔，衝入燼魔群</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/10-boss-grand-hush.jpg" alt="無聲大聖堂・燼刃衛在寂滅大導師・賽倫的同心圓預警中硬撼首領" width="100%"><br><sub>無聲大聖堂・燼刃衛在寂滅大導師・賽倫的同心圓預警中硬撼首領</sub></td>
</tr>
</table>

### 主要介面

<table>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/11-title.jpg" alt="標題畫面：寂主・奧薩洛斯自陰影中低頭怒視" width="100%"><br><sub>標題畫面：寂主・奧薩洛斯自陰影中低頭怒視</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/12-class-select.jpg" alt="擇定持燼者：八種職業之一的縛淵術士，附招牌技能與職業專精" width="100%"><br><sub>擇定持燼者：八種職業之一的縛淵術士，附招牌技能與職業專精</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/13-inventory-tooltip.jpg" alt="行囊：即時 3D 角色預覽（誓燈騎士，可拖曳旋轉）與裝備欄位" width="100%"><br><sub>行囊：即時 3D 角色預覽（誓燈騎士，可拖曳旋轉）與裝備欄位</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/14-skill-tree.jpg" alt="技能樹：縛淵術士的締約召喚與技能配置列" width="100%"><br><sub>技能樹：縛淵術士的締約召喚與技能配置列</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/15-paragon.jpg" alt="燼紋盤：點亮燼紋節點、嵌入燼符" width="100%"><br><sub>燼紋盤：點亮燼紋節點、嵌入燼符</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/16-world-map.jpg" alt="世界地圖：瑟蘭的七大地域，灰燼荒原的據點、地城與首領一覽" width="100%"><br><sub>世界地圖：瑟蘭的七大地域，灰燼荒原的據點、地城與首領一覽</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/17-blacksmith.jpg" alt="鐵匠布蘭・淬火：為「霜語守望・淵語之書」挑選淬火配方" width="100%"><br><sub>鐵匠布蘭・淬火：為「霜語守望・淵語之書」挑選淬火配方</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/18-dialogue.jpg" alt="殘燈鎮・與守燈長老艾爾溫的對話" width="100%"><br><sub>殘燈鎮・與守燈長老艾爾溫的對話</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/19-quest-log.jpg" alt="任務日誌：第一幕〈將熄的火盆〉" width="100%"><br><sub>任務日誌：第一幕〈將熄的火盆〉</sub></td>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/20-world-tier.jpg" alt="世界階級：冒險者、老練者、夢魘、煉獄四階難度" width="100%"><br><sub>世界階級：冒險者、老練者、夢魘、煉獄四階難度</sub></td>
</tr>
<tr>
<td width="50%" valign="top"><img src="https://raw.githubusercontent.com/blhsing/ashen-abyss/media/screenshots/21-settings.jpg" alt="設定：顯示、音量、遊戲與操作說明四個分頁" width="100%"><br><sub>設定：顯示、音量、遊戲與操作說明四個分頁</sub></td>
<td width="50%"></td>
</tr>
</table>

## 遊玩原型（Windows 10／11，64 位元）

1. 從 [0.1.0 版發行頁](../../releases/tag/v0.1.0)下載遊戲本體的所有分割檔：`AshenAbyss-0.1.0-win64.7z.001`、`.002`……（GitHub 限制每個發行檔案不得超過 2 GB，因此遊戲以分割壓縮檔提供）。
2. 將所有分割檔放在同一個資料夾，用 [7-Zip](https://www.7-zip.org/) 開啟 `.001` 檔（它會自動接合各分割檔）。
3. 解壓縮後執行 `AshenAbyss.exe`。遊戲以全螢幕啟動，同一時間只會執行一份。

**顯示卡需求：** 支援 Vulkan 1.3 的顯示卡（NVIDIA GeForce GTX 10 系列或更新、AMD Radeon RX 5000／Radeon 680M 或更新、Intel Arc／Iris Xe），並安裝最新驅動程式。若 Vulkan 無法啟動，遊戲會自動改用 Direct3D 12。具備雙顯示卡的筆電上，遊戲會要求 Windows 使用高效能顯示卡。建議 8 GB 以上系統記憶體及 4 GB 以上顯示記憶體；顯示卡較弱時可在「設定 → 畫質」降低負擔。

**檔案存放位置**

| 項目 | 位置 |
|---|---|
| 存檔（8 個欄位）與設定 | `%APPDATA%\AshenAbyss\saves`、`%APPDATA%\AshenAbyss\settings.json` |
| 紀錄檔（回報問題時請附上） | `%APPDATA%\AshenAbyss\logs` |
| 截圖（PrtSc 或 F12） | `圖片\AshenAbyss` |

## 操作方式

| 按鍵 | 功能 |
|---|---|
| 滑鼠左鍵 | 移動／攻擊／交談／拾取 |
| 滑鼠右鍵、1–4 | 技能（在技能頁面中配置） |
| 空白鍵 | 閃避 |
| Q | 藥水 |
| I 或 C | 行囊與角色 |
| K／P | 技能／燼紋 |
| M 或 Tab／J | 地圖／任務日誌 |
| 按住 Alt | 顯示所有掉落物名稱 |
| Esc | 關閉面板／暫停選單 |
| PrtSc 或 F12／F11 | 截圖／切換全螢幕 |
| Shift＋左鍵（行囊） | 標記為廢品 · Ctrl＋左鍵（行囊）：丟在地上 |

切換到其他視窗（Alt-Tab）時遊戲會自動暫停。

## 從原始碼建置

本儲存庫包含程式碼、資料、著色器、場景、工具與文件。二進位素材（模型、材質、音訊、字型、圖示）不放在儲存庫中，而是附在每個發行版本裡。

1. 安裝 [Godot 4.7.2 .NET](https://godotengine.org/) 編輯器與 .NET 10 SDK。
2. 複製（clone）本儲存庫。
3. 從發行頁下載 `AshenAbyss-0.1.0-assets.7z.001`（及之後的分割檔，如有），解壓縮到儲存庫根目錄（會填入 `game/assets/…`；對應的 `.import` 檔已在儲存庫中）。
4. 用編輯器開啟 `game/project.godot`（第一次匯入需要數分鐘），或執行 `tools\import.ps1` 後再執行 `tools\run.ps1`。
5. `tools\install.ps1` 會匯出發行版本，並安裝到本機、建立開始功能表捷徑。

`tools\` 中的腳本需要專案位於較短的路徑：它們會以 `subst` 將專案對應為 `Y:` 磁碟機（避開 Windows 的路徑長度限制）。程式架構、測試參數與工具腳本的說明請見 `ARCHITECTURE.md`。

## 0.1.0 版現況

已完成：8 種職業、7 大區域與一座城鎮、14 座支線地城、含 7 位區域首領的主線劇情、帶詞綴的裝備、傳奇威能、鑲嵌與工藝、技能樹、燼紋盤、世界階級、深淵裂隙、8 個存檔欄位、點陣圖示、黑暗光影、AI 生成音樂（19 首中的 15 首）與台灣口音配音（188 句中的 25 句）。

製作中：完整怪物圖鑑（目前遊戲中約出現 129 種怪物中的 17 種）、具名菁英與要塞、世界及巔峰首領、非人形生物、其餘的音樂與配音，以及華麗介面邊框。已知問題列於 `QA_NOTES.md`。

## 致謝與授權

第三方素材與工具及其授權條款列於 [`credits/`](credits/)（CC0 材質與模型、SIL OFL 字型、附署名的 CC BY 3.0 圖示、MIT／Apache-2.0 生成模型）。音樂與配音皆以開放模型在本機生成；遊戲中沒有任何聲音模仿真實人物。

本遊戲自有的程式碼與內容尚未選定授權條款；在加入授權之前，保留一切權利。

本作品為獨立專案，與其他任何遊戲或發行商均無關聯，亦未獲其背書。
