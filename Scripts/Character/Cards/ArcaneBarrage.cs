using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 灯光表演 (Lightshow) - 0费攻击牌（普通，奥术派系）。
/// 对随机敌人造成 N 次 2 点伤害（N = 2 + 本局已施放次数 + 升级次数）。
/// 每次释放攻击次数 +1（本局内，按玩家区分）；每次升级攻击次数 +1；可无限升级。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneBarrage : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级（每次升级攻击次数 +1）
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Beams", 2)
    ];

    public override string CustomPortraitPath => "res://assets/card_art/lightshow.png";

    public ArcaneBarrage()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 光束数 = 2（基础）+ 本局已施放的灯光表演次数（每次释放 +1，按玩家区分）+ 升级次数（每次升级 +1）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.LightshowCastsByPlayer.TryGetValue(base.Owner.NetId, out var casts);
        int beams = 2 + casts + CurrentUpgradeLevel;

        // 对随机敌人造成 beams 次 2 点伤害（每次随机选一个存活可命中敌人）
        for (int i = 0; i < beams; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var randomEnemy = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (randomEnemy == null)
            {
                break;
            }
            // Move 标记：每段伤害都触发振翅（Flutter）层数减少（IsPoweredAttack）；
            // 传 cardSource/cardPlay（蜷身等依赖 cardSource 的敌方 Power 才能触发）
            await CreatureCmd.Damage(choiceContext, [randomEnemy], 2m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }

        // 施放本张后计数 +1（供下一次灯光表演递增，按玩家区分）
        rec.LightshowCastsByPlayer[base.Owner.NetId] = casts + 1;
    }

    protected override void OnUpgrade()
    {
        // 每次升级攻击次数 +1（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮）
        base.DynamicVars["Beams"].UpgradeValueBy(1);
    }
}
