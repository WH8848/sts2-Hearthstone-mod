using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 狂热者 (Zealot) 随从卡 - 炉石传说风格。
/// 1费召唤一个 3/4 的狂热者，冲锋：召唤后可立即攻击。
/// 衍生卡（由奥术智慧升级后召唤）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class ZealotCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(Zealot);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    /// <summary>
    /// 冲锋：召唤后可立即攻击（狂热者的 OnSummon 已实现召唤即攻击）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        JainaKeywords.Charge
    ];

    public ZealotCard()
        : base(1, CardRarity.Token)
    {
    }
}
