using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 昔时古树 (Ancient of Yore) - 1费技能牌（初始，非法术牌）。
/// 获得 5 点格挡。
/// 升级后变为"古拉巴什贡品 (Gurubashi Offering)"：获得 8 点格挡（防御+）。
/// 初始卡：Basic 稀有度,不出现在战斗奖励掉落/发现池中。
/// 非法术牌：不挂"法术牌"关键词,不被视为法术。
/// 防御标签（CardTag.Defend）:视为"防御"卡（升级后为"防御+"）,与防御类效果联动。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 4, Order = 2)]
public sealed class AncientTreeCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 防御类卡牌标签（CardTag.Defend）——升级后仍保留（防御+）
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

    /// <summary>
    /// 无关键词（非法术牌：不作为法术牌参与减费/复制/随机释放池）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    /// <summary>
    /// 可升级 1 次（升级为古拉巴什贡品：格挡 5 -> 8）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 动态格挡变量（{Block} 预览实际数值；升级后 8）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：昔时古树官方原画 / 升级后（古拉巴什贡品）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded
            ? "res://assets/card_art/gurubashi_offering.png"
            : "res://assets/card_art/ancient_tree.png";

    public AncientTreeCard()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"古拉巴什贡品 (Gurubashi Offering)"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        await CreatureCmd.GainBlock(base.Owner.Creature, new BlockVar(base.DynamicVars.Block.BaseValue, ValueProp.Move), cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级为古拉巴什贡品：格挡 5 -> 8（UpgradeValueBy 设置 WasJustUpgraded,
        // 升级预览数值绿色高亮;BaseValue 随升级增长,卡面与结算自动跟随）
        base.DynamicVars.Block.UpgradeValueBy(3m);
    }
}
