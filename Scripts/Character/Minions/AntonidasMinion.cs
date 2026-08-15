using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 大法师安东尼达斯 (Archmage Antonidas) - 吉安娜专属随从。
/// 属性：攻击 5，生命 7。每当你施放一个攻击牌或技能牌，将一张"火球术"攻击牌置入你的手牌。
/// 光环效果：挂在随从自身，随从死亡后被动失效。
/// </summary>
[RegisterMonster]
public sealed class AntonidasMinion : JainaMinionBase
{
    /// <summary>
    /// 召唤来源卡（打出随从卡召唤时为随从卡实例；随机召唤等由召唤方显式设置）。
    /// 安东尼达斯的光环不响应"召唤出它的这张卡"的施放事件（炉石：随从进场后才开始计算）。
    /// </summary>
    public MegaCrit.Sts2.Core.Models.CardModel? SummonSourceCard { get; private set; }

    /// <summary>
    /// 设置召唤来源卡（随机召唤类效果召唤安东尼达斯后由召唤方调用，
    /// 使该效果卡不触发安东尼达斯光环）
    /// </summary>
    public void SetSummonSourceCard(MegaCrit.Sts2.Core.Models.CardModel sourceCard)
    {
        SummonSourceCard = sourceCard;
    }

    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 7;

    public override int MaxInitialHp => 7;

    protected override string MinionVisualsPath => "res://assets/card_art/archmage_antonidas.png";

    /// <summary>
    /// 召唤时挂上送火球术光环
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        SummonSourceCard = options.Source as MegaCrit.Sts2.Core.Models.CardModel;
        await PowerCmd.Apply<AntonidasPower>(choiceContext, [Creature], 1m, Creature, options.Source);
    }
}
