using System.Windows;
using ViscerealityCompanion.App;
using ViscerealityCompanion.Core.Models;
using ViscerealityCompanion.Core.Services;

namespace ViscerealityCompanion.App.ViewModels;

public sealed partial class StudyShellViewModel
{
    private async Task OpenTwinEventsWindowAsync()
    {
        await DispatchAsync(() =>
        {
            if (_twinEventsWindow is { IsLoaded: true })
            {
                if (_twinEventsWindow.WindowState == WindowState.Minimized)
                {
                    _twinEventsWindow.WindowState = WindowState.Normal;
                }

                _twinEventsWindow.Activate();
                return;
            }

            var owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow;

            var window = new StudyTwinEventsWindow(this)
            {
                Owner = owner
            };
            window.Closed += OnTwinEventsWindowClosed;
            _twinEventsWindow = window;
            window.Show();
            window.Activate();
        }).ConfigureAwait(false);
    }

    private void OnTwinEventsWindowClosed(object? sender, EventArgs e)
    {
        if (_twinEventsWindow is not null)
        {
            _twinEventsWindow.Closed -= OnTwinEventsWindowClosed;
            _twinEventsWindow = null;
        }
    }

    private Task PreviousWorkflowGuideStepAsync()
    {
        WorkflowGuideStepIndex = Math.Max(0, WorkflowGuideStepIndex - 1);
        return Task.CompletedTask;
    }

    private Task NextWorkflowGuideStepAsync()
    {
        if (CanGoToNextWorkflowGuideStep)
        {
            WorkflowGuideStepIndex = Math.Min(WorkflowGuideCatalog.Length - 1, WorkflowGuideStepIndex + 1);
        }

        return Task.CompletedTask;
    }

    private Task CloseWorkflowGuideWindowAsync()
    {
        CloseWorkflowGuideWindow();
        return Task.CompletedTask;
    }

    private async Task OpenWorkflowGuideWindowAsync()
    {
        await DispatchAsync(() =>
        {
            if (_workflowGuideWindow is { IsLoaded: true })
            {
                if (_workflowGuideWindow.WindowState == WindowState.Minimized)
                {
                    _workflowGuideWindow.WindowState = WindowState.Normal;
                }

                _workflowGuideWindow.Activate();
                return;
            }

            WorkflowGuideStepIndex = 0;
            var owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow;

            var window = new StudyWorkflowGuideWindow(this)
            {
                Owner = owner
            };
            window.Closed += OnWorkflowGuideWindowClosed;
            _workflowGuideWindow = window;
            window.Show();
            window.Activate();
        }).ConfigureAwait(false);
    }

    private void OnWorkflowGuideWindowClosed(object? sender, EventArgs e)
    {
        if (_workflowGuideWindow is not null)
        {
            _workflowGuideWindow.Closed -= OnWorkflowGuideWindowClosed;
            _workflowGuideWindow = null;
        }
    }

    private void CloseWorkflowGuideWindow()
    {
        if (_workflowGuideWindow is null)
        {
            return;
        }

        _workflowGuideWindow.Closed -= OnWorkflowGuideWindowClosed;
        _workflowGuideWindow.Close();
        _workflowGuideWindow = null;
    }

    private async Task OpenExperimentSessionWindowAsync()
    {
        await DispatchAsync(() =>
        {
            if (_experimentSessionWindow is { IsLoaded: true })
            {
                if (_experimentSessionWindow.WindowState == WindowState.Minimized)
                {
                    _experimentSessionWindow.WindowState = WindowState.Normal;
                }

                _experimentSessionWindow.Activate();
                return;
            }

            var owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? _workflowGuideWindow
                ?? Application.Current?.MainWindow;

            var window = new StudyExperimentSessionWindow(this)
            {
                Owner = owner
            };
            window.Closed += OnExperimentSessionWindowClosed;
            _experimentSessionWindow = window;
            window.Show();
            window.Activate();
        }).ConfigureAwait(false);
    }

