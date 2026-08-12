using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 小精灵 (Imp) 随从卡 - 0费召唤一个 1/1 的小精灵。
/// 衍生卡：由灵体采集者/灌注生成，不进入掉落池。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ImpCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(ImpMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 1;

    /// <summary>
    /// 小精灵无亡语无冲锋
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    public ImpCard()
        : base(0, CardRarity.Token)
    {
    }
}
