# VR Festival Poi System

Unity向けの軽量な縁日ポイ＋水システムです。動的な紙のDamage・亀裂・穴・Wetness・水中負荷と、軽量な水面・波紋・飛沫を提供します。

## Requirements

- Unity 6000.3以降（検証バージョン: 6000.3.21f1）
- Built-in Render Pipeline
- 外部Package依存なし
- XR Interaction Toolkit、Input System、特定Tag/Layerへの必須依存なし

## Installation

Package Managerの`+`から`Install package from git URL`を選び、Repository URLを入力します。

```text
https://github.com/InTheDokubo/Poi_VREnnnichi.git?path=/Assets/com.vrfestival.poi~
```

安定版を固定する場合:

```text
https://github.com/InTheDokubo/Poi_VREnnnichi.git?path=/Assets/com.vrfestival.poi~#v1.0.0
```

Repositoryのサブディレクトリに置く場合は、UPMのGit path指定形式を使用してください。

```text
https://github.com/<owner>/<repository>.git?path=/path/to/com.vrfestival.poi#v1.0.0
```

## Quick Start

1. Package Managerで`Samples > Basic Sample > Import`を実行します。
2. Importされた`Demo.unity`を開いて再生します。
3. 独自Sceneでは`Runtime/Prefabs/Poi.prefab`と`Water.prefab`をDrag & Dropします。

## Prefabs

- `Poi.prefab`: Rigidbody、軽量Collider、動的Paper、Wetness、Water Damage、汎用Grab APIを内包します。
- `Water.prefab`: Water Volume、Water Surface、Ripple、Splashを内包し、単独配置できます。

PoiはSample、Camera、GameManager、Goldfishへ依存しません。WaterもPoiやゲーム固有コードへ依存しません。

## Configuration

- `PoiPaperSettings`: 耐久力、濡れやすさ、拡散、乾燥、水中Damage。
- `PoiFrameVisualSettings`: Frame色、Material、外部3Dモデル、位置・回転・スケール。
- `PoiConfiguration`: 上記設定をPoiへ適用します。

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

- Built-in Render Pipeline専用Shader。URP/HDRPはMaterial/Shader差し替えが必要です。
- 本格流体、屈折、反射、泡、魚AI、ゲームスコアは含みません。
- 実機VR入力は利用側XR Frameworkで接続します。

外部Frameモデル要件は`Documentation~/ExternalModelAndVR.md`を参照してください。

## License

MIT License. Copyright (c) 2026 In_The_Dokubo. 詳細は`LICENSE.md`を参照してください。
