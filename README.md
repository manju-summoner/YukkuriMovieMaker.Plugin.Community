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