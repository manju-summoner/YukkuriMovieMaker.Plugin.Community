# YukkuriMovieMaker4コミュニティプラグイン
YMM4コミュニティで開発するYMM4プラグインです。  
プラグインのサンプルも兼ねています。  
ここで開発されたプラグインはYMM4にデフォルトで組み込まれます。

# ビルド方法
YMM v4.30.0.0以降が必要です。

1. このリポジトリをクローンします。
1. `Directory.Build.props.sample`をコピーして`Directory.Build.props`を作成します。
1. `Directory.Build.props`を編集して、`YMM4DirPath`にYMM4をインストールしているフォルダのパスを指定（またはそこに記載されているフォルダにYMM4をインストール）します。  
※ `Directory.Build.props`はVisualStudio上では表示されないため、エクスプローラーからメモ帳等で開き、編集してください。  
※ パスの末尾は必ず`\`で終わる必要があります。（例: `D:\YMM4\`）
1. `YukkuriMovieMaker.Plugin.Community.sln`を開き、ビルドします。

# 使用方法
YMM4フォルダ内の`YukkuriMovieMaker.Plugin.Community.dll`を置き換えてください。