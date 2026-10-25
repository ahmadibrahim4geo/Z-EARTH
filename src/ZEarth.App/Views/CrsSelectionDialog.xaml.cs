using System.Windows;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain.Crs;

namespace ZEarth.App.Views
{
    public partial class CrsSelectionDialog : Window
    {
        public CrsSelectionViewModel ViewModel { get; }
        public CrsDefinition? SelectedCrs => ViewModel.SelectedCrs;

        public CrsSelectionDialog(int initialEpsg = 4326) : this(null, initialEpsg) { }

        public CrsSelectionDialog(ICrsRegistry? registry, int initialEpsg = 4326)
        {
            InitializeComponent();
            ThemeService.Instance.ApplyWindowTheme(this, ThemeService.Instance.CurrentTheme);

            ViewModel = new CrsSelectionViewModel(registry, initialEpsg);
            DataContext = ViewModel;
        }

        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is CrsTreeNode node)
            {
                ViewModel.SelectedNode = node;
                if (!node.IsCategory && node.Crs != null)
                {
                    ViewModel.SelectedCrs = node.Crs;
                }
            }
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CanApply && ViewModel.SelectedCrs != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
