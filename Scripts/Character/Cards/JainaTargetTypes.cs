using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using MinionLib.Action;
using MinionLib.Targeting;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术的扩展目标类型。
/// </summary>
public static class JainaTargetTypes
{
    /// <summary>
    /// 任意活物（含施法者自己、己方随从、敌人）。
    /// <b>多人规则</b>：手打的卡牌不可以攻击<b>队友</b>与<b>队友随从</b>
    /// （目标选择过滤——只能选自己/自己随从/敌人/敌方随从）；
    /// <b>随机打出</b>（随机释放：Yogg/惊奇卡牌/戏法图腾/重放等 AutoPlay）保持全范围——
    /// 随机打出的卡可以攻击队友与队友随从。
    /// 游戏原生 AnyEnemy 只匹配 Side==Enemy 的敌人，而吉安娜的随从是
    /// 玩家侧宠物（Side==Player, IsPet），本体也不属于任何敌人。
    /// 此类型 = 任意存活生物（MinionLib 的 AnyCreature 语义），
    /// 让火球术/寒冰箭等指向型法术可以把己方随从、甚至自己选为目标（炉石风味）。
    /// </summary>
    public static readonly TargetType AnyTargetable = CustomTargetTypeManager.Register(
        new AnyTargetableType(),
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
    /// 任意活物目标类型（火球术/寒冰箭/冰枪术/埃匹希斯冲击/火焰冲击/熔岩镜像等）：
    /// 手打时排除<b>队友</b>（其它玩家）的角色与其随从；随机释放（AutoPlay）全范围。
    /// </summary>
    private sealed class AnyTargetableType : ICustomTargetType
    {
        public bool IsSingleTarget => true;

        /// <summary>
        /// 手打目标选择 UI 预览（本地玩家操作）：队友（其它玩家的角色/随从）不高亮。
        /// </summary>
        public bool IsValidTargetPreview(Creature target)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }
            if (target.IsPlayer)
            {
                return LocalContext.IsMe(target.Player);
            }
            if (target.PetOwner != null)
            {
                return LocalContext.IsMe(target.PetOwner);
            }
            // 敌方生物（怪物/敌方随从）：总是可选
            return true;
        }

        /// <summary>
        /// 手打校验（手打目标选择与 TryPlayCard 都会调用）：
        /// 非随机释放流程中，队友角色与其随从不是合法目标（用户多人规则）；
        /// 随机释放（AutoPlay：AutoPlayGuard.IsInAutoPlay 调用栈检测，两端确定性）保持全范围。
        /// </summary>
        public bool IsValidTarget(CardModel card, Creature target)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                if (target.IsPlayer)
                {
                    return target.Player == card.Owner;
                }
                if (target.PetOwner != null)
                {
                    return target.PetOwner == card.Owner;
                }
            }
            return true;
        }

        public bool IsValidTarget(PotionModel potion, Creature target)
        {
            return IsValidTarget(target);
        }

        public bool IsValidTarget(ActionModel action, Creature target)
        {
            return IsValidTarget(target);
        }

        private static bool IsValidTarget(Creature target)
        {
            return target is { IsAlive: true };
        }
    }

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
