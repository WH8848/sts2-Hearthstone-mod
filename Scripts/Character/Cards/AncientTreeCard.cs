using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 昔时古树 (Ancient Tree) - 1费技能牌（初始，非法术牌）。
/// 获得 5 点格挡。
/// 初始卡：Basic 稀有度,不出现在战斗奖励掉落/发现池中。
/// 非法术牌：不挂"法术牌"关键词(与火焰冲击/戏法图腾同口径),不被视为法术。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 1)]
public sealed class AncientTreeCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 初始技能牌不可升级
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 无关键词（非法术牌：不作为法术牌参与减费/复制/随机释放池）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    /// <summary>
    /// 动态格挡变量（{Block} 预览实际数值）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move)];

    public override string CustomPortraitPath => "res://assets/card_art/ice_barrier.png";

    public AncientTreeCard()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        await CreatureCmd.GainBlock(base.Owner.Creature, new BlockVar(5m, ValueProp.Move), cardPlay);
    }
}
