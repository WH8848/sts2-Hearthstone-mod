using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// 能力牌：无特殊关键词（升级后获得保留）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded ? [CardKeyword.Retain] : [];

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

    protected override void OnUpgrade()
    {
        // 升级后获得保留（LocalKeywords 懒缓存可能已在未升级状态初始化）
        AddKeyword(CardKeyword.Retain);
    }
}
