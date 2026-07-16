namespace CommandBars.PackageDemo;

partial class MainForm
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
        Design.MenuBarDefinition menuBarDefinition1 = new Design.MenuBarDefinition();
        Design.PopupDefinition popupDefinition1 = new Design.PopupDefinition();
        Design.ButtonDefinition buttonDefinition1 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition2 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition3 = new Design.ButtonDefinition();
        Design.SeparatorDefinition separatorDefinition1 = new Design.SeparatorDefinition();
        Design.ButtonDefinition buttonDefinition4 = new Design.ButtonDefinition();
        Design.PopupDefinition popupDefinition2 = new Design.PopupDefinition();
        Design.ButtonDefinition buttonDefinition5 = new Design.ButtonDefinition();
        Design.ToolbarDefinition toolbarDefinition1 = new Design.ToolbarDefinition();
        Design.ButtonDefinition buttonDefinition6 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition7 = new Design.ButtonDefinition();
        Design.ButtonDefinition buttonDefinition8 = new Design.ButtonDefinition();
        Design.ComboBoxDefinition comboBoxDefinition1 = new Design.ComboBoxDefinition();
        Design.CommandDefinition commandDefinition1 = new Design.CommandDefinition();
        Design.CommandDefinition commandDefinition2 = new Design.CommandDefinition();
        Design.CommandDefinition commandDefinition3 = new Design.CommandDefinition();
        Imaging.SvgImage svgImage1 = new Imaging.SvgImage();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        Imaging.SvgImage svgImage2 = new Imaging.SvgImage();
        Imaging.SvgImage svgImage3 = new Imaging.SvgImage();
        _manager = new CommandBarManager();
        _svgImages = new CommandBars.Imaging.SvgImageList(components);
        _dockTop = new CommandBars.Controls.DockHost();
        _client = new Label();
        SuspendLayout();
        // 
        // _manager
        // 
        menuBarDefinition1.BarType = Model.CommandBarType.MenuBar;
        buttonDefinition1.CommandId = "file.new";
        buttonDefinition1.ImageKey = "new";
        buttonDefinition1.Shortcut = Keys.Control | Keys.N;
        buttonDefinition1.Text = "&New";
        buttonDefinition2.CommandId = "file.open";
        buttonDefinition2.ImageKey = "open";
        buttonDefinition2.Shortcut = Keys.Control | Keys.O;
        buttonDefinition2.Text = "&Open…";
        buttonDefinition3.CommandId = "file.save";
        buttonDefinition3.ImageKey = "save";
        buttonDefinition3.Shortcut = Keys.Control | Keys.S;
        buttonDefinition3.Text = "&Save";
        separatorDefinition1.Kind = Model.CommandItemKind.Separator;
        buttonDefinition4.CommandId = "file.exit";
        buttonDefinition4.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        buttonDefinition4.Text = "E&xit";
        popupDefinition1.Items.Add(buttonDefinition1);
        popupDefinition1.Items.Add(buttonDefinition2);
        popupDefinition1.Items.Add(buttonDefinition3);
        popupDefinition1.Items.Add(separatorDefinition1);
        popupDefinition1.Items.Add(buttonDefinition4);
        popupDefinition1.Kind = Model.CommandItemKind.Popup;
        popupDefinition1.Text = "&File";
        buttonDefinition5.CommandId = "help.about";
        buttonDefinition5.DisplayStyle = Model.CommandItemDisplayStyle.TextOnly;
        buttonDefinition5.Text = "&About…";
        popupDefinition2.Items.Add(buttonDefinition5);
        popupDefinition2.Kind = Model.CommandItemKind.Popup;
        popupDefinition2.Text = "&Help";
        menuBarDefinition1.Items.Add(popupDefinition1);
        menuBarDefinition1.Items.Add(popupDefinition2);
        menuBarDefinition1.Name = "MenuBar";
        menuBarDefinition1.Text = "Menu Bar";
        buttonDefinition6.CommandId = "file.new";
        buttonDefinition6.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition6.ImageKey = "new";
        buttonDefinition6.Text = "New";
        buttonDefinition7.CommandId = "file.open";
        buttonDefinition7.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition7.ImageKey = "open";
        buttonDefinition7.Text = "Open";
        buttonDefinition8.CommandId = "file.save";
        buttonDefinition8.DisplayStyle = Model.CommandItemDisplayStyle.ImageOnly;
        buttonDefinition8.ImageKey = "save";
        buttonDefinition8.Text = "Save";
        comboBoxDefinition1.CommandId = "format.font";
        comboBoxDefinition1.Kind = Model.CommandItemKind.ComboBox;
        comboBoxDefinition1.Name = "CmbFont";
        toolbarDefinition1.Items.Add(buttonDefinition6);
        toolbarDefinition1.Items.Add(buttonDefinition7);
        toolbarDefinition1.Items.Add(buttonDefinition8);
        toolbarDefinition1.Items.Add(comboBoxDefinition1);
        toolbarDefinition1.Name = "Standard";
        toolbarDefinition1.Text = "Standard";
        _manager.BarDefinitions.Add(menuBarDefinition1);
        _manager.BarDefinitions.Add(toolbarDefinition1);
        commandDefinition1.Id = "file.new";
        commandDefinition1.ImageKey = "new";
        commandDefinition1.Text = "&New";
        commandDefinition2.Id = "file.open";
        commandDefinition2.Text = "&Open";
        commandDefinition3.Id = "file.save";
        commandDefinition3.ImageKey = "save";
        commandDefinition3.Text = "&Save";
        _manager.CommandDefinitions.Add(commandDefinition1);
        _manager.CommandDefinitions.Add(commandDefinition2);
        _manager.CommandDefinitions.Add(commandDefinition3);
        _manager.Images = _svgImages;
        _manager.ShowToolTips = true;
        // 
        // _svgImages
        // 
        svgImage1.Key = "new";
        svgImage1.Svg = resources.GetString("svgImage1.Svg");
        svgImage2.Key = "open";
        svgImage2.Svg = resources.GetString("svgImage2.Svg");
        svgImage3.Key = "save";
        svgImage3.Svg = resources.GetString("svgImage3.Svg");
        _svgImages.Images.Add(svgImage1);
        _svgImages.Images.Add(svgImage2);
        _svgImages.Images.Add(svgImage3);
        // 
        // _dockTop
        // 
        _dockTop.Dock = DockStyle.Top;
        _dockTop.Location = new Point(0, 0);
        _dockTop.Manager = _manager;
        _dockTop.Name = "_dockTop";
        _dockTop.Size = new Size(900, 74);
        _dockTop.TabIndex = 0;
        // 
        // _client
        // 
        _client.BackColor = Color.White;
        _client.Dock = DockStyle.Fill;
        _client.Location = new Point(0, 74);
        _client.Name = "_client";
        _client.Size = new Size(900, 476);
        _client.TabIndex = 1;
        _client.Text = "Package-consuming demo — open this form in the designer to test the out-of-process design-time support.";
        _client.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 550);
        Controls.Add(_client);
        Controls.Add(_dockTop);
        Name = "MainForm";
        Text = "CommandBars — package demo (out-of-process designer)";
        ResumeLayout(false);
    }

    #endregion

    private CommandBars.CommandBarManager _manager;
    private CommandBars.Imaging.SvgImageList _svgImages;
    private CommandBars.Controls.DockHost _dockTop;
    private System.Windows.Forms.Label _client;
}
