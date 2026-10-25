using System.Windows;
using System.Windows.Input;
using ZEarth.App.ViewModels;

namespace ZEarth.App.Views
{
    public partial class SplashScreenView : Window
    {
        public SplashViewModel ViewModel => (SplashViewModel)DataContext;

        public SplashScreenView()
        {
            InitializeComponent();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CancelStartup();
                }
            };
        }

        private void CancelStartup()
        {
            ViewModel.CancelStartup();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelStartup();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(-1);
        }
    }
}
