using UnityEngine;

namespace Poi
{
    public sealed class PoiPaperDetachedFragment : MonoBehaviour
    {
        public Mesh OwnedMesh { private get; set; }

        private void OnDestroy()
        {
            if (OwnedMesh != null) Destroy(OwnedMesh);
        }
    }
}
