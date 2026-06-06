# Unity MCP連携 検証メモ

## 結論

採用候補は [CoplayDev/MCP for Unity](https://github.com/CoplayDev/unity-mcp) とする。

理由:

- Unity 6000.4.10f1 の本プロジェクトで package import とコンパイルが通った。
- MCP client から Unity Editor へ接続し、scene 情報の読み取りと GameObject 作成を確認できた。
- MIT license の無償 OSS で、公式 Unity AI MCP の subscription / seat 要件を避けられる。
- Claude Code、Codex、VS Code、Cursor など複数 client を想定した設定が用意されている。

## 比較

| 候補 | 要件 | 費用 / subscription | 所感 |
| --- | --- | --- | --- |
| Unity 公式 Unity AI MCP | Unity AI entitlement と seat 割り当てが必要。Unity pricing では Unity AI concurrent MCP connections が plan に紐づく。 | Unity Personal は Unity AI trial 後に月額 subscription が必要。Unity Pro 以上では paid plan に Unity AI が含まれる。 | 公式だが、issue のスコープ外である「課金開始」に近い。今回は見送り。 |
| CoplayDev/MCP for Unity | Unity 2021.3 LTS+、Python 3.10+、uv、MCP client。 | MIT license。MCP for Unity 自体は無償。 | 今回の採用候補。導入が軽く、ローカル検証に成功。 |
| akiojin/unity-mcp-server | OpenUPM package。Unity Editor の scene analysis / input automation などを MCP 経由で提供。 | OSS package。 | OpenUPM 経由で導入しやすいが、今回は stars / docs / client configurator の充実度で CoplayDev を優先。 |

参考:

- [Unity Support: Unity AI MCP Connection Fails / Unity AI Gateway connection Error](https://support.unity.com/hc/en-us/articles/48958235901460-Unity-AI-MCP-Connection-Fails-Unity-AI-Gateway-connection-Error)
- [Unity Plans & Pricing](https://unity.com/products?c=unity+services&s=collaboration)
- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
- [MCP for Unity Install guide](https://coplaydev.github.io/unity-mcp/getting-started/install)
- [OpenUPM: com.akiojin.unity-mcp-server](https://openupm.com/packages/com.akiojin.unity-mcp-server/)

## 採用候補の要件

- Unity: project は `6000.4.10f1`。CoplayDev の要件は Unity 2021.3 LTS 以上のため満たす。
- 追加ツール: `uv` / `uvx` が必要。ローカルでは `/opt/homebrew/bin/uv` と `/opt/homebrew/bin/uvx` を確認済み。
- MCP client: 今回は Python MCP client から `http://127.0.0.1:8080/mcp` へ接続して検証した。
- 料金: CoplayDev/MCP for Unity は MIT license の無償 OSS。公式 Unity AI MCP は Unity AI subscription / paid plan entitlement の確認が必要。

## 導入内容

`Packages/manifest.json` に以下を追加した。

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
```

Unity package resolve 後、`Packages/packages-lock.json` では `hash: 78ee5418415953b79c358bfe6355fcc3fde7912b` として固定された。

また、headless 検証用に `Assets/McpValidation/Editor/UnityMcpValidationRunner.cs` を追加した。これは Unity Editor 側の HTTP bridge を `http://127.0.0.1:8080` に接続するための Editor-only helper で、本番 scene や asset を自動編集するものではない。

AI client 側の project shared MCP 設定として、root の `.mcp.json` に以下を追加した。Claude Code は project scope の MCP server 設定を `.mcp.json` から読み込む。

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uvx",
      "args": [
        "--from",
        "mcpforunityserver",
        "mcp-for-unity",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

この設定では AI client が `mcp-for-unity` server process を stdio transport で起動する。Unity Editor 側の MCP for Unity bridge は別途 Unity Editor 内で接続可能な状態にしておく必要がある。

Codex 向けには `.mcp.json` ではなく `.codex/config.toml` に MCP server を設定した。Codex CLI と IDE extension は `config.toml` の MCP 設定を共有し、project-scoped `.codex/config.toml` は trusted project でのみ読み込まれる。

```toml
[mcp_servers.unityMCP]
command = "uvx"
args = ["--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"]
```

Claude Code と Codex で MCP 設定ファイル自体は共通化できない。Claude Code は `.mcp.json`、Codex は `.codex/config.toml` を読むため、同じ `uvx` 起動コマンドをそれぞれの形式で重複定義している。

## ローカル検証結果

### 1. package import / compile

実行:

```bash
/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/hirokisakabe/unity-try \
  -logFile /tmp/unity-mcp-import.log
```

結果:

- `Library/PackageCache/com.coplaydev.unity-mcp@efaf786e8772` が作成された。
- Unity script compilation は成功。

### 2. Unity Editor と MCP server 接続

MCP server:

```bash
uvx --from mcpforunityserver mcp-for-unity --transport http --http-url http://127.0.0.1:8080
```

Unity bridge:

```bash
/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/hirokisakabe/unity-try \
  -executeMethod UnityTry.McpValidation.Editor.UnityMcpValidationRunner.StartHttpBridge \
  -logFile /tmp/unity-mcp-bridge.log
```

結果:

```text
[UnityMcpValidation] HTTP bridge started=True, verified=True, connected=True, error=
```

### 3. AI client から scene 情報読み取り

Python MCP client で `manage_scene` を呼び出した。

結果:

```json
{"success":true,"message":"Retrieved active scene information.","data":{"name":"","path":"","buildIndex":-1,"isDirty":false,"isLoaded":true,"rootCount":0}}
```

### 4. AI client から検証用 GameObject 作成

Python MCP client で `manage_gameobject` を呼び出した。

結果:

```json
{"success":true,"message":"GameObject 'MCP_Validation_Cube' created successfully in scene.","data":{"name":"MCP_Validation_Cube","componentNames":["UnityEngine.Transform","UnityEngine.MeshFilter","UnityEngine.BoxCollider","UnityEngine.MeshRenderer"]}}
```

続けて `find_gameobjects` で検索し、`MCP_Validation_Cube` の `instanceID` が返ることを確認した。

## 注意点

- AI client から MCP tools を使うには、Unity package の導入だけでなく `.mcp.json` などの client 側 MCP 設定が必要。
- Codex から MCP tools を使うには、trusted project として `.codex/config.toml` の project config が読み込まれる必要がある。読み込み後は Codex CLI の `/mcp` で `unityMCP` の状態を確認できる。
- stdio 設定では AI client が MCP server process を起動するが、Unity Editor 側の bridge は Unity Editor 上で接続可能な状態にしておく必要がある。
- `uvx` が PATH で解決できない環境では、各 user の local config で absolute path に置き換える。
- 初回は Unity Editor が同じ project を開いていたため、batchmode 起動が project lock で失敗した。headless 検証時は既存 Editor を閉じる。
- `-executeMethod` 内で async 処理を同期 wait すると Editor main thread が塞がり、MCP tool call が詰まる。検証 helper は `EditorApplication.delayCall` で bridge 起動を逃がしている。
- 今回作成した `MCP_Validation_Cube` は未保存 scene 上の一時オブジェクトで、重要 asset には保存していない。
