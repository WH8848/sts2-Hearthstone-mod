using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 大法师安东尼达斯光环：每当你施放一个攻击牌或技能牌，将一张"火球术"攻击牌置入你的手牌。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除，被动自动失效。
/// </summary>
[RegisterPower]
public sealed class AntonidasPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner?.PetOwner;
        if (owner == null || cardPlay.Card.Owner != owner)
        {
            return;
        }
        // 英雄技能（火焰冲击等）不是法术/攻击牌意义上的"施放"，不触发
        if (cardPlay.Card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower))
        {
            return;
        }
        // 不响应"召唤出安东尼达斯的这张卡"的施放事件
        // （如火焰之地传送门召唤安东尼达斯时，这张传送门本身不触发其效果——炉石：随从进场后才开始计算）
        if (Owner?.Monster is jaina.Scripts.Character.Minions.AntonidasMinion ant &&
            ant.SummonSourceCard != null && ReferenceEquals(cardPlay.Card, ant.SummonSourceCard))
        {
            return;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return;
        }
        // MutableClone 的卡无 Owner，AddGeneratedCardToCombat 内部会 NRE；用 CreateCard 生成带 Owner 的实例。
        // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义，牌不消失不消耗）。
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(Fireball)));
        if (canonical == null)
        {
            return;
        }
        var combatState = owner.Creature.CombatState;
        var fireball = combatState.CreateCard(canonical, owner);
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(fireball);
        await CardPileCmd.AddGeneratedCardToCombat(fireball, PileType.Hand, owner);
    }
}
