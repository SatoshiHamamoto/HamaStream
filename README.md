# HamaStream

HamaStream は VRChat の PC 向けワールドで使う、AVPro Video ベースの URL 動画プレイヤーです。URL の共有再生、ローカル一時停止、再生範囲による制御、プレイリスト、手動 Resync に対応します。

## 動作環境

- VRChat Worlds SDK と UdonSharp を導入済みの Unity プロジェクト
- Unity 2022.3.22f1（開発・確認環境）
- VRChat PC クライアント。Quest 版は対象外
- AVPro Video は VRChat SDK 側のコンポーネントを使用します。このリポジトリに SDK 本体は含みません。

## 導入

導入と Inspector の設定は [使用マニュアル](Docs/USER_MANUAL.md) を参照してください。

1. このリポジトリの `Assets/SH/HamaStream` を、対応する `.meta` とともに Unity プロジェクトの同じパスへコピーします。`.unitypackage` が GitHub Release にある場合は、Unity の `Assets > Import Package > Custom Package...` から全項目をインポートできます。
2. Unity のインポートと UdonSharp のコンパイルが終わるまで待ち、Console にエラーがないことを確認します。
3. `Tools > HamaStream > Rebuild Minimal Prefab` を実行し、`Assets/SH/HamaStream/Prefabs/HamaStream.prefab` を確認します。
4. Prefab をシーンへ配置します。配置後、`Tools > HamaStream > Configure Scene URL Load Peers` を実行してシーンを保存します。配置数や Prefab を変更したときも再実行してください。
5. 各 Playlist Controller の Inspector で項目を設定し、再生範囲の BoxCollider の Center と Size を調整します。`Is Trigger` を有効にしてください。
6. VRChat の Build & Test で動作を確認します。AVPro Video の動作確認には VRChat クライアントを使用してください。

`Tools > HamaStream > Add Minimal Player To Scene` から Prefab の生成と現在のシーンへの配置もできます。

## 主な操作

- URL 入力からの再生、共有停止、ループ切り替え
- 増減できるプレイリストモジュール（初期 2 つ、各最大 10 項目）
- ユーザーごとの一時停止と Resync
- 再生範囲外でのローカル一時停止と、範囲内へ戻ったときの再開
- 同一シーン内の複数プレイヤーで URL 読み込み間隔を共有

再生位置は HamaStream 側で同期しません。通常動画の途中参加者は先頭から再生されます。詳しい操作と制約は [使用マニュアル](Docs/USER_MANUAL.md) を参照してください。

## 配布と更新

更新時は `Assets/SH/HamaStream` のアセットと `.meta` をセットで更新してください。`.unitypackage` を使用した場合は同じ Unity プロジェクトへ新しい版をインポートします。別パスに同じアセットを残すと GUID が重複します。

配布者は Unity の Project ウィンドウで `Assets/SH/HamaStream` を選び、`Assets > Export Package...` からパッケージを作成します。エクスポート対象を確認し、VRChat SDK や UdonSharp などの外部パッケージは含めず、生成した `.unitypackage` を GitHub Release に添付します。

`.unitypackage` の別プロジェクトへの導入確認は実施済みです。初回 Release はまだ作成していません。

## ライセンス

HamaStream の配布対象ファイルは [MIT License](LICENSE) です。VRChat SDK、UdonSharp、AVPro Video などの外部製品はこのライセンスの対象ではありません。
