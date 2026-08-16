using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 夜隐者圣所 (Nightcloak Sanctum) - 吉安娜地标。
/// 使用效果：给予一名角色 1 层冻结，召唤一个 2/2 的不稳定的骷髅。
/// 耐久度 3（每次使用 -1，归零时地标被摧毁）。
/// </summary>
[RegisterMonster]
public sealed class NightcloakSanctumLandmark : JainaLandmarkBase
{
    /// <summary>
    /// 战斗视觉：夜隐者圣所卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/nightcloak_sanctum.png";

    /// <summary>
    /// 耐久度 3
    /// </summary>
    public override int LandmarkDurability => 3;

    /// <summary>
    /// 使用效果：给予目标角色 1 层冻结，并召唤一个 2/2 的不稳定的骷髅。
    /// </summary>
    public override async Task OnLandmarkEffect(PlayerChoiceContext choiceContext, Creature target)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }
        var applier = Creature.PetOwner?.Creature ?? Creature;

        // 给予目标 1 层冻结
        await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, applier, null);

        // 召唤一个 2/2 的不稳定的骷髅（我方场上）
        var petOwner = Creature.PetOwner;
        if (petOwner != null)
        {
            await JainaMinionPool.SummonMinionByType(
                choiceContext, petOwner, typeof(VolatileSkeleton), maxHp: 2, attack: 2);
        }
    }
}
