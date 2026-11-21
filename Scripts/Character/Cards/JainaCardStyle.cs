using STS2RitsuLib;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 读取"先古卡样式"开关（RitsuLib 设置页里注册的 ancient_card_style）。
/// 证据：RitsuLibFramework.GetDataStore(modId).Get&lt;TModel&gt;(dataKey)
/// （STS2-RitsuLib/src/Settings/ModSettings/Binding/ModSettingsValueBindings.cs:84-88），
/// CardVisualStyle.Ancient 会切换卡面到先古布局（ContentAssetProfiles.cs:204）。
/// </summary>
public static class JainaCardStyle
{
    /// <summary>先古卡样式是否开启（读取失败时降级为关闭，不影响出牌）。</summary>
    public static bool Ancient
    {
        get
        {
            try
            {
                return RitsuLibFramework.GetDataStore(Entry.ModId)
                    .Get<JainaSettings>("settings")
                    .AncientCardStyle;
            }
            catch
            {
                return false;
            }
        }
    }
}
