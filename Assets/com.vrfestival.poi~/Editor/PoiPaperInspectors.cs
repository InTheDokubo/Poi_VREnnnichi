using UnityEditor;
using UnityEngine;

namespace Poi.Editor
{
    [CustomEditor(typeof(PoiPaperSurface))]
    public sealed class PoiPaperSurfaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            serializedObject.Update();
            int resolution = serializedObject.FindProperty("gridResolution").intValue;
            if (resolution >= 128)
            {
                EditorGUILayout.HelpBox(
                    "Very high Simulation Grid Resolution. VR may suffer high CPU cost and tearing spikes. " +
                    "If you only need smoother tear edges, keep Grid Resolution at 32 and increase Mesh Subdivisions instead. The value is not clamped.",
                    MessageType.Error);
            }
            else if (resolution >= 64)
            {
                EditorGUILayout.HelpBox(
                    "Grid Resolution 64+ increases Wetness, Water, Damage, Tear, mesh and collider rebuild cost. " +
                    "32 is recommended for normal VR use. The value remains available for intentional high-resolution use.",
                    MessageType.Warning);
            }
            else if (resolution == 32)
            {
                EditorGUILayout.HelpBox("Grid Resolution 32 is the recommended VR simulation setting.", MessageType.Info);
            }

            PoiPaperSurface surface = (PoiPaperSurface)target;
            PoiPaperMeshGenerator generator = surface.GetComponent<PoiPaperMeshGenerator>();
            if (generator != null)
            {
                SerializedObject generatorObject = new SerializedObject(generator);
                int subdivisions = generatorObject.FindProperty("meshSubdivisions").intValue;
                EditorGUILayout.LabelField("Simulation Grid", resolution + " x " + resolution);
                EditorGUILayout.LabelField(
                    "Approx. Visual Mesh Grid",
                    (resolution * subdivisions) + " x " + (resolution * subdivisions));
                EditorGUILayout.HelpBox(
                    "Grid Resolution controls Damage, Wetness, Break, Water and Tear simulation. " +
                    "Mesh Subdivisions adds visual tear-edge detail without adding simulation cells.",
                    MessageType.None);
            }
        }
    }

    [CustomEditor(typeof(PoiPaperMeshGenerator))]
    public sealed class PoiPaperMeshGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            PoiPaperMeshGenerator generator = (PoiPaperMeshGenerator)target;
            EditorGUILayout.HelpBox(
                "Increase Mesh Subdivisions first when you want smoother tear edges. It does not increase Damage or Wetness cell count, " +
                "but very high visual density still increases mesh rebuild cost.",
                MessageType.Info);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Visual Components", generator.LastVisualComponentCount.ToString());
                EditorGUILayout.LabelField("Removed Tiny Islands", generator.LastRemovedVisualIslandCount.ToString());
                EditorGUILayout.LabelField("Removed Island Area", generator.LastRemovedVisualIslandArea.ToString("G4") + " m²");
                EditorGUILayout.LabelField("Detached Fragments", generator.DetachedFragmentCount.ToString());
            }
        }
    }
}
