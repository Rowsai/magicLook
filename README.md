# magicLook Phase1/Phase4 表示・設定レイアウト修正版

## 修正内容

- `ブリザガ + サンダガ` の組み合わせ表示が抑制されていた問題を修正。
- `Phase1` に `ブリザガ + サンダガ` の4パターンを追加。
- `Phase1` と `Phase4` に同じラベルが存在しても、設定テキストが混ざらないよう `Phase + Label` で既存テキストを保持するよう修正。
- 保存済みテキストが空の場合は、初期値を再設定するよう修正。
- 添付デザインに合わせた設定描画用の `ConfigWindowPhaseTextRenderer.cs` を追加。

## 重要

今回、既存の `ConfigWindow.cs` 本体は添付されていなかったため、設定画面レイアウトは差し替え用レンダラーとして追加しています。
既存の `ConfigWindow.cs` の「magicLook 詠唱組み合わせ設定」を描画している箇所を、以下の1行に置き換えてください。

```csharp
ConfigWindowPhaseTextRenderer.Draw(this.configuration, this.plugin.SaveConfig);
```

`plugin` / `configuration` のフィールド名が既存ConfigWindow内で違う場合は、実際のフィールド名に合わせてください。

## Phase4 初期値

- 真ブリザガ / 真サンダガ = 踏まない
- 真ブリザガ / 偽サンダガ = 直線だけ
- 偽ブリザガ / 真サンダガ = 扇だけ
- 偽ブリザガ / 偽サンダガ = 両方踏む
