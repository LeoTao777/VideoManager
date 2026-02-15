using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VideoManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //TrySetIcon();
        }

        //private void TrySetIcon()
        //{
        //    try
        //    {
        //        var uri = new Uri("pack://application:,,,/Assets/Ico/play_market_icon.ico", UriKind.Absolute);
        //        Icon = BitmapFrame.Create(uri);
        //    }
        //    catch
        //    {
        //        // 图标文件不存在或未作为 Resource 包含时忽略
        //    }
        //}
    }
}