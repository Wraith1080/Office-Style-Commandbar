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
    /// referenced by each item's ImageKey; the bars are defined through the
    /// manager's BarDefinitions. Every object gets its own local, as VS generates.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Design.MenuBarDefinition menuBarDefinition2 = new Design.MenuBarDefinition();
        Design.PopupDefinition popupDefinition4 = new Design.PopupDefinition();
        Design.ButtonDefinition buttonDefinition18 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition19 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition20 = new Design.ButtonDefinition();
        Design.SeparatorDefinition separatorDefinition3 = new Design.SeparatorDefinition();
        Design.ButtonDefinition buttonDefinition21 = new Design.ButtonDefinition();
        Design.PopupDefinition popupDefinition5 = new Design.PopupDefinition();
        Design.ButtonDefinition buttonDefinition22 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition23 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition24 = new Design.ButtonDefinition();
        Design.PopupDefinition popupDefinition6 = new Design.PopupDefinition();
        Design.ButtonDefinition buttonDefinition25 = new Design.ButtonDefinition();
        Design.ToolbarDefinition toolbarDefinition4 = new Design.ToolbarDefinition();
        Design.ButtonDefinition buttonDefinition26 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition27 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition28 = new Design.ButtonDefinition();
        Design.SeparatorDefinition separatorDefinition4 = new Design.SeparatorDefinition();
        Design.ButtonDefinition buttonDefinition29 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition30 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition31 = new Design.ButtonDefinition();
        Design.ToolbarDefinition toolbarDefinition5 = new Design.ToolbarDefinition();
        Design.ToggleButtonDefinition toggleButtonDefinition4 = new Design.ToggleButtonDefinition();
        Design.ToggleButtonDefinition toggleButtonDefinition5 = new Design.ToggleButtonDefinition();
        Design.ToggleButtonDefinition toggleButtonDefinition6 = new Design.ToggleButtonDefinition();
        Design.ToolbarDefinition toolbarDefinition6 = new Design.ToolbarDefinition();
        Design.ButtonDefinition buttonDefinition32 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition33 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition34 = new Design.ButtonDefinition();
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
        menuBarDefinition2.BarType = Model.CommandBarType.MenuBar;
        buttonDefinition18.CommandId = "file.new";
        buttonDefinition18.ImageKey = "new";
        buttonDefinition18.Shortcut = Keys.Control | Keys.N;
        buttonDefinition18.Text = "&New";
        buttonDefinition19.CommandId = "file.open";
        buttonDefinition19.ImageKey = "open";
        buttonDefinition19.Shortcut = Keys.Control | Keys.O;
        buttonDefinition19.Text = "&Open…";
        buttonDefinition20.CommandId = "file.save";
        buttonDefinition20.ImageKey = "save";
        buttonDefinition20.Shortcut = Keys.Control | Keys.S;
        buttonDefinition20.Text = "&Save";
        separatorDefinition3.Kind = Model.CommandItemKind.Separator;
        buttonDefinition21.CommandId = "file.exit";
        buttonDefinition21.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        buttonDefinition21.Text = "E&xit";
        popupDefinition4.Items.Add(buttonDefinition18);
        popupDefinition4.Items.Add(buttonDefinition19);
        popupDefinition4.Items.Add(buttonDefinition20);
        popupDefinition4.Items.Add(separatorDefinition3);
        popupDefinition4.Items.Add(buttonDefinition21);
        popupDefinition4.Kind = Model.CommandItemKind.Popup;
        popupDefinition4.Text = "&File";
        buttonDefinition22.CommandId = "edit.cut";
        buttonDefinition22.ImageKey = "cut";
        buttonDefinition22.Shortcut = Keys.Control | Keys.X;
        buttonDefinition22.Text = "Cu&t";
        buttonDefinition23.CommandId = "edit.copy";
        buttonDefinition23.ImageKey = "copy";
        buttonDefinition23.Shortcut = Keys.Control | Keys.C;
        buttonDefinition23.Text = "&Copy";
        buttonDefinition24.CommandId = "edit.paste";
        buttonDefinition24.ImageKey = "paste";
        buttonDefinition24.Shortcut = Keys.Control | Keys.V;
        buttonDefinition24.Text = "&Paste";
        popupDefinition5.Items.Add(buttonDefinition22);
        popupDefinition5.Items.Add(buttonDefinition23);
        popupDefinition5.Items.Add(buttonDefinition24);
        popupDefinition5.Kind = Model.CommandItemKind.Popup;
        popupDefinition5.Text = "&Edit";
        buttonDefinition25.CommandId = "help.about";
        buttonDefinition25.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        buttonDefinition25.Text = "&About…";
        popupDefinition6.Items.Add(buttonDefinition25);
        popupDefinition6.Kind = Model.CommandItemKind.Popup;
        popupDefinition6.Text = "&Help";
        menuBarDefinition2.Items.Add(popupDefinition4);
        menuBarDefinition2.Items.Add(popupDefinition5);
        menuBarDefinition2.Items.Add(popupDefinition6);
        menuBarDefinition2.Name = "MenuBar";
        menuBarDefinition2.Text = "Menu Bar";
        toolbarDefinition4.IconSize = 16;
        buttonDefinition26.CommandId = "file.new";
        buttonDefinition26.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition26.ImageKey = "new";
        buttonDefinition26.Text = "New";
        buttonDefinition27.CommandId = "file.open";
        buttonDefinition27.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition27.ImageKey = "open";
        buttonDefinition27.Text = "Open";
        buttonDefinition28.CommandId = "file.save";
        buttonDefinition28.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition28.ImageKey = "save";
        buttonDefinition28.Text = "Save";
        separatorDefinition4.Kind = Model.CommandItemKind.Separator;
        buttonDefinition29.CommandId = "edit.cut";
        buttonDefinition29.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition29.ImageKey = "cut";
        buttonDefinition29.Text = "Cut";
        buttonDefinition30.CommandId = "edit.copy";
        buttonDefinition30.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition30.ImageKey = "copy";
        buttonDefinition30.Text = "Copy";
        buttonDefinition31.CommandId = "edit.paste";
        buttonDefinition31.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition31.ImageKey = "paste";
        buttonDefinition31.Text = "Paste";
        toolbarDefinition4.Items.Add(buttonDefinition26);
        toolbarDefinition4.Items.Add(buttonDefinition27);
        toolbarDefinition4.Items.Add(buttonDefinition28);
        toolbarDefinition4.Items.Add(separatorDefinition4);
        toolbarDefinition4.Items.Add(buttonDefinition29);
        toolbarDefinition4.Items.Add(buttonDefinition30);
        toolbarDefinition4.Items.Add(buttonDefinition31);
        toolbarDefinition4.Name = "Standard";
        toolbarDefinition4.Text = "Standard";
        toolbarDefinition5.IconSize = 16;
        toggleButtonDefinition4.CommandId = "format.bold";
        toggleButtonDefinition4.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        toggleButtonDefinition4.ImageKey = "bold";
        toggleButtonDefinition4.Kind = Model.CommandItemKind.ToggleButton;
        toggleButtonDefinition4.Text = "Bold";
        toggleButtonDefinition5.CommandId = "format.italic";
        toggleButtonDefinition5.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        toggleButtonDefinition5.ImageKey = "italic";
        toggleButtonDefinition5.Kind = Model.CommandItemKind.ToggleButton;
        toggleButtonDefinition5.Text = "Italic";
        toggleButtonDefinition6.CommandId = "format.underline";
        toggleButtonDefinition6.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        toggleButtonDefinition6.ImageKey = "underline";
        toggleButtonDefinition6.Kind = Model.CommandItemKind.ToggleButton;
        toggleButtonDefinition6.Text = "Underline";
        toolbarDefinition5.Items.Add(toggleButtonDefinition4);
        toolbarDefinition5.Items.Add(toggleButtonDefinition5);
        toolbarDefinition5.Items.Add(toggleButtonDefinition6);
        toolbarDefinition5.Name = "Formatting";
        toolbarDefinition5.Text = "Formatting";
        toolbarDefinition6.Dock = Model.DockState.Left;
        toolbarDefinition6.IconSize = 16;
        buttonDefinition32.CommandId = "nav.back";
        buttonDefinition32.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition32.ImageKey = "back";
        buttonDefinition32.Text = "Back";
        buttonDefinition33.CommandId = "nav.forward";
        buttonDefinition33.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition33.ImageKey = "forward";
        buttonDefinition33.Text = "Forward";
        buttonDefinition34.CommandId = "nav.home";
        buttonDefinition34.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition34.ImageKey = "home";
        buttonDefinition34.Text = "Home";
        toolbarDefinition6.Items.Add(buttonDefinition32);
        toolbarDefinition6.Items.Add(buttonDefinition33);
        toolbarDefinition6.Items.Add(buttonDefinition34);
        toolbarDefinition6.Name = "Navigation";
        toolbarDefinition6.Text = "Navigation";
        _manager.BarDefinitions.Add(menuBarDefinition2);
        _manager.BarDefinitions.Add(toolbarDefinition4);
        _manager.BarDefinitions.Add(toolbarDefinition5);
        _manager.BarDefinitions.Add(toolbarDefinition6);
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
