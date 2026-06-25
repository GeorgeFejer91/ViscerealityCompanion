using System.Windows;
using ViscerealityCompanion.App.ViewModels;

namespace ViscerealityCompanion.MinimalRunner;

public partial class ExperimentRunnerWindow : Window
{
    public ExperimentRunnerWindow(StudyShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
