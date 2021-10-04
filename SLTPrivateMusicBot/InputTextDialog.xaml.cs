namespace SLTPrivateMusicBot
{
    using MahApps.Metro.Controls;
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
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Shapes;

    /// <summary>
    /// Interaction logic for InputTextDialog.xaml
    /// </summary>
    public partial class InputTextDialog : MetroWindow
    {
        public string URL { get; set; }

        public InputTextDialog()
        {
            this.InitializeComponent();
        }

        private void Btn_Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void TB_Url_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.URL = this.TB_Url.Text;
        }
    }
}