    private void OnExperimentSessionWindowClosed(object? sender, EventArgs e)
    {
        if (_experimentSessionWindow is not null)
        {
            _experimentSessionWindow.Closed -= OnExperimentSessionWindowClosed;
            _experimentSessionWindow = null;
        }
    }

    public bool ShouldPromptForPeripersonalSessionPreparation
        => _isPeripersonalWorkflow
            && _peripersonalWorkflow?.State is not PeripersonalOperatorWorkflowState.Prepared
            && _peripersonalWorkflow?.State is not PeripersonalOperatorWorkflowState.Recording;

    public Task<bool> SavePeripersonalSessionPreparationAsync()
    {
        if (!CanSavePeripersonalSessionPreparation)
        {
            return Task.FromResult(false);
        }

        var state = CapturePeripersonalSessionPreparationState();
        state.Save();
        PeripersonalParticipantLedger.Append(state);
        ApplyPeripersonalSessionPreparationState(state);
        return Task.FromResult(true);
    }

    public async Task<bool> StartPreparedPeripersonalSessionAsync()
    {
        if (!CanSavePeripersonalSessionPreparation)
        {
            return false;
        }

        var state = CapturePeripersonalSessionPreparationState();
        state.Save();
        PeripersonalParticipantLedger.Append(state);
        ApplyPeripersonalSessionPreparationState(state);

        if (!IsRequiredExperimentWifiReady())
        {
            await ApplyOutcomeAsync(
                "Start Session",
                new OperationOutcome(
                    OperationOutcomeKind.Warning,
                    $"Connect both devices to {RequiredExperimentWifiSsid}.",
                    BuildRequiredExperimentWifiDetail(_headsetStatus))).ConfigureAwait(false);
            return false;
        }

        if (_peripersonalWorkflow?.State is PeripersonalOperatorWorkflowState.Prepared or PeripersonalOperatorWorkflowState.Recording)
        {
            return true;
        }

        await PreparePeripersonalSessionAsync().ConfigureAwait(false);
        return _peripersonalWorkflow?.State is PeripersonalOperatorWorkflowState.Prepared or PeripersonalOperatorWorkflowState.Recording;
    }

    public async Task<bool> StartPluggedInPeripersonalSessionAsync()
    {
        const string ActionLabel = "Start Session";

        if (!CanSavePeripersonalSessionPreparation)
        {
            await ApplyOutcomeAsync(
                ActionLabel,
                new OperationOutcome(
                    OperationOutcomeKind.Warning,
                    "Participant setup is incomplete.",
                    "Enter participant name, participant number, age, and handedness before starting the session.")).ConfigureAwait(false);
            return false;
        }

        await RefreshStatusAsync().ConfigureAwait(false);

        if (_headsetStatus?.IsUsbAdbVisible != true)
        {
            await ApplyOutcomeAsync(
                ActionLabel,
                new OperationOutcome(
                    OperationOutcomeKind.Warning,
                    "Keep the headset plugged in for this setup step.",
                    "The setup step expects a USB-visible Quest before it launches Unity, sends the metadata, starts recording/streams, forces particles off, and opens Block 1. Reconnect USB, keep the headset awake, then press Start Session again.")).ConfigureAwait(false);
            return false;
        }

        var state = await DispatchAsync(CapturePeripersonalSessionPreparationState).ConfigureAwait(false);
        state.Save();
        PeripersonalParticipantLedger.Append(state);
        await DispatchAsync(() => ApplyPeripersonalSessionPreparationState(state)).ConfigureAwait(false);

        await StartExperimentAsync().ConfigureAwait(false);

        return _peripersonalWorkflow?.State == PeripersonalOperatorWorkflowState.Recording
            && IsPeripersonalQuestionnaireBlockVisible(PeripersonalBlockOneId);
    }

    public bool LoadPeripersonalSessionPreparation()
    {
        var state = PeripersonalSessionPreparationState.Load();
        if (state is null)
        {
            return false;
        }

        ApplyPeripersonalSessionPreparationState(state);
        return true;
    }

