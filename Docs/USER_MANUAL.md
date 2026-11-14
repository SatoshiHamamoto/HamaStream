# HamaStream 詳細マニュアル

[README](../README.md) | [クイックスタート](QUICK_START.md)

## 対象環境

VRChat PC 版のワールドで動作します。開発環境は Unity 2022.3.22f1、VRChat Worlds SDK、UdonSharp です。Quest 版は対象外です。動画再生には SDK の AVPro Video を使用します。

## セットアップの詳細

1. このリポジトリの `Assets/SH/HamaStream` を、対応する `.meta` とともに Unity プロジェクトの同じパスへコピーします。`.unitypackage` が GitHub Release にある場合は、`Assets > Import Package > Custom Package...` から全項目をインポートできます。既存の別パスに同じ GUID のアセットを置かないでください。
2. Unity のインポートと UdonSharp のコンパイル完了を待ち、Console のエラーを確認します。
3. `Tools > HamaStream > Rebuild Minimal Prefab` で Prefab を生成します。生成先は `Assets/SH/HamaStream/Prefabs/HamaStream.prefab` です。元になる VRChat SDK の AVPro サンプル Prefab が必要です。
4. Prefab をシーンに配置します。`Tools > HamaStream > Add Minimal Player To Scene` なら生成と配置を一度に行えます。
5. `Playback Area` の BoxCollider を視聴範囲に合わせ、`Is Trigger` をオンにします。
6. プレイリストを使う場合は、`PlaylistControls` ごとに付く `HamaStreamPlaylistController` を設定します。初期状態では 2 つあります。
7. `Tools > HamaStream > Configure Scene Playlists` と `Tools > HamaStream > Configure Scene URL Load Peers` を実行し、シーンを保存します。プレイリストを増減した場合は前者、プレイヤーの追加・複製・削除後は両方を再実行します。
8. VRChat の Build & Test で URL 再生、範囲内外の動作、複数人での共有を確認します。AVPro Video の確認には VRChat クライアントを使用します。

更新時は、アセットと `.meta` をセットで上書きします。`.unitypackage` を使用する場合は、新しい版を同じプロジェクトへインポートします。

## HamaStreamController の Inspector

Prefab が設定する参照は、UI や映像出力先を差し替える場合以外は維持してください。

| 項目 | 説明 |
| --- | --- |
| `Video Player` | 再生に使う AVPro Video Player。 |
| `Url Input` | ワールド内の URL 入力欄。 |
| `Loop Toggle` | ワールド内のループ切り替え。 |
| `Default Loop` | 起動時にオーナーが適用するループ設定。以後の変更は共有されます。 |
| `Status Text` | 再生状態やエラーの表示先。 |
| `Current Url Text` | 現在共有されている URL の表示先。 |
| `Owner Text` | オブジェクトオーナーの表示先。 |
| `Playlist Controllers` | 子のプレイリストモジュール一覧。メニューから自動設定されます。 |
| `Enable Output` | 映像を Render Texture に出力する場合にオン。 |
| `Video Screen Renderer` | 出力元のスクリーン Renderer。`Enable Output` がオンの場合に表示。 |
| `Standby Screen Renderer` | 停止中の画像を重ねる表示面。Builder が生成した子オブジェクトを参照します。 |
| `Output Render Texture` | 映像のコピー先。`Enable Output` がオンの場合に表示。未指定だと出力されません。 |
| `Standby Texture` | 停止・読み込み待ち・再生終了・動画エラー時にスクリーンへ表示する画像。`Enable Output` がオンなら Render Texture にも出力します。未指定なら従来の表示のままです。 |
| `Playback Area` | ローカル再生を許可する範囲の BoxCollider。必須で、`Is Trigger` をオンにします。 |
| `Url Load Cooldown Seconds` | URL 読み込み間隔。最低 5 秒です。 |
| `Url Load Peers` | 同じシーンで読み込み間隔を共有するプレイヤー。メニューから自動設定します。 |

`Enable Output` をオンにしても、出力先を表示するマテリアルやオブジェクトの設定は別途必要です。

スタンバイ画像を設定するには、画像を Unity に取り込み、シーン内の `HamaStream` を選んで `Standby Texture` に割り当てます。`Standby Screen Renderer` が設定されていることも確認してください。一時停止中は映像フレームを維持します。Prefab 全体の既定値にする場合は、シーンの変更を Prefab に Apply してください。

## HamaStreamPlaylistController の Inspector

初期状態では Playlist 1 と Playlist 2 があり、それぞれ最大 10 件登録できます。項目の URL が空ならワールド内では表示されません。各 `PlaylistControls` の同じ GameObject に UI と Controller があります。

プレイリストを追加するには、シーン内の HamaStream を選択し、`Tools > HamaStream > Add Playlist To Selected Player` を実行します。追加されたモジュールは空の項目設定で作られます。削除するには対象の `PlaylistControls` を選択し、`Tools > HamaStream > Remove Selected Playlist` を実行します。どちらも参照と見出しを自動更新します。手動で GameObject を削除した場合は `Configure Scene Playlists` を実行してください。全プレイリストを削除した後でも、追加メニューから再作成できます。配置順で番号が振り直されます。プレイリストの増減は Unity のシーン編集時に行います。

| 項目 | 説明 |
| --- | --- |
| `Main Controller` | このプレイリストが操作する `HamaStreamController`。 |
| `Playlist Buttons` | 生成されたボタンへの参照。編集不可。 |
| `Playlist Display Name Labels` | 生成された表示名ラベルへの参照。編集不可。 |
| `Playlist Url Labels` | 生成された URL ラベルへの参照。編集不可。 |
| `Item Count` | 登録する項目数。0～10。 |
| `Display Name` | 項目の表示名。空欄なら表示名も空欄です。 |
| `URL` | 選択時に再生する URL。空欄の項目は非表示です。 |
| `Selected Button Color` | 選択中のボタン色。 |

プレイリストの項目を選ぶと、登録 URL の共有再生が始まります。自動連続再生や前後への移動はありません。直接 URL を再生すると、プレイリストの選択表示は解除されます。

## VR 内での操作と共有範囲

| 操作 | 影響 |
| --- | --- |
| URL 入力後に `Play` | URL と再生状態を全員に共有。入力しただけでは共有されません。 |
| URL 入力欄が空のまま `Play` | 現在の共有 URL で再生またはローカル一時停止を解除。 |
| プレイリスト選択 | URL、再生状態、選択項目を共有。 |
| `Pause` | 自分だけ一時停止。再生位置を保持します。 |
| `Stop` | 全員の再生を停止。共有 URL は保持します。 |
| `Resync` または再同期キューブ | 自分だけ現在の URL に再接続。共有状態は変更しません。 |
| `Loop` | ループ設定を全員に共有。 |

再生範囲外ではそのユーザーの再生が一時停止し、範囲内に戻ると再開します。再生位置は HamaStream 側で同期しません。通常動画への途中参加では先頭から再生されます。URL の読み込みや Resync を続けて操作した場合は、約 5 秒待つ必要があります。

## よくある確認点

- `Configuration error`: 必須の参照、特に `Video Player`、`Url Input`、`Playback Area` を確認します。
- プレイリストが見えない: `Item Count` と各項目の `URL` を確認します。
- 映像が再生されない: VRChat PC クライアントで確認し、URL、再生範囲、UI の状態表示を確認します。
- プレイヤーを増減した: `Configure Scene URL Load Peers` を再実行し、シーンを保存します。

共有状態はプレイヤーごとに同期され、ローカル一時停止と Resync は操作したユーザーにだけ適用されます。
