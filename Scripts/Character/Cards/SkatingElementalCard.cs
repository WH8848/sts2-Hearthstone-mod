using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 滑冰元素 (Sleet Skater) - 1费随从卡（普通）。
/// 微缩，战吼：选择1名敌人，给予其1层冻结，获得等同于其减少的总体伤害的格挡。属性 3/4。
/// 微型复制品（0费1/1）由微缩系统自动生成，保留全部文字效果。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SkatingElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"滑冰元素"（Sleet Skater, WBW_103348）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/sleet_skater.png";

    /// <summary>
    /// 微缩（打出后生成0费1/1微型复制品）+ 战吼（悬停解释）+ 冻结（悬停注释,战吼给予冻结）+
    /// 消耗（随从卡打出后消耗,模板默认;微型复制品生成时自动去除消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Miniaturize, JainaKeywords.Battlecry, JainaKeywords.Freeze, CardKeyword.Exhaust];

    /// <summary>
    /// 悬停提示：显示微缩生成的衍生物「滑冰元素（微型）」卡面
    /// （与实战中微缩生成的复制品一致：0 费 1/1、带「微型」、去掉「微缩」与「消耗」、保留全部文字效果）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            var mini = MiniDerivative;
            if (mini != null)
            {
                yield return new CardHoverTip(mini);
            }
        }
    }

    /// <summary>
    /// 微型衍生物卡（仅用于悬停展示，懒加载一次后复用）
    /// </summary>
    private static SkatingElementalCard? _miniDerivative;

    private static SkatingElementalCard? MiniDerivative
    {
        get
        {
            if (_miniDerivative != null)
            {
                return _miniDerivative;
            }
            try
            {
                var canonical = MegaCrit.Sts2.Core.Models.ModelDb.Card<SkatingElementalCard>();
                var mini = (SkatingElementalCard)canonical.ToMutable();
                mini.AddKeyword(JainaKeywords.Mini);
                mini.RemoveKeyword(JainaKeywords.Miniaturize);
                mini.RemoveKeyword(CardKeyword.Exhaust);
                mini.EnergyCost.SetCustomBaseCost(0);
                mini.SetOverrideStats(1, 1);
                _miniDerivative = mini;
                return mini;
            }
            catch (System.Exception ex)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[Jaina] build mini skating derivative failed: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 战吼选择 1 名敌人（打出时进入目标选择）
    /// </summary>
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override Type MinionType => typeof(SkatingElementalMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    public SkatingElementalCard()
        : base(1, CardRarity.Common)
    {
    }

    /// <summary>
    /// 打出：把战吼选择的目标传给随从（召唤时随从触发战吼读取；
    /// 静态传递，召唤完成后清除）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SkatingElementalMinion.BattlecryTargetOverride = cardPlay.Target;
        try
        {
            await base.OnPlay(choiceContext, cardPlay);
        }
        finally
        {
            SkatingElementalMinion.BattlecryTargetOverride = null;
        }
    }
}
