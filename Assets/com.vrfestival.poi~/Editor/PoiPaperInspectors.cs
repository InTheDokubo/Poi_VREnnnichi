using UnityEditor;
using UnityEngine;

namespace Poi.Editor
{
    [CustomEditor(typeof(PoiConfiguration))]
    public sealed class PoiConfigurationEditor : UnityEditor.Editor
    {
        private bool showInternalReferences;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("利用者が通常変更するのは、次の2つの設定Assetだけです。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("paperSettings"), new GUIContent("紙の物性設定", "耐久力、濡れ方、乾きやすさ、水中移動への耐性をまとめたAssetです。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frameVisualSettings"), new GUIContent("フレーム外観設定", "標準色、Material、外部3DモデルをまとめたAssetです。"));
            showInternalReferences = EditorGUILayout.Foldout(showInternalReferences, "内部参照（通常は変更不要）", true);
            if (showInternalReferences)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("frame"), new GUIContent("フレームTransform"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("damageSystem"), new GUIContent("紙ダメージ処理"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("waterInteraction"), new GUIContent("水との相互作用"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("meshGenerator"), new GUIContent("紙Mesh生成"));
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            if (GUILayout.Button("設定を今すぐ適用"))
            {
                foreach (Object item in targets) ((PoiConfiguration)item).ApplyConfiguration();
            }
        }
    }

    [CustomEditor(typeof(PoiFrameVisualSettings))]
    public sealed class PoiFrameVisualSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "このAssetをPoiRootの Poi Configuration > Frame Visual Settings に指定します。外部モデルは見た目だけを担当し、RigidbodyやColliderはPoiRoot側を使用してください。",
                MessageType.Info);
            EditorGUILayout.LabelField("標準フレーム", EditorStyles.boldLabel);
            Draw("materialOverride", "差し替えMaterial");
            Draw("color", "フレームの色");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("外部3Dモデル（任意）", EditorStyles.boldLabel);
            Draw("externalModelPrefab", "外観用Prefab");
            Draw("localPosition", "位置補正");
            Draw("localEulerAngles", "回転補正");
            Draw("localScale", "拡大率");
            Draw("disableExternalModelColliders", "外部Colliderを無効にする");
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }
    }

    [CustomEditor(typeof(PoiPaperSettings))]
    public sealed class PoiPaperSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "このAssetをPoiRootの Poi Configuration > Paper Settings に指定します。" +
                "数値を変更した場合は、再生し直すか Poi Configuration の Apply Configuration を実行してください。",
                MessageType.Info);

            EditorGUILayout.LabelField("かんたんプリセット", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("破れやすい")) ApplyPreset(0.65f, 1.25f, 0.2f, 0.2f, 1.0f, 0.45f, 0.22f);
            if (GUILayout.Button("標準")) ApplyPreset(1f, 1f, 0.3f, 0.35f, 0.7f, 0.35f, 0.18f);
            if (GUILayout.Button("丈夫")) ApplyPreset(1.6f, 0.7f, 0.55f, 0.65f, 0.4f, 0.15f, 0.1f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            DrawSection("紙の基本耐久力", "大きいほど丈夫／小さいほど破れやすい、を基準に調整します。",
                ("breakThreshold", "破れるまでの耐久値"),
                ("damageMultiplier", "ダメージの受けやすさ"),
                ("fullyWetStrengthMultiplier", "濡れたときに残る強度"),
                ("damageFalloffPower", "損傷範囲の集中度"));
            DrawSection("濡れ方・乾きやすさ", "濡れる速さ、染みの広がり、乾燥速度を調整します。",
                ("wettingRate", "濡れやすさ"),
                ("wetnessDiffusionRate", "染みの広がりやすさ"),
                ("dryingRate", "乾きやすさ"),
                ("wetnessUpdateInterval", "濡れ判定の更新間隔（秒）"));
            DrawSection("水中移動への耐性", "速く振ったときや水へ入れた瞬間の壊れやすさです。耐性を上げるには、開始速度を上げ、各ダメージ値を下げます。",
                ("minimumWaterDamageSpeed", "ダメージが始まる水中速度"),
                ("waterDamageMultiplier", "水中移動ダメージ"),
                ("waterEntryMultiplier", "水へ入れた瞬間のダメージ"),
                ("maximumWaterDamagePerSample", "1回あたりの最大水ダメージ"));
            DrawSection("破れた紙片の見た目", "破れた後に紙片が残り、自然に消えるまでの時間です。",
                ("fragmentLifetime", "紙片が物理落下する時間（秒）"),
                ("fragmentDissolveDuration", "紙片が消える時間（秒）"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSection(string title, string help, params (string property, string label)[] fields)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(help, MessageType.None);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(fields[i].property);
                EditorGUILayout.PropertyField(property, new GUIContent(fields[i].label, property.tooltip));
            }
        }

        private void ApplyPreset(float threshold, float damage, float wetStrength, float minimumWaterSpeed,
            float waterDamage, float entryDamage, float maximumWaterDamage)
        {
            serializedObject.FindProperty("breakThreshold").floatValue = threshold;
            serializedObject.FindProperty("damageMultiplier").floatValue = damage;
            serializedObject.FindProperty("fullyWetStrengthMultiplier").floatValue = wetStrength;
            serializedObject.FindProperty("minimumWaterDamageSpeed").floatValue = minimumWaterSpeed;
            serializedObject.FindProperty("waterDamageMultiplier").floatValue = waterDamage;
            serializedObject.FindProperty("waterEntryMultiplier").floatValue = entryDamage;
            serializedObject.FindProperty("maximumWaterDamagePerSample").floatValue = maximumWaterDamage;
            serializedObject.ApplyModifiedProperties();
        }
    }

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
