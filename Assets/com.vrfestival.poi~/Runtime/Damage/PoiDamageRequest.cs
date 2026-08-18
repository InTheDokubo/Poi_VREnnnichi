using System;
using UnityEngine;

namespace Poi
{
    [Serializable]
    public struct PoiDamageRequest
    {
        public Vector3 WorldPosition;
        public Vector3 WorldDirection;
        [Min(0f)] public float Amount;
        [Min(0f)] public float Radius;
        public int Seed;

        public PoiDamageRequest(Vector3 worldPosition, Vector3 worldDirection, float amount, float radius, int seed = 0)
        {
            WorldPosition = worldPosition;
            WorldDirection = worldDirection;
            Amount = amount;
            Radius = radius;
            Seed = seed;
        }
    }
}
