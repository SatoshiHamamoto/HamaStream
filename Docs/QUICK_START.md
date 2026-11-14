# HamaStream 簡易マニュアル

[README](../README.md) | [詳細マニュアル](USER_MANUAL.md)

## セットアップ

1. VRChat Worlds SDK と UdonSharp を導入した Unity 2022.3.22f1 プロジェクトを用意します。
2. unitypackageを、Unity の `Assets > Import Package > Custom Package...` からインポートします。
3. Unity の読み込みと UdonSharp のコンパイルが終わったら、`Tools > HamaStream > Add Minimal Player To Scene` を実行します。
4. 配置した `HamaStream` の `Playback Area` に割り当てられた BoxCollider の `Center` と `Size` を調整します。
5. 必要なら `PlaylistControls1` と `PlaylistControls2` に付く `HamaStreamPlaylistController` の Inspector で、各項目の表示名と URL を設定します。
6. `Tools > HamaStream > Configure Scene Playlists` と `Tools > HamaStream > Configure Scene URL Load Peers` を実行し、シーンを保存します。プレイリストを増減した場合は前者、プレイヤーを追加・複製・削除した場合は両方を再実行します。
7. VRChat の Build & Test で確認します。
Prefab だけを作り直す場合は `Tools > HamaStream > Rebuild Minimal Prefab` を使用します。詳細は [セットアップ手順](USER_MANUAL.md#セットアップの詳細) を参照してください。

## VR 内での操作

| 操作 | 結果 |
| --- | --- |
| URL を入力して `Play` | URL を共有して再生。入力欄は空になります。 |
| プレイリストの項目を選択 | その URL を共有して再生。 |
| `Pause` | 自分だけ一時停止。 |
| `Play` | URL 入力欄が空なら、現在の URL の再生・一時停止解除。 |
| `Stop` | 全員の再生を停止。URL は残ります。 |
| `Resync` | 自分だけ現在の URL に再接続。ライブ配信のずれを直したいときに使用。 |
| `Loop` | ループ設定を全員で共有。 |

独立した再同期ボタンやキューブも、自分だけの `Resync` として使えます。再生範囲を出ると自分の再生が一時停止し、戻ると再開します。URL 読み込み直後の再操作は約 5 秒待ってください。
