# ポイ：外部3DモデルとVR Grab要件

## 外部Frameモデル

外部モデルはFBXなどからUnityへImportし、Prefab化してから`PoiFrameVisualSettings.externalModelPrefab`へ指定する。

- 単位はメートル。Import Scaleは原則`1`。
- 標準寸法は外径約90 mm、持ち手長約95 mm。
- Unity座標は右手が`+X`、持ち手先端方向が`-Y`、紙面法線が`+Z`。
- 原点はFrameリングの中心を推奨する。
- モデルのTransformはPosition `(0,0,0)`、Rotation `(0,0,0)`、Scale `(1,1,1)`を推奨する。
- 原点・向き・寸法が異なる場合は設定assetのLocal Position / Euler Angles / Scaleで補正する。
- Built-in Render Pipelineで表示可能なMaterialを使用する。
- 外部Prefabは外観専用。Collider、Rigidbody、破損スクリプト、Grabスクリプトは不要。
- 外部Colliderは初期設定で無効化され、標準Frame/Handle Colliderが物理判定を担当する。
- Paperは外部モデルへ統合しない。動的破損する`Paper`子Objectをそのまま使用する。
- VR向けの目安として、1モデル数千～数万Triangles、Material 1～2個程度を推奨する。

## VR Grab

`PoiRoot`にはRigidbody、軽量Primitive Collider、`PoiGrabTarget`、`GrabAttach`が存在する。

- `GrabAttach`は持ち手中央の推奨握り位置。位置と回転はPrefab Variantで調整可能。
- 独自VR入力では選択開始時に`BeginGrab(handTransform)`を呼ぶ。
- 選択終了時に`EndGrab(handLinearVelocity, handAngularVelocity)`を呼ぶ。
- 保持中はFixedUpdateの`MovePosition / MoveRotation`で追従するため、水中速度と回転負荷へ反映される。
- リリース時は手の線形速度・角速度をRigidbodyへ引き継げる。
- XR Interaction Toolkitを後から導入する場合は`XRGrabInteractable`を`PoiRoot`へ追加し、Attach Transformへ`GrabAttach`を指定できる。
- XR Toolkit自身のMovement Typeを使用する場合、`PoiGrabTarget.BeginGrab`を同時使用せず、どちらか一方を移動責任者にする。
- Sampleのデスクトップ操作コンポーネントはVRシーンでは無効化し、VR Interactorだけを移動責任者にする。

## 変更してはいけない構造

- `PoiRoot`のRigidbodyを外部モデル側へ移動しない。
- `Paper`をFrameモデルへ結合しない。
- `FrameColliders`とHandle Colliderを見た目モデルへ依存させない。
- 非一様なRoot Scaleは紙座標、Collider、Grab姿勢へ影響するため避ける。
