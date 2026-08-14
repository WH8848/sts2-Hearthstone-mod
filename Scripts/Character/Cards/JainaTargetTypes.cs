using MegaCrit.Sts2.Core.Entities.Cards;
using MinionLib.Targeting;
using MinionLib.Targeting.Pets;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术的扩展目标类型。
/// </summary>
public static class JainaTargetTypes
{
    /// <summary>
    /// 任意活物（含施法者自己、己方随从、敌人）。
    /// 游戏原生 AnyEnemy 只匹配 Side==Enemy 的敌人，而吉安娜的随从是
    /// 玩家侧宠物（Side==Player, IsPet），本体也不属于任何敌人。
    /// 此类型 = 任意存活生物（MinionLib 的 AnyCreature 语义），
    /// 让火球术/寒冰箭等指向型法术可以把己方随从、甚至自己选为目标（炉石风味）。
    /// </summary>
    public static readonly TargetType AnyTargetable = CustomTargetTypeManager.Register(
        new AnyCreatureTargetType(),
        "jaina", nameof(AnyTargetable));
}
