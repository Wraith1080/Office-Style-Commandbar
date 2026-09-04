namespace CommandBars.Designer.Protocol.Endpoints;

/// <summary>Stable endpoint names shared by the client senders and server handlers.</summary>
public static class EndpointNames
{
    public const string GetBarDefinitions = nameof(GetBarDefinitions);
    public const string SetBarDefinitions = nameof(SetBarDefinitions);
    public const string GetDockHostDesignContext = nameof(GetDockHostDesignContext);
    public const string SetDockHostDefinitions = nameof(SetDockHostDefinitions);
    public const string AddStockIcons = nameof(AddStockIcons);
}

/// <summary>Editor names used by [Editor("name", ...)] on the runtime properties and
/// by the client's TypeRoutingProvider.</summary>
public static class EditorNames
{
    public const string BarDefinitionsEditor = nameof(BarDefinitionsEditor);
    public const string DockHostAddToolbarEditor = nameof(DockHostAddToolbarEditor);
    public const string DockHostAddMenuBarEditor = nameof(DockHostAddMenuBarEditor);
    public const string DockHostAddCommandsEditor = nameof(DockHostAddCommandsEditor);
    public const string DockHostAddCommandsToBarEditor = nameof(DockHostAddCommandsToBarEditor);
    public const string DockHostEditBarsEditor = nameof(DockHostEditBarsEditor);
    public const string DockHostEditCatalogEditor = nameof(DockHostEditCatalogEditor);
    public const string SvgMarkupEditor = nameof(SvgMarkupEditor);
    public const string SvgStockIconsEditor = nameof(SvgStockIconsEditor);
    public const string SvgImportEditor = nameof(SvgImportEditor);
}
