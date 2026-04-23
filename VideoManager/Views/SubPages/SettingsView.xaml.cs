using System.Windows;
using System.Windows.Controls;
using VideoManager.ViewModels;

namespace VideoManager.Views.SubPages
{
    /// <summary>
    /// SettingsView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.Reload();
            }
        }
    }
}
