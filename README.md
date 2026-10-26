# Sayobot Download ruleset 原型

以 **osu!lazer 2026.1005.0 正式版**為目標的譜面下載原型，目前版本 **0.2.0**。所有下載和匯入都在 osu! 行程內執行；沒有反射讀寫／呼叫遊戲成員、`UnsafeAccessor`、Harmony、程序注入或外部下載程式。

## 安裝及試用

1. 從 `artifacts` 取得 `osu.Game.Rulesets.Sayobot.dll`。
2. 在 lazer 設定中按「開啟 osu! 資料夾」，將 DLL 放入其中的 `rulesets` 子目錄；不存在時可自行建立。
3. 重啟 lazer。看到 **Sayobot Download 已載入**通知即代表服務啟動。
4. 保持原本的 osu!／taiko／catch／mania 遊戲模式，照常按譜面下載按鈕。
5. 在設定搜尋 **Sayobot**，可調整接管、無影片、官方回退及代理選項，也可輸入譜面集 ID（例如 `41823`）手動測試。

只需要複製這一個 DLL。不要把編譯工具、測試程式、`osu.Game.dll` 或 `osu.Framework.dll` 放進 `rulesets`。

預設啟用接管、使用無影片包、下載失敗時回退官方；Sayobot 預設直接連線，不依賴系統代理。同時最多傳輸兩個譜面集。

移除 DLL 並重啟即可解除安裝。設定中的接管開關只影響新請求；進行中的工作可在通知列取消。

## 介面語言

支援 **繁體中文、簡體中文、English、日本語**，自動跟隨 lazer 的語言設定，切換後不需重啟。設定標題、說明、輸入提示、下載／匯入進度、通知及錯誤說明均已翻譯；其他語言使用英文，數值格式仍跟隨遊戲語系。

繁體中文支援 `zh-Hant`、台灣／香港／澳門語系；簡體中文支援 `zh`、`zh-Hans`。使用公開的 `ILocalisableStringData`／`LocalisableFormattableString` 介面，翻譯直接編入 DLL，不需額外語言檔或外部程式。錯誤通知提供翻譯後的原因，runtime 日誌保留原始例外以便診斷。

## 原型範圍

- 只訂閱公開的 `BeatmapModelDownloader.DownloadBegan`，取消原請求後建立自己的下載工作，不替換遊戲的網路處理器或下載服務。
- 適用於經過該下載器的入口；尚未逐一驗證所有多人、排名遊戲、自動更新入口。
- 使用自己的通知顯示下載進度和取消按鈕。原生通知可能短暫顯示「已取消」，按鈕內的進度不會跟隨 Sayobot 工作。匯入後，原生資料庫追蹤器會更新譜面的可用狀態。
- `DownloadBegan` 在原請求啟動後才發出，無法保證原伺服器完全不收到請求。
- 原請求的影片選項未公開，因此以 **Sayobot：下載無影片版本**設定為準。
- 下載入口使用 `https://dl.sayobot.cn/beatmaps/download/{full|novideo}/{setId}`，允許 HTTPS 重新導向至其他 `sayobot.cn` 節點及其連接埠。
- 下載前後不會向 Sayobot 傳送 osu! 的 Token、Cookie 或帳戶資訊。HTTP 用戶端與遊戲的 HTTP 堆疊分開。
- 檢查 ZIP、`.osu` 格式和譜面集 ID；原生請求有 MD5 校驗碼時也會比對，避免匯入鏡像舊版本。單一檔案上限 2 GiB；單一 `.osu` 上限 16 MiB。
- 舊譜面缺少線上 ID 時，使用已知譜面集 ID 及可匹配的難度 MD5 補全資料，讓原生可用狀態追蹤正常更新。
- 鏡像缺圖、校驗失敗、HTTP 錯誤或閒置 45 秒時，可回退原生下載。手動取消及開始匯入後的錯誤不會自動回退；回退請求會略過接管一次，避免循環。
- 已開始匯入時不再接受下載取消；一般匯入遵循 lazer 自身的暫停匯入行為。更新使用 `ImportAsUpdate` 保留加入日期等本地資料。

## 建置

需要 .NET 10 SDK。Linux 上可執行：

```sh
./scripts/build.sh
```

腳本會取得指定正式版的參考組件（需 Python 3 及 `unsquashfs`），編譯、執行離線測試並產生 `artifacts` 安裝包。若工作區內已有 `.tools/dotnet/dotnet`，會優先使用該 SDK。

也可以使用自己解壓的正式版組件：

```sh
dotnet build osu.Game.Rulesets.Sayobot/osu.Game.Rulesets.Sayobot.csproj -c Release \
  -p:LazerReferenceDirectory=/absolute/path/to/lazer/assemblies
```

沒有正式版參考組件時，專案可退回 NuGet `ppy.osu.Game` 的 API 參考版本；發佈包則應使用正式版組件重新編譯。

## 驗證

下載器離線測試：

```sh
dotnet run --project tests/Sayobot.Tests -c Release
```

本地化測試（使用正式版框架組件，不啟動遊戲或連線）：

```sh
dotnet run --project tests/Sayobot.LocalisationTests -c Release
```

加上一次實際 Sayobot 下載（譜面集 `41823`，約 2.87 MB；測完刪除暫存檔）：

```sh
dotnet run --project tests/Sayobot.Tests -c Release -- --live
```

使用正式版組件、臨時資料庫及模擬登入服務的無視窗整合測試，不使用玩家資料或帳號：

```sh
dotnet run --project tests/Sayobot.IntegrationTests -c Release
```

這些測試不會把 DLL 安裝到現有遊戲資料夾。
