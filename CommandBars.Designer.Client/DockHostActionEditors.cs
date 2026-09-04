using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;
using Microsoft.DotNet.DesignTools.Client;

namespace CommandBars.Designer.Client;

internal enum DockHostActionKind
{
    AddToolbar,
    AddMenuBar,
    AddCommands,
    EditBars,
    EditCatalog,
}

internal abstract class DockHostActionEditor : UITypeEditor
{
    protected abstract DockHostActionKind Action { get; }

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => UITypeEditorEditStyle.Modal;

    public override object? EditValue(
        ITypeDescriptorContext? context,
        IServiceProvider provider,
        object? value)
    {
        if (provider is null || context?.Instance is null)
            return value;

        var editorService = provider.GetRequiredService<IWindowsFormsEditorService>();
        var client = provider.GetRequiredService<IDesignToolsClient>();
        var session = provider.GetRequiredService<DesignerSession>();
        object hostProxy = context.Instance;

        var getSender = client.Protocol
            .GetEndpoint<GetDockHostDesignContextEndpoint>()
            .GetSender(client);
        var response = getSender.SendRequest(new GetDockHostDesignContextRequest(hostProxy));
        var hostContext = DefinitionsSerializer.DeserializeDockHostContext(response.ContextJson);
        if (!hostContext.HasManager)
        {
            MessageBox.Show(
                "Assign this DockHost's Manager property before editing command bars.",
                "CommandBars DockHost",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return value;
        }

        DesignSnapshot snapshot = hostContext.Snapshot;
        bool changed = Action switch
        {
            DockHostActionKind.AddToolbar => AddBar(
                editorService, ref snapshot, BarKind.Toolbar, hostContext.Edge),
            DockHostActionKind.AddMenuBar => AddBar(
                editorService, ref snapshot, BarKind.MenuBar, hostContext.Edge),
            DockHostActionKind.AddCommands => AddCommands(
                editorService, ref snapshot, hostContext.Edge),
            DockHostActionKind.EditBars => EditSnapshot(
                editorService, snapshot, BarDefinitionsInitialPage.BarsAndMenus),
            DockHostActionKind.EditCatalog => EditSnapshot(
                editorService, snapshot, BarDefinitionsInitialPage.Commands),
            _ => false,
        };
        if (!changed)
            return value;

        var validation = CatalogDesignService.ValidateCatalogFirst(snapshot);
        if (!validation.IsValid)
        {
            using var issues = new CatalogIssuesDialog(validation.Diagnostics);
            editorService.ShowDialog(issues);
            return value;
        }

        var setSender = client.Protocol
            .GetEndpoint<SetDockHostDefinitionsEndpoint>()
            .GetSender(client);
        setSender.SendRequest(new SetDockHostDefinitionsRequest(
            session.Id,
            hostProxy,
            DefinitionsSerializer.Serialize(snapshot)));
        return value;
    }

    private static bool AddBar(
        IWindowsFormsEditorService editorService,
        ref DesignSnapshot snapshot,
        BarKind kind,
        DockEdgeData edge)
    {
        if (!EnsureCatalogFirst(editorService, ref snapshot))
            return false;
        if (kind == BarKind.MenuBar)
        {
            if (edge != DockEdgeData.Top)
            {
                MessageBox.Show("A menu bar can be created only in the top DockHost.",
                    "Add Menu Bar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (snapshot.Bars.Any(bar => bar.BarType == BarKind.MenuBar))
            {
                MessageBox.Show("This manager already has a menu bar.",
                    "Add Menu Bar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        using var dialog = new NewBarDialog(
            kind,
            edge,
            snapshot.Bars.Select(bar => bar.Name));
        if (editorService.ShowDialog(dialog) != DialogResult.OK)
            return false;
        snapshot.Bars.Add(dialog.CreatedBar);
        return true;
    }

    private static bool AddCommands(
        IWindowsFormsEditorService editorService,
        ref DesignSnapshot snapshot,
        DockEdgeData edge)
    {
        if (!EnsureCatalogFirst(editorService, ref snapshot))
            return false;
        var candidates = snapshot.Bars
            .Where(bar => bar.Visible && bar.Dock == edge)
            .ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(
                "There are no visible bars assigned to this DockHost. Add a toolbar first.",
                "Add Commands",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return false;
        }

        BarDefData? target;
        if (candidates.Count == 1)
        {
            target = candidates[0];
        }
        else
        {
            using var chooser = new BarTargetDialog(candidates, edge);
            if (editorService.ShowDialog(chooser) != DialogResult.OK)
                return false;
            target = chooser.SelectedBar;
        }
        if (target == null)
            return false;

        var placementTarget = target.BarType == BarKind.MenuBar
            ? CommandPlacementTargetData.MenuBar
            : CommandPlacementTargetData.Toolbar;
        using var picker = new CommandPickerDialog(
            snapshot,
            placementTarget,
            "Add Commands to " + target.Name);
        if (editorService.ShowDialog(picker) != DialogResult.OK)
            return false;
        foreach (var command in picker.SelectedCommands)
            target.Placements.Add(new CommandPlacementData { CommandId = command.Id });
        return picker.SelectedCommands.Count > 0;
    }

    private static bool EditSnapshot(
        IWindowsFormsEditorService editorService,
        DesignSnapshot snapshot,
        BarDefinitionsInitialPage initialPage)
    {
        using var dialog = new BarDefinitionsDialog(snapshot, initialPage);
        return editorService.ShowDialog(dialog) == DialogResult.OK;
    }

    private static bool EnsureCatalogFirst(
        IWindowsFormsEditorService editorService,
        ref DesignSnapshot snapshot)
    {
        if (!snapshot.Bars.Any(bar => bar.Items.Count > 0))
            return true;
        var plan = CatalogDesignService.CreateLegacyMigrationPlan(snapshot);
        using var preview = new LegacyMigrationPreviewDialog(plan);
        if (editorService.ShowDialog(preview) != DialogResult.OK)
            return false;
        snapshot = plan.MigratedSnapshot;
        return true;
    }
}

internal sealed class DockHostAddToolbarEditor : DockHostActionEditor
{
    protected override DockHostActionKind Action => DockHostActionKind.AddToolbar;
}

internal sealed class DockHostAddMenuBarEditor : DockHostActionEditor
{
    protected override DockHostActionKind Action => DockHostActionKind.AddMenuBar;
}

internal sealed class DockHostAddCommandsEditor : DockHostActionEditor
{
    protected override DockHostActionKind Action => DockHostActionKind.AddCommands;
}

internal sealed class DockHostEditBarsEditor : DockHostActionEditor
{
    protected override DockHostActionKind Action => DockHostActionKind.EditBars;
}

internal sealed class DockHostEditCatalogEditor : DockHostActionEditor
{
    protected override DockHostActionKind Action => DockHostActionKind.EditCatalog;
}
