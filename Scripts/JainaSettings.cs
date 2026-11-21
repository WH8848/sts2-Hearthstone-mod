namespace jaina.Scripts;

/// <summary>
/// 吉安娜模组的玩家设置，持久化在 RitsuLib 数据存储 "jaina" / "settings"（SaveScope.Global）。
/// 证据：RitsuLib ModSettingsValueBinding&lt;TModel,TValue&gt; 通过
/// RitsuLibFramework.GetDataStore(modId).Get&lt;TModel&gt;(dataKey) 读写
/// （STS2-RitsuLib/src/Settings/ModSettings/Binding/ModSettingsValueBindings.cs:84-88）。
/// </summary>
public sealed class JainaSettings
{
    /// <summary>
    /// 先古卡样式：开启后所有吉安娜卡牌使用游戏先古卡布局（卡图区域为 250x351 竖版）。
    /// 对应 RitsuLib CardVisualStyle.Ancient（ContentAssetProfiles.cs:204）。
    /// </summary>
    public bool AncientCardStyle { get; set; }
}
