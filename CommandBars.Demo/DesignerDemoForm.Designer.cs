namespace CommandBars.Demo;

partial class DesignerDemoForm
{
    /// <summary>Required designer variable.</summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>Clean up any resources being used.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support — do not modify the contents of this
    /// method with the code editor. Icons are embedded in the SvgImageList and
    /// referenced by catalog entries; bars contain lightweight catalog placements.
    /// Every object gets its own local, as VS generates.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Design.CommandDefinition catalogCommand1 = new Design.CommandDefinition();
        Design.CommandPlacementDefinition catalogPlacement1 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition catalogPlacement2 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition catalogPlacement3 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition catalogPlacement4 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition catalogPlacement5 = new Design.CommandPlacementDefinition();
        Design.CommandDefinition catalogCommand2 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand3 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand4 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand5 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand6 = new Design.CommandDefinition();
        Design.CommandPlacementDefinition catalogPlacement6 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition catalogPlacement7 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition catalogPlacement8 = new Design.CommandPlacementDefinition();
        Design.CommandDefinition catalogCommand7 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand8 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand9 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand10 = new Design.CommandDefinition();
        Design.CommandPlacementDefinition catalogPlacement9 = new Design.CommandPlacementDefinition();
        Design.CommandDefinition catalogCommand11 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand12 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand13 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand14 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand15 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand16 = new Design.CommandDefinition();
        Design.CommandDefinition catalogCommand17 = new Design.CommandDefinition();
        Design.MenuBarDefinition catalogBar1 = new Design.MenuBarDefinition();
        Design.CommandPlacementDefinition barPlacement10 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement11 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement12 = new Design.CommandPlacementDefinition();
        Design.ToolbarDefinition catalogBar2 = new Design.ToolbarDefinition();
        Design.CommandPlacementDefinition barPlacement13 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement14 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement15 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement16 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement17 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement18 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement19 = new Design.CommandPlacementDefinition();
        Design.ToolbarDefinition catalogBar3 = new Design.ToolbarDefinition();
        Design.CommandPlacementDefinition barPlacement20 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement21 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement22 = new Design.CommandPlacementDefinition();
        Design.ToolbarDefinition catalogBar4 = new Design.ToolbarDefinition();
        Design.CommandPlacementDefinition barPlacement23 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement24 = new Design.CommandPlacementDefinition();
        Design.CommandPlacementDefinition barPlacement25 = new Design.CommandPlacementDefinition();
        Imaging.SvgImage svgImage13 = new Imaging.SvgImage();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DesignerDemoForm));
        Imaging.SvgImage svgImage14 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage15 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage16 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage17 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage18 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage19 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage20 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage21 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage22 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage23 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage24 = new Imaging.SvgImage();
        _manager = new CommandBarManager();
        _svgImages = new CommandBars.Imaging.SvgImageList(components);
        _dockTop = new CommandBars.Controls.DockHost();
        _dockLeft = new CommandBars.Controls.DockHost();
        _dockRight = new CommandBars.Controls.DockHost();
        _dockBottom = new CommandBars.Controls.DockHost();
        _client = new Label();
        SuspendLayout();
        // 
        // _manager
        // 
        catalogCommand1.Id = "file.menu";
        catalogCommand1.Kind = Design.CommandDefinitionKind.Popup;
        catalogCommand1.Text = "\u0026File";
        catalogPlacement1.CommandId = "file.new";
        catalogPlacement1.UseCatalogDisplayStyle = false;
        catalogCommand1.Items.Add(catalogPlacement1);
        catalogPlacement2.CommandId = "file.open";
        catalogPlacement2.UseCatalogDisplayStyle = false;
        catalogCommand1.Items.Add(catalogPlacement2);
        catalogPlacement3.CommandId = "file.save";
        catalogPlacement3.UseCatalogDisplayStyle = false;
        catalogCommand1.Items.Add(catalogPlacement3);
        catalogPlacement4.Kind = Design.CommandPlacementKind.Separator;
        catalogPlacement4.UseCatalogDisplayStyle = false;
        catalogCommand1.Items.Add(catalogPlacement4);
        catalogPlacement5.CommandId = "file.exit";
        catalogPlacement5.UseCatalogDisplayStyle = false;
        catalogPlacement5.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        catalogCommand1.Items.Add(catalogPlacement5);
        _manager.CommandDefinitions.Add(catalogCommand1);
        catalogCommand2.Id = "file.new";
        catalogCommand2.Text = "\u0026New";
        catalogCommand2.ImageKey = "new";
        catalogCommand2.Shortcut = (Keys)131150;
        _manager.CommandDefinitions.Add(catalogCommand2);
        catalogCommand3.Id = "file.open";
        catalogCommand3.Text = "\u0026Open\u2026";
        catalogCommand3.ImageKey = "open";
        catalogCommand3.Shortcut = (Keys)131151;
        _manager.CommandDefinitions.Add(catalogCommand3);
        catalogCommand4.Id = "file.save";
        catalogCommand4.Text = "\u0026Save";
        catalogCommand4.ImageKey = "save";
        catalogCommand4.Shortcut = (Keys)131155;
        _manager.CommandDefinitions.Add(catalogCommand4);
        catalogCommand5.Id = "file.exit";
        catalogCommand5.Text = "E\u0026xit";
        catalogCommand5.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        _manager.CommandDefinitions.Add(catalogCommand5);
        catalogCommand6.Id = "edit.menu";
        catalogCommand6.Kind = Design.CommandDefinitionKind.Popup;
        catalogCommand6.Text = "\u0026Edit";
        catalogPlacement6.CommandId = "edit.cut";
        catalogPlacement6.UseCatalogDisplayStyle = false;
        catalogCommand6.Items.Add(catalogPlacement6);
        catalogPlacement7.CommandId = "edit.copy";
        catalogPlacement7.UseCatalogDisplayStyle = false;
        catalogCommand6.Items.Add(catalogPlacement7);
        catalogPlacement8.CommandId = "edit.paste";
        catalogPlacement8.UseCatalogDisplayStyle = false;
        catalogCommand6.Items.Add(catalogPlacement8);
        _manager.CommandDefinitions.Add(catalogCommand6);
        catalogCommand7.Id = "edit.cut";
        catalogCommand7.Text = "Cu\u0026t";
        catalogCommand7.ImageKey = "cut";
        catalogCommand7.Shortcut = (Keys)131160;
        _manager.CommandDefinitions.Add(catalogCommand7);
        catalogCommand8.Id = "edit.copy";
        catalogCommand8.Text = "\u0026Copy";
        catalogCommand8.ImageKey = "copy";
        catalogCommand8.Shortcut = (Keys)131139;
        _manager.CommandDefinitions.Add(catalogCommand8);
        catalogCommand9.Id = "edit.paste";
        catalogCommand9.Text = "\u0026Paste";
        catalogCommand9.ImageKey = "paste";
        catalogCommand9.Shortcut = (Keys)131158;
        _manager.CommandDefinitions.Add(catalogCommand9);
        catalogCommand10.Id = "help.menu";
        catalogCommand10.Kind = Design.CommandDefinitionKind.Popup;
        catalogCommand10.Text = "\u0026Help";
        catalogPlacement9.CommandId = "help.about";
        catalogPlacement9.UseCatalogDisplayStyle = false;
        catalogPlacement9.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        catalogCommand10.Items.Add(catalogPlacement9);
        _manager.CommandDefinitions.Add(catalogCommand10);
        catalogCommand11.Id = "help.about";
        catalogCommand11.Text = "\u0026About\u2026";
        catalogCommand11.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        _manager.CommandDefinitions.Add(catalogCommand11);
        catalogCommand12.Id = "format.bold";
        catalogCommand12.Kind = Design.CommandDefinitionKind.Toggle;
        catalogCommand12.Text = "Bold";
        catalogCommand12.ImageKey = "bold";
        catalogCommand12.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        _manager.CommandDefinitions.Add(catalogCommand12);
        catalogCommand13.Id = "format.italic";
        catalogCommand13.Kind = Design.CommandDefinitionKind.Toggle;
        catalogCommand13.Text = "Italic";
        catalogCommand13.ImageKey = "italic";
        catalogCommand13.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        _manager.CommandDefinitions.Add(catalogCommand13);
        catalogCommand14.Id = "format.underline";
        catalogCommand14.Kind = Design.CommandDefinitionKind.Toggle;
        catalogCommand14.Text = "Underline";
        catalogCommand14.ImageKey = "underline";
        catalogCommand14.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        _manager.CommandDefinitions.Add(catalogCommand14);
        catalogCommand15.Id = "nav.back";
        catalogCommand15.Text = "Back";
        catalogCommand15.ImageKey = "back";
        catalogCommand15.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        _manager.CommandDefinitions.Add(catalogCommand15);
        catalogCommand16.Id = "nav.forward";
        catalogCommand16.Text = "Forward";
        catalogCommand16.ImageKey = "forward";
        catalogCommand16.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        _manager.CommandDefinitions.Add(catalogCommand16);
        catalogCommand17.Id = "nav.home";
        catalogCommand17.Text = "Home";
        catalogCommand17.ImageKey = "home";
        catalogCommand17.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        _manager.CommandDefinitions.Add(catalogCommand17);
        catalogBar1.Name = "MenuBar";
        catalogBar1.Text = "Menu Bar";
        barPlacement10.CommandId = "file.menu";
        barPlacement10.UseCatalogDisplayStyle = false;
        catalogBar1.Placements.Add(barPlacement10);
        barPlacement11.CommandId = "edit.menu";
        barPlacement11.UseCatalogDisplayStyle = false;
        catalogBar1.Placements.Add(barPlacement11);
        barPlacement12.CommandId = "help.menu";
        barPlacement12.UseCatalogDisplayStyle = false;
        catalogBar1.Placements.Add(barPlacement12);
        _manager.BarDefinitions.Add(catalogBar1);
        catalogBar2.Name = "Standard";
        catalogBar2.Text = "Standard";
        catalogBar2.IconSize = 16;
        barPlacement13.CommandId = "file.new";
        barPlacement13.UseCatalogDisplayStyle = false;
        barPlacement13.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar2.Placements.Add(barPlacement13);
        barPlacement14.CommandId = "file.open";
        barPlacement14.UseCatalogDisplayStyle = false;
        barPlacement14.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar2.Placements.Add(barPlacement14);
        barPlacement15.CommandId = "file.save";
        barPlacement15.UseCatalogDisplayStyle = false;
        barPlacement15.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar2.Placements.Add(barPlacement15);
        barPlacement16.Kind = Design.CommandPlacementKind.Separator;
        barPlacement16.UseCatalogDisplayStyle = false;
        catalogBar2.Placements.Add(barPlacement16);
        barPlacement17.CommandId = "edit.cut";
        barPlacement17.UseCatalogDisplayStyle = false;
        barPlacement17.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar2.Placements.Add(barPlacement17);
        barPlacement18.CommandId = "edit.copy";
        barPlacement18.UseCatalogDisplayStyle = false;
        barPlacement18.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar2.Placements.Add(barPlacement18);
        barPlacement19.CommandId = "edit.paste";
        barPlacement19.UseCatalogDisplayStyle = false;
        barPlacement19.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar2.Placements.Add(barPlacement19);
        _manager.BarDefinitions.Add(catalogBar2);
        catalogBar3.Name = "Formatting";
        catalogBar3.Text = "Formatting";
        catalogBar3.IconSize = 16;
        barPlacement20.CommandId = "format.bold";
        barPlacement20.UseCatalogDisplayStyle = false;
        barPlacement20.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar3.Placements.Add(barPlacement20);
        barPlacement21.CommandId = "format.italic";
        barPlacement21.UseCatalogDisplayStyle = false;
        barPlacement21.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar3.Placements.Add(barPlacement21);
        barPlacement22.CommandId = "format.underline";
        barPlacement22.UseCatalogDisplayStyle = false;
        barPlacement22.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar3.Placements.Add(barPlacement22);
        _manager.BarDefinitions.Add(catalogBar3);
        catalogBar4.Name = "Navigation";
        catalogBar4.Text = "Navigation";
        catalogBar4.Dock = Model.DockState.Left;
        catalogBar4.IconSize = 16;
        barPlacement23.CommandId = "nav.back";
        barPlacement23.UseCatalogDisplayStyle = false;
        barPlacement23.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar4.Placements.Add(barPlacement23);
        barPlacement24.CommandId = "nav.forward";
        barPlacement24.UseCatalogDisplayStyle = false;
        barPlacement24.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar4.Placements.Add(barPlacement24);
        barPlacement25.CommandId = "nav.home";
        barPlacement25.UseCatalogDisplayStyle = false;
        barPlacement25.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        catalogBar4.Placements.Add(barPlacement25);
        _manager.BarDefinitions.Add(catalogBar4);
        _manager.Images = _svgImages;
        _manager.ShowToolTips = true;
        _manager.Theme = Rendering.CommandBarTheme.OfficeXP;
        // 
        // _svgImages
        // 
        svgImage13.Key = "new";
        svgImage13.Svg = resources.GetString("svgImage13.Svg");
        svgImage14.Key = "open";
        svgImage14.Svg = resources.GetString("svgImage14.Svg");
        svgImage15.Key = "save";
        svgImage15.Svg = resources.GetString("svgImage15.Svg");
        svgImage16.Key = "cut";
        svgImage16.Svg = resources.GetString("svgImage16.Svg");
        svgImage17.Key = "copy";
        svgImage17.Svg = resources.GetString("svgImage17.Svg");
        svgImage18.Key = "paste";
        svgImage18.Svg = resources.GetString("svgImage18.Svg");
        svgImage19.Key = "bold";
        svgImage19.Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'> <text x='16' y='24' font-family='Segoe UI, Arial' font-size='22' font-weight='bold' text-anchor='middle' fill='#2d3746'>B</text> </svg>";
        svgImage20.Key = "italic";
        svgImage20.Svg = resources.GetString("svgImage20.Svg");
        svgImage21.Key = "underline";
        svgImage21.Svg = resources.GetString("svgImage21.Svg");
        svgImage22.Key = "back";
        svgImage22.Svg = resources.GetString("svgImage22.Svg");
        svgImage23.Key = "forward";
        svgImage23.Svg = resources.GetString("svgImage23.Svg");
        svgImage24.Key = "home";
        svgImage24.Svg = resources.GetString("svgImage24.Svg");
        _svgImages.Images.Add(svgImage13);
        _svgImages.Images.Add(svgImage14);
        _svgImages.Images.Add(svgImage15);
        _svgImages.Images.Add(svgImage16);
        _svgImages.Images.Add(svgImage17);
        _svgImages.Images.Add(svgImage18);
        _svgImages.Images.Add(svgImage19);
        _svgImages.Images.Add(svgImage20);
        _svgImages.Images.Add(svgImage21);
        _svgImages.Images.Add(svgImage22);
        _svgImages.Images.Add(svgImage23);
        _svgImages.Images.Add(svgImage24);
        // 
        // _dockTop
        // 
        _dockTop.Dock = DockStyle.Top;
        _dockTop.Location = new Point(0, 0);
        _dockTop.Manager = _manager;
        _dockTop.Margin = new Padding(3, 4, 3, 4);
        _dockTop.Name = "_dockTop";
        _dockTop.Size = new Size(1058, 64);
        _dockTop.TabIndex = 0;
        // 
        // _dockLeft
        // 
        _dockLeft.Dock = DockStyle.Left;
        _dockLeft.Edge = CommandBars.Controls.DockEdge.Left;
        _dockLeft.Location = new Point(0, 64);
        _dockLeft.Manager = _manager;
        _dockLeft.Margin = new Padding(3, 4, 3, 4);
        _dockLeft.Name = "_dockLeft";
        _dockLeft.Size = new Size(30, 724);
        _dockLeft.TabIndex = 1;
        // 
        // _dockRight
        // 
        _dockRight.Dock = DockStyle.Right;
        _dockRight.Edge = CommandBars.Controls.DockEdge.Right;
        _dockRight.Location = new Point(1030, 64);
        _dockRight.Manager = _manager;
        _dockRight.Margin = new Padding(3, 4, 3, 4);
        _dockRight.Name = "_dockRight";
        _dockRight.Size = new Size(28, 724);
        _dockRight.TabIndex = 2;
        // 
        // _dockBottom
        // 
        _dockBottom.Dock = DockStyle.Bottom;
        _dockBottom.Edge = CommandBars.Controls.DockEdge.Bottom;
        _dockBottom.Location = new Point(0, 788);
        _dockBottom.Manager = _manager;
        _dockBottom.Margin = new Padding(3, 4, 3, 4);
        _dockBottom.Name = "_dockBottom";
        _dockBottom.Size = new Size(1058, 28);
        _dockBottom.TabIndex = 3;
        // 
        // _client
        // 
        _client.BackColor = Color.White;
        _client.Dock = DockStyle.Fill;
        _client.Location = new Point(30, 64);
        _client.Name = "_client";
        _client.Size = new Size(1000, 724);
        _client.TabIndex = 4;
        _client.Text = "Client area — icons are embedded in the SvgImageList and referenced by ImageKey.";
        _client.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // DesignerDemoForm
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1058, 816);
        Controls.Add(_client);
        Controls.Add(_dockRight);
        Controls.Add(_dockLeft);
        Controls.Add(_dockBottom);
        Controls.Add(_dockTop);
        Margin = new Padding(3, 4, 3, 4);
        Name = "DesignerDemoForm";
        Text = "CommandBars — Designer-defined bars";
        ResumeLayout(false);
    }

    #endregion

    private CommandBars.CommandBarManager _manager;
    private CommandBars.Imaging.SvgImageList _svgImages;
    private CommandBars.Controls.DockHost _dockTop;
    private CommandBars.Controls.DockHost _dockLeft;
    private CommandBars.Controls.DockHost _dockRight;
    private CommandBars.Controls.DockHost _dockBottom;
    private System.Windows.Forms.Label _client;
}
