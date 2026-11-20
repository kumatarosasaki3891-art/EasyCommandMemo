# Flathub / Flatpak パッケージ化ガイド (EasyCommandMemo)

このドキュメントでは、**EasyCommandMemo（簡単コマンド帳）** を Flatpak パッケージとしてローカルでビルド・テストする手順、および Flathub へ申請する手順を説明します。

---

## 1. 作成・配置されたファイル一覧

| ファイル名 | 役割 |
| :--- | :--- |
| `io.github.kumatarosasaki3891_art.EasyCommandMemo.yaml` | Flatpak ビルドマニフェスト（25.08 ランタイム + .NET 10 SDK 拡張） |
| `io.github.kumatarosasaki3891_art.EasyCommandMemo.metainfo.xml` | AppStream メタ情報（Flathub ストア表示・日英対応） |
| `io.github.kumatarosasaki3891_art.EasyCommandMemo.desktop` | デスクトップエントリ（通常起動＋クイック検索アクション付き） |
| `nuget-sources.json` | Flathub オフラインビルド用の NuGet パッケージ依存定義 |
| `LICENSE` | MIT ライセンスファイル |

### Application ID について
- **ID**: `io.github.kumatarosasaki3891_art.EasyCommandMemo`
- GitHub ユーザー名のハイフン（`-`）は Flathub / AppStream ガイドラインに従いアンダースコア（`_`）へ置換しています。

### finish-args（付与権限）について
- `--socket=x11`, `--share=ipc`, `--device=dri`: Avalonia UI の GUI 描画。
- ネットワークアクセス権やホストファイルシステム権限は不要です（ローカル SQLite のみ使用し、JSON インポート／エクスポートのファイル選択ダイアログおよび既定ブラウザ起動は Flatpak が暗黙に提供する XDG Desktop Portal 経由で動作します）。

### クイック検索ショートカットについて（Flatpak / Wayland）
- Flatpak サンドボックス内では OS 全体のグローバルホットキーフックは利用できないため、アプリ側のグローバルホットキー監視は無効化されます。
- 代わりにデスクトップ環境（GNOME など）のカスタムショートカットから以下のコマンドを割り当ててください（単一起動 IPC により起動中のウィンドウへ即座にクイック検索が開きます）：

```bash
flatpak run io.github.kumatarosasaki3891_art.EasyCommandMemo --quick
```

---

## 2. ローカルでのビルドと動作確認（Linux / WSL2 環境）

### 必要ツールのインストール (Ubuntu / Debian 例)
```bash
sudo apt update
sudo apt install flatpak flatpak-builder
flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo

# ランタイムと SDK、.NET 10 SDK 拡張機能のインストール
flatpak install flathub org.freedesktop.Platform//25.08
flatpak install flathub org.freedesktop.Sdk//25.08
flatpak install flathub org.freedesktop.Sdk.Extension.dotnet10//25.08
```

### ローカルソースからのビルド・インストール・起動確認
初期状態のマニフェストは `type: dir`（作業ディレクトリ直下）を参照しているため、そのままビルドできます。

```bash
flatpak-builder --user --install --force-clean build-dir io.github.kumatarosasaki3891_art.EasyCommandMemo.yaml
flatpak run io.github.kumatarosasaki3891_art.EasyCommandMemo
```

単体 `.flatpak` バンドルを出力したい場合:
```bash
flatpak-builder --repo=repo --force-clean build-dir io.github.kumatarosasaki3891_art.EasyCommandMemo.yaml
flatpak build-bundle repo EasyCommandMemo-1.1.0.flatpak io.github.kumatarosasaki3891_art.EasyCommandMemo
```

---

## 3. Flathub への申請手順

1. GitHub でソースコードを公開し、リリースタグ（例：`linux-v1.1.0`）を Push します。
2. `io.github.kumatarosasaki3891_art.EasyCommandMemo.yaml` の `sources` を `type: dir` から `type: git`（`url`, `tag`, `commit` 指定）へ切り替えます。
3. [flathub/flathub](https://github.com/flathub/flathub) をフォークし、`io.github.kumatarosasaki3891_art.EasyCommandMemo` ブランチにマニフェストと `nuget-sources.json` を配置して `new-pr` ブランチへ PR を作成します。
