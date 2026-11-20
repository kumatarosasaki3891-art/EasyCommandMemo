# Microsoft Store / Windows アプリアイコン アセット一覧

本フォルダーには、Microsoft Store（パートナー センター）の提出用画像および Windows MSIX パッケージングに必要なすべての標準サイズアセットが揃っています。

---

## 📌 登録済み Microsoft パートナーセンター情報（GitHub 公開用にマスク済み）

- **Package/Identity/Name**: `XXXX`
- **Package/Identity/Publisher**: `CN=XXXX`
- **Package/Properties/PublisherDisplayName**: `XXXX`
- **Package Family Name (PFN)**: `XXXX`
- **Package SID**: `XXXX`
- **Microsoft Store ID**: `XXXX`

---

## 1. Microsoft Store（パートナー センター）掲載情報用

ストアへの申請時、パートナー センターの「ストアの登録情報」>「画像」セクションでアップロードする画像です。

| ファイル名 | 推奨解像度 | 用途・説明 |
|---|---|---|
| `StoreListing-Icon-300x300.png` | 300 × 300 px | **【必須】アプリ アイコン**（ストアの検索結果や製品詳細ページで表示されるメインアイコン） |
| `StoreListing-Icon-512x512.png` | 512 × 512 px | 一般的なストア掲載・プロモーション用アセット |
| `StoreListing-Icon-2160x2160.png` | 2160 × 2160 px | **【推奨】高解像度アイコン / 2:3 または 1:1 正方形アート**（大型ディスプレイ・ストア特集用） |
| `StoreLogo.png` | 50 × 50 px | ストア ロゴ（50x50 または 100x100） |

---

## 2. Windows / MSIX パッケージ (Package.appxmanifest) 用

MSIX Packaging Tool や Visual Studio でパッケージングする際、`Package.appxmanifest` で指定するビジュアルアセットです。
scale-100 / 125 / 150 / 200 / 400 および targetsize (16〜256px, unplated / lightunplated) を網羅しています。

| アセット名 | 基準サイズ | ファイル一覧の例 | 用途 |
|---|---|---|---|
| **Square44x44Logo** | 44 × 44 px | `Square44x44Logo.png`<br>`Square44x44Logo.scale-*.png`<br>`Square44x44Logo.targetsize-*.png`<br>`Square44x44Logo.altform-unplated_targetsize-*.png` | タスクバー、スタートメニューのアプリ一覧、通知バナー |
| **Square150x150Logo** | 150 × 150 px | `Square150x150Logo.png`<br>`Square150x150Logo.scale-*.png` | スタートメニューの中タイル、検索結果 |
| **Square71x71Logo** | 71 × 71 px | `Square71x71Logo.png`<br>`Square71x71Logo.scale-*.png` | スタートメニューの小タイル |
| **Square310x310Logo** | 310 × 310 px | `Square310x310Logo.png`<br>`Square310x310Logo.scale-*.png` | スタートメニューの大タイル |
| **Wide310x150Logo** | 310 × 150 px | `Wide310x150Logo.scale-*.png` | スタートメニューのワイドタイル（ブランドカラー中央配置） |
| **SplashScreen** | 620 × 300 px | `SplashScreen.scale-*.png` | アプリ起動時のスプラッシュスクリーン |
| **StoreLogo** | 50 × 50 px | `StoreLogo.png`<br>`StoreLogo.scale-*.png` | ストア用バッジロゴ |

---

## 3. デスクトップ アプリケーション本体用 (`Assets/` 直下)

| ファイル名 | 用途 |
|---|---|
| `app.ico` | Windows EXE 埋め込み用マルチサイズアイコン（16, 24, 32, 48, 64, 128, 256px 内蔵）<br>各ウィンドウのタイトルバーおよびタスクバー表示に使用 |
| `icon.png` | アプリ内部（MainWindow ヘッダー左上）での高精細表示用アイコン（512 × 512 px） |
| `icon.jpg` | 元画像（2048 × 2048 px） |
