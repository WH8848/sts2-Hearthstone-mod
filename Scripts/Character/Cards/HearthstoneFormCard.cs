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
/// 你的全部卡牌获得保留和消耗；格挡不再在你的回合开始时消失；
/// 当你抽到状态牌时额外抽一张卡牌；下回合开始每回合获得十点能量，
/// 抽五张牌变为抽一张牌；手牌上限之后抽到的牌会被消耗；
/// 抽牌堆和弃牌堆无牌可抽时进入疲劳。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class HearthstoneFormCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 关键词：疲劳（mod 关键词不渲染到描述，仅悬停注释）+ 虚无（真实虚无：
    /// 回合结束留在手牌则消耗；升级后移除虚无）。
    /// 保留/消耗不挂 Keywords（原版关键词会渲染到描述顶部/底部）——
    /// 保留/消耗写在本卡描述文本中；其悬停注释由
    /// <see cref="AdditionalHoverTips"/> 提供（HoverTipFactory.FromKeyword）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Fatigue, CardKeyword.Ethereal];

    /// <summary>
    /// 升级：移除真实虚无（升级后本卡不再消耗）
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }

    /// <summary>
    /// 悬停提示（关键词注释，不产生渲染行）：
    /// 未升级 = 虚无 + 保留 + 消耗；升级后移除虚无注释。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            if (!IsUpgraded)
            {
                yield return HoverTipFactory.FromKeyword(CardKeyword.Ethereal);
            }
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

        // 挂炉石形态光环（全卡保留+消耗 / 格挡不消失 / 10 能量 / 限抽 1 张 / 状态卡补抽 / 烧牌 / 疲劳）
        await PowerCmd.Apply<HearthstoneFormPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
