using NUnit.Framework;

namespace Exchanger.Tests;

[TestFixture]
public sealed class ProjectAssetSmokeTests
{
    private static readonly string[] ScenePaths =
    [
        "src/scenes/App/App.tscn",
        "src/scenes/HomeScreen/HomeScreen.tscn",
        "src/scenes/CashPaymentScreen/CashPaymentScreen.tscn",
        "src/scenes/CardAmountScreen/CardAmountScreen.tscn",
        "src/scenes/CardCustomAmountScreen/CardCustomAmountScreen.tscn",
        "src/scenes/CardTerminalScreen/CardTerminalScreen.tscn",
        "src/scenes/SuccessScreen/SuccessScreen.tscn",
        "src/scenes/ErrorScreen/ErrorScreen.tscn",
        "src/scenes/SettingsScreen/SettingsScreen.tscn",
        "src/scenes/ServiceAccessScreen/ServiceAccessScreen.tscn",
        "src/ui/ConnectionStatusIndicator/ConnectionStatusIndicator.tscn",
    ];

    [TestCaseSource(nameof(ScenePaths))]
    public void SceneFile_ExistsAndHasGodotHeader(string relativePath)
    {
        string path = Path.Combine(ProjectPaths.FindRoot(), relativePath);

        Assert.That(File.Exists(path), Is.True, $"Не найдена сцена {relativePath}");
        Assert.That(File.ReadLines(path).FirstOrDefault(), Does.StartWith("[gd_scene"));
    }

    [TestCase("src/scenes/HomeScreen/HomeScreen.tscn", "theme_type_variation = &\"PanelTitle\"")]
    [TestCase("src/scenes/CashPaymentScreen/CashPaymentScreen.tscn", "theme_type_variation = &\"PrimaryButton\"")]
    [TestCase("src/scenes/CardAmountScreen/CardAmountScreen.tscn", "theme_type_variation = &\"WhitePanel\"")]
    [TestCase("src/scenes/CardCustomAmountScreen/CardCustomAmountScreen.tscn", "theme_type_variation = &\"CompactButton\"")]
    [TestCase("src/scenes/CardTerminalScreen/CardTerminalScreen.tscn", "theme_type_variation = &\"PanelText\"")]
    [TestCase("src/scenes/SuccessScreen/SuccessScreen.tscn", "theme_type_variation = &\"PanelTitle\"")]
    [TestCase("src/scenes/ErrorScreen/ErrorScreen.tscn", "theme_type_variation = &\"ErrorText\"")]
    [TestCase("src/scenes/ServiceAccessScreen/ServiceAccessScreen.tscn", "theme_type_variation = &\"SecondaryButton\"")]
    public void UserAndServiceScene_DeclaresSemanticThemeContract(string relativePath, string marker)
    {
        string content = File.ReadAllText(Path.Combine(ProjectPaths.FindRoot(), relativePath));

        Assert.That(content, Does.Contain(marker));
    }

