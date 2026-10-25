using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ZEarth.App.Services;
using ZEarth.Domain.Crs;

namespace ZEarth.App.ViewModels
{
    public class CrsTreeNode
    {
        public string Name { get; set; } = string.Empty;
        public bool IsCategory { get; set; } = true;
        public CrsDefinition? Crs { get; set; }
        public ObservableCollection<CrsTreeNode> Children { get; } = new();

        public override string ToString() => IsCategory ? $"[{Name}]" : Crs?.ToString() ?? Name;
    }

    public class CrsSelectionViewModel : ViewModelBase
    {
        private readonly ICrsRegistry _crsRegistry;
        private string _searchQuery = string.Empty;
        private CrsDefinition? _selectedCrs;
        private CrsTreeNode? _selectedNode;

        public ObservableCollection<CrsTreeNode> RootNodes { get; } = new();
        public ObservableCollection<CrsDefinition> SearchResults { get; } = new();

        public bool IsSearching => !string.IsNullOrWhiteSpace(SearchQuery);
        public bool IsTreeVisible => !IsSearching;
        public bool IsSearchResultsVisible => IsSearching;

        public string FavoriteButtonText => IsSelectedFavorite ? "★ In Favorites" : "☆ Add to Favorites";

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    OnPropertyChanged(nameof(IsSearching));
                    OnPropertyChanged(nameof(IsTreeVisible));
                    OnPropertyChanged(nameof(IsSearchResultsVisible));
                    PerformSearch();
                }
            }
        }

        public CrsDefinition? SelectedCrs
        {
            get => _selectedCrs;
            set
            {
                if (SetProperty(ref _selectedCrs, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(CanApply));
                    OnPropertyChanged(nameof(IsSelectedFavorite));
                    OnPropertyChanged(nameof(FavoriteButtonText));
                    OnPropertyChanged(nameof(SelectedCrsDetails));
                }
            }
        }

        public CrsTreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    if (value != null && !value.IsCategory && value.Crs != null)
                    {
                        SelectedCrs = value.Crs;
                    }
                }
            }
        }

        public bool HasSelection => SelectedCrs != null;
        public bool CanApply => SelectedCrs != null && !SelectedCrs.IsVerticalOnly;

        public bool IsSelectedFavorite => SelectedCrs != null && SettingsService.Instance.Settings.FavoriteCrsList.Contains(SelectedCrs.EpsgCode);

        public string SelectedCrsDetails
        {
            get
            {
                if (SelectedCrs == null) return "Select a coordinate system from the catalog tree or search results.";

                var verticalNotice = SelectedCrs.IsVerticalOnly
                    ? "\n⚠️ NOTICE: Vertical-only CRS. Informational only; cannot be used as horizontal 2D map projection.\n"
                    : "";

                return $"Authority / Code: {SelectedCrs.AuthorityCode}\n" +
                       $"Name: {SelectedCrs.Name}\n" +
                       $"Category: {SelectedCrs.CategoryPath}\n" +
                       $"Area of Use: {SelectedCrs.AreaOfUse}\n" +
                       $"Type: {(SelectedCrs.IsVerticalOnly ? "Vertical Height" : (SelectedCrs.IsProjected ? "Projected (Cartesian X/Y)" : "Geographic 2D (Lat/Lon)"))}\n" +
                       $"Datum: {SelectedCrs.Datum}\n" +
                       $"Ellipsoid: {SelectedCrs.Ellipsoid}\n" +
                       $"Units: {SelectedCrs.Unit}\n" +
                       (string.IsNullOrEmpty(SelectedCrs.ProjectionMethod) ? "" : $"Projection Method: {SelectedCrs.ProjectionMethod}\n") +
                       verticalNotice +
                       (string.IsNullOrEmpty(SelectedCrs.Wkt) ? "" : $"\nWKT Definition:\n{SelectedCrs.Wkt}");
            }
        }

        public ICommand ToggleFavoriteCommand { get; }

        public CrsSelectionViewModel(ICrsRegistry? registry = null, int initialEpsg = 4326)
        {
            _crsRegistry = registry ?? new CrsRegistry();
            ToggleFavoriteCommand = new RelayCommand(ExecuteToggleFavorite);

            BuildTree();

            // Default selection to WGS 84 (EPSG:4326) or initialEpsg
            SelectedCrs = _crsRegistry.GetByEpsg(initialEpsg) ?? _crsRegistry.GetByEpsg(4326);
        }

        public string CatalogStatus => _crsRegistry.GetAll().Count > 1000
            ? $"Offline EPSG/PROJ catalog: {_crsRegistry.GetAll().Count:N0} systems • geographic folders derived from areas of use"
            : "Limited built-in CRS presets: proj.db unavailable; install the complete GDAL/PROJ runtime.";

        private void BuildTree()
        {
            RootNodes.Clear();
            var allCrs = _crsRegistry.GetAll();

            // 1. Favorites Node
            var favNode = new CrsTreeNode { Name = "Favorites", IsCategory = true };
            var favList = SettingsService.Instance.Settings.FavoriteCrsList;
            foreach (var epsg in favList)
            {
                var crs = _crsRegistry.GetByEpsg(epsg);
                if (crs != null)
                {
                    favNode.Children.Add(new CrsTreeNode
                    {
                        Name = $"{crs.AuthorityCode} - {crs.Name}",
                        IsCategory = false,
                        Crs = crs
                    });
                }
            }
            RootNodes.Add(favNode);

            // Project CRS is live configuration, not fabricated EPSG:32636.
            // Source-layer CRSs must be added only when the active layer exposes them.
            var layersNode = new CrsTreeNode { Name = "Layers (Current Map CRS)", IsCategory = true };
            var inUseCodes = new[] { SettingsService.Instance.Settings.MapCrsEpsg };
            foreach (var code in inUseCodes)
            {
                var crs = _crsRegistry.GetByEpsg(code);
                if (crs != null)
                {
                    layersNode.Children.Add(new CrsTreeNode
                    {
                        Name = $"{crs.AuthorityCode} - {crs.Name} ({crs.AreaOfUse})",
                        IsCategory = false,
                        Crs = crs
                    });
                }
            }
            RootNodes.Add(layersNode);

            // 3. Hierarchical Taxonomy from CategoryPath
            var categoryMap = new Dictionary<string, CrsTreeNode>();

            foreach (var crs in allCrs)
            {
                var path = crs.CategoryPath;
                if (string.IsNullOrEmpty(path)) path = crs.IsProjected ? "Projected Coordinate Systems/Other" : "Geographic Coordinate Systems/World";

                var parts = path.Split('/');
                CrsTreeNode? parentNode = null;
                var currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = (i == 0) ? parts[0] : $"{currentPath}/{parts[i]}";
                    if (!categoryMap.TryGetValue(currentPath, out var node))
                    {
                        node = new CrsTreeNode { Name = parts[i], IsCategory = true };
                        categoryMap[currentPath] = node;

                        if (parentNode == null)
                        {
                            RootNodes.Add(node);
                        }
                        else
                        {
                            parentNode.Children.Add(node);
                        }
                    }
                    parentNode = node;
                }

                parentNode?.Children.Add(new CrsTreeNode
                {
                    Name = $"{crs.AuthorityCode} - {crs.Name}",
                    IsCategory = false,
                    Crs = crs
                });
            }
        }

        private void PerformSearch()
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            var results = _crsRegistry.Search(SearchQuery);
            foreach (var crs in results)
            {
                SearchResults.Add(crs);
            }

            if (SearchResults.Count > 0 && (SelectedCrs == null || !SearchResults.Contains(SelectedCrs)))
            {
                SelectedCrs = SearchResults.First();
            }
        }

        private void ExecuteToggleFavorite()
        {
            if (SelectedCrs == null) return;
            var list = SettingsService.Instance.Settings.FavoriteCrsList;
            if (list.Contains(SelectedCrs.EpsgCode))
            {
                list.Remove(SelectedCrs.EpsgCode);
            }
            else
            {
                list.Add(SelectedCrs.EpsgCode);
            }
            SettingsService.Instance.Save();

            OnPropertyChanged(nameof(IsSelectedFavorite));
            OnPropertyChanged(nameof(FavoriteButtonText));
            BuildTree();
        }
    }
}
