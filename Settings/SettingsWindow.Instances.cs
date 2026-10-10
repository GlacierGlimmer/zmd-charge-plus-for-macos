using Avalonia.LogicalTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EndfieldChargePlus.Customization;
namespace EndfieldChargePlus.Settings;

public partial class SettingsWindow
{
    private AppSettings _rootSettings = new();
    private AppSettings _persistedRootSettings = new();
    private string _selectedInstanceId = "";
    private ComboBox? _instanceChoice;
    private CheckBox? _multiSwitch;
    private TextBox? _instanceName;
    private TextBlock? _editingLabel;
    private readonly List<(Control Control,string Zh,string En)> _instanceTranslations=new();
    private T InstanceText<T>(T control,string zh,string en) where T : Control
    {
        _instanceTranslations.Add((control,zh,en)); SetInstanceText(control,LocalizationManager.Text(zh,en)); return control;
    }
    private static void SetInstanceText(Control control,string text)
    {
        if(control is TextBlock block) block.Text=text;
        else if(control is TextBox box) box.Watermark=text;
        else if(control is ContentControl content) content.Content=text;
    }
    private void ApplyInstanceEditorLocalization()
    {
        if(_instanceChoice is not null && _instanceChoice.Items.Count>0) { string? selected=(_instanceChoice.SelectedItem as InstanceChoice)?.Id; _instanceChoice.Items[0]=new InstanceChoice("",LocalizationManager.Text("主 HUD","Primary HUD")); _instanceChoice.SelectedItem=_instanceChoice.Items.OfType<InstanceChoice>().FirstOrDefault(i=>i.Id==selected); }
        foreach(var (control,zh,en) in _instanceTranslations) SetInstanceText(control,LocalizationManager.Text(zh,en));
        if(_editingLabel is not null) _editingLabel.Text=LocalizationManager.Text("当前编辑：", "Editing: ")+(_rootSettings.HudInstances.FirstOrDefault(i=>i.Id==_selectedInstanceId)?.Name ?? LocalizationManager.Text("主 HUD","Primary HUD"));
        if(HudFontCombo.Items.Count>0) { int font=HudFontCombo.SelectedIndex; HudFontCombo.Items[0]=LocalizationManager.Text("系统默认","System default"); if(font==0) HudFontCombo.SelectedIndex=0; }
    }
    private sealed record InstanceChoice(string Id, string Name) { public override string ToString() => Name; }