    private PeripersonalSessionPreparationState CapturePeripersonalSessionPreparationState()
        => PeripersonalSessionPreparationState.Create(
            _study.Id,
            ParticipantIdDraft,
            PeripersonalParticipantNameDraft,
            PeripersonalParticipantAgeDraft,
            PeripersonalSessionIdDraft,
            PeripersonalHandednessDraft,
            PeripersonalLanguageCodeDraft,
            SelectedCondition?.Id);

    private void ApplyPeripersonalSessionPreparationState(PeripersonalSessionPreparationState state)
    {
        if (!string.IsNullOrWhiteSpace(state.StudyId)
            && !string.Equals(state.StudyId, _study.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var participantName = state.ParticipantName ?? string.Empty;
        var participantNumber = string.IsNullOrWhiteSpace(state.ParticipantNumber)
            ? PeripersonalParticipantNumberDisplay
            : state.ParticipantNumber.Trim();
        if (TryParsePeripersonalParticipantNumber(participantNumber, out var parsedParticipantNumber))
        {
            SetPeripersonalParticipantNumber(parsedParticipantNumber, loadLedgerEntry: false);
        }
        else
        {
            SetPeripersonalParticipantNumber(PeripersonalParticipantNumberMinimum, loadLedgerEntry: false);
        }

        PeripersonalParticipantNameDraft = participantName;
        PeripersonalParticipantAgeDraft = state.Age;
        PeripersonalSessionIdDraft = state.SessionId;
        PeripersonalHandednessDraft = state.Handedness;
        PeripersonalLanguageCodeDraft = state.LanguageCode;

        if (!string.IsNullOrWhiteSpace(state.InitialConditionId))
        {
            var matchingCondition = Conditions.FirstOrDefault(condition =>
                string.Equals(condition.Id, state.InitialConditionId, StringComparison.OrdinalIgnoreCase));
            if (matchingCondition is not null)
            {
                SelectedCondition = matchingCondition;
            }
        }

        PeripersonalWorkflowLevel = OperationOutcomeKind.Success;
        PeripersonalWorkflowSummary = "Peripersonal session metadata is loaded.";
        PeripersonalWorkflowDetail = $"Loaded participant/session variables from {PeripersonalPreparationFilePath}. Start Experiment will send these values to the runtime and begin recording.";
        UpdateParticipantSessionState();
        RefreshPeripersonalWorkflowProperties();
    }

    private void CloseExperimentSessionWindow()
    {
        if (_experimentSessionWindow is null)
        {
            return;
        }

        _experimentSessionWindow.Closed -= OnExperimentSessionWindowClosed;
        _experimentSessionWindow.Close();
        _experimentSessionWindow = null;
    }

    private void OpenClockAlignmentWindow()
    {
        if (_clockAlignmentWindow is { IsLoaded: true })
        {
            if (_clockAlignmentWindow.WindowState == WindowState.Minimized)
            {
                _clockAlignmentWindow.WindowState = WindowState.Normal;
            }

            _clockAlignmentWindow.Activate();
            return;
        }

        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? _workflowGuideWindow
            ?? Application.Current?.MainWindow;

        var window = new StudyClockAlignmentWindow(this)
        {
            Owner = owner
        };
        window.Closed += OnClockAlignmentWindowClosed;
        _clockAlignmentWindow = window;
        window.Show();
        window.Activate();
    }

    private void OnClockAlignmentWindowClosed(object? sender, EventArgs e)
    {
        if (_clockAlignmentWindow is not null)
        {
            _clockAlignmentWindow.Closed -= OnClockAlignmentWindowClosed;
            _clockAlignmentWindow = null;
        }
    }

    private void CloseClockAlignmentWindow()
    {
        if (_clockAlignmentWindow is null)
        {
            return;
        }

        _clockAlignmentWindow.Closed -= OnClockAlignmentWindowClosed;
        _clockAlignmentWindow.Close();
        _clockAlignmentWindow = null;
    }

    private void CloseTwinEventsWindow()
    {
        if (_twinEventsWindow is null)
        {
            return;
        }

        _twinEventsWindow.Closed -= OnTwinEventsWindowClosed;
        _twinEventsWindow.Close();
        _twinEventsWindow = null;
    }
}
