using System;

namespace Poi
{
    [Serializable]
    public struct PoiPaperCell
    {
        public bool IsPaper;
        public float Damage;
        [UnityEngine.Range(0f, 1f)] public float Wetness;
        public bool IsBroken;
    }
}
