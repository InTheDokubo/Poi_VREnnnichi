using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent]
    public sealed class PoiConfiguration : MonoBehaviour
    {
        [SerializeField] private PoiPaperSettings paperSettings;
        [SerializeField] private PoiFrameVisualSettings frameVisualSettings;
        [SerializeField] private Transform frame;
        [SerializeField] private PoiPaperDamageSystem damageSystem;
        [SerializeField] private PoiPaperWaterInteraction waterInteraction;
        [SerializeField] private PoiPaperMeshGenerator meshGenerator;

        private GameObject externalFrameInstance;
        private MaterialPropertyBlock frameProperties;

        public PoiPaperSettings PaperSettings { get => paperSettings; set => paperSettings = value; }
        public PoiFrameVisualSettings FrameVisualSettings { get => frameVisualSettings; set => frameVisualSettings = value; }
        public Transform Frame { set => frame = value; }
        public PoiPaperDamageSystem DamageSystem { set => damageSystem = value; }
        public PoiPaperWaterInteraction WaterInteraction { set => waterInteraction = value; }
        public PoiPaperMeshGenerator MeshGenerator { set => meshGenerator = value; }

        private void Awake() => ApplyConfiguration();

        [ContextMenu("Apply Configuration")]
        public void ApplyConfiguration()
        {
            Cache();
            if (paperSettings != null)
            {
                damageSystem.ApplySettings(paperSettings);
                waterInteraction.ApplySettings(paperSettings);
                meshGenerator.ApplySettings(paperSettings);
            }
            ApplyFrameVisual();
        }

        private void ApplyFrameVisual()
        {
            if (frame == null || frameVisualSettings == null) return;
            MeshRenderer builtInRenderer = frame.GetComponent<MeshRenderer>();
            if (builtInRenderer != null)
            {
                builtInRenderer.enabled = frameVisualSettings.externalModelPrefab == null;
                if (frameVisualSettings.materialOverride != null)
                    builtInRenderer.sharedMaterial = frameVisualSettings.materialOverride;
                else
                {
                    if (frameProperties == null) frameProperties = new MaterialPropertyBlock();
                    builtInRenderer.GetPropertyBlock(frameProperties);
                    frameProperties.SetColor("_Color", frameVisualSettings.color);
                    builtInRenderer.SetPropertyBlock(frameProperties);
                }
            }

            if (externalFrameInstance != null)
            {
                if (Application.isPlaying) Destroy(externalFrameInstance);
                else DestroyImmediate(externalFrameInstance);
                externalFrameInstance = null;
            }
            if (frameVisualSettings.externalModelPrefab == null) return;

            externalFrameInstance = Instantiate(frameVisualSettings.externalModelPrefab, frame);
            externalFrameInstance.name = frameVisualSettings.externalModelPrefab.name + "_FrameVisual";
            externalFrameInstance.transform.localPosition = frameVisualSettings.localPosition;
            externalFrameInstance.transform.localRotation = Quaternion.Euler(frameVisualSettings.localEulerAngles);
            externalFrameInstance.transform.localScale = frameVisualSettings.localScale;
            if (frameVisualSettings.disableExternalModelColliders)
            {
                Collider[] colliders = externalFrameInstance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            }
        }

        private void Cache()
        {
            if (frame == null) frame = transform.Find("Frame");
            if (damageSystem == null) damageSystem = GetComponentInChildren<PoiPaperDamageSystem>(true);
            if (waterInteraction == null) waterInteraction = GetComponentInChildren<PoiPaperWaterInteraction>(true);
            if (meshGenerator == null) meshGenerator = GetComponentInChildren<PoiPaperMeshGenerator>(true);
        }
    }
}
