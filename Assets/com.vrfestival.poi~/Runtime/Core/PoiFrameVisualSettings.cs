using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(menuName = "Poi/フレーム外観設定", fileName = "PoiFrameVisualSettings")]
    public sealed class PoiFrameVisualSettings : ScriptableObject
    {
        [Header("標準フレーム")]
        [Tooltip("標準フレームへ適用するMaterial。未指定ならPackage標準Materialを使用します。")]
        public Material materialOverride;
        [Tooltip("Materialを差し替えない場合に使用する標準フレームの色です。")]
        public Color color = new Color(0.91f, 0.38f, 0.17f, 1f);

        [Header("外部3Dモデル（任意）")]
        [Tooltip("FBXなどから作成したPrefabを指定します。未指定なら標準Frameを表示します。")]
        public GameObject externalModelPrefab;
        [Tooltip("外部モデルのローカル位置補正です。")]
        public Vector3 localPosition;
        [Tooltip("外部モデルのローカル回転補正（度）です。")]
        public Vector3 localEulerAngles;
        [Tooltip("外部モデルのローカル拡大率です。通常は(1, 1, 1)を使用します。")]
        public Vector3 localScale = Vector3.one;
        [Tooltip("外部モデルは外観専用とし、そのColliderを無効化します。標準FrameのColliderは維持されます。")]
        public bool disableExternalModelColliders = true;
    }
}
