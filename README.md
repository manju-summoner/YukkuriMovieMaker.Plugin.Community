# YukkuriMovieMaker4コミュニティプラグイン
YMM4コミュニティで開発するYMM4プラグインです。  
プラグインのサンプルも兼ねています。  
ここで開発されたプラグインはYMM4にデフォルトで組み込まれます。

# ビルド方法
YMM v4.30.0.0以降が必要です。

1. このリポジトリをクローンします。
1. `Directory.Build.props.sample`をコピーして`Directory.Build.props`を作成します。
1. `Directory.Build.props`を編集して、`YMM4DirPath`にYMM4をインストールしているフォルダのパスを指定（またはそこに記載されているフォルダにYMM4をインストール）します。  
※ 普段使いしているYMM4とは別に、開発専用のYMM4を用意することをお勧めします。
※ `Directory.Build.props`はVisualStudio上では表示されないため、エクスプローラーからメモ帳等で開き、編集してください。  
※ パスの末尾は必ず`\`で終わる必要があります。（例: `D:\YMM4\`）
1. `YukkuriMovieMaker.Plugin.Community.sln`を開き、ビルドします。
※ビルドを実行すると、`YMM4DirPath`に指定したフォルダ内に`YukkuriMovieMaker.Plugin.Community.dll`がコピーされます。`

# デバッグ方法
1. `YukkuriMovieMaker.Plugin.Community`プロジェクトを右クリックし、`スタートアッププロジェクトに設定`を選択する
1. `デバッグ(D)`→`YukkuriMovieMaker.Plugin.Community のデバッグ プロパティ`を選択する
1. ウィンドウ左上の`新しいプロファイルを作成します`ボタン→`実行可能ファイル`をクリックする
1. `実行可能ファイル`欄に、`Directory.Build.props`に設定しているYMM4フォルダ内の`YukkuriMovieMaker.exe`を指定する
1. デバッグ開始ボタン`▶ YukkuriMovieMaker.Plugin.Community`右側の`▼`ボタンをクリックし、`3.`で作成したプロファイル`プロファイル 1`を選択する
1. デバッグ開始ボタンが`▶ プロファイル 1`に変わるので、ボタンをクリックしてデバッグを開始する

# 使用方法（一般ユーザー向け）
YMM4にデフォルトで組み込まれます。YMM4のアップデートをお待ち下さい。

# コントリビューション
PRの送り先ブランチは以下を使い分けてください。
- **新機能の追加**: `develop` ブランチ宛
- **リリース済み機能のバグ修正**: `master` ブランチ宛

# 参照可能なアセンブリ
プラグインは公開APIである`YukkuriMovieMaker.Plugin.dll`（および`YukkuriMovieMaker.Plugin.Community.csproj`に元から記載されている公開アセンブリ）のみを参照してください。

`YukkuriMovieMaker.dll`などYMM4本体の内部実装アセンブリを参照したPRは、メンテナンス性の都合上マージ対象外となります。コミュニティプラグインは本体に組み込んで配布するものであり、かつプラグイン開発者向けの参考実装（サンプル）も兼ねているため、安定した公開APIのみに依存し、本体の内部変更の影響を受けない状態を保つ方針です。

本体内部のAPIが必要な機能は、コミュニティプラグインとしてではなく、通常のプラグインとして個別にリリースしてください。
