using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ViscerealityCompanion.App;
using ViscerealityCompanion.App.ViewModels;
using ViscerealityCompanion.Core.Models;

namespace ViscerealityCompanion.Integration.Tests;

[Collection("WpfUi")]
public sealed class StudyExperimentSessionWindowTests
{
    private readonly WpfUiFixture fixture;

    public StudyExperimentSessionWindowTests(WpfUiFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task StudyShellView_ExposesConditionsTab_AndActiveConditionsDriveSessionSelection()
    {
        await fixture.InvokeAsync(async () =>
        {
            var app = fixture.Application;
            if (app.Dispatcher.CheckAccess())
            {
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            using var viewModel = new StudyShellViewModel(CreateStudy($"sussex-conditions-tab-{Guid.NewGuid():N}"));
            var view = new StudyShellView
            {
                DataContext = viewModel
            };
            var window = new Window
            {
                Content = view,
                Width = 1800,
                Height = 1200,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.Show();
            await WaitForConditionAsync(() => view.IsLoaded && window.IsVisible, TimeSpan.FromSeconds(5));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            var tabs = Assert.IsType<TabControl>(view.FindName("StudyPhaseTabs"));
            var conditionsTab = Assert.IsType<TabItem>(view.FindName("ConditionsTab"));
            tabs.SelectedItem = conditionsTab;
            view.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await WaitForConditionAsync(
                () => viewModel.ConditionProfiles.VisualProfileOptions.Count > 0 &&
                      viewModel.ConditionProfiles.ControllerBreathingProfileOptions.Count > 0 &&
                      viewModel.ConditionProfiles.ConditionItems.Count == 3,
                TimeSpan.FromSeconds(5));

            Assert.IsType<Button>(view.FindName("NewConditionButton"));
            Assert.IsType<Button>(view.FindName("SaveConditionButton"));
            Assert.IsType<Button>(view.FindName("LoadConditionButton"));
            Assert.IsType<Button>(view.FindName("ShareConditionButton"));
            Assert.IsType<CheckBox>(view.FindName("ConditionActiveCheckBox"));
            var visualProfileCombo = Assert.IsType<ComboBox>(view.FindName("ConditionVisualProfileComboBox"));
            Assert.NotEmpty(visualProfileCombo.Items);
            var breathingProfileCombo = Assert.IsType<ComboBox>(view.FindName("ConditionBreathingProfileComboBox"));
            Assert.NotEmpty(breathingProfileCombo.Items);
            visualProfileCombo.ApplyTemplate();
            var selectedContent = Assert.IsType<ContentPresenter>(FindDescendant<ContentPresenter>(visualProfileCombo));
            Assert.False(string.IsNullOrWhiteSpace(selectedContent.Content?.ToString()));
            Assert.Same(Application.Current.Resources["InkBrush"], TextElement.GetForeground(selectedContent));
            var dropDownToggle = Assert.IsType<ToggleButton>(FindDescendant<ToggleButton>(visualProfileCombo));
            Assert.NotNull(BindingOperations.GetBindingExpression(dropDownToggle, ToggleButton.IsCheckedProperty));
            dropDownToggle.IsChecked = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(visualProfileCombo.IsDropDownOpen);
            visualProfileCombo.IsDropDownOpen = false;
            var table = Assert.IsType<DataGrid>(view.FindName("ConditionsTable"));
            Assert.Equal(3, table.Items.Count);
            Assert.Equal(2, viewModel.Conditions.Count);

            var inactiveCondition = viewModel.ConditionProfiles.ConditionItems.First(condition => condition.Id == "inactive");
            inactiveCondition.IsActive = true;
            Assert.Equal(3, viewModel.Conditions.Count);

            window.Close();
        });
    }

    [Fact]
    public async Task ExperimentSessionWindow_ExposesCalibrationButtons_AndPanelDetailExpanders()
    {
        await fixture.InvokeAsync(async () =>
        {
            var app = fixture.Application;
            if (app.Dispatcher.CheckAccess())
            {
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            using var viewModel = new StudyShellViewModel(CreateStudy());
            var window = new StudyExperimentSessionWindow(viewModel)
            {
                Width = 1600,
                Height = 1000,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.Show();
            await WaitForConditionAsync(() => window.IsLoaded && window.IsVisible, TimeSpan.FromSeconds(5));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            var dynamicButton = Assert.IsType<Button>(window.FindName("StartDynamicAxisCalibrationButton"));
            var fixedButton = Assert.IsType<Button>(window.FindName("StartFixedAxisCalibrationButton"));
            var resetButton = Assert.IsType<Button>(window.FindName("ResetCalibrationButton"));
            Assert.Equal("Start Dynamic-Axis Calibration", dynamicButton.Content);
            Assert.Equal("Start Fixed-Axis Calibration", fixedButton.Content);
            Assert.Equal("Reset Calibration", resetButton.Content);
            var conditionCombo = Assert.IsType<ComboBox>(window.FindName("SessionConditionComboBox"));
            var applyConditionButton = Assert.IsType<Button>(window.FindName("ApplySessionConditionButton"));
            Assert.Equal(2, conditionCombo.Items.Count);
            Assert.DoesNotContain(
                conditionCombo.Items.Cast<StudyConditionDefinition>(),
                condition => condition.Id == "inactive");
            Assert.Equal("Apply Condition", applyConditionButton.Content);
            var peripersonalPanel = Assert.IsType<StackPanel>(window.FindName("PeripersonalSessionPanel"));
            Assert.Equal(Visibility.Collapsed, peripersonalPanel.Visibility);

            Assert.IsType<Expander>(window.FindName("SessionControlDetailExpander"));
            Assert.IsType<Expander>(window.FindName("ClockNetworkDetailExpander"));
            Assert.IsType<Expander>(window.FindName("LatestCommandDetailExpander"));
            Assert.IsType<Expander>(window.FindName("ControllerDetailExpander"));
            Assert.IsType<Expander>(window.FindName("CoherenceDetailExpander"));
            Assert.IsType<Expander>(window.FindName("ParticlesDetailExpander"));
            Assert.IsType<Expander>(window.FindName("RecenterDetailExpander"));
            Assert.IsType<Expander>(window.FindName("QuestScreenshotDetailExpander"));
            Assert.IsType<Expander>(window.FindName("ClockProbeDetailExpander"));

            window.Close();
        });
    }

    [Fact]
    public async Task ExperimentSessionWindow_PeripersonalShell_ExposesContinuousRecordingWorkflowControls()
    {
        await fixture.InvokeAsync(async () =>
        {
            var app = fixture.Application;
            if (app.Dispatcher.CheckAccess())
            {
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            using var viewModel = new StudyShellViewModel(CreateStudy("peripersonal-space"));
            var window = new StudyExperimentSessionWindow(viewModel)
            {
                Width = 1600,
                Height = 1000,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.Show();
            await WaitForConditionAsync(() => window.IsLoaded && window.IsVisible, TimeSpan.FromSeconds(5));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            var peripersonalPanel = Assert.IsType<StackPanel>(window.FindName("PeripersonalSessionPanel"));
            Assert.Equal(Visibility.Visible, peripersonalPanel.Visibility);
            Assert.IsType<TextBox>(window.FindName("PeripersonalSessionIdTextBox"));
            var handednessCombo = Assert.IsType<ComboBox>(window.FindName("PeripersonalHandednessComboBox"));
            Assert.Equal("right-handed", handednessCombo.SelectedValue);
            Assert.Equal("Breath controller: left controller.", viewModel.PeripersonalBreathControllerSideLabel);
            Assert.Equal("session-001", viewModel.PeripersonalSessionIdDraft);
            var languageCombo = Assert.IsType<ComboBox>(window.FindName("PeripersonalLanguageComboBox"));
            Assert.Equal("en", languageCombo.SelectedValue);
            Assert.IsType<Button>(window.FindName("PreparePeripersonalSessionButton"));
            Assert.IsType<Button>(window.FindName("OpenPeripersonalQuestionnaireBlock1Button"));
            Assert.IsType<Button>(window.FindName("MarkPeripersonalBlock1SubmittedButton"));
            Assert.IsType<Button>(window.FindName("OpenPeripersonalQuestionnaireBlock2Button"));
            Assert.IsType<Button>(window.FindName("OpenPeripersonalQuestionnaireBlock3Button"));
            Assert.IsType<TextBox>(window.FindName("PeripersonalXrBlockIdTextBox"));
            Assert.IsType<Button>(window.FindName("MarkPeripersonalXrBlockEndButton"));
            Assert.IsType<Button>(window.FindName("RecordingToggleButton"));
            Assert.IsType<Button>(window.FindName("ParticlesToggleButton"));
            Assert.IsType<Button>(window.FindName("RunClockProbeButton"));

            window.Close();
        });
    }

    [Fact]
    public void PeripersonalSessionPreparationState_SavesReadableJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"peripersonal-prep-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "session-preparation.json");
        try
        {
            var saved = PeripersonalSessionPreparationState.Create(
                "peripersonal-space",
                "P001",
                "Participant Example",
                "27",
                "session-001",
                "right-handed",
                "en",
                "left-visible");

            saved.Save(path);
            var loaded = Assert.IsType<PeripersonalSessionPreparationState>(
                PeripersonalSessionPreparationState.Load(path));
            var json = File.ReadAllText(path);

            Assert.Equal("Participant Example", loaded.ParticipantName);
            Assert.Equal("P001", loaded.ParticipantNumber);
            Assert.Equal("27", loaded.Age);
            Assert.Equal("session-001", loaded.SessionId);
            Assert.Equal("right-handed", loaded.Handedness);
            Assert.Equal("en", loaded.LanguageCode);
            Assert.Equal("left-visible", loaded.InitialConditionId);
            Assert.Contains("\"participantName\": \"Participant Example\"", json);
            Assert.Contains("\"participantNumber\": \"P001\"", json);
            Assert.Contains("\"age\": \"27\"", json);

            var ledgerPath = Path.Combine(root, "participant-ledger.jsonl");
            PeripersonalParticipantLedger.Append(saved, ledgerPath);
            var ledgerEntry = Assert.IsType<PeripersonalParticipantLedgerEntry>(
                PeripersonalParticipantLedger.FindLatest("P001", "peripersonal-space", ledgerPath));

            Assert.Equal("P001", ledgerEntry.ParticipantNumber);
            Assert.Equal("Participant Example", ledgerEntry.ParticipantName);
            Assert.Equal("27", ledgerEntry.Age);
            Assert.Single(PeripersonalParticipantLedger.Load(ledgerPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static StudyShellDefinition CreateStudy(string? studyId = null)
    {
        var id = studyId ?? "sussex-experiment-session-test";
        var isPeripersonal = string.Equals(id, "peripersonal-space", StringComparison.OrdinalIgnoreCase);

        return new StudyShellDefinition(
            id,
            isPeripersonal ? "Peripersonal Space" : "Sussex Test",
            isPeripersonal ? "Peripersonal study team" : "Sussex",
            isPeripersonal ? "Peripersonal experiment session window regression test." : "Experiment session window regression test.",
            new StudyPinnedApp(
                isPeripersonal ? "Peripersonal" : "Sussex",
                isPeripersonal ? "com.Viscereality.ViscerealityPeriPersonal" : "com.Viscereality.SussexExperiment",
                string.Empty,
                string.Empty,
                string.Empty,
                "preview",
                string.Empty,
                AllowManualSelection: true,
                LaunchInKioskMode: false),
            new StudyPinnedDeviceProfile(
                "sussex-test-device-profile",
                "Sussex Test Device Profile",
                string.Empty,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            new StudyMonitoringProfile(
                ExpectedBreathingLabel: string.Empty,
                ExpectedHeartbeatLabel: string.Empty,
                ExpectedCoherenceLabel: string.Empty,
                ExpectedLslStreamName: "HRV_Biofeedback",
                ExpectedLslStreamType: "HRV",
                RecenterDistanceThresholdUnits: 0.2d,
                LslConnectivityKeys: [],
                LslStreamNameKeys: [],
                LslStreamTypeKeys: [],
                LslValueKeys: [],
                ControllerValueKeys: [],
                ControllerStateKeys: [],
                ControllerTrackingKeys: [],
                AutomaticBreathingValueKeys: [],
                HeartbeatValueKeys: [],
                HeartbeatStateKeys: [],
                CoherenceValueKeys: [],
                CoherenceStateKeys: [],
                PerformanceFpsKeys: [],
                PerformanceFrameTimeKeys: [],
                PerformanceTargetFpsKeys: [],
                PerformanceRefreshRateKeys: [],
                RecenterDistanceKeys: [],
                ParticleVisibilityKeys: []),
            new StudyControlProfile(
                RecenterCommandActionId: "recenter",
                ParticleVisibleOnActionId: "particles.on",
                ParticleVisibleOffActionId: "particles.off",
                StartBreathingCalibrationActionId: "calibration.start",
                ResetBreathingCalibrationActionId: "calibration.reset",
                StartExperimentActionId: "experiment.start",
                EndExperimentActionId: "experiment.end"),
            isPeripersonal ? CreatePeripersonalConditions() : CreateSussexConditions());
    }

    private static IReadOnlyList<StudyConditionDefinition> CreateSussexConditions()
        =>
            [
                new StudyConditionDefinition(
                    "current",
                    "Current",
                    "Current test settings.",
                    "condition-current-visual",
                    "condition-current-breathing"),
                new StudyConditionDefinition(
                    "fixed-radius-no-orbit",
                    "Fixed Radius, No Orbit",
                    "Fixed radius test condition.",
                    "condition-fixed-radius-no-orbit",
                    "condition-fixed-radius-breathing"),
                new StudyConditionDefinition(
                    "inactive",
                    "Inactive",
                    "Inactive condition hidden from the session dropdown.",
                    "condition-current-visual",
                    "condition-current-breathing",
                    isActive: false)
            ];

    private static IReadOnlyList<StudyConditionDefinition> CreatePeripersonalConditions()
        =>
            [
                new StudyConditionDefinition(
                    "left-visible",
                    "Left Visible",
                    "Left-hand peripersonal XR block with particles visible.",
                    "peripersonal-left-visible",
                    "peripersonal-runtime-default",
                    properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["activeHand"] = "left",
                        ["particlesVisible"] = "true",
                        ["visibilityMode"] = "particles_visible"
                    }),
                new StudyConditionDefinition(
                    "left-anchor-only",
                    "Left Anchor Only",
                    "Left-hand peripersonal XR block with default particles hidden and anchor indicator retained.",
                    "peripersonal-left-anchor-only",
                    "peripersonal-runtime-default",
                    properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["activeHand"] = "left",
                        ["particlesVisible"] = "false",
                        ["visibilityMode"] = "anchor_only"
                    })
            ];

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), "Timed out waiting for the expected WPF state.");
    }

    private static T? FindDescendant<T>(DependencyObject? node)
        where T : DependencyObject
    {
        if (node is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            var child = VisualTreeHelper.GetChild(node, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
