using System.Windows;
using ViscerealityCompanion.App.ViewModels;

namespace ViscerealityCompanion.MinimalRunner;

public partial class SessionSetupWindow : Window
{
    private bool _isStarting;

    public SessionSetupWindow(StudyShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PreparationParticipantNameTextBox.Focus();
        PreparationParticipantNameTextBox.SelectAll();
    }

    private async void StartSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStarting || DataContext is not StudyShellViewModel viewModel)
        {
            return;
        }

        _isStarting = true;
        var originalContent = StartSessionButton.Content;
        StartSessionButton.IsEnabled = false;
        StartSessionButton.Content = "Starting...";
        try
        {
            if (!viewModel.CanSavePeripersonalSessionPreparation)
            {
                MessageBox.Show(
                    "Please enter participant name, participant number, age, and handedness before starting the session.",
                    "Session Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (await viewModel.StartPluggedInPeripersonalSessionAsync().ConfigureAwait(true))
            {
                DialogResult = true;
            }
        }
        finally
        {
            _isStarting = false;
            StartSessionButton.Content = originalContent;
            StartSessionButton.IsEnabled = viewModel.CanSavePeripersonalSessionPreparation;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
