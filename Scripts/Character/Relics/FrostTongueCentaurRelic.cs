using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 霜舌半人马 (Frost Tongue Centaur) - 吉安娜稀有遗物。
/// 在你施放一个冰霜法术后，召唤一个1/1的霜冻元素。
/// 战斗开始由 <see cref="FrostTongueCentaurPower"/> 每回合驱动（幂等挂载）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class FrostTongueCentaurRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 遗物图标：小图 85x85（程序绘制占位，卡图待 wiki 原画）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/frost_tongue_centaur_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/frost_tongue_centaur_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/frost_tongue_centaur_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 战斗开始：给玩家挂霜舌半人马（施放冰霜法术后召唤 1/1 霜冻元素，幂等）
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }
        await FrostTongueCentaurPower.EnsureAppliedAsync(new ThrowingPlayerChoiceContext(), Owner);
    }
}
