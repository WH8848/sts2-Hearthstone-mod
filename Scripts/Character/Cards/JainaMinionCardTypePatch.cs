using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 随从卡类型标签补丁。
/// 游戏 CardType 为封闭枚举（Attack/Skill/Power/...），没有"随从"类型，
/// 通过 Patch NCard.UpdateTypePlaque 让吉安娜随从卡的类型标签显示"随从"而非"技能"。
/// 文本来自 gameplay_ui 表的 CARD_TYPE.MINION（mod 本地化合并）。
/// </summary>
[HarmonyPatch(typeof(NCard), "UpdateTypePlaque")]
public static class JainaMinionCardTypePatch
{
    private static readonly LocString MinionTypeText = new("gameplay_ui", "CARD_TYPE.MINION");

    public static void Postfix(NCard __instance, MegaLabel ___typeLabel)
    {
        if (__instance.Model is JainaMinionCardTemplate)
        {
            ___typeLabel.SetTextAutoSize(MinionTypeText.GetFormattedText());
        }
    }
}
