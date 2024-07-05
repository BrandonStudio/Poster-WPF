#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Poster
{
    /// <summary>
    /// InputLine.xaml 的交互逻辑
    /// </summary>
    [ContentProperty(nameof(MainContent))]
    public partial class HintLine : UserControl
    {
        public HintLine()
        {
            InitializeComponent();
        }

        //protected override void OnInitialized(EventArgs e)
        //{
        //    base.OnInitialized(e);

        //    var content = new ContentPresenter
        //    {
        //        Content = MainContent
        //    };
        //}

        public string Hint
        {
            get => (string)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        public double HintWidth
        {
            get => (double)GetValue(HintWidthProperty);
            set => SetValue(HintWidthProperty, value);
        }

        public object MainContent
        {
            get => GetValue(MainContentProperty);
            set => SetValue(MainContentProperty, value);
        }

        public static readonly DependencyProperty HintProperty =
            DependencyProperty.Register(
                nameof(Hint), typeof(string), typeof(HintLine),
                new PropertyMetadata((o, e) =>
                {
                    var ctrl = o as HintLine;
                    ctrl!.hintLabel.Content = e.NewValue;
                }));

        public static readonly DependencyProperty HintWidthProperty =
            DependencyProperty.Register(
                nameof(HintWidth), typeof(double), typeof(HintLine),
                new PropertyMetadata((o, e) =>
                {
                    var ctrl = o as HintLine;
                    ctrl!.hintLabel.Width = (double)e.NewValue;
                }));

        public static readonly DependencyProperty MainContentProperty =
            DependencyProperty.Register(
                nameof(MainContent), typeof(object), typeof(HintLine), new PropertyMetadata());

        //public event TextChangedEventHandler TextChanged
        //{
        //    add => input.TextChanged += value;
        //    remove => input.TextChanged -= value;
        //}
    }
}
