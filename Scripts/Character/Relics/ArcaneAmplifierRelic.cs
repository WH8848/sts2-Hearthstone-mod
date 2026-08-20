using System.Collections.Generic;
using System.Linq;
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
/// 奥术增幅体 (Arcane Amplifier) - 吉安娜普通遗物。
/// 你的英雄技能会额外造成2点伤害（每场战斗开始时给玩家挂
/// <see cref="ArcaneAmplifierPower"/>，Amount=2，与野火同机制；
/// 火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸/小精灵的祝福均读取）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class ArcaneAmplifierRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    /// <summary>
    /// 遗物图标：小图 85x85（程序绘制占位，卡图待 wiki 原画）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/arcane_amplifier_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/arcane_amplifier_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/arcane_amplifier_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 战斗开始：给玩家挂奥术增幅（英雄技能伤害 +2，幂等）
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }
        await ArcaneAmplifierPower.EnsureAppliedAsync(new ThrowingPlayerChoiceContext(), Owner);
    }
}
