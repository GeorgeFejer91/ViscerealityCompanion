using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ViscerealityCompanion.App.ViewModels;
using ViscerealityCompanion.Core.Services;

namespace ViscerealityCompanion.MinimalRunner;

public partial class App : Application
{
    private StudyShellViewModel? _viewModel;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var study = await MinimalRunnerBootstrap.LoadPeripersonalStudyAsync().ConfigureAwait(true);
            _viewModel = new StudyShellViewModel(study);
            _viewModel.LoadPeripersonalSessionPreparation();

            var setupWindow = new SessionSetupWindow(_viewModel);
            if (setupWindow.ShowDialog() != true)
            {
                _viewModel.Dispose();
                _viewModel = null;
                Shutdown();
                return;
            }

            var runnerWindow = new ExperimentRunnerWindow(_viewModel);
            MainWindow = runnerWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            runnerWindow.Closed += OnRunnerWindowClosed;
            runnerWindow.Show();
        }
        catch (Exception ex)
        {
            var logPath = WriteUnhandledExceptionLog("startup", ex, isTerminating: false);
            MessageBox.Show(
                $"The minimal runner could not start.{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Crash log: {logPath}",
                "Viscereality Minimal Runner",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnRunnerWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Closed -= OnRunnerWindowClosed;
        }

        _viewModel?.Dispose();
        _viewModel = null;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = WriteUnhandledExceptionLog("dispatcher", e.Exception, isTerminating: false);
        MessageBox.Show(
            $"The minimal runner hit an unhandled error and needs to close.{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}{Environment.NewLine}{Environment.NewLine}Crash log: {logPath}",
            "Viscereality Minimal Runner",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(-1);
    }

    private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception.");
        WriteUnhandledExceptionLog("appdomain", exception, e.IsTerminating);
    }

    private static void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteUnhandledExceptionLog("task", e.Exception, isTerminating: false);
        e.SetObserved();
    }

    private static string WriteUnhandledExceptionLog(string source, Exception exception, bool isTerminating)
    {
        try
        {
            var logDirectory = CompanionOperatorDataLayout.LogsRootPath;
            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(
                logDirectory,
                $"minimal-runner-unhandled-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{source}.log");

            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Source: {source}");
            builder.AppendLine($"IsTerminating: {isTerminating}");
            builder.AppendLine($"Message: {exception.Message}");
            builder.AppendLine();
            builder.AppendLine(exception.ToString());

            File.WriteAllText(logPath, builder.ToString());
            return logPath;
        }
        catch (Exception logException)
        {
            return $"Failed to write crash log: {logException.Message}";
        }
    }
}
