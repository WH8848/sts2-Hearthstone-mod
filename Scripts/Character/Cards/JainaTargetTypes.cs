using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using MinionLib.Action;
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

    /// <summary>
    /// 自己场上的一个小精灵（ImpMinion）——巫卜（非公平游戏升级形态）
    /// "消灭1个小精灵，抽3张牌"的代价目标：只有场上存在自己的小精灵时才能打出。
    /// 联机：手打选择/随机释放补全统一走 MinionLib 的自定义目标类型链路
    /// （CustomTargetTypeCardPatch 的 CardModel.IsValidTarget 路由），两端确定性。
    /// </summary>
    public static readonly TargetType AnyOwnImp = CustomTargetTypeManager.Register(
        new OwnImpTargetType(),
        "jaina", nameof(AnyOwnImp));

    /// <summary>
    /// "自己的小精灵"目标类型：仅玩家自己的存活小精灵（ImpMinion）可选。
    /// </summary>
    private sealed class OwnImpTargetType : ICustomTargetType
    {
        public bool IsSingleTarget => true;

        public bool IsValidTargetPreview(Creature target)
        {
            return IsValidTarget(target) && LocalContext.IsMe(target.PetOwner);
        }

        public bool IsValidTarget(CardModel card, Creature target)
        {
            return IsValidTarget(target) && target.PetOwner == card.Owner;
        }

        public bool IsValidTarget(PotionModel potion, Creature target)
        {
            return IsValidTarget(target) && target.PetOwner == potion.Owner;
        }

        public bool IsValidTarget(ActionModel action, Creature target)
        {
            return IsValidTarget(target) &&
                   (target.PetOwner == action.Owner.PetOwner || target.PetOwner == action.Owner.Player);
        }

        private static bool IsValidTarget(Creature target)
        {
            return target is { IsAlive: true, Side: CombatSide.Player, IsPet: true, Monster: ImpMinion };
        }
    }
}
