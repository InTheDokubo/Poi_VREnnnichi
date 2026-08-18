using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(menuName = "Poi/Frame Visual Settings", fileName = "PoiFrameVisualSettings")]
    public sealed class PoiFrameVisualSettings : ScriptableObject
    {
        [Header("Built-in Frame")]
        public Material materialOverride;
        public Color color = new Color(0.91f, 0.38f, 0.17f, 1f);

        [Header("External Model (optional)")]
        [Tooltip("FBXなどから作成したPrefabを指定します。未指定なら標準Frameを表示します。")]
        public GameObject externalModelPrefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        [Tooltip("外部モデルは外観専用とし、そのColliderを無効化します。標準FrameのColliderは維持されます。")]
        public bool disableExternalModelColliders = true;
    }
}
