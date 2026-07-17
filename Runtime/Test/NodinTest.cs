using System.Collections.Generic;
using UnityEngine;
using Nodin;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>用于测试条件显示的颜色枚举</summary>
public enum ColorOption { Red, Blue, Green }

/// <summary>
/// Nodin 所有标签效果测试脚本
/// 挂载到任意 GameObject 即可在 Inspector 中查看效果
/// </summary>
public class NodinTest : NodinMonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  1. LabelText — 自定义标签文本
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("标签 & 显示")]
    [LabelText("自定义名称")]
    public string labeledField = "这个字段用了 [LabelText]";

    [FoldoutGroup("标签 & 显示")]
    [HideLabel]
    public string hiddenLabelField = "这个字段隐藏了标签";

    [FoldoutGroup("标签 & 显示")]
    [LabelText("整数字段")]
    public int labeledInt = 42;

    [FoldoutGroup("标签 & 显示")]
    [LabelText("浮点数字段")]
    public float labeledFloat = 3.14f;

    // ══════════════════════════════════════════════════════════
    //  2. InfoBox — 信息提示框
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("信息提示")]
    [InfoBox("这是一条普通信息提示")]
    [LabelText("普通信息")]
    public string infoField = "";

    [FoldoutGroup("信息提示")]
    [InfoBox("这是一条警告提示", InfoMessageType.Warning)]
    [LabelText("警告信息")]
    public string warningField = "";

    [FoldoutGroup("信息提示")]
    [InfoBox("这是一条错误提示", InfoMessageType.Error)]
    [LabelText("错误信息")]
    public string errorField = "";

    [FoldoutGroup("信息提示")]
    [InfoBox("条件显示：当 showSecret 为 true 时才可见", InfoMessageType.Info, nameof(showSecret))]
    [LabelText("条件 InfoBox")]
    public string conditionalInfoField = "";

    // ══════════════════════════════════════════════════════════
    //  3. FoldoutGroup — 可折叠分组
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("折叠分组A", true)]
    [LabelText("字段 A1")]
    public string foldA1 = "分组 A（默认展开）";

    [FoldoutGroup("折叠分组A")]
    [LabelText("字段 A2")]
    public string foldA2 = "分组 A 的第二个字段";

    [FoldoutGroup("折叠分组B", false)]
    [LabelText("字段 B1")]
    public string foldB1 = "分组 B（默认折叠）";

    [FoldoutGroup("折叠分组B")]
    [LabelText("字段 B2")]
    public string foldB2 = "分组 B 的第二个字段";

    // 子分组测试（用 '/' 分隔）
    [FoldoutGroup("父分组/子分组1")]
    [LabelText("子字段 1")]
    public string subField1 = "父分组下的子分组1";

    [FoldoutGroup("父分组/子分组2")]
    [LabelText("子字段 2")]
    public string subField2 = "父分组下的子分组2";

    // ══════════════════════════════════════════════════════════
    //  4. BoxGroup — 带标题的盒子分组
    // ══════════════════════════════════════════════════════════
    [BoxGroup("盒子分组")]
    [LabelText("盒子字段 1")]
    public string boxField1 = "在盒子分组中";

    [BoxGroup("盒子分组")]
    [LabelText("盒子字段 2")]
    public int boxField2 = 100;

    // ══════════════════════════════════════════════════════════
    //  5. 条件显示 & 启用
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("条件显示")]
    [LabelText("显示秘密")]
    public bool showSecret = false;

    [FoldoutGroup("条件显示")]
    [ShowIf(nameof(showSecret))]
    [LabelText("秘密内容")]
    public string secretContent = "只有勾选「显示秘密」才能看到我";

    [FoldoutGroup("条件显示")]
    [HideIf(nameof(showSecret))]
    [LabelText("隐藏测试")]
    public string hideTestField = "当「显示秘密」勾选时我会隐藏";

    [FoldoutGroup("条件显示")]
    [LabelText("颜色选项")]
    public ColorOption colorOption = ColorOption.Red;

    [FoldoutGroup("条件显示")]
    [ShowIf(nameof(colorOption), ColorOption.Blue)]
    [LabelText("蓝色专属字段")]
    public string blueOnlyField = "只有选 Blue 时才显示";

    [FoldoutGroup("条件显示")]
    [ShowIf(nameof(colorOption), ColorOption.Green)]
    [LabelText("绿色专属字段")]
    public string greenOnlyField = "只有选 Green 时才显示";

    // ══════════════════════════════════════════════════════════
    //  6. EnableIf / DisableIf — 条件启用/禁用
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("条件启用")]
    [LabelText("锁定编辑")]
    public bool lockEditing = false;

    [FoldoutGroup("条件启用")]
    [EnableIf(nameof(lockEditing), false)]
    [LabelText("可编辑字段")]
    public string editableField = "未锁定时可编辑";

    [FoldoutGroup("条件启用")]
    [DisableIf(nameof(lockEditing))]
    [LabelText("禁用字段")]
    public string disabledField = "锁定后变灰不可编辑";

    // ══════════════════════════════════════════════════════════
    //  7. ReadOnly — 只读
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("只读字段")]
    [ReadOnly]
    [LabelText("只读文本")]
    public string readOnlyText = "这个字段不可编辑";

    [FoldoutGroup("只读字段")]
    [ReadOnly]
    [LabelText("只读数字")]
    public int readOnlyNumber = 999;

    // ══════════════════════════════════════════════════════════
    //  8. MultiLineProperty — 多行文本
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("多行文本")]
    [MultiLineProperty(5)]
    [LabelText("5行文本")]
    public string multiLineText = "这是第一行\n这是第二行\n这是第三行\n这是第四行\n这是第五行";

    [FoldoutGroup("多行文本")]
    [MultiLineProperty(3)]
    [LabelText("3行文本")]
    public string shortMultiLine = "简短的\n多行文本";

    // ══════════════════════════════════════════════════════════
    //  9. Button — 按钮
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("按钮测试")]
    [Button("普通按钮")]
    public void NormalButton()
    {
        Debug.Log("[NodinTest] 普通按钮被点击！");
    }

    [FoldoutGroup("按钮测试")]
    [Button("小按钮", ButtonSizes.Small)]
    public void SmallButton()
    {
        Debug.Log("[NodinTest] 小按钮被点击！");
    }

    [FoldoutGroup("按钮测试")]
    [Button("大按钮", ButtonSizes.Large)]
    public void LargeButton()
    {
        Debug.Log("[NodinTest] 大按钮被点击！");
    }

    [FoldoutGroup("按钮测试")]
    [Button("带颜色按钮")]
    [GUIColor(0.4f, 0.8f, 1f)]
    public void ColoredButton()
    {
        Debug.Log("[NodinTest] 带颜色按钮被点击！");
    }

    [FoldoutGroup("按钮测试")]
    [Button("危险按钮")]
    [GUIColor(1f, 0.3f, 0.3f)]
    public void DangerButton()
    {
        Debug.LogWarning("[NodinTest] 危险按钮被点击！");
    }

    // ══════════════════════════════════════════════════════════
    //  10. GUIColor — GUI 颜色
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("颜色测试")]
    [GUIColor(0f, 1f, 0f)]
    [LabelText("绿色字段")]
    public string greenField = "这个字段是绿色的";

    [FoldoutGroup("颜色测试")]
    [GUIColor("#FF6600")]
    [LabelText("橙色字段（Hex）")]
    public string orangeField = "这个字段是橙色的（Hex 颜色）";

    [FoldoutGroup("颜色测试")]
    [GUIColor(1f, 0f, 1f)]
    [LabelText("紫色字段")]
    public int purpleField = 777;

    // ══════════════════════════════════════════════════════════
    //  11. ShowInInspector — 强制显示非 public 字段
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("非 Public 字段")]
    [ShowInInspector]
    [LabelText("私有字段")]
    private string _privateField = "我是 private 字段，但被强制显示了";

    [FoldoutGroup("非 Public 字段")]
    [ShowInInspector]
    [LabelText("受保护字段")]
    protected int _protectedField = 42;

    // ══════════════════════════════════════════════════════════
    //  12. ValueDropdown — 下拉列表
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("下拉列表")]
    [ValueDropdown(nameof(GetWeaponOptions))]
    [LabelText("选择武器")]
    public string selectedWeapon = "剑";

    [FoldoutGroup("下拉列表")]
    [ValueDropdown(nameof(GetPotionOptions))]
    [LabelText("选择药水")]
    public string selectedPotion = "治疗药水";

    private string[] GetWeaponOptions() => new[] { "剑", "斧", "弓", "法杖", "匕首" };
    private string[] GetPotionOptions() => new[] { "治疗药水", "魔力药水", "力量药水", "速度药水" };

    // ══════════════════════════════════════════════════════════
    //  13. FolderPath — 文件夹路径选择器
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("路径选择")]
    [FolderPath]
    [LabelText("相对路径")]
    public string folderPath = "Assets/Game";

    [FoldoutGroup("路径选择")]
    [FolderPath(AbsolutePath = true)]
    [LabelText("绝对路径")]
    public string absolutePath = "";

    // ══════════════════════════════════════════════════════════
    //  14. OnValueChanged — 值改变回调
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("值改变回调")]
    [LabelText("有回调的字段")]
    [OnValueChanged(nameof(OnFieldChanged))]
    public string callbackField = "修改我会触发回调";

    private void OnFieldChanged()
    {
        Debug.Log($"[NodinTest] 字段值改变为: {callbackField}");
    }

    // ══════════════════════════════════════════════════════════
    //  15. ListDrawerSettings — List 绘制设置
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("列表设置")]
    [LabelText("带索引标签的列表")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<string> indexedList = new List<string> { "元素0", "元素1", "元素2" };

    [FoldoutGroup("列表设置")]
    [LabelText("不可拖拽的列表")]
    [ListDrawerSettings(DraggableItems = false)]
    public List<int> nonDraggableList = new List<int> { 10, 20, 30 };

    [FoldoutGroup("列表设置")]
    [LabelText("隐藏添加按钮")]
    [ListDrawerSettings(HideAddButton = true)]
    public List<float> noAddList = new List<float> { 1.1f, 2.2f, 3.3f };

    // ══════════════════════════════════════════════════════════
    //  16. DictionaryDrawerSettings — Dictionary 绘制设置
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("字典设置")]
    [LabelText("自定义键值标签")]
    [DictionaryDrawerSettings(KeyLabel = "角色名", ValueLabel = "等级")]
    public Dictionary<string, int> characterLevels = new Dictionary<string, int>
    {
        { "战士", 10 },
        { "法师", 8 },
        { "盗贼", 12 }
    };

    [FoldoutGroup("字典设置")]
    [LabelText("武器伤害表")]
    [DictionaryDrawerSettings(KeyLabel = "武器", ValueLabel = "伤害")]
    public Dictionary<string, float> weaponDamage = new Dictionary<string, float>
    {
        { "铁剑", 15f },
        { "钢斧", 22f },
        { "长弓", 18f }
    };

    // ══════════════════════════════════════════════════════════
    //  17. OnInspectorGUI — 自定义 GUI 绘制
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("自定义 GUI")]
    [LabelText("玩家名称")]
    public string playerName = "勇者";

    [FoldoutGroup("自定义 GUI")]
    [OnInspectorGUI(nameof(DrawCustomSection))]
    public string _customGuiPlaceholder; // 占位字段

#if UNITY_EDITOR
    private void DrawCustomSection()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("═══ 自定义绘制区域 ═══", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("这是通过 [OnInspectorGUI] 自定义绘制的内容", MessageType.Info);
        EditorGUILayout.LabelField($"玩家: {playerName}");
        EditorGUILayout.Space(5);
    }
#endif

    // ══════════════════════════════════════════════════════════
    //  18. AssetsOnly — 仅允许 Asset
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("资产限制")]
    [AssetsOnly]
    [LabelText("仅 Asset 引用")]
    public Object assetsOnlyField;

    // ══════════════════════════════════════════════════════════
    //  混合使用测试
    // ══════════════════════════════════════════════════════════
    [FoldoutGroup("混合测试")]
    [LabelText("带 InfoBox 的字段")]
    [InfoBox("这个字段同时使用了多个标签")]
    [GUIColor(0.8f, 1f, 0.8f)]
    [ReadOnly]
    public string mixedField = "多标签混合使用";

    [FoldoutGroup("混合测试")]
    [LabelText("条件 + 颜色")]
    [ShowIf(nameof(showSecret))]
    [GUIColor("#FFD700")]
    public string conditionalColoredField = "条件显示 + 金色";
}
