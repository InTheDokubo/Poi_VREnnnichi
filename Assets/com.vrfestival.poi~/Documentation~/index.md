# VR Festival Poi System Documentation

`Poi.prefab`と`Water.prefab`をSceneへ配置すると基本構成が成立します。

- Paper性能は`PoiPaperSettings`で管理します。
- Frame外観は`PoiFrameVisualSettings`で管理します。
- 外部からのDamageは`PoiDamageRequest`へ統一します。
- 水判定は`PoiWaterVolume`の公開APIを使用します。
- XR Frameworkとの接続は`PoiGrabTarget`または利用側Grab Componentで行います。

外部モデルとVRの詳細は`ExternalModelAndVR.md`を参照してください。