    private void InitializeInstanceEditor()
    {
        _multiSwitch = InstanceText(new CheckBox(),"启用多实例 HUD", "Enable multiple HUD instances");
        _instanceChoice = new ComboBox { MinWidth=300 };
        _instanceName = InstanceText(new TextBox(),"实例名称", "Instance name");
        _editingLabel = new TextBlock { Foreground=Brushes.Orange, TextWrapping=TextWrapping.Wrap };
        var editButton = InstanceText(new Button(),"定位并编辑所选 HUD", "Locate and edit selected HUD");
        var add = InstanceText(new Button(),"新增 HUD", "Add HUD");
        var remove = InstanceText(new Button(),"删除所选实例", "Remove selected instance");
        var buttons = new StackPanel { Orientation=Orientation.Horizontal, Spacing=10 };
        buttons.Children.Add(add); buttons.Children.Add(remove); buttons.Children.Add(editButton);
        ExperimentalPanel.Children.Add(InstanceText(new TextBlock { FontSize=18 },"多实例 HUD", "Multiple HUD instances"));
        ExperimentalPanel.Children.Add(InstanceText(new TextBlock { TextWrapping=TextWrapping.Wrap },
            "各 HUD 可独立设置方案、轮播、字体、大小、常驻模式和目标显示器。选中后点击定位并编辑，再到前两个选项卡调整。橙色框表示当前编辑的实例。新增或删除后保存并应用；关闭设置窗口后取消高亮。采样间隔、语言和开机启动由主 HUD 统一管理。最多 16 个 HUD。",
            "Each HUD has independent profiles, cycling, font, size, visibility and target display. Select Locate and edit, then adjust the first two tabs. An orange outline identifies the edited HUD. Save & Apply after adding or removing an instance; closing Settings clears the outline. Sampling interval, language and startup are shared with the primary HUD. Up to 16 HUDs."));
        ExperimentalPanel.Children.Add(_multiSwitch); ExperimentalPanel.Children.Add(_instanceChoice);
        ExperimentalPanel.Children.Add(_instanceName); ExperimentalPanel.Children.Add(buttons); ExperimentalPanel.Children.Add(_editingLabel);
        add.Click += (_,_) =>
        {
            _rootSettings=CollectSettingsFromUi();
            if (_rootSettings.HudInstances.Count>=15) return;
            var local=CollectEditorSettings() with { MultiHudEnabled=false, HudInstances=new(),
                HudOffsetY=CollectEditorSettings().HudOffsetY+80, AlwaysVisible=true };
            var instance=new HudInstanceSettings { Name=$"HUD {_rootSettings.HudInstances.Count+2}", Settings=local };
            _rootSettings=_rootSettings with { MultiHudEnabled=true, HudInstances=_rootSettings.HudInstances.Append(instance).ToList() };
            _selectedInstanceId=instance.Id; ApplyEditorControls(local); RefreshInstanceEditor();
        };
        remove.Click += (_,_) =>
        {
            if (string.IsNullOrEmpty(_selectedInstanceId)) return;
            _rootSettings=CollectSettingsFromUi();
            _rootSettings=_rootSettings with { HudInstances=_rootSettings.HudInstances.Where(i => i.Id!=_selectedInstanceId).ToList() };
            _selectedInstanceId=""; ApplyEditorControls(_rootSettings); RefreshInstanceEditor();
        };
        editButton.Click += async (_,_) =>
        {
            _rootSettings=CollectSettingsFromUi();
            if (_instanceChoice.SelectedItem is not InstanceChoice selected) return;
            _selectedInstanceId=selected.Id;
            var settings=string.IsNullOrEmpty(selected.Id) ? _rootSettings : _rootSettings.HudInstances.First(i => i.Id==selected.Id).Settings;
            ApplyEditorControls(settings); RefreshInstanceEditor();
            if (!await _runtime.HighlightInstanceAsync(selected.Id,selected.Name))
            { _editingLabel.Text=LocalizationManager.Text("请先保存并应用以显示新增 HUD，再点击定位。", "Save & Apply to show a new HUD before locating it."); return; }
            if (this.GetLogicalDescendants().OfType<TabControl>().FirstOrDefault() is { } tabs) tabs.SelectedIndex=0;
        };
        Closed += (_,_) => _runtime.EndInstanceEditing();
        RefreshInstanceEditor();
    }

    private void RefreshInstanceEditor()
    {
        if (_instanceChoice is null || _multiSwitch is null || _instanceName is null) return;
        _multiSwitch.IsChecked=_rootSettings.MultiHudEnabled;
        _instanceChoice.Items.Clear();
        _instanceChoice.Items.Add(new InstanceChoice("",LocalizationManager.Text("主 HUD", "Primary HUD")));
        foreach (var instance in _rootSettings.HudInstances) _instanceChoice.Items.Add(new InstanceChoice(instance.Id,instance.Name));
        _instanceChoice.SelectedItem=_instanceChoice.Items.OfType<InstanceChoice>().FirstOrDefault(i => i.Id==_selectedInstanceId);
        _instanceName.IsEnabled=!string.IsNullOrEmpty(_selectedInstanceId);
        _instanceName.Text=_rootSettings.HudInstances.FirstOrDefault(i => i.Id==_selectedInstanceId)?.Name ?? "";
        if (_editingLabel is not null) _editingLabel.Text=LocalizationManager.Text("当前编辑：", "Editing: ")+_instanceChoice.SelectedItem;
        SamplingIntervalBox.IsEnabled=string.IsNullOrEmpty(_selectedInstanceId);
        StartupSwitch.IsEnabled=string.IsNullOrEmpty(_selectedInstanceId);
    }

    private void ApplySettingsToControls(AppSettings settings)
    {
        _rootSettings=settings; _persistedRootSettings=settings; _selectedInstanceId=""; ApplyEditorControls(settings); RefreshInstanceEditor();
    }

    private AppSettings CollectSettingsFromUi()
    {
        var editor=CollectEditorSettings() with { MultiHudEnabled=false, HudInstances=new() };
        bool multi=_multiSwitch?.IsChecked ?? _rootSettings.MultiHudEnabled;
        if (string.IsNullOrEmpty(_selectedInstanceId))
            _rootSettings=editor with { MultiHudEnabled=multi, HudInstances=_rootSettings.HudInstances };
        else
            _rootSettings=_rootSettings with { MultiHudEnabled=multi,
                HudInstances=_rootSettings.HudInstances.Select(i => i.Id==_selectedInstanceId ? i with {
                    Settings=editor, Name=string.IsNullOrWhiteSpace(_instanceName?.Text) ? i.Name : _instanceName.Text.Trim() } : i).ToList() };
        return _rootSettings;
    }
}
