using System.Windows;
using AttendanceManagementSystem.ViewModels;

namespace AttendanceManagementSystem.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
