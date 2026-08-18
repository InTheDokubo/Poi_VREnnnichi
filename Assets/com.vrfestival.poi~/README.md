# VR Festival Poi System

Unity向けの軽量な縁日ポイ＋水システムです。動的な紙のDamage・亀裂・穴・Wetness・水中負荷と、軽量な水面・波紋・飛沫を提供します。

## Requirements

- Unity 6000.3以降（検証バージョン: 6000.3.21f1）
- Built-in Render Pipeline / Universal Render Pipeline (URP)
- 第三者製Packageへの必須依存なし
- XR Interaction Toolkit、Input System、特定Tag/Layerへの必須依存なし

### Dependencies

`package.json`から以下のUnity標準Moduleが自動導入されます。通常は利用者が別途追加する必要はありません。

- `com.unity.modules.physics`
- `com.unity.modules.particlesystem`
- `com.unity.modules.imgui`（Basic Sampleの画面説明用）

任意・利用環境依存：

- URPプロジェクトでは`com.unity.render-pipelines.universal`が必要です。通常のURP Templateには最初から導入されています。Built-inプロジェクトへURPを追加する必要はありません。
- Input Systemは必須ではありません。Basic SampleはInput Systemのみ、旧Input Managerのみ、Bothの各設定を自動判別します。
- XR Interaction Toolkitは必須ではありません。VR Grabを使用する場合のみ利用側プロジェクトへ導入してください。
- HDRPは現時点で専用Shaderを同梱していません。

## Installation

Package Managerの`+`から`Install package from git URL`を選び、Repository URLを入力します。

```text
https://github.com/InTheDokubo/Poi_VREnnnichi.git?path=/Assets/com.vrfestival.poi~
```

安定版を固定する場合:

```text
https://github.com/InTheDokubo/Poi_VREnnnichi.git?path=/Assets/com.vrfestival.poi~#v1.0.4
```

Repositoryのサブディレクトリに置く場合は、UPMのGit path指定形式を使用してください。

```text
https://github.com/<owner>/<repository>.git?path=/path/to/com.vrfestival.poi#v1.0.0
```

## Quick Start

1. Package Managerで`Samples > Basic Sample > Import`を実行します。
2. Importされた`Demo.unity`を開いて再生します。
3. 独自Sceneでは`Runtime/Prefabs/Poi.prefab`と`Water.prefab`をDrag & Dropします。

### Updating an imported Sample

SamplesはPackage外の`Assets/Samples/VR Festival Poi System/<version>/`へコピーされるため、自動更新されません。旧`1.0.1` Sampleを導入済みの場合はそのフォルダーを削除し、Package Managerから`1.0.2`のBasic Sampleを再Importしてください。

### Pink materials

- URPではPackage Managerに`Universal RP`が導入され、Project SettingsのGraphics／QualityへURP Pipeline Assetが設定されていることを確認してください。
- Built-inでは追加Packageは不要です。
- 本PackageのMaterialへRender Pipeline Converterを実行する必要はありません。Built-in／URP用SubShaderを自動選択します。
- HDRPでは専用Shaderへの差し替えが必要です。

## Prefabs

- `Poi.prefab`: Rigidbody、軽量Collider、動的Paper、Wetness、Water Damage、汎用Grab APIを内包します。
- `Water.prefab`: Water Volume、Water Surface、Ripple、Splashを内包し、単独配置できます。

PoiはSample、Camera、GameManager、Goldfishへ依存しません。WaterもPoiやゲーム固有コードへ依存しません。

## Configuration

- `PoiPaperSettings`: 耐久力、濡れやすさ、拡散、乾燥、水中Damage。
- `PoiFrameVisualSettings`: Frame色、Material、外部3Dモデル、位置・回転・スケール。
- `PoiConfiguration`: 上記設定をPoiへ適用します。

### 紙の物性を変更する

1. Projectウィンドウで `Create > Poi > 紙の物性設定` を選び、設定Assetを作成します。
2. シーンまたはPrefab Variantの`PoiRoot`を選択します。
3. `Poi Configuration > Paper Settings`へ作成したAssetを指定します。
4. 再生開始時に自動適用されます。再生中にAssetを変更した場合は、`Poi Configuration`のコンテキストメニューから`Apply Configuration`を実行します。

Package内の`DefaultPaperSettings`を直接編集せず、利用プロジェクトの`Assets/PoiSettings/`などへ独自Assetを保存してください。Package更新で独自設定が失われるのを防げます。

Inspectorには「破れやすい」「標準」「丈夫」のプリセットがあります。プリセットを出発点に、次の項目を調整してください。

