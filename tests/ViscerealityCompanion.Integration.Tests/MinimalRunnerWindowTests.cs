using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using System.Text.Json.Nodes;
using ViscerealityCompanion.App.ViewModels;
using ViscerealityCompanion.Core.Models;
using ViscerealityCompanion.Core.Services;
using ViscerealityCompanion.MinimalRunner;

namespace ViscerealityCompanion.Integration.Tests;

[Collection("WpfUi")]
public sealed class MinimalRunnerWindowTests
{
    private readonly WpfUiFixture fixture;

    public MinimalRunnerWindowTests(WpfUiFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task SessionSetupWindow_RequiresParticipantNameNumberAgeAndHandednessBeforeStartSession()
    {
        await fixture.InvokeAsync(async () =>
        {
            var app = fixture.Application;
            if (app.Dispatcher.CheckAccess())
            {
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            using var viewModel = new StudyShellViewModel(CreatePeripersonalStudy());
            var window = new SessionSetupWindow(viewModel)
            {
                Width = 620,
                Height = 680,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.Show();
            await WaitForConditionAsync(() => window.IsLoaded && window.IsVisible, TimeSpan.FromSeconds(5));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            Assert.NotNull(window.Icon);
            var participantNameTextBox = Assert.IsType<TextBox>(window.FindName("PreparationParticipantNameTextBox"));
            var participantNumberDisplay = Assert.IsType<TextBlock>(window.FindName("PreparationParticipantNumberDisplay"));
            var increaseParticipantNumberButton = Assert.IsType<Button>(window.FindName("IncreaseParticipantNumberButton"));
            var decreaseParticipantNumberButton = Assert.IsType<Button>(window.FindName("DecreaseParticipantNumberButton"));
            var ageTextBox = Assert.IsType<TextBox>(window.FindName("PreparationAgeTextBox"));
            var handednessSwitch = Assert.IsType<ToggleButton>(window.FindName("PreparationHandednessSwitch"));
            var startButton = Assert.IsType<Button>(window.FindName("StartSessionButton"));
            Assert.IsType<Image>(window.FindName("MinimalRunnerSetupLogo"));
            Assert.IsType<Border>(window.FindName("HeadsetPluggedSetupNotice"));

            Assert.Equal(string.Empty, participantNameTextBox.Text);
            Assert.Equal("P001", participantNumberDisplay.Text);
            Assert.Equal("P001", viewModel.ParticipantIdDraft);
            Assert.False(decreaseParticipantNumberButton.IsEnabled);
            Assert.True(increaseParticipantNumberButton.IsEnabled);
            Assert.Equal(string.Empty, ageTextBox.Text);
            Assert.False(handednessSwitch.IsChecked);
            Assert.Equal("right-handed", viewModel.PeripersonalHandednessDraft);
            Assert.False(startButton.IsEnabled);

            viewModel.IncreasePeripersonalParticipantNumberCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal("P002", viewModel.ParticipantIdDraft);
            Assert.Equal("P002", participantNumberDisplay.Text);
            Assert.True(decreaseParticipantNumberButton.IsEnabled);

            viewModel.IncreasePeripersonalParticipantNumberCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal("P003", viewModel.ParticipantIdDraft);
            Assert.Equal("P003", participantNumberDisplay.Text);

            viewModel.DecreasePeripersonalParticipantNumberCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal("P002", viewModel.ParticipantIdDraft);
            Assert.Equal("P002", participantNumberDisplay.Text);

            handednessSwitch.IsChecked = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal("left-handed", viewModel.PeripersonalHandednessDraft);

            handednessSwitch.IsChecked = false;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal("right-handed", viewModel.PeripersonalHandednessDraft);

            viewModel.PeripersonalParticipantNameDraft = "Participant Example";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(startButton.IsEnabled);

            viewModel.PeripersonalParticipantAgeDraft = "27";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(startButton.IsEnabled);

            window.Close();
        });
    }

    [Fact]
    public async Task ExperimentRunnerWindow_ExposesOnlyCoreRunControls()
    {
        await fixture.InvokeAsync(async () =>
        {
            var app = fixture.Application;
            if (app.Dispatcher.CheckAccess())
            {
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            using var viewModel = new StudyShellViewModel(CreatePeripersonalStudy())
            {
                ParticipantIdDraft = "P019",
                PeripersonalParticipantNameDraft = "Participant Example",
                PeripersonalParticipantAgeDraft = "27"
            };
            var window = new ExperimentRunnerWindow(viewModel)
            {
                Width = 900,
                Height = 700,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.Show();
            await WaitForConditionAsync(() => window.IsLoaded && window.IsVisible, TimeSpan.FromSeconds(5));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            Assert.NotNull(window.Icon);
            Assert.IsType<Image>(window.FindName("MinimalRunnerHeaderLogo"));
            Assert.IsType<Border>(window.FindName("HeadsetUnplugReadyNotice"));
            Assert.IsType<Button>(window.FindName("StartExperimentRecordingButton"));
            Assert.IsType<Button>(window.FindName("StopExperimentRecordingButton"));
            Assert.IsType<Button>(window.FindName("TogglePeripersonalParticlesSwitch"));
            Assert.IsType<Button>(window.FindName("TogglePeripersonalBlock1Switch"));
            Assert.IsType<Button>(window.FindName("TogglePeripersonalBlock2Switch"));
            Assert.IsType<Button>(window.FindName("TogglePeripersonalBlock3Switch"));
            AssertSwitchUsesConfirmedState(window, "TogglePeripersonalParticlesSwitch", "IsParticlesToggleState");
            AssertSwitchUsesConfirmedState(window, "TogglePeripersonalBlock1Switch", "IsPeripersonalBlock1Active");
            AssertSwitchUsesConfirmedState(window, "TogglePeripersonalBlock2Switch", "IsPeripersonalBlock2Active");
            AssertSwitchUsesConfirmedState(window, "TogglePeripersonalBlock3Switch", "IsPeripersonalBlock3Active");

            Assert.Null(window.FindName("StartDynamicAxisCalibrationButton"));
            Assert.Null(window.FindName("ResetCalibrationButton"));
            Assert.Null(window.FindName("ParticlesToggleButton"));
            Assert.Null(window.FindName("ShowPeripersonalBlock1Button"));
            Assert.Null(window.FindName("FinishPeripersonalBlock1Button"));
            Assert.Null(window.FindName("ShowPeripersonalBlock2Button"));
            Assert.Null(window.FindName("FinishPeripersonalBlock2Button"));
            Assert.Null(window.FindName("ShowPeripersonalBlock3Button"));
            Assert.Null(window.FindName("FinishPeripersonalBlock3Button"));

            window.Close();
        });
    }

    [Fact]
    public void PeripersonalHeadsetEventInterpreter_RecognizesLaunchAndCompletionReceipts()
    {
        var launchJson = """
            {
              "protocol_version":"viscereality.peripersonal.command_receipt.v1",
              "command_id":"launch-command",
              "sequence":5,
              "session_id":"session-001",
              "target_app":"unity",
              "action":"open_questionnaire_block",
              "transport_received":"lsl",
              "accepted":true,
              "executed":true,
              "completed":true,
              "issue_code":"",
              "message":"Questionnaire launch requested.",
              "observed_state":{
                "questionnaire_request_id":"start-experiment-block-1-20260623210429307",
                "questionnaire_status":"launch_requested",
                "last_command_id":"launch-command"
              }
            }
            """;

        Assert.True(PeripersonalHeadsetEventInterpreter.TryParseQuestionnaireEvent(launchJson, out var launch));
        Assert.True(launch.IsLaunch);
        Assert.False(launch.IsCompletion);
        Assert.Equal("block-1", launch.BlockId);

        var completionJson = """
            {
              "event":"questionnaire_completed",
              "operator_questionnaire_block_id":"block-2",
              "questionnaire_request_id":"panel-request-2",
              "questionnaire_status":"submitted"
            }
            """;

        Assert.True(PeripersonalHeadsetEventInterpreter.TryParseQuestionnaireEvent(completionJson, out var completion));
        Assert.True(completion.IsCompletion);
        Assert.Equal("block-2", completion.BlockId);

        var runtimeBlockThreeCompletion = """
            {
              "session_ready":true,
              "recording_active":true,
              "particles_visible":true,
              "visibility_mode":"particles_visible",
              "questionnaire_status":"completed",
              "operator_questionnaire_block_id":"block-3",
              "questionnaire_request_id":"panel-request-3"
            }
            """;

        Assert.True(PeripersonalHeadsetEventInterpreter.TryParseQuestionnaireEvent(runtimeBlockThreeCompletion, out var runtimeCompletion));
        Assert.True(runtimeCompletion.IsCompletion);
        Assert.Equal("block-3", runtimeCompletion.BlockId);

        var unityHttpStatusCompletion = """
            {
              "session_ready":true,
              "recording_active":true,
              "observed_state":{
                "particles_visible":false,
                "visibility_mode":"anchor_only",
                "questionnaire_status":"completed"
              },
              "last_result":{
                "request_id":"panel-request-2",
                "status":"completed",
                "stage":"maia_spatial:spatial_frame_reference_1"
              }
            }
            """;

        Assert.True(PeripersonalHeadsetEventInterpreter.TryParseQuestionnaireEvent(unityHttpStatusCompletion, out var statusCompletion));
        Assert.True(statusCompletion.IsCompletion);
        Assert.Equal("block-2", statusCompletion.BlockId);
    }

    [Fact]
    public void PeripersonalRuntimeStateInterpreter_RecognizesParticleVisibilityFromConfirmedFrames()
    {
        var ackJson = """
            {
              "protocol_version":"viscereality.peripersonal.command_receipt.v1",
              "command_id":"particles-on-command",
              "action":"set_particles_visible",
              "observed_state":{
                "particles_visible":true,
                "visibility_mode":"particles_visible"
              }
            }
            """;

        var ackReading = new LslMonitorReading(
            "Streaming LSL sample.",
            "confirmed particle command",
            null,
            0,
            DateTimeOffset.UtcNow,
            TextValue: ackJson,
            SampleValues: [ackJson],
            ChannelFormat: LslChannelFormat.String);

        Assert.True(PeripersonalRuntimeStateInterpreter.TryParseParticleVisibility(ackReading, out var visible));
        Assert.True(visible);

        var looseReading = new LslMonitorReading(
            "Streaming LSL sample.",
            "runtime state",
            null,
            0,
            DateTimeOffset.UtcNow,
            TextValue: "session_ready=true; particles_visible=false; visibility_mode=anchor_only",
            SampleValues: ["session_ready=true; particles_visible=false; visibility_mode=anchor_only"],
            ChannelFormat: LslChannelFormat.String);

        Assert.True(PeripersonalRuntimeStateInterpreter.TryParseParticleVisibility(looseReading, out visible));
        Assert.False(visible);

        var unityHttpStatus = """
            {
              "session_ready":true,
              "recording_active":true,
              "last_command":{
                "command_id":"particles-on-command",
                "command_name":"set_particles_visible",
                "sequence":8,
                "accepted":true,
                "message":"Particles made visible."
              }
            }
            """;

        var statusReading = new LslMonitorReading(
            "Streaming Unity HTTP status.",
            "Unity HTTP bridge status from http://127.0.0.1:8787/v1/status.",
            null,
            0,
            DateTimeOffset.UtcNow,
            TextValue: unityHttpStatus,
            SampleValues: [unityHttpStatus],
            ChannelFormat: LslChannelFormat.String);

        Assert.True(PeripersonalRuntimeStateInterpreter.TryParseParticleVisibility(statusReading, out visible));
        Assert.True(visible);
    }

    [Fact]
    public void PeripersonalQuestionnaireResultFileInterpreter_RecognizesUnityResultFileCompletion()
    {
        var jsonl = """
            {"recorded_at_utc":"2026-06-24T00:42:41.0828680Z","request_id":"ddcf4e5e-580e-465b-869a-1d34ac2878be","stage":"maia_spatial:spatial_frame_reference_1","result_json":"{\"protocol_version\":\"quest.questionnaire.v1\",\"schema\":\"quest.questionnaire.v1.result\",\"request_id\":\"ddcf4e5e-580e-465b-869a-1d34ac2878be\",\"status\":\"completed\",\"stage\":\"maia_spatial:spatial_frame_reference_1\",\"condition_number\":2,\"answers\":{\"open_stage\":\"maia_spatial:spatial_frame_reference_1\",\"completed_stage\":\"maia_spatial:spatial_frame_reference_1\"},\"terminal\":{\"reason\":\"completed\",\"current_stage\":\"maia_spatial:spatial_frame_reference_1\"}}"}
            """;

        var completions = PeripersonalQuestionnaireResultFileInterpreter.ParseCompletionEvents(jsonl);

        var completion = Assert.Single(completions);
        Assert.True(completion.IsCompletion);
        Assert.False(completion.IsLaunch);
        Assert.Equal("block-2", completion.BlockId);
        Assert.Equal("completed", completion.Status);
        Assert.Equal("ddcf4e5e-580e-465b-869a-1d34ac2878be", completion.RequestId);
    }

    [Fact]
    public void PeripersonalQuestionnaireResultFileInterpreter_RecognizesThreeStageHandoffResults()
    {
        var jsonl = string.Join(
            Environment.NewLine,
            BuildQuestionnaireResultLine(
                "block-one-request",
                "maia_spatial:language_selection",
                1),
            BuildQuestionnaireResultLine(
                "block-two-request",
                "maia_spatial:spatial_frame_reference_1",
                2),
            BuildQuestionnaireResultLine(
                "block-three-request",
                "maia_spatial:spatial_frame_reference_2",
                3));

        var completions = PeripersonalQuestionnaireResultFileInterpreter.ParseCompletionEvents(jsonl);

        Assert.Equal(["block-1", "block-2", "block-3"], completions.Select(static item => item.BlockId));
        Assert.All(completions, static completion =>
        {
            Assert.True(completion.IsCompletion);
            Assert.Equal("completed", completion.Status);
            Assert.Equal("questionnaire_results.jsonl", completion.Action);
        });
    }

    [Fact]
    public void PeripersonalQuestionnaireResultFileCompletion_MarksBlockFinishedInRunnerState()
    {
        using var viewModel = new StudyShellViewModel(CreatePeripersonalStudy());
        var workflow = CreateRestoredRecordingWorkflow();
        SetPrivateField(viewModel, "_peripersonalWorkflow", workflow);
        SetPrivateField(viewModel, "_peripersonalVisibleQuestionnaireBlock", "block-2");

        var completion = new PeripersonalQuestionnaireHeadsetEvent(
            "block-2",
            "completed",
            "ddcf4e5e-580e-465b-869a-1d34ac2878be",
            string.Empty,
            "questionnaire_results.jsonl",
            IsLaunch: false,
            IsCompletion: true);

        InvokePrivateVoid(viewModel, "ApplyPeripersonalHeadsetQuestionnaireEvent", completion);

        Assert.False(viewModel.IsPeripersonalBlock2Active);
        Assert.Contains("Finished", viewModel.PeripersonalBlock2Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PeripersonalParticleCard_UsesConfirmedStateInsteadOfGenericTwinTelemetry()
    {
        using var viewModel = new StudyShellViewModel(CreatePeripersonalStudy());

        SetPrivateField(viewModel, "_peripersonalParticlesVisible", true);
        InvokePrivateVoid(viewModel, "UpdateParticlesCard");

        Assert.True(viewModel.IsParticlesToggleState);
        Assert.Equal(OperationOutcomeKind.Success, viewModel.ParticlesLevel);
        Assert.Contains("visible", viewModel.ParticlesSummary, StringComparison.OrdinalIgnoreCase);

        SetPrivateField(viewModel, "_peripersonalPendingParticlesVisible", false);
        InvokePrivateVoid(viewModel, "UpdateParticlesCard");

        Assert.True(viewModel.IsParticlesToggleState);
        Assert.Equal(OperationOutcomeKind.Warning, viewModel.ParticlesLevel);
        Assert.Contains("Waiting for headset confirmation", viewModel.ParticlesSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PeripersonalParticleToggle_AppliesSuccessfulObservedStateReceipt()
    {
        await fixture.InvokeAsync(async () =>
        {
            using var viewModel = new StudyShellViewModel(CreatePeripersonalStudy());
            SetPrivateField(viewModel, "_peripersonalWorkflow", CreateRestoredRecordingWorkflow());

            await viewModel.ParticlesOnAsync();

            Assert.True(viewModel.IsParticlesToggleState);
            Assert.Equal(OperationOutcomeKind.Success, viewModel.ParticlesLevel);
            Assert.Contains("visible", viewModel.ParticlesSummary, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task PeripersonalBlockToggle_AppliesSuccessfulLaunchAndCloseReceipts()
    {
        await fixture.InvokeAsync(async () =>
        {
            using var viewModel = new StudyShellViewModel(CreatePeripersonalStudy());
            SetPrivateField(viewModel, "_peripersonalWorkflow", CreateRestoredRecordingWorkflow());

            await InvokePrivateTaskAsync(viewModel, "OpenPeripersonalQuestionnaireBlockAsync", "block-2", 2);

            Assert.True(viewModel.IsPeripersonalBlock2Active);
            Assert.Contains("On screen", viewModel.PeripersonalBlock2Summary, StringComparison.OrdinalIgnoreCase);

            await InvokePrivateTaskAsync(viewModel, "ClosePeripersonalQuestionnaireBlockAsync", "block-2", 2);

            Assert.False(viewModel.IsPeripersonalBlock2Active);
            Assert.Contains("Ready", viewModel.PeripersonalBlock2Summary, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void AssertSwitchUsesConfirmedState(Window window, string name, string expectedPath)
    {
        var button = Assert.IsType<Button>(window.FindName(name));
        var binding = BindingOperations.GetBinding(button, FrameworkElement.TagProperty);
        Assert.NotNull(binding);
        Assert.Equal(expectedPath, binding.Path.Path);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private static void InvokePrivateVoid(object target, string methodName, params object?[] parameters)
    {
        var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(target, parameters);
    }

    private static async Task InvokePrivateTaskAsync(object target, string methodName, params object?[] parameters)
    {
        var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(target, parameters);
        var task = Assert.IsAssignableFrom<Task>(result);
        await task;
    }

    private static string BuildQuestionnaireResultLine(string requestId, string stage, int conditionNumber)
    {
        var result = new JsonObject
        {
            ["protocol_version"] = "quest.questionnaire.v1",
            ["schema"] = "quest.questionnaire.v1.result",
            ["request_id"] = requestId,
            ["status"] = "completed",
            ["stage"] = stage,
            ["condition_number"] = conditionNumber,
            ["answers"] = new JsonObject
            {
                ["open_stage"] = stage,
                ["completed_stage"] = stage
            },
            ["terminal"] = new JsonObject
            {
                ["reason"] = "completed",
                ["current_stage"] = stage
            }
        };
        var outer = new JsonObject
        {
            ["recorded_at_utc"] = "2026-06-24T00:42:41.0828680Z",
            ["request_id"] = requestId,
            ["stage"] = stage,
            ["result_json"] = result.ToJsonString()
        };
        return outer.ToJsonString();
    }

    private static PeripersonalOperatorWorkflowService CreateRestoredRecordingWorkflow()
    {
        var study = CreatePeripersonalStudy();
        var root = Path.Combine(Path.GetTempPath(), "viscereality-minimal-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var workflow = new PeripersonalOperatorWorkflowService(
            study,
            new FakePeripersonalCommandTransport(),
            windowsSessionRoot: root);
        var session = new PeripersonalPreparedSession(
            study.Id,
            "P001",
            "session-001",
            "left-handed",
            "right",
            "P001_session-001_20260624-000000",
            root,
            Path.Combine(root, "ledger.jsonl"),
            DateTimeOffset.UtcNow,
            ParticipantName: "George",
            ParticipantNumber: "P001");

        return workflow.RestorePreparedSession(
            session,
            PeripersonalOperatorWorkflowState.Recording,
            questionnaireBlockOneSubmitted: false);
    }

    private static StudyShellDefinition CreatePeripersonalStudy()
        => new(
            "peripersonal-space",
            "Peripersonal Space",
            "Peripersonal study team",
            "Peripersonal minimal runner regression test.",
            new StudyPinnedApp(
                "Peripersonal",
                "com.Viscereality.ViscerealityPeriPersonal",
                string.Empty,
                string.Empty,
                string.Empty,
                "preview",
                string.Empty,
                AllowManualSelection: true,
                LaunchInKioskMode: false),
            new StudyPinnedDeviceProfile(
                "peripersonal-test-device-profile",
                "Peripersonal Test Device Profile",
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
            [
                new StudyConditionDefinition(
                    "left-visible",
                    "Left Visible",
                    "Left-hand peripersonal XR block with particles visible.",
                    "peripersonal-left-visible",
                    "peripersonal-runtime-default")
            ]);

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

    private sealed class FakePeripersonalCommandTransport : IPeripersonalCommandTransport
    {
        public Task<PeripersonalCommandReceiptEnvelope> SendAsync(
            PeripersonalCommandEnvelope command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PeripersonalCommandReceiptEnvelope.Create(
                command,
                accepted: true,
                executed: true,
                completed: true,
                transportReceived: "test",
                observedState: BuildObservedState(command)));

        private static JsonObject? BuildObservedState(PeripersonalCommandEnvelope command)
            => command.Action switch
            {
                "prepare_session" => new JsonObject
                {
                    ["session_ready"] = true,
                    ["session_folder_name"] = command.Payload?["session_folder_name"]?.GetValue<string>() ?? string.Empty
                },
                "start_recording" => new JsonObject
                {
                    ["recording_active"] = true
                },
                "set_particles_visible" => new JsonObject
                {
                    ["particles_visible"] = true,
                    ["visibility_mode"] = "particles_visible"
                },
                "set_particles_hidden" => new JsonObject
                {
                    ["particles_visible"] = false,
                    ["visibility_mode"] = "anchor_only"
                },
                "open_questionnaire_block" => new JsonObject
                {
                    ["questionnaire_status"] = "launch_requested",
                    ["operator_questionnaire_block_id"] = command.Payload?["operator_questionnaire_block_id"]?.GetValue<string>() ?? string.Empty,
                    ["questionnaire_request_id"] = command.Payload?["request_id"]?.GetValue<string>() ?? string.Empty
                },
                "close_questionnaire_block" => new JsonObject
                {
                    ["questionnaire_status"] = "close_requested",
                    ["operator_questionnaire_block_id"] = command.Payload?["operator_questionnaire_block_id"]?.GetValue<string>() ?? string.Empty,
                    ["questionnaire_request_id"] = command.Payload?["request_id"]?.GetValue<string>() ?? string.Empty
                },
                "stop_recording_and_close_apps" => new JsonObject
                {
                    ["recording_active"] = false,
                    ["quest_apps_close_requested"] = true
                },
                _ => null
            };
    }
}