    [Test]
    public void SettingsScreen_AppliesSemanticThemeContractOnlyToFallbackVisuals()
    {
        string content = File.ReadAllText(Path.Combine(
            ProjectPaths.FindRoot(),
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(content, Does.Contain("if (!UsesSelfContainedThemeScene)"));
            Assert.That(content, Does.Contain("ApplyFallbackThemeSemantics();"));
            Assert.That(content, Does.Contain("ThemeSemanticTypes.SectionTabButton"));
            Assert.That(content, Does.Contain("ThemeSemanticTypes.DangerButton"));
            Assert.That(content, Does.Contain("case CheckButton:"));
            Assert.That(content, Does.Contain("case OptionButton:"));
        }
    }

    [Test]
    public void SettingsScreen_HandlesSingleTouchAndHeldMouseDragScrolling()
    {
        string root = ProjectPaths.FindRoot();
        string project = File.ReadAllText(Path.Combine(root, "project.godot"));
        string settings = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project, Does.Contain("pointing/emulate_touch_from_mouse=true"));
            Assert.That(settings, Does.Contain("case InputEventScreenTouch touch when touch.Index == 0:"));
            Assert.That(settings, Does.Contain("case InputEventScreenDrag drag when drag.Index == _activeScrollTouchIndex:"));
            Assert.That(settings, Does.Contain("case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton:"));
            Assert.That(settings, Does.Contain("_scroll.ScrollVertical = _scrollDragStartOffset - Mathf.RoundToInt(dragDistance);"));
            Assert.That(settings, Does.Contain("Mathf.Abs(dragDistance) < ScrollDragDeadzonePixels"));
        }
    }

    [Test]
    public void SettingsScreen_HasFixedSaveAndExitFooterOutsideScrollableContent()
    {
        string root = ProjectPaths.FindRoot();
        string scene = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.tscn"));
        string settings = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scene, Does.Contain("[node name=\"Footer\" type=\"PanelContainer\" parent=\".\""));
            Assert.That(scene, Does.Contain("[node name=\"SaveAndExitButton\" type=\"Button\" parent=\"Footer\""));
            Assert.That(scene, Does.Not.Contain("parent=\"SafeMargin/Scroll/Content/Footer\""));
            Assert.That(scene, Does.Contain("text = \"СОХРАНИТЬ И ВЫЙТИ\""));
            Assert.That(scene, Does.Contain("custom_minimum_size = Vector2(0, 1100)"));
            Assert.That(settings, Does.Contain("SaveAndExitRequested?.Invoke();"));
            Assert.That(settings, Does.Contain("_footer.GetGlobalRect().HasPoint(pointerPosition)"));
            Assert.That(settings, Does.Contain("_onScreenKeyboard.VisibilityChanged += OnScreenKeyboardVisibilityChanged;"));
            Assert.That(settings, Does.Contain("_saveAndExitButton.Disabled = isVisible;"));
            Assert.That(settings, Does.Contain("_saveAndExitButton.MouseFilter = isVisible"));
            Assert.That(settings, Does.Contain("_footer.MouseFilter = isVisible"));
            Assert.That(settings, Does.Contain("if (_onScreenKeyboard?.IsVisible ?? false)"));
        }
    }

    [Test]
    public void PhysicalServiceButton_BypassesOnlyTheOnScreenButtonGuard()
    {
        string root = ProjectPaths.FindRoot();
        string app = File.ReadAllText(Path.Combine(root, "src/scenes/App/App.cs"));
        string settings = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(app, Does.Contain("_peripheryController.ServiceRequested += OnPhysicalServiceRequested;"));
            Assert.That(app, Does.Contain("if (_settingsScreen.TryFinishEditing())"));
            Assert.That(settings, Does.Contain("private void OnSaveAndExitPressed()"));
            Assert.That(settings, Does.Contain("if (_onScreenKeyboard?.IsVisible ?? false)"));
            Assert.That(settings, Does.Contain("_onScreenKeyboard?.Hide();"));
        }
    }

    [Test]
    public void ServiceManualAddition_OpensRegisteredNumericKeyboard()
    {
        string root = ProjectPaths.FindRoot();
        string settings = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));
        string keyboard = File.ReadAllText(Path.Combine(
            root,
            "src/ui/OnScreenKeyboard/OnScreenKeyboardController.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Does.Contain("ShowServiceDialogNumericKeyboard();"));
            Assert.That(settings, Does.Contain("_onScreenKeyboard?.ShowNumericField(_serviceDialogAmount);"));
            Assert.That(keyboard, Does.Contain("public void ShowNumericField(SpinBox spinBox)"));
            Assert.That(keyboard, Does.Contain("if (!_scroll.IsAncestorOf(field))"));
            Assert.That(keyboard, Does.Contain("edit.EmitSignal(LineEdit.SignalName.TextChanged, edit.Text);"));
            Assert.That(keyboard, Does.Contain("ApplyTextMutation(edit"));
        }
    }

    [TestCase("src/scenes/SuccessScreen/SuccessScreen.cs")]
    [TestCase("src/scenes/CardAmountScreen/CardAmountScreen.cs")]
    [TestCase("src/scenes/CardTerminalScreen/CardTerminalScreen.cs")]
    [TestCase("src/scenes/CashPaymentScreen/CashPaymentSummaryFormatter.cs")]
    [TestCase("src/scenes/CardCustomAmountScreen/CardCustomAmountDisplayFormatter.cs")]
    public void CustomerFacingTokenCounts_UseSharedRussianDeclension(string relativePath)
    {
        string content = File.ReadAllText(Path.Combine(ProjectPaths.FindRoot(), relativePath));

        Assert.That(content, Does.Contain("TokenCountFormatter.Format"));
    }

    [Test]
    public void SelfContainedThemeScreens_AreNotRestyledByHostControllers()
    {
        string root = ProjectPaths.FindRoot();
        string themeManager = File.ReadAllText(Path.Combine(
            root,
            "src/core/ThemeManager/ThemeManager.cs"));
        string bindableScreen = File.ReadAllText(Path.Combine(
            root,
            "src/core/ThemeManager/ThemeBindableScreen.cs"));
        string settings = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));
        string cardKeypad = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/CardCustomAmountScreen/CardCustomAmountScreen.cs"));
        string serviceKeypad = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/ServiceAccessScreen/ServiceAccessScreen.cs"));
        string home = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/HomeScreen/HomeScreen.cs"));
        string cardAmount = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/CardAmountScreen/CardAmountScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bindableScreen, Does.Contain("UsesSelfContainedThemeScene"));
            Assert.That(bindableScreen, Does.Not.Contain("themeView.Name ="));
            Assert.That(bindableScreen, Does.Not.Contain("themeView.SetAnchorsAndOffsetsPreset"));
            Assert.That(themeManager, Does.Contain("UsesSelfContainedThemeScene: true"));
            Assert.That(settings, Does.Not.Contain(".Modulate = show"));
            Assert.That(home, Does.Not.Contain("MountRuntimeComponent"));
            Assert.That(home, Does.Contain("home.scroll.content.stock_status.state"));
            Assert.That(home, Does.Not.Contain("Branding.Subtitle"));
            Assert.That(home, Does.Contain("SafeMargin/Scroll/Content/SpeechText"));
            Assert.That(home, Does.Contain("_speechTextLabel.Text = _settings.Branding.ShortText;"));
            Assert.That(home, Does.Contain("_speechTextLabel.Visible = _settings.MenuVisibility.ShowCustomText;"));
            Assert.That(home, Does.Contain("home.decoration.custom_text_block"));
            Assert.That(settings, Does.Contain("BrandingValidationField.ShortText"));
            Assert.That(settings, Does.Not.Contain("BrandingValidationField.Subtitle"));
            Assert.That(settings, Does.Contain("ПОКАЗЫВАТЬ ПОЛЬЗОВАТЕЛЬСКИЙ ТЕКСТ"));
            Assert.That(cardAmount, Does.Contain("preset_grid.slot_{index:00}"));
            Assert.That(settings, Does.Contain("bonus_panel.margin.content.rows"));
            Assert.That(settings, Does.Contain("advertisement_playlist_panel.margin.content.rows"));
            Assert.That(cardKeypad, Does.Contain("if (!UsesSelfContainedThemeScene)"));
            Assert.That(serviceKeypad, Does.Contain("if (!UsesSelfContainedThemeScene)"));
        }
    }

    [Test]
    public void TrustedThemeRuntime_AllowsScriptsAndArbitraryRuntimeNodes()
    {
        string root = ProjectPaths.FindRoot();
        string contract = File.ReadAllText(Path.Combine(
            root,
            "src/core/ThemeManager/ThemeSceneContract.cs"));
        string staticBackground = File.ReadAllText(Path.Combine(
            root,
            "src/ui/StaticBackground/StaticBackground.cs"));
        string visualSlot = File.ReadAllText(Path.Combine(
            root,
            "src/ui/ThemeVisualSlot/ThemeVisualSlot.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(contract, Does.Not.Contain("ScriptNotAllowed"));
            Assert.That(contract, Does.Not.Contain("GetScript()"));
            Assert.That(contract, Does.Not.Contain("ThemeDecorationBudget"));
            Assert.That(staticBackground, Does.Not.Contain("ThemeDecorationBudget"));
            Assert.That(visualSlot, Does.Not.Contain("ThemeDecorationBudget"));
        }
    }

    [Test]
    public void SettingsScreen_ExposesOnlyOwnerFacingSections()
    {
        string root = ProjectPaths.FindRoot();
        string scene = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.tscn"));
        string code = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scene, Does.Not.Contain("НАСТРОЙКИ ВЛАДЕЛЬЦА АВТОМАТА"));
            Assert.That(scene, Does.Not.Contain("Готовые суммы и ручной ввод используют общий диапазон и шаг."));
            Assert.That(scene, Does.Not.Contain("Короткий сигнал не запускает оплату, промо или выдачу жетонов."));
            Assert.That(scene, Does.Not.Contain("FALLBACK-ТЕКСТ"));
            Assert.That(scene, Does.Not.Contain("text = \"ТАЙМАУТЫ\""));
            Assert.That(scene, Does.Contain("text = \"ТЕКСТ НА ГЛАВНОМ ЭКРАНЕ\""));
            Assert.That(scene, Does.Contain("text = \"СВОЙ ТЕКСТ • ДО 48 ЗНАКОВ\""));
            Assert.That(scene, Does.Not.Contain("text = \"КОРОТКИЙ ТЕКСТ • ДО 48 ЗНАКОВ\""));
            Assert.That(scene, Does.Contain("text = \"ТЕКСТ ВМЕСТО ПРОМО • ДО 120 ЗНАКОВ\""));
            Assert.That(scene, Does.Contain("text = \"ВРЕМЯ ОЖИДАНИЯ\""));
            Assert.That(scene, Does.Contain("text = \"ОБЩИЕ\""));
            Assert.That(scene, Does.Contain("text = \"ЗВУК\""));
            Assert.That(scene, Does.Contain("text = \"ПРОМО\""));
            Assert.That(scene, Does.Contain("text = \"ТАРИФЫ\""));
            Assert.That(scene, Does.Contain("text = \"ОПЛАТА\""));
            Assert.That(scene, Does.Contain("text = \"СЕРВИС\""));
            Assert.That(scene, Does.Contain("text = \"УЧЁТ ЖЕТОНОВ\""));
            Assert.That(scene, Does.Contain("text = \"УЧИТЫВАТЬ ОСТАТОК ЖЕТОНОВ\""));
            Assert.That(scene, Does.Contain("text = \"НЕ ПРОДАВАТЬ БОЛЬШЕ ОСТАТКА\""));
            Assert.That(scene, Does.Contain("ServiceInventoryPanel/Margin/Content/Hopper1"));
            Assert.That(scene, Does.Contain("ServiceInventoryPanel/Margin/Content/Hopper2"));
            Assert.That(scene, Does.Contain("text = \"ХОППЕР 1 — ОСТАТОК: 0 ЖЕТОНОВ\""));
            Assert.That(scene, Does.Contain("text = \"ХОППЕР 2 — ОСТАТОК: 0 ЖЕТОНОВ\""));
            Assert.That(scene, Does.Contain("text = \"ПЕРЕСЧИТАТЬ\""));
            Assert.That(scene, Does.Contain("text = \"ДОБАВИТЬ\""));
            Assert.That(code, Does.Contain("SetTokenInventoryCounts(int hopper1Count, int hopper2Count)"));
            Assert.That(code, Does.Contain("SetConfiguredHoppers(bool hopper1Configured, bool hopper2Configured)"));
            Assert.That(code, Does.Contain("_serviceHopper1RecountButton.Visible = enabled && _hopper1Configured;"));
            Assert.That(code, Does.Contain("_serviceHopper2RecountButton.Visible = enabled && _hopper2Configured;"));
            Assert.That(code, Does.Contain("_serviceInventoryCount.Visible = enabled;"));
            Assert.That(code, Does.Contain("_purchaseLimitEnabled.Visible = enabled;"));
            Assert.That(code, Does.Contain("TokenInventoryPolicy.FormatHopperExactCount(1, hopper1)"));
            Assert.That(code, Does.Contain("TokenInventoryPolicy.FormatHopperExactCount(2, hopper2)"));
            Assert.That(code, Does.Contain("_appearanceSectionButton.Visible = false;"));
            Assert.That(code, Does.Contain("_equipmentSectionButton.Visible = false;"));
            Assert.That(code, Does.Contain("_diagnosticsSectionButton.Visible = false;"));
            Assert.That(code, Does.Contain("_applicationName.Visible = false;"));
            Assert.That(code, Does.Contain("_brandingPreviewPanel.Visible = false;"));
            Assert.That(code, Does.Contain("BrandingValidationField.ShortText or BrandingValidationField.SupportPhone"));
            Assert.That(code, Does.Contain("_advertisementPreviewPanel.Visible = false;"));
            Assert.That(code, Does.Contain("_diagnosticsTestPanel.Visible = false;"));
        }
    }

    [Test]
    public void App_UpdatesExistingSupportLabelInsideExternalThemeFooter()
    {
        string root = ProjectPaths.FindRoot();
        string code = File.ReadAllText(Path.Combine(root, "src/scenes/App/App.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Does.Contain("footer.FindChild(\"Support\", recursive: true, owned: false) is Label supportLabel"));
            Assert.That(code, Does.Contain("supportLabel.Text = $\"ТЕХПОДДЕРЖКА:\\n{_settings.Branding.SupportPhone}\""));
            Assert.That(code, Does.Contain("не добавляя и не перестраивая её визуальные узлы"));
        }
    }

    [Test]
    public void SettingsScreen_ContainsThemeOwnedOnScreenKeyboards()
    {
        string root = ProjectPaths.FindRoot();
        string scene = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.tscn"));
        string code = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scene, Does.Contain("parent=\"SafeMargin/KeyboardOverlay/TextKeyboard\""));
            Assert.That(scene, Does.Contain("parent=\"SafeMargin/KeyboardOverlay/NumericKeyboard\""));
            Assert.That(scene, Does.Contain("name=\"Key_00\""));
            Assert.That(scene, Does.Contain("name=\"Key_32\""));
            Assert.That(scene, Does.Contain("name=\"Digit0\""));
            Assert.That(scene, Does.Contain("name=\"Digit9\""));
            Assert.That(scene, Does.Contain("name=\"HideButton\""));
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(scene, "name=\\\"Key_[0-9]{2}\\\"").Count,
                Is.EqualTo(33));
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(scene, "name=\\\"Digit[0-9]\\\"").Count,
                Is.EqualTo(10));
            Assert.That(code, Does.Contain("new OnScreenKeyboardController"));
            Assert.That(code, Does.Contain("RegisterEditableDescendants(_scroll)"));
            Assert.That(code, Does.Not.Contain("new Button { Text = \"й\""));
        }
    }

    [Test]
    public void CardCustomAmount_PrimaryEraseButtonDeletesOnlyLastDigit()
    {
        string root = ProjectPaths.FindRoot();
        string scene = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/CardCustomAmountScreen/CardCustomAmountScreen.tscn"));
        string code = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/CardCustomAmountScreen/CardCustomAmountScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scene, Does.Contain("text = \"СТЕРЕТЬ\""));
            Assert.That(code, Does.Contain("_clearButton.Pressed += Backspace;"));
            Assert.That(code, Does.Contain("_digits = _digits[..^1];"));
            Assert.That(code, Does.Not.Contain("_clearButton.Pressed += Clear;"));
        }
    }

    [Test]
    public void SettingsScreen_BonusAmountHasNoSuffixThatHidesDigits()
    {
        string code = File.ReadAllText(Path.Combine(
            ProjectPaths.FindRoot(),
            "src/scenes/SettingsScreen/SettingsScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Does.Not.Contain("Suffix = \" жет."));
            Assert.That(code, Does.Contain("slot.Bonus.Suffix = string.Empty;"));
        }
    }

    [Test]
    public void OnScreenKeyboardController_DoesNotConstructThemeVisualNodes()
    {
        string content = File.ReadAllText(Path.Combine(
            ProjectPaths.FindRoot(),
            "src/ui/OnScreenKeyboard/OnScreenKeyboardController.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(content, Does.Not.Contain("new Button"));
            Assert.That(content, Does.Not.Contain("new PanelContainer"));
            Assert.That(content, Does.Not.Contain("AddChild("));
            Assert.That(content, Does.Not.Contain("SetAnchors"));
            Assert.That(content, Does.Not.Contain("CustomMinimumSize"));
            Assert.That(content, Does.Not.Contain("SizeFlagsHorizontal"));
            Assert.That(content, Does.Not.Contain("SizeFlagsVertical"));
            Assert.That(content, Does.Contain("VirtualKeyboardEnabled = false"));
            Assert.That(content, Does.Contain("EnsureControlVisible"));
            Assert.That(content, Does.Contain("_activeSpinBox!.Apply()"));
        }
    }

    [Test]
    public void ServiceAccessScreen_UsesOwnerFriendlyLanguage()
    {
        string root = ProjectPaths.FindRoot();
        string scene = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/ServiceAccessScreen/ServiceAccessScreen.tscn"));
        string code = File.ReadAllText(Path.Combine(
            root,
            "src/scenes/ServiceAccessScreen/ServiceAccessScreen.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scene, Does.Contain("text = \"ВХОД В НАСТРОЙКИ\""));
            Assert.That(scene, Does.Not.Contain("СЕРВИСНЫЙ ВХОД"));
            Assert.That(scene, Does.Not.Contain("СЕРВИСНЫЙ PIN"));
            Assert.That(code, Does.Not.Contain("доступ к конфигурации"));
        }
    }

    [Test]
    public void BuiltInTheme_DisablesTextShadowAndButtonOutline()
    {
        string content = File.ReadAllText(Path.Combine(
            ProjectPaths.FindRoot(),
            "src/core/ThemeManager/BuiltInFallbackTheme.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(content, Does.Contain("SetColor(\"font_shadow_color\", \"Label\", Colors.Transparent)"));
            Assert.That(content, Does.Contain("SetConstant(\"shadow_offset_x\", \"Label\", 0)"));
            Assert.That(content, Does.Contain("SetConstant(\"shadow_offset_y\", \"Label\", 0)"));
            Assert.That(content, Does.Contain("SetConstant(\"outline_size\", \"Button\", 0)"));
        }
    }

    [Test]
    public void WindowsExport_UsesSanitizedStagingWithoutTestsOrEditorAddons()
    {
        string root = ProjectPaths.FindRoot();
        string preset = File.ReadAllText(Path.Combine(root, "export_presets.cfg"));
        string script = File.ReadAllText(Path.Combine(root, "tools/export-windows.ps1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preset, Does.Contain("name=\"Windows x64 Release\""));
            Assert.That(preset, Does.Contain("name=\"Windows x64 Debug\""));
            Assert.That(
                preset,
                Does.Contain("export_path=\"artifacts/windows/release/Exchanger.exe\"").IgnoreCase);
            Assert.That(
                preset,
                Does.Contain("export_path=\"artifacts/windows/debug/Exchanger.Debug.exe\"").IgnoreCase);
            Assert.That(preset, Does.Contain("include_filter=\"config/*.toml\""));
            Assert.That(preset, Does.Contain("tests/**"));
            Assert.That(preset, Does.Contain("references/**"));
            Assert.That(preset, Does.Not.Contain("addons/**"));
            Assert.That(preset, Does.Contain("addons/godot_ai/**"));
            Assert.That(preset, Does.Contain("addons/log/**"));
            Assert.That(preset, Does.Contain("addons/reload_current_scene/**"));
            Assert.That(preset, Does.Not.Contain("addons/godotx_toast/**"));
            Assert.That(preset, Does.Not.Contain("addons/native_video/**"));
            Assert.That(script, Does.Contain("artifacts\\export-staging"));
            Assert.That(script, Does.Contain("[ValidateSet(\"Release\", \"Debug\")]"));
            Assert.That(script, Does.Contain("--export-release"));
            Assert.That(script, Does.Contain("--export-debug"));
            Assert.That(script, Does.Contain("^_mcp_game_helper="));
            Assert.That(script, Does.Contain("^GodotxToast="));
            Assert.That(script, Does.Contain("addons\\native_video"));
            Assert.That(script, Does.Contain("native_video.windows.release.x86_64.dll"));
            Assert.That(script, Does.Contain("native_video.windows.debug.x86_64.dll"));
            Assert.That(script, Does.Contain("Native Video runtime addon is incomplete in staging"));
            Assert.That(script, Does.Contain("Native Video runtime library was not exported"));
        }
    }

    [Test]
    public void NativeVideo_UsesMobileRendererAndProvidesBothRuntimeLibraries()
    {
        string root = ProjectPaths.FindRoot();
        string project = File.ReadAllText(Path.Combine(root, "project.godot"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project, Does.Contain("config/features=PackedStringArray(\"4.7\", \"C#\", \"Mobile\")"));

            Assert.That(File.Exists(Path.Combine(root, "addons", "native_video", "native_video.gdextension")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "addons", "native_video", "native_video.windows.debug.x86_64.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "addons", "native_video", "native_video.windows.release.x86_64.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "addons", "native_video", "libnative_video.linux.debug.x86_64.so")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "addons", "native_video", "libnative_video.linux.release.x86_64.so")), Is.True);
        }
    }

    [Test]
    public void AdvertisementPlayers_UseNormalStopUnbindLifecycleWithoutRetentionCache()
    {
        string root = ProjectPaths.FindRoot();
        string nodePlayer = File.ReadAllText(Path.Combine(
            root,
            "src/ui/AdvertisementPlayer/AdvertisementPlayer.cs"));
        string presenter = File.ReadAllText(Path.Combine(
            root,
            "src/ui/AdvertisementPlayer/AdvertisementPresenter.cs"));

        foreach (string source in new[] { nodePlayer, presenter })
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(source, Does.Contain("_videoPlayer.Stop();"));
                Assert.That(source, Does.Contain("_videoPlayer.Stream = null;"));
                Assert.That(source, Does.Not.Contain("Dictionary<string, VideoStreamPlayer>"));
                Assert.That(source, Does.Not.Contain("MaximumRetainedVideoPlayers"));
                Assert.That(source, Does.Not.Contain("Native Video does not safely destroy"));
            }
        }
    }

    [Test]
    public void ApplicationAudio_ProvidesMusicAndSystemEffectsOnDedicatedBuses()
    {
        string root = ProjectPaths.FindRoot();
        string source = File.ReadAllText(Path.Combine(root, "src/core/Audio/ApplicationAudio.cs"));
        string busLayout = File.ReadAllText(Path.Combine(root, "default_bus_layout.tres"));
        string settingsScene = File.ReadAllText(Path.Combine(root, "src/scenes/SettingsScreen/SettingsScreen.tscn"));

        foreach (string soundPath in new[]
        {
            "sounds/bg_music.mp3",
            "sounds/click.ogg",
            "sounds/cash_added.wav",
            "sounds/confirm_payment.wav",
            "sounds/error_style_5_echo_001.wav",
            "sounds/hold_pet.wav",
            "sounds/release_pet.wav",
        })
        {
            Assert.That(File.Exists(Path.Combine(root, soundPath)), Is.True, $"Не найден системный звук {soundPath}");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source, Does.Contain("PlayPaymentSuccess"));
            Assert.That(source, Does.Contain("PlayPaymentError"));
            Assert.That(source, Does.Contain("PlayCashAdded"));
            Assert.That(source, Does.Contain("PlayPetHeld"));
            Assert.That(source, Does.Contain("PlayPetReleased"));
            Assert.That(source, Does.Contain("button.Pressed += PlayButtonClick"));
            Assert.That(busLayout, Does.Contain("bus/2/name = &\"Music\""));
            Assert.That(busLayout, Does.Contain("bus/3/name = &\"Effects\""));
            Assert.That(busLayout, Does.Contain("bus/2/send = &\"Master\""));
            Assert.That(busLayout, Does.Contain("bus/3/send = &\"Master\""));
            Assert.That(settingsScene, Does.Contain("[node name=\"MusicPanel\""));
            Assert.That(settingsScene, Does.Contain("[node name=\"SoundEffectsPanel\""));
        }
    }

    [Test]
    public void BundledConfiguration_IsReadThroughGodotFileAccess()
    {
        string root = ProjectPaths.FindRoot();
        string loader = File.ReadAllText(Path.Combine(
            root,
            "src/core/Configuration/AppSettingsLoader.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loader, Does.Contain("MaterializeEmbeddedResource"));
            Assert.That(loader, Does.Contain("Godot.FileAccess.Open"));
            Assert.That(loader, Does.Contain("user://config/.embedded/default_settings.json"));
            Assert.That(loader, Does.Contain("user://config/.embedded/technical_settings.toml"));
            Assert.That(loader, Does.Not.Contain("ProjectSettings.GlobalizePath(DefaultSettingsPath)"));
            Assert.That(loader, Does.Not.Contain("ProjectSettings.GlobalizePath(DefaultTechnicalSettingsPath)"));
        }
    }

}
