using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
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
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class HearthstoneFormCard : ModCardTemplate
{
    /// <summary>
    /// 关键词（渲染行）：未升级 = 虚无 + 疲劳；升级后移除虚无，只留疲劳。
    /// 本卡不挂 Retain/Exhaust 关键词（挂上会被游戏渲染到描述顶部/底部）——
    /// "保留/消耗"的悬停注释由 <see cref="AdditionalHoverTips"/> 提供（HoverTipFactory.FromKeyword），
    /// 只显示在悬停提示里，不产生渲染行。
    /// 保留与消耗效果由 <see cref="HearthstoneFormPower"/> 赋予全部卡牌。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Fatigue]
        : [CardKeyword.Ethereal,
           jaina.Scripts.Character.Keywords.JainaKeywords.Fatigue];

    /// <summary>
    /// 悬停提示：保留 / 消耗 关键词注释（不挂在 Keywords 上，避免渲染行；
    /// HoverTipFactory.FromKeyword 直接提供关键词悬停提示）。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromKeyword(CardKeyword.Retain);
            yield return HoverTipFactory.FromKeyword(CardKeyword.Exhaust);
        }
    }

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
