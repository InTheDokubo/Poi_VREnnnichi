using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(menuName = "Poi/紙の物性設定", fileName = "PoiPaperSettings")]
    public sealed class PoiPaperSettings : ScriptableObject
    {
        [Header("紙の耐久力")]
        [Tooltip("紙が破れ始めるまでに蓄積できるダメージ量。大きいほど丈夫です。")]
        [Min(0.01f)] public float breakThreshold = 1f;
        [Tooltip("すべてのダメージに掛かる倍率。大きいほど破れやすくなります。")]
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("完全に濡れた紙に残る強度の割合。1なら濡れても弱くならず、0.3なら乾いた紙の30%の強度です。")]
        [Range(0.05f, 1f)] public float fullyWetStrengthMultiplier = 0.3f;
        [Tooltip("ダメージ中心から周囲への減衰の強さ。大きいほど損傷が狭い範囲へ集中します。")]
        [Min(0.1f)] public float damageFalloffPower = 1.5f;

        [Header("濡れ方と乾き方")]
        [Tooltip("水に触れた場所が濡れる速さ。大きいほど短時間で濡れます。")]
        [Min(0f)] public float wettingRate = 0.75f;
        [Tooltip("濡れが隣接する紙へ広がる速さ。大きいほど染みが速く広がります。")]
        [Min(0f)] public float wetnessDiffusionRate = 0.9f;
        [Tooltip("水から出した紙が乾く速さ。大きいほど早く乾きます。0なら乾きません。")]
        [Min(0f)] public float dryingRate = 0.025f;
        [Tooltip("濡れ判定の更新間隔（秒）。小さいほど滑らかですがCPU負荷が増えます。通常は0.1を推奨します。")]
        [Range(0.02f, 0.5f)] public float wetnessUpdateInterval = 0.1f;

        [Header("破れた紙片")]
        [Tooltip("切り離された紙片が、通常の物理挙動で残る時間（秒）。")]
        [Min(0f)] public float fragmentLifetime = 0.9f;
        [Tooltip("紙片が自然に溶けるように消えるまでの時間（秒）。")]
        [Min(0.05f)] public float fragmentDissolveDuration = 0.75f;

        [Header("水中移動への耐性")]
        [Tooltip("水中ダメージが発生し始める相対速度（m/s）。大きいほど水中で振り回しても壊れにくくなります。")]
        [Min(0f)] public float minimumWaterDamageSpeed = 0.35f;
        [Tooltip("水中移動によるダメージ倍率。小さいほど水の抵抗に強くなります。")]
        [Min(0f)] public float waterDamageMultiplier = 0.7f;
        [Tooltip("乾いた紙を水へ入れた瞬間の追加ダメージ。小さいほど水への出し入れに強くなります。")]
        [Min(0f)] public float waterEntryMultiplier = 0.35f;
        [Tooltip("1回の水判定で受ける最大ダメージ。小さくすると急激な破損を抑えられます。")]
        [Min(0f)] public float maximumWaterDamagePerSample = 0.18f;
    }
}
