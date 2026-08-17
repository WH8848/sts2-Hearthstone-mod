using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 炉石形态 (Hearthstone Form) - 3费能力牌（稀有）。
/// 你的全部卡牌获得保留和消耗；当你抽到状态卡时额外抽一张。
/// 此后每回合你获得十点能量，每回合只能抽一张卡。
/// 升级后：额外获得保留（本卡保留）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class HearthstoneFormCard : ModCardTemplate
{
    /// <summary>
    /// 关键词（卡面右侧显示注释）：未升级 = 虚无 + 保留 + 消耗 + 疲劳；
    /// 升级后移除虚无，右侧 = 保留 + 消耗 + 疲劳。
    /// 疲劳行为由 <see cref="HearthstoneFormPower"/> 实现，此处仅为关键词注释展示。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [CardKeyword.Retain, CardKeyword.Exhaust,
           jaina.Scripts.Character.Keywords.JainaKeywords.Fatigue]
        : [CardKeyword.Ethereal, CardKeyword.Retain, CardKeyword.Exhaust,
           jaina.Scripts.Character.Keywords.JainaKeywords.Fatigue];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/hearthstone_form.png";

    public HearthstoneFormCard()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 卡名不变
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂炉石形态光环（每回合 10 能量 / 限抽 1 张 / 状态卡补抽 / 全卡保留+消耗）
        await PowerCmd.Apply<HearthstoneFormPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }

    /// <summary>
    /// 升级：移除虚无（LocalKeywords 懒初始化只算一次，升级形态 Keywords
    /// 缓存自基础状态——需显式移除 Ethereal，否则升级后卡面仍显示"虚无"）。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}

/// <summary>
/// 炉石形态卡面渲染修正：关键词的"保留"（beforeDescription 顶部）与"消耗"（afterDescription 底部）
/// 会在描述顶部/底部额外渲染一行——移除这两个独立关键词行（描述文本中的"保留/消耗"长句不受影响）。
/// 右侧关键词注释（悬停提示）仍由 Keywords 提供，不受影响。
/// </summary>
[HarmonyPatch]
public static class HearthstoneFormKeywordRenderPatch
{
    private static MethodBase TargetMethod()
    {
        // DescriptionPreviewType 是 CardModel 的私有嵌套类型，需反射获取
        var previewType = typeof(MegaCrit.Sts2.Core.Models.CardModel)
            .GetNestedType("DescriptionPreviewType", BindingFlags.NonPublic | BindingFlags.Public);
        return typeof(MegaCrit.Sts2.Core.Models.CardModel).GetMethod("GetDescriptionForPile",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new[]
            {
                typeof(MegaCrit.Sts2.Core.Entities.Cards.PileType),
                previewType,
                typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature)
            },
            null);
    }

    private static void Postfix(MegaCrit.Sts2.Core.Models.CardModel __instance, ref string __result)
    {
        if (__instance is not HearthstoneFormCard)
        {
            return;
        }
        // 移除顶部"保留"与底部"消耗"的独立关键词行（金色词条或纯文本均可匹配）
        var kept = new List<string>();
        foreach (var line in __result.Split('\n'))
        {
            string clean = line.Trim()
                .Replace("[gold]", string.Empty)
                .Replace("[/gold]", string.Empty);
            if (clean == "保留" || clean == "消耗" ||
                clean == "Retain" || clean == "Exhaust")
            {
                continue;
            }
            kept.Add(line);
        }
        __result = string.Join('\n', kept);
    }
}
