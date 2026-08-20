using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 奥术爆裂 (Arcane Burst) - 0费攻击牌（衍生，英雄技能，奥术派系）。
/// 造成 2 点伤害，每次打出获得 +2 伤害（本局对战内递增）。
/// 由魔导师晨拥替换英雄技能后，每回合开始自动加入手牌（英雄技能卡不占手牌位）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class ArcaneBurstCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 英雄技能关键词（悬停解释；不挂法术/派系关键词——英雄技能不算法术牌，
    /// 不触发法术相关效果，与火焰冲击一致）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower];

    /// <summary>
    /// 动态伤害显示：当前伤害 = 2 + 本局已打出次数×2 + 野火加成
    /// （与 OnPlay 实际结算一致，按玩家区分；非战斗中显示基础 2 点）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ComputedDamageVar(2m, card =>
        {
            // canonical（图鉴渲染等）不可变：访问 Owner 会抛异常，直接返回基础值
            if (card is not ArcaneBurstCard arcane || !card.IsMutable ||
                card.Owner?.Creature?.CombatState == null)
            {
                return 2m;
            }
            var combatState = card.Owner.Creature.CombatState;
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            rec.ArcaneBurstCastsByPlayer.TryGetValue(card.Owner.NetId, out var casts);
            var wildfire = card.Owner.Creature.GetPower<WildfirePower>();
            var amplifier = card.Owner.Creature.GetPower<ArcaneAmplifierPower>();
            return 2 + casts * 2 + (wildfire?.WildfireStacks ?? 0) + (amplifier?.AmplifierBonus ?? 0);
        })
    ];

    public override string CustomPortraitPath => "res://assets/card_art/arcane_burst.png";

    /// <summary>
    /// 英雄技能卡不可升级（伤害递增由本局打出次数 ArcaneBurstCasts 驱动，与升级无关）
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    public ArcaneBurstCard()
        : base(0, CardType.Attack, CardRarity.Token, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 卡名不变
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
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
        // 每次打出获得 +2 伤害（本局内递增，第 1 次 2 点、第 2 次 4 点……，按玩家区分）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.ArcaneBurstCastsByPlayer.TryGetValue(base.Owner.NetId, out var burstCasts);
        rec.ArcaneBurstCastsByPlayer[base.Owner.NetId] = burstCasts + 1;

        // 野火：英雄技能伤害永久加成（可叠加，本局对战）；奥术增幅：英雄技能额外伤害
        var wildfire = base.Owner.Creature.GetPower<WildfirePower>();
        var wildfireStacks = wildfire?.WildfireStacks ?? 0;
        var amplifier = base.Owner.Creature.GetPower<ArcaneAmplifierPower>();
        var amplifierBonus = amplifier?.AmplifierBonus ?? 0;
        var totalDamage = 2 + burstCasts * 2 + wildfireStacks + amplifierBonus;

        if (cardPlay.Target is { IsAlive: true } target)
        {
            await DamageCmd.Attack(totalDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);

            // 记录英雄技能伤害（火眼莫德雷斯战吼条件用）
            jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(this, totalDamage);
        }
    }

    /// <summary>
    /// 每回合开始自动加入手牌（仅当已被英雄卡替换为当前英雄技能时）。
    /// 英雄技能卡不占手牌位：满手也直接入手。
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != base.Owner)
        {
            return;
        }
        // 仅当魔导师晨拥已替换英雄技能为奥术爆裂时入手（按玩家区分）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        if (!rec.CurrentHeroPowerTypeByPlayer.TryGetValue(player.NetId, out var heroPowerType) ||
            heroPowerType != typeof(ArcaneBurstCard))
        {
            return;
        }
        CardPile? pile = base.Pile;
        if (pile == null || pile.Type != PileType.Hand)
        {
            await CardPileCmd.Add(this, PileType.Hand);
        }
    }
}