| やりたいこと | 主に変更する項目 | 調整方向 |
|---|---|---|
| 紙全体を丈夫にする | 破れるまでの耐久値 | 上げる |
| 衝突への耐性を上げる | ダメージの受けやすさ | 下げる |
| 濡れても強度を保つ | 濡れたときに残る強度 | 上げる |
| 早く濡らす | 濡れやすさ | 上げる |
| 染みを広げる | 染みの広がりやすさ | 上げる |
| 早く乾かす | 乾きやすさ | 上げる |
| 水中で振り回しても壊れにくくする | ダメージが始まる水中速度 | 上げる |
| 水の抵抗による破損を弱める | 水中移動ダメージ | 下げる |
| 水へ入れた瞬間の破損を弱める | 水へ入れた瞬間のダメージ | 下げる |
| 突然大きく破れるのを抑える | 1回あたりの最大水ダメージ | 下げる |

「衝突への耐性」は専用の独立値ではなく、`破れるまでの耐久値`と`ダメージの受けやすさ`の組み合わせで決まります。水中移動だけを調整したい場合は「水中移動への耐性」の4項目を変更してください。

### フレームの外観を変更する

`Create > Poi > フレーム外観設定`からAssetを作り、`PoiRoot > Poi Configuration > Frame Visual Settings`へ指定します。色やMaterialのほか、外部3DモデルのPrefabと位置・回転・拡大率を設定できます。外部モデルは見た目専用とし、`Rigidbody`、Collider、`XRGrabInteractable`を外部モデルやHandleへ追加しないでください。

## Public API

- `PoiPaperDamageSystem.ApplyDamage(PoiDamageRequest)`
- `PoiPaperDamageSystem.ResetPaper()`
- `PoiWaterVolume.Contains(Vector3)`
- `PoiWaterVolume.GetDepth(Vector3)`
- `PoiWaterVolume.GetWaterVelocity(Vector3)`
- `PoiWaterSurfaceVisual.AddRipple(Vector3, float)`
- `PoiGrabTarget.BeginGrab(Transform)` / `EndGrab(Vector3, Vector3)`
- `PoiPaperSurface.GetCell(Vector2Int)`でDamage、Wetness、Broken状態を取得

## VR Integration

`XRGrabInteractable` must be added to `PoiRoot` (the object with the `Rigidbody`), never to the child `Handle`. Assign `GrabAttach` to its Attach Transform. Velocity Tracking, Kinematic, and Instantaneous movement are supported by the water-damage motion sampler. Do not drive the poi with both XR Interaction Toolkit and `PoiGrabTarget.BeginGrab` simultaneously.

特定XR Frameworkには依存しません。`PoiGrabTarget`を独自Interactorから呼ぶか、XR Interaction Toolkit導入後に`XRGrabInteractable`を追加し、`GrabAttach`をAttach Transformへ指定します。移動責任者は一方だけにしてください。

## Performance Notes

- セルGameObjectなし
- Wetness更新は既定10 Hz
- 毎フレームのPaper Mesh/MeshCollider再生成なし
- 切り離された紙片は短時間Rigidbodyで落下した後、Noise Dissolveして自動破棄されます。利用側でDestroyする必要はありません。
- Rippleは固定数プール
- Water Surfaceの微細波はGPU頂点変位

### Simulation Grid vs Visual Mesh

- `Grid Resolution`はDamage、Wetness、Water、Broken、Tear判定に使うCPU側Simulation解像度です。VRでは`32`を推奨します。
- `Mesh Subdivisions`はSimulation Cellごとの追加Visual分割数です。穴や亀裂の輪郭を滑らかにしたい場合は、まずこちらを調整してください。
- 例：`Grid Resolution = 32`、`Mesh Subdivisions = 4`ならSimulationは`32 x 32`のまま、Visual Meshは概ね`128 x 128`相当です。
- Mesh SubdivisionsもMesh再構築頂点数を増やすため、必要以上に上げないでください。
- `Grid Resolution >= 64`ではInspectorに性能警告、`>= 128`ではVR向けの強い警告を表示します。値自体はClampしません。

## Known Limitations

- Built-inとURP用Shaderを同梱しています。HDRPではMaterial/Shaderの差し替えが必要です。
- 本格流体、屈折、反射、泡、魚AI、ゲームスコアは含みません。
- 実機VR入力は利用側XR Frameworkで接続します。

外部Frameモデル要件は`Documentation~/ExternalModelAndVR.md`を参照してください。

## License

MIT License. Copyright (c) 2026 In_The_Dokubo. 詳細は`LICENSE.md`を参照してください。
