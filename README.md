# unity-try

## セットアップ

この Unity プロジェクトでは VRM、3Dモデル、音声、画像、動画、ネイティブプラグインなどのバイナリアセットを Git LFS で管理します。clone 前に Git LFS をインストールし、ローカル環境で有効化してください。

```bash
git lfs install
git clone https://github.com/hirokisakabe/unity-try.git
```

既に clone 済みの場合は、Git LFS を有効化して LFS 対象ファイルを取得してください。

```bash
git lfs install
git lfs pull
```

初回の Unity Package 解決では `packages.unity.com`、`registry.npmjs.org`、`github.com` へのネットワークアクセスが必要です。UniVRM は Git URL 依存として解決するため、`git` コマンドも `PATH` から実行できる状態にしてください。
