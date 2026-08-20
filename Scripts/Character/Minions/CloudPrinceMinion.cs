using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 云雾王子 (Cloud Prince) - 吉安娜专属随从。
/// 属性：攻击 4，生命 4。元素种族。
/// 战吼：选择1名敌人，你的状态栏中每有1种状态，则对其造成6点伤害。
/// </summary>
[RegisterMonster]
public sealed class CloudPrinceMinion : JainaMinionBase
{
    /// <summary>
    /// 战吼选择的目标（由随从卡 OnPlay 在召唤前静态传入；召唤完成后清除）
    /// </summary>
    public static MegaCrit.Sts2.Core.Entities.Creatures.Creature? BattlecryTargetOverride;

    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：云雾王子卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/cloud_prince.png";

    /// <summary>
    /// 战吼：选择1名敌人，你的状态栏中每有1种状态，则对其造成6点伤害
    /// （状态数 = 主人当前身上的 Power 数量；选择目标失效时回退随机一名存活敌人）。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 目标：战吼选择的目标（失效时回退随机一名存活敌人）
        var target = BattlecryTargetOverride;
        if (target == null || !target.IsAlive || !target.IsHittable || target.Side == Creature.Side)
        {
            target = combatState.GetOpponentsOf(owner.Creature)
                .Where(e => e != null && e.IsAlive && e.IsHittable)
                .FirstOrDefault();
        }
        if (target == null)
        {
            return;
        }

        // 状态栏中的状态数（主人身上的 Power 数量），每 1 种状态对目标造成 6 点伤害
        // （固定 6 点，不吃力量——ValueProp.Unpowered，与卡面 {Damage} 预览一致）
        int statusCount = owner.Creature.Powers.Count;
        for (int i = 0; i < statusCount; i++)
        {
            await CreatureCmd.Damage(choiceContext, [target], 6m, ValueProp.Unpowered, Creature, null, null);
        }
    }
}
