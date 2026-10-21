using System;
using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Menu;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Audio, language, and display settings panel with tabbed layout. Uses an
    /// _initialized guard to prevent save-triggered callbacks from firing during
    /// UI population (LoadSettingsToUI → widget changes → callbacks → premature
    /// saves). Language changes call LocaleLoader.LoadCurrentLanguage() for
    /// immediate effect, then emit LanguageChanged so parent menus can refresh
    /// their translated button text. Sliders become non-editable when their
    /// toggle is off, enforcing the "disabled means off" semantics visually.
    /// Display changes (window mode, monitor, window size, V-Sync, frame rate
    /// cap) are applied live through <see cref="DisplaySettingsService"/> and saved
    /// at once, so the next start restores exactly what is on screen; the lists
    /// come from <see cref="DisplayOptions"/>, shared with the first-run launcher.
    /// The window size list is rebuilt for the chosen monitor and is disabled in
    /// the fullscreen modes, which always run at the monitor's resolution.
    /// </summary>
    public partial class SettingsMenu : Control, ISettingsPanel
    {
        /// <summary>
        /// Raised after the player selects a different language and it has been applied.
        /// Parent menus (<see cref="MainMenu"/>, <see cref="GameMenu"/>) subscribe to
        /// refresh their translated button text.
        /// </summary>
        public event Action LanguageChanged;
        private const string Header = "Window/Column/Header/Row/";
        private const string Pages = "Window/Column/Body/Pages/";
        private const string Audio = Pages + "Audio/";
        private const string Display = Pages + "Display/";
        private const string Mods = Pages + "Mods/";

        /// <summary>Names of <see cref="LanguageCode.Supported"/>, same order.</summary>
        private static readonly string[] LanguageKeys = { "settings/lang_ru", "settings/lang_en", "settings/lang_zh" };

        private static readonly string[] TabKeys = { "settings/tab_general", "settings/tab_audio", "settings/tab_display", "settings/tab_mods" };

        private Button[] _tabButtons;
        private Control[] _pages;
        private int _tab;
        private CheckButton _soundsToggle;
        private CheckButton _musicToggle;
        private CheckButton _ambientToggle;
        private HSlider _soundsVolume;
        private HSlider _musicVolume;
        private HSlider _ambientVolume;
        private Label _soundsVolumeValue;
        private Label _musicVolumeValue;
        private Label _ambientVolumeValue;
        private OptionButton _langSelect;
        private Button _resetButton;
        private Label _footerHint;

        private VBoxContainer _modListContainer;
        private Label _selectedModName;
        private VBoxContainer _settingsContainer;
        private string _selectedModId;
        private readonly Dictionary<Control, string> _controlKeys = new();

        private OptionButton _windowModeSelect;
        private OptionButton _resolutionSelect;
        private OptionButton _monitorSelect;
        private OptionButton _fpsSelect;
        private CheckButton _vsyncToggle;
        private Label _resHint;
        private List<Vector2I> _windowSizes = new();

        private readonly Dictionary<Label, string> _labelKeys = new();
        private bool _initialized;

        public override void _Ready()
        {
            CthangoverTheme.Apply(this);
            GetNode<ColorRect>("Backdrop").Color = UiTokens.Backdrop;
            GetNode<PanelContainer>("Window").AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));
            GetNode<ColorRect>("Window/Column/RuleHeader").Color = UiTokens.Line;
            GetNode<ColorRect>("Window/Column/RuleFooter").Color = UiTokens.Line;

            var title = GetNode<Label>(Header + "Title");
            title.AddThemeFontSizeOverride("font_size", UiTokens.FontTitle);
            title.AddThemeColorOverride("font_color", UiTokens.InkBright);
            RegisterLabel(title, "menu/settings");

            var close = GetNode<Button>(Header + "CloseButton");
            CthangoverTheme.ApplyIcon(close);
            close.Pressed += OnBackPressed;

            _tabButtons = new[]
            {
                GetNode<Button>(Header + "Tabs/GeneralTab"),
                GetNode<Button>(Header + "Tabs/AudioTab"),
                GetNode<Button>(Header + "Tabs/DisplayTab"),
                GetNode<Button>(Header + "Tabs/ModsTab"),
            };
            _pages = new[]
            {
                GetNode<Control>(Pages + "General"),
                GetNode<Control>(Pages + "Audio"),
                GetNode<Control>(Pages + "Display"),
                GetNode<Control>(Pages + "Mods"),
            };
            var tabGroup = new ButtonGroup();
            foreach (var tab in _tabButtons)
            {
                CthangoverTheme.ApplyTab(tab);
                tab.ButtonGroup = tabGroup;
            }
            tabGroup.Pressed += OnTabPressed;

            _langSelect = GetNode<OptionButton>(Pages + "General/LangRow/LangSelect");

            _soundsToggle = GetNode<CheckButton>(Audio + "SoundsRow/SoundsToggle");
            _soundsVolume = GetNode<HSlider>(Audio + "SoundsRow/SoundsVolume");
            _soundsVolumeValue = GetNode<Label>(Audio + "SoundsRow/SoundsValue");
            _musicToggle = GetNode<CheckButton>(Audio + "MusicRow/MusicToggle");
            _musicVolume = GetNode<HSlider>(Audio + "MusicRow/MusicVolume");
            _musicVolumeValue = GetNode<Label>(Audio + "MusicRow/MusicValue");
            _ambientToggle = GetNode<CheckButton>(Audio + "AmbientRow/AmbientToggle");
            _ambientVolume = GetNode<HSlider>(Audio + "AmbientRow/AmbientVolume");
            _ambientVolumeValue = GetNode<Label>(Audio + "AmbientRow/AmbientValue");
            foreach (var value in new[] { _soundsVolumeValue, _musicVolumeValue, _ambientVolumeValue })
                value.AddThemeColorOverride("font_color", UiTokens.AccentBright);

            _windowModeSelect = GetNode<OptionButton>(Display + "ModeRow/ModeSelect");
            _resolutionSelect = GetNode<OptionButton>(Display + "ResRow/ResSelect");
            _monitorSelect = GetNode<OptionButton>(Display + "MonitorRow/MonitorSelect");
            _fpsSelect = GetNode<OptionButton>(Display + "FpsRow/FpsSelect");
            _vsyncToggle = GetNode<CheckButton>(Display + "VSyncRow/VSyncToggle");
            _resHint = GetNode<Label>(Display + "ResHint");
            StyleHint(_resHint);

            GetNode<PanelContainer>(Mods + "ModListPanel").AddThemeStyleboxOverride("panel", ListPanelStyle());
            _modListContainer = GetNode<VBoxContainer>(Mods + "ModListPanel/Column/ModScroll/ModList");
            _selectedModName = GetNode<Label>(Mods + "Detail/SelectedModName");
            _selectedModName.AddThemeFontSizeOverride("font_size", UiTokens.FontH3);
            _selectedModName.AddThemeColorOverride("font_color", UiTokens.InkBright);
            _settingsContainer = GetNode<VBoxContainer>(Mods + "Detail/SettingsScroll/SettingsContainer");

            _resetButton = GetNode<Button>("Window/Column/Footer/Row/ResetButton");
            CthangoverTheme.ApplyDanger(_resetButton);
            _resetButton.Pressed += OnResetPressed;
            _footerHint = GetNode<Label>("Window/Column/Footer/Row/Hint");
            StyleHint(_footerHint);
            RegisterLabel(_footerHint, "settings/saved_at_once");

            RegisterSection(Pages + "General/LangSection", "settings/language");
            RegisterLabel(GetNode<Label>(Pages + "General/LangRow/LangLabel"), "settings/interface_language");
            RegisterSection(Audio + "VolumeSection", "settings/volume");
            RegisterLabel(GetNode<Label>(Audio + "SoundsRow/SoundsLabel"), "settings/sounds");
            RegisterLabel(GetNode<Label>(Audio + "MusicRow/MusicLabel"), "settings/music");
            RegisterLabel(GetNode<Label>(Audio + "AmbientRow/AmbientLabel"), "settings/ambient");
            RegisterSection(Display + "WindowSection", "settings/section_window");
            RegisterLabel(GetNode<Label>(Display + "ModeRow/ModeLabel"), "settings/window_mode");
            RegisterLabel(GetNode<Label>(Display + "ResRow/ResLabel"), "settings/resolution");
            RegisterLabel(GetNode<Label>(Display + "MonitorRow/MonitorLabel"), "settings/monitor");
            RegisterLabel(_resHint, "settings/resolution_fullscreen_hint");
            RegisterSection(Display + "RenderSection", "settings/section_performance");
            RegisterLabel(GetNode<Label>(Display + "VSyncRow/VSyncLabel"), "settings/vsync");
            RegisterLabel(GetNode<Label>(Display + "FpsRow/FpsLabel"), "settings/max_fps");
            RegisterSection(Mods + "ModListPanel/Column/ModsTitle", "settings/mod_list_title");
            RegisterLabel(GetNode<Label>(Mods + "Detail/SettingsScroll/SettingsContainer/Placeholder"), "settings/mods_no_settings");

            _soundsToggle.Toggled += OnSoundsToggled;
            _musicToggle.Toggled += OnMusicToggled;
            _ambientToggle.Toggled += OnAmbientToggled;
            _soundsVolume.ValueChanged += OnSoundsVolumeChanged;
            _musicVolume.ValueChanged += OnMusicVolumeChanged;
            _ambientVolume.ValueChanged += OnAmbientVolumeChanged;
            _langSelect.ItemSelected += OnLanguageSelected;

            _windowModeSelect.ItemSelected += OnWindowSettingChanged;
            _resolutionSelect.ItemSelected += OnWindowSettingChanged;
            _monitorSelect.ItemSelected += OnMonitorSelected;
            _fpsSelect.ItemSelected += OnRenderingSettingChanged;
            _vsyncToggle.Toggled += OnVsyncToggled;

            LoadSettingsToUI();
            BuildModList();
            ShowTab(0);
            _initialized = true;
        }

        /// <summary>Index of the visible tab: 0 general, 1 sound, 2 display, 3 mod settings.</summary>
        public int CurrentTab => _tab;

        /// <summary>Shows tab <paramref name="index"/>: its page, the gold tab button and the footer reset that belongs to it.</summary>
        public void ShowTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _pages.Length - 1);
            for (var i = 0; i < _pages.Length; i++)
            {
                _pages[i].Visible = i == _tab;
                _tabButtons[i].SetPressedNoSignal(i == _tab);
            }
            UpdateResetButton();
        }

        private void OnTabPressed(BaseButton button) => ShowTab(Array.IndexOf(_tabButtons, button));

        /// <summary>The footer reset acts on the visible tab: display settings, or the selected mod's settings.</summary>
        private void UpdateResetButton()
        {
            _resetButton.Visible = _tab == 2 || (_tab == 3 && !string.IsNullOrEmpty(_selectedModId));
            _resetButton.Text = TranslationServer.Translate(_tab == 2 ? "settings/display_reset" : "settings/mods_reset");
        }

        private void OnResetPressed()
        {
            if (_tab == 2)
                OnResetDisplay();
            else if (_tab == 3)
                OnResetModSettings();
        }

        private void RegisterLabel(Label label, string key)
        {
            if (label != null)
                _labelKeys[label] = key;
        }

        /// <summary>Section caption: small muted caps, as section titles everywhere in the kit.</summary>
        private void RegisterSection(string path, string key)
        {
            var label = GetNode<Label>(path);
            label.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            label.AddThemeColorOverride("font_color", UiTokens.Muted);
            RegisterLabel(label, key);
        }

        private static void StyleHint(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            label.AddThemeColorOverride("font_color", UiTokens.Faint);
        }

        private static StyleBoxFlat ListPanelStyle()
        {
            var style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line);
            style.ContentMarginLeft = style.ContentMarginRight = UiTokens.Space5;
            style.ContentMarginTop = style.ContentMarginBottom = UiTokens.Space5;
            return style;
        }

        private void RefreshUIText()
        {
            foreach (var kv in _labelKeys)
            {
                if (IsInstanceValid(kv.Key))
                    kv.Key.Text = TranslationServer.Translate(kv.Value);
            }
            for (var i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].Text = TranslationServer.Translate(TabKeys[i]);
            GetNode<Button>(Header + "CloseButton").TooltipText = TranslationServer.Translate("settings/back");
            UpdateResetButton();
            PopulateLangOptions();
            PopulateDisplayOptions();
        }

        private void LoadSettingsToUI()
        {
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            _soundsToggle.ButtonPressed = s.SoundsEnabled;
            _musicToggle.ButtonPressed = s.MusicsEnabled;
            _ambientToggle.ButtonPressed = s.AmbientEnabled;
            _soundsVolume.Value = s.SoundsVolume;
            _musicVolume.Value = s.MusicsVolume;
            _ambientVolume.Value = s.AmbientVolume;
            _soundsVolumeValue.Text = $"{s.SoundsVolume}%";
            _musicVolumeValue.Text = $"{s.MusicsVolume}%";
            _ambientVolumeValue.Text = $"{s.AmbientVolume}%";

            UpdateSlidersEditable();
            PopulateLangOptions();
            RefreshUIText();
            LoadDisplayToUI(s);
        }

        /// <summary>Shows the saved display settings; the window size list is built for the saved monitor.</summary>
        private void LoadDisplayToUI(SettingsData s)
        {
            _windowModeSelect.Selected = DisplayOptions.WindowModeIndex(s.WindowMode);
            _monitorSelect.Selected = DisplaySettingsService.ClampScreen(s.CurrentScreen);
            FillWindowSizes(new Vector2I(s.ResolutionWidth, s.ResolutionHeight));
            _vsyncToggle.ButtonPressed = s.VsyncEnabled;
            _fpsSelect.Selected = DisplayOptions.FrameRateIndex(s.MaxFps);
            UpdateWindowSizeEditable();
        }

        private void PopulateLangOptions()
        {
            _langSelect.Clear();
            foreach (var key in LanguageKeys)
                _langSelect.AddItem(TranslationServer.Translate(key));

            var s = GameData.Instance?.Settings;
            if (s == null) return;
            _langSelect.Selected = Mathf.Max(0, System.Array.IndexOf(LanguageCode.Supported, LanguageCode.Normalize(s.Language) ?? LanguageCode.Supported[0]));
        }

        /// <summary>Re-fills the translated display lists, keeping the selected items.</summary>
        private void PopulateDisplayOptions()
        {
            var mode = _windowModeSelect.Selected;
            var monitor = _monitorSelect.Selected;
            var fps = _fpsSelect.Selected;

            _windowModeSelect.Clear();
            foreach (var key in DisplayOptions.WindowModeKeys)
                _windowModeSelect.AddItem(TranslationServer.Translate(key));

            _monitorSelect.Clear();
            for (var i = 0; i < DisplaySettingsService.ScreenCount; i++)
            {
                var size = DisplayServer.ScreenGetSize(i);
                var key = i == DisplayServer.GetPrimaryScreen() ? "settings/monitor_screen_primary" : "settings/monitor_screen";
                _monitorSelect.AddItem(string.Format(TranslationServer.Translate(key), i + 1, size.X, size.Y));
            }

            _fpsSelect.Clear();
            foreach (var rate in DisplayOptions.FrameRates)
                _fpsSelect.AddItem(rate == 0 ? TranslationServer.Translate("settings/max_fps_unlimited") : string.Format(TranslationServer.Translate("settings/max_fps_value"), rate));

            _windowModeSelect.Selected = Mathf.Clamp(mode, 0, DisplayOptions.WindowModes.Length - 1);
            _monitorSelect.Selected = Mathf.Clamp(monitor, 0, DisplaySettingsService.ScreenCount - 1);
            _fpsSelect.Selected = Mathf.Clamp(fps, 0, DisplayOptions.FrameRates.Length - 1);
        }

        /// <summary>Fills the window sizes of the chosen monitor, keeping <paramref name="wanted"/> (or the nearest smaller size) selected.</summary>
        private void FillWindowSizes(Vector2I wanted)
        {
            _windowSizes = DisplayOptions.WindowSizes(DisplayServer.ScreenGetSize(Mathf.Max(0, _monitorSelect.Selected)), wanted);
            _resolutionSelect.Clear();
            foreach (var size in _windowSizes)
                _resolutionSelect.AddItem($"{size.X}×{size.Y}");
            _resolutionSelect.Selected = DisplayOptions.WindowSizeIndex(_windowSizes, wanted);
        }

        private Vector2I SelectedWindowSize()
        {
            var index = _resolutionSelect.Selected;
            return index >= 0 && index < _windowSizes.Count ? _windowSizes[index] : DisplayServer.WindowGetSize();
        }

        private DisplayServer.WindowMode SelectedWindowMode() =>
            DisplayOptions.WindowModes[Mathf.Clamp(_windowModeSelect.Selected, 0, DisplayOptions.WindowModes.Length - 1)];

        private void UpdateWindowSizeEditable()
        {
            var windowed = DisplayOptions.UsesWindowSize((int)SelectedWindowMode());
            _resolutionSelect.Disabled = !windowed;
            _resHint.Visible = !windowed;
        }

        private void ApplyAndSave()
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            s.SoundsEnabled = _soundsToggle.ButtonPressed;
            s.MusicsEnabled = _musicToggle.ButtonPressed;
            s.AmbientEnabled = _ambientToggle.ButtonPressed;
            s.SoundsVolume = (int)_soundsVolume.Value;
            s.MusicsVolume = (int)_musicVolume.Value;
            s.AmbientVolume = (int)_ambientVolume.Value;
            s.Save();
        }

        /// <summary>Saves the window part (mode, monitor, size) and moves the window there.</summary>
        private void ApplyWindowAndSave()
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var size = SelectedWindowSize();
            s.WindowMode = (int)SelectedWindowMode();
            s.CurrentScreen = Mathf.Max(0, _monitorSelect.Selected);
            s.ResolutionWidth = size.X;
            s.ResolutionHeight = size.Y;
            DisplaySettingsService.ApplyWindow(s);
            s.Save();
        }

        /// <summary>Saves V-Sync and the frame rate cap and applies them.</summary>
        private void ApplyRenderingAndSave()
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            s.VsyncEnabled = _vsyncToggle.ButtonPressed;
            s.MaxFps = DisplayOptions.FrameRates[Mathf.Clamp(_fpsSelect.Selected, 0, DisplayOptions.FrameRates.Length - 1)];
            DisplaySettingsService.ApplyRendering(s);
            s.Save();
        }

        private void OnSoundsToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }
        private void OnMusicToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }
        private void OnAmbientToggled(bool on) { UpdateSlidersEditable(); ApplyAndSave(); }

        private void OnSoundsVolumeChanged(double v) { _soundsVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }
        private void OnMusicVolumeChanged(double v) { _musicVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }
        private void OnAmbientVolumeChanged(double v) { _ambientVolumeValue.Text = $"{(int)v}%"; ApplyAndSave(); }

        private void OnWindowSettingChanged(long idx)
        {
            UpdateWindowSizeEditable();
            ApplyWindowAndSave();
        }

        private void OnMonitorSelected(long idx)
        {
            FillWindowSizes(SelectedWindowSize());
            ApplyWindowAndSave();
        }

        private void OnRenderingSettingChanged(long idx) => ApplyRenderingAndSave();
        private void OnVsyncToggled(bool on) => ApplyRenderingAndSave();

        /// <summary>Returns the display settings to the game defaults (game_config.json), applies and saves them.</summary>
        private void OnResetDisplay()
        {
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var defaults = new SettingsData();
            s.WindowMode = defaults.WindowMode;
            s.CurrentScreen = defaults.CurrentScreen;
            s.ResolutionWidth = defaults.ResolutionWidth;
            s.ResolutionHeight = defaults.ResolutionHeight;
            s.VsyncEnabled = defaults.VsyncEnabled;
            s.MaxFps = defaults.MaxFps;
            DisplaySettingsService.Apply(s);
            s.Save();

            _initialized = false;
            LoadDisplayToUI(s);
            _initialized = true;
        }

        private void OnLanguageSelected(long idx)
        {
            if (!_initialized) return;
            var s = GameData.Instance?.Settings;
            if (s == null) return;

            var lang = LanguageCode.Supported[Mathf.Clamp((int)idx, 0, LanguageCode.Supported.Length - 1)];
            if (LanguageCode.Normalize(s.Language) == lang) return;

            s.Language = lang;
            s.Save();
            LocaleLoader.LoadCurrentLanguage();
            RefreshUIText();
            LanguageChanged?.Invoke();
        }

        /// <inheritdoc cref="IMenuPanel" />
        public void Open()
        {
            Visible = true;
            ShowTab(_tab);
        }

        private void OnBackPressed() => Visible = false;

        private void UpdateSlidersEditable()
        {
            _soundsVolume.Editable = _soundsToggle.ButtonPressed;
            _musicVolume.Editable = _musicToggle.ButtonPressed;
            _ambientVolume.Editable = _ambientToggle.ButtonPressed;
        }

        private void BuildModList()
        {
            foreach (var child in _modListContainer.GetChildren())
                child.QueueFree();

            var modsWithSettings = ModSettingsRegistry.GetModsWithSettings();

            if (modsWithSettings.Count == 0)
                return;

            var group = new ButtonGroup();
            foreach (var modId in modsWithSettings)
            {
                var btn = new Button
                {
                    Text = ModSettingsRegistry.GetDisplayTitle(modId),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    Alignment = HorizontalAlignment.Left,
                    ToggleMode = true,
                    ButtonGroup = group,
                    FocusMode = FocusModeEnum.None,
                };
                CthangoverTheme.ApplyTab(btn);
                btn.Pressed += () => OnModSelected(modId);
                _modListContainer.AddChild(btn);
            }

            ((Button)_modListContainer.GetChild(0)).SetPressedNoSignal(true);
            OnModSelected(modsWithSettings[0]);
        }

        private void OnModSelected(string modId)
        {
            _selectedModId = modId;
            _selectedModName.Text = ModSettingsRegistry.GetDisplayTitle(modId);
            BuildModSettingsPanel(modId);
            UpdateResetButton();
        }

        private void BuildModSettingsPanel(string modId)
        {
            _controlKeys.Clear();

            foreach (var child in _settingsContainer.GetChildren())
                child.QueueFree();

            _settingsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var definitions = ModSettingsRegistry.GetDefinitions(modId);
            if (definitions.Count == 0)
            {
                var placeholder = new Label
                {
                    Text = TranslationServer.Translate("settings/mods_no_settings"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                _settingsContainer.AddChild(placeholder);
                return;
            }

            var currentBlob = ModSettingsRegistry.WriteCurrentValues(modId);

            foreach (var def in definitions)
            {
                var row = new HBoxContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 40),
                };
                row.AddThemeConstantOverride("separation", 12);

                var label = new Label
                {
                    Text = TranslationServer.Translate(def.Name),
                    CustomMinimumSize = new Vector2(260, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                row.AddChild(label);

                if (def.Type == SettingType.Action)
                {
                    AddActionControls(row, modId, def);
                    _settingsContainer.AddChild(row);
                    continue;
                }

                Control control = CreateControlForType(def);
                ApplyControlValue(control, def, currentBlob);
                _controlKeys[control] = def.Key;

                switch (def.Type)
                {
                    case SettingType.Bool:
                        if (control is CheckButton checkbox)
                            checkbox.Toggled += _ => OnModSettingChanged();
                        break;
                    case SettingType.Integer:
                    case SettingType.Float:
                        if (control is SpinBox spin)
                            spin.ValueChanged += _ => OnModSettingChanged();
                        break;
                    case SettingType.String:
                        if (control is LineEdit lineEdit)
                            lineEdit.TextChanged += _ => OnModSettingChanged();
                        break;
                    case SettingType.Enum:
                        if (control is OptionButton option)
                            option.ItemSelected += _ => OnModSettingChanged();
                        break;
                    case SettingType.Slider:
                        if (control is HSlider slider)
                        {
                            slider.ValueChanged += _ =>
                            {
                                var val = (double)slider.GetMeta("step", 1.0).AsDouble();
                                var step = val;
                                var stepped = Mathf.Round(slider.Value / step) * step;
                                slider.Value = stepped;
                                OnModSettingChanged();
                            };
                        }
                        break;
                }

                row.AddChild(control);

                if (def.Type == SettingType.Slider)
                {
                    var valLabel = new Label
                    {
                        Text = FormatSliderValue(currentBlob, def),
                        CustomMinimumSize = new Vector2(48, 0),
                        HorizontalAlignment = HorizontalAlignment.Right,
                    };
                    row.AddChild(valLabel);
                }

                _settingsContainer.AddChild(row);
            }

            var timer = new Timer { OneShot = true, Autostart = true, WaitTime = 0.01 };
            timer.Timeout += () =>
            {
                timer.QueueFree();
                LogModSettingsSizes();
            };
            _settingsContainer.AddChild(timer);
        }

        private void LogModSettingsSizes()
        {
            if (_settingsContainer == null)
                return;

            GameLogger.Log("MODS_SETTINGS_UI", $"SettingsContainer size: {_settingsContainer.Size}", LogLevel.Debug);
            foreach (var child in _settingsContainer.GetChildren())
            {
                if (child is HBoxContainer row)
                {
                    GameLogger.Log("MODS_SETTINGS_UI", $"  Row size: {row.Size}", LogLevel.Debug);
                    foreach (var rowChild in row.GetChildren())
                    {
                        if (rowChild is HSlider slider)
                            GameLogger.Log("MODS_SETTINGS_UI", $"    HSlider size: {slider.Size}, min_size: {slider.CustomMinimumSize}", LogLevel.Debug);
                        else if (rowChild is Control c)
                            GameLogger.Log("MODS_SETTINGS_UI", $"    {rowChild.GetType().Name} size: {c.Size}", LogLevel.Debug);
                    }
                }
            }
        }

        /// <summary>
        /// Action setting: a button (with an optional confirmation) that runs
        /// <see cref="ModSettingsRegistry.RunAction"/> and shows the result next
        /// to it. Actions store no value, so they stay out of <c>_controlKeys</c>.
        /// </summary>
        private void AddActionControls(HBoxContainer row, string modId, ModSettingDefinition def)
        {
            var button = new Button { Text = TranslationServer.Translate(def.ActionText ?? def.Name) };
            var result = new Label { VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

            void Run() => result.Text = TranslationServer.Translate(ModSettingsRegistry.RunAction(modId, def.Key) ?? string.Empty);

            button.Pressed += () =>
            {
                if (string.IsNullOrEmpty(def.ConfirmText))
                {
                    Run();
                    return;
                }

                var dialog = new ConfirmationDialog
                {
                    DialogText = TranslationServer.Translate(def.ConfirmText),
                    Exclusive = true,
                };
                dialog.Confirmed += () =>
                {
                    Run();
                    dialog.QueueFree();
                };
                dialog.Canceled += dialog.QueueFree;
                AddChild(dialog);
                dialog.PopupCentered();
            };

            row.AddChild(button);
            row.AddChild(result);
        }

        private static Control CreateControlForType(ModSettingDefinition def)
        {
            switch (def.Type)
            {
                case SettingType.Bool:
                    return new CheckButton();
                case SettingType.Integer:
                    return new SpinBox
                    {
                        AllowGreater = true,
                        AllowLesser = true,
                        Step = 1.0,
                        MinValue = int.MinValue,
                        MaxValue = int.MaxValue,
                    };
                case SettingType.Float:
                    return new SpinBox
                    {
                        AllowGreater = true,
                        AllowLesser = true,
                        Step = 0.1,
                        MinValue = double.MinValue,
                        MaxValue = double.MaxValue,
                    };
                case SettingType.String:
                    return new LineEdit();
                case SettingType.Enum:
                {
                    var opt = new OptionButton();
                    if (def.Options != null)
                    {
                        foreach (var kv in def.Options)
                            opt.AddItem(TranslationServer.Translate(kv.Value));
                    }
                    return opt;
                }
                case SettingType.Slider:
                {
                    var slider = new HSlider
                    {
                        MinValue = def.Min,
                        MaxValue = def.Max,
                        Step = 0.01,
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                        SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    };
                    slider.SetMeta("step", Variant.From(def.Step > 0 ? def.Step : 1.0));
                    return slider;
                }
                default:
                    return new HBoxContainer();
            }
        }

        private static void ApplyControlValue(Control control, ModSettingDefinition def, DataBlob blob)
        {
            switch (def.Type)
            {
                case SettingType.Bool:
                    if (control is CheckButton checkbox)
                        checkbox.ButtonPressed = blob.GetString(def.Key, def.DefaultValue ?? "false").ToLowerInvariant() == "true";
                    break;
                case SettingType.Integer:
                    if (control is SpinBox spin)
                    {
                        var defVal = int.TryParse(blob.GetString(def.Key, def.DefaultValue ?? "0"), out var parsed) ? parsed : 0;
                        spin.Value = blob.GetInt(def.Key, defVal);
                    }
                    break;
                case SettingType.Float:
                    if (control is SpinBox spinFloat)
                    {
                        var defVal = float.TryParse(blob.GetString(def.Key, def.DefaultValue ?? "0"), out var parsed) ? parsed : 0f;
                        spinFloat.Value = blob.GetFloat(def.Key, defVal);
                    }
                    break;
                case SettingType.String:
                    if (control is LineEdit lineEdit)
                        lineEdit.Text = blob.GetString(def.Key, def.DefaultValue ?? "");
                    break;
                case SettingType.Enum:
                    if (control is OptionButton opt)
                    {
                        var current = blob.GetString(def.Key, def.DefaultValue ?? "");
                        opt.Selected = GetOptionIndex(def, current);
                    }
                    break;
                case SettingType.Slider:
                    if (control is HSlider slider)
                    {
                        var defVal = float.TryParse(blob.GetString(def.Key, def.DefaultValue ?? "0"), out var parsed) ? parsed : 0f;
                        slider.Value = blob.GetFloat(def.Key, defVal);
                    }
                    break;
            }
        }

        private static string ReadControlValue(Control control, SettingType type, ModSettingDefinition def)
        {
            switch (type)
            {
                case SettingType.Bool:
                    return control is CheckButton checkbox ? checkbox.ButtonPressed.ToString().ToLowerInvariant() : "false";
                case SettingType.Integer:
                    return control is SpinBox spin ? ((int)spin.Value).ToString() : "0";
                case SettingType.Float:
                    return control is SpinBox spinFloat ? spinFloat.Value.ToString("F4") : "0";
                case SettingType.String:
                    return control is LineEdit lineEdit ? lineEdit.Text : "";
                case SettingType.Enum:
                    if (control is OptionButton opt && def.Options != null && opt.Selected >= 0 && opt.Selected < def.Options.Count)
                    {
                        var idx = 0;
                        foreach (var kv in def.Options)
                        {
                            if (idx == opt.Selected)
                                return kv.Key;
                            idx++;
                        }
                    }
                    return def.DefaultValue ?? "";
                case SettingType.Slider:
                    return control is HSlider slider ? slider.Value.ToString("F4") : "0";
                default:
                    return "";
            }
        }

        private static string FormatSliderValue(DataBlob blob, ModSettingDefinition def)
        {
            var value = blob.GetFloat(def.Key, float.TryParse(def.DefaultValue, out var d) ? d : 0f);
            var step = def.Step > 0 ? def.Step : 1f;
            if (step >= 1f)
                return $"{(int)value}";
            return $"{value:F2}";
        }

        private static int GetOptionIndex(ModSettingDefinition def, string key)
        {
            if (def.Options == null)
                return 0;
            var idx = 0;
            foreach (var kv in def.Options)
            {
                if (kv.Key == key)
                    return idx;
                idx++;
            }
            return 0;
        }

        private void OnModSettingChanged()
        {
            if (!_initialized || string.IsNullOrEmpty(_selectedModId))
                return;

            var blob = new DataBlob();
            var definitions = ModSettingsRegistry.GetDefinitions(_selectedModId);

            foreach (var def in definitions)
            {
                var control = FindControlForKey(def.Key);
                if (control == null)
                    continue;
                var value = ReadControlValue(control, def.Type, def);
                blob.SetString(def.Key, value);

                if (def.Type == SettingType.Slider)
                    UpdateSliderValueLabel(control);
            }

            ModSettingsRegistry.SaveModSettings(_selectedModId, blob);
            ModSettingsRegistry.ApplyValues(_selectedModId, blob);
        }

        private Control FindControlForKey(string key)
        {
            foreach (var kv in _controlKeys)
            {
                if (kv.Value == key)
                    return kv.Key;
            }
            return null;
        }

        private void UpdateSliderValueLabel(Control slider)
        {
            if (!(slider is HSlider hslider))
                return;

            var parent = slider.GetParent();
            if (parent == null)
                return;

            var lastChild = parent.GetChild(parent.GetChildCount() - 1);
            if (lastChild is Label valLabel)
            {
                var step = (double)slider.GetMeta("step", 1.0).AsDouble();
                if (step >= 1.0)
                    valLabel.Text = $"{(int)hslider.Value}";
                else
                    valLabel.Text = $"{hslider.Value:F2}";
            }
        }

        private void OnResetModSettings()
        {
            if (string.IsNullOrEmpty(_selectedModId))
                return;

            ModSettingsRegistry.DeleteModSettings(_selectedModId);
            BuildModSettingsPanel(_selectedModId);
        }
    }
}
