using System.ComponentModel.Design;

namespace CommandBars.Design;

/// <summary>
/// Collection editor for a bar's <see cref="ItemDefinition"/> list. The Add
/// button offers a dropdown of item kinds (button, toggle, split, popup,
/// separator, label, combo), matching the feel of the ToolStrip items editor.
/// </summary>
public sealed class ItemDefinitionCollectionEditor : CollectionEditor
{
    public ItemDefinitionCollectionEditor(Type type) : base(type)
    {
    }

    /// <summary>The concrete kinds offered by the Add dropdown.</summary>
    protected override Type[] CreateNewItemTypes() => new[]
    {
        typeof(ButtonDefinition),
        typeof(ToggleButtonDefinition),
        typeof(SplitButtonDefinition),
        typeof(PopupDefinition),
        typeof(SeparatorDefinition),
        typeof(LabelDefinition),
        typeof(ComboBoxDefinition),
    };

    /// <summary>Base type for the editor's element handling.</summary>
    protected override Type CreateCollectionItemType() => typeof(ItemDefinition);
}

/// <summary>
/// Collection editor for the manager's <see cref="BarDefinition"/> list. The Add
/// button offers "Toolbar" and "Menu Bar".
/// </summary>
public sealed class BarDefinitionCollectionEditor : CollectionEditor
{
    public BarDefinitionCollectionEditor(Type type) : base(type)
    {
    }

    protected override Type[] CreateNewItemTypes() => new[]
    {
        typeof(ToolbarDefinition),
        typeof(MenuBarDefinition),
    };

    protected override Type CreateCollectionItemType() => typeof(BarDefinition);
}
