using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Poster
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ComboBox methodSelector;

        readonly IEnumerable<HttpMethod> _httpMethods =
            typeof(HttpMethod).GetProperties()
            .Where(p => p.PropertyType == typeof(HttpMethod))
            .Select(p => (HttpMethod)p.GetValue(null));

        public ObservableCollection<RequestHeader> RequestHeaders { get; set; } =
            new ObservableCollection<RequestHeader>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            methodSelector = this.FindUid("methodSelector") as ComboBox;
            methodSelector.ItemsSource = _httpMethods;
            methodSelector.SelectedIndex = 0;
        }


    }

    public class RequestHeader
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
