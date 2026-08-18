using UnityEngine;

namespace Poi
{
    public sealed class PoiPaperDetachedFragment : MonoBehaviour
    {
        public Mesh OwnedMesh { private get; set; }

        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int DissolveSeedId = Shader.PropertyToID("_DissolveSeed");

        private PoiPaperMeshGenerator owner;
        private MeshRenderer meshRenderer;
        private Collider fragmentCollider;
        private Rigidbody fragmentBody;
        private MaterialPropertyBlock properties;
        private float lifetime;
        private float dissolveDuration;
        private float age;
        private bool dissolving;
        private bool disposing;

        public bool IsDissolving => dissolving;

        public void Initialize(
            PoiPaperMeshGenerator fragmentOwner,
            MeshRenderer renderer,
            Collider collider,
            Rigidbody body,
            float lifetimeBeforeDissolve,
            float duration,
            float dissolveSeed)
        {
            owner = fragmentOwner;
            meshRenderer = renderer;
            fragmentCollider = collider;
            fragmentBody = body;
            lifetime = Mathf.Max(0f, lifetimeBeforeDissolve);
            dissolveDuration = Mathf.Max(0.05f, duration);
            properties = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(properties);
            properties.SetFloat(DissolveAmountId, 0f);
            properties.SetFloat(DissolveSeedId, dissolveSeed);
            meshRenderer.SetPropertyBlock(properties);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (!dissolving)
            {
                if (age < lifetime) return;
                BeginDissolve();
            }

            float progress = Mathf.Clamp01((age - lifetime) / dissolveDuration);
            properties.SetFloat(DissolveAmountId, progress);
            meshRenderer.SetPropertyBlock(properties);
            if (progress >= 1f) Dispose();
        }

        private void BeginDissolve()
        {
            dissolving = true;
            // Stop contacts as soon as the visible silhouette starts shrinking.
            // Gravity remains active so a fragment does not freeze in mid-air.
            if (fragmentCollider != null) fragmentCollider.enabled = false;
            if (fragmentBody != null) fragmentBody.detectCollisions = false;
        }

        public void Dispose()
        {
            if (disposing) return;
            disposing = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (owner != null) owner.NotifyDetachedFragmentDestroyed(gameObject);
            if (OwnedMesh != null) Destroy(OwnedMesh);
        }
    }
}
