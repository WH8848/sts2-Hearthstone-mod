using MegaCrit.Sts2.Core.Entities.Cards;
using MinionLib.Targeting;
using MinionLib.Targeting.Pets;
using MinionLib.Targeting.Utilities;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术的扩展目标类型。
/// </summary>
public static class JainaTargetTypes
{
    /// <summary>
    /// 敌人 + 己方随从（不含施法者本体）。
    /// 游戏原生 AnyEnemy 只匹配 Side==Enemy 的敌人，而吉安娜的随从是
    /// 玩家侧宠物（Side==Player, IsPet），所以原生目标类型选不中自己的随从。
    /// 此类型 = 所有敌人（AnyEnemy）∪ 己方随从（AnyMinion，即 Side==Player、
    /// IsPet、MinionModel 且 PetOwner==施法者），让火球术/寒冰箭等指向型法术
    /// 可以把己方随从选为目标（炉石风味），同时不会把施法者本体纳入目标。
    /// </summary>
    public static readonly TargetType EnemyOrOwnMinion = CustomTargetTypeManager.Register(
        new UnionTargetType(
            BuiltInTargetType.From(TargetType.AnyEnemy),
            new AnyMinionTargetType()),
        "jaina", nameof(EnemyOrOwnMinion));
}
