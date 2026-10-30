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

    public enum CrsTabFilter
    {
        Horizontal,
        Vertical,
        All
    }

    public class CrsSelectionViewModel : ViewModelBase
    {
        private readonly ICrsRegistry _crsRegistry;
        private string _searchQuery = string.Empty;
        private CrsDefinition? _selectedCrs;
        private CrsTreeNode? _selectedNode;
        private CrsTabFilter _tabFilter = CrsTabFilter.Horizontal;

        public ObservableCollection<CrsTreeNode> RootNodes { get; } = new();
        public ObservableCollection<CrsDefinition> SearchResults { get; } = new();

        public bool IsSearching => !string.IsNullOrWhiteSpace(SearchQuery);
        public bool IsTreeVisible => !IsSearching;
        public bool IsSearchResultsVisible => IsSearching;

        public string FavoriteButtonText => IsSelectedFavorite ? "★ In Favorites" : "☆ Add to Favorites";

        public CrsTabFilter SelectedTabFilter
        {
            get => _tabFilter;
            set
            {
                if (SetProperty(ref _tabFilter, value))
                {
                    OnPropertyChanged(nameof(IsHorizontalTabSelected));
                    OnPropertyChanged(nameof(IsVerticalTabSelected));
                    OnPropertyChanged(nameof(IsAllTabSelected));
                    BuildTree();
                }
            }
        }

        public bool IsHorizontalTabSelected
        {
            get => _tabFilter == CrsTabFilter.Horizontal;
            set { if (value) SelectedTabFilter = CrsTabFilter.Horizontal; }
        }

        public bool IsVerticalTabSelected
        {
            get => _tabFilter == CrsTabFilter.Vertical;
            set { if (value) SelectedTabFilter = CrsTabFilter.Vertical; }
        }

        public bool IsAllTabSelected
        {
            get => _tabFilter == CrsTabFilter.All;
            set { if (value) SelectedTabFilter = CrsTabFilter.All; }
        }

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

                var projectionStatus = SelectedCrs.IsVerticalOnly
                    ? "⚠️ NOTICE: Vertical height CRS. Informational only; cannot be applied to 2D map plane.\n"
                    : (SelectedCrs.IsProjected
                        ? "🗺️ 2D Map Reprojection: Supported (Planar Map Projection will adapt 2D view in real-time).\n"
                        : "🌐 2D Map Reprojection: Supported (Equirectangular / Plate Carrée 2D View).\n");

                return $"Authority / Code: {SelectedCrs.AuthorityCode}\n" +
                       $"Name: {SelectedCrs.Name}\n" +
                       $"Category: {SelectedCrs.CategoryPath}\n" +
                       $"Area of Use: {SelectedCrs.AreaOfUse}\n" +
                       $"Type: {(SelectedCrs.IsVerticalOnly ? "Vertical Height" : (SelectedCrs.IsProjected ? "Projected (Cartesian X/Y)" : "Geographic 2D (Lat/Lon)"))}\n" +
                       $"Datum: {SelectedCrs.Datum}\n" +
                       $"Ellipsoid: {SelectedCrs.Ellipsoid}\n" +
                       $"Units: {SelectedCrs.Unit}\n" +
                       (string.IsNullOrEmpty(SelectedCrs.ProjectionMethod) ? "" : $"Projection Method: {SelectedCrs.ProjectionMethod}\n") +
                       $"{projectionStatus}" +
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
            ? $"ArcGIS Pro-aligned CRS Catalog: {_crsRegistry.GetAll().Count:N0} systems • Horizontal & Vertical systems hierarchy"
            : $"Curated Core CRS Catalog: {_crsRegistry.GetAll().Count:N0} systems (UTM 1-60 N/S, Egypt Belts, World, Polar)";

        private void BuildTree()
        {
            RootNodes.Clear();
            var allCrs = _crsRegistry.GetAll();

            // Filter according to active tab
            IEnumerable<CrsDefinition> filteredCrs = SelectedTabFilter switch
            {
                CrsTabFilter.Horizontal => allCrs.Where(c => !c.IsVerticalOnly),
                CrsTabFilter.Vertical => allCrs.Where(c => c.IsVerticalOnly),
                _ => allCrs
            };

            // 1. Favorites Node
            var favNode = new CrsTreeNode { Name = "Favorites", IsCategory = true };
            var favList = SettingsService.Instance.Settings.FavoriteCrsList;
            foreach (var epsg in favList)
            {
                var crs = _crsRegistry.GetByEpsg(epsg);
                if (crs != null)
                {
                    if (SelectedTabFilter == CrsTabFilter.Horizontal && crs.IsVerticalOnly) continue;
                    if (SelectedTabFilter == CrsTabFilter.Vertical && !crs.IsVerticalOnly) continue;

                    favNode.Children.Add(new CrsTreeNode
                    {
                        Name = $"{crs.AuthorityCode} - {crs.Name}",
                        IsCategory = false,
                        Crs = crs
                    });
                }
            }
            if (favNode.Children.Count > 0 || SelectedTabFilter != CrsTabFilter.Vertical)
            {
                RootNodes.Add(favNode);
            }

            // 2. Layers Node (Current Map CRS)
            var layersNode = new CrsTreeNode { Name = "Layers (Current Map CRS)", IsCategory = true };
            var inUseCodes = new[] { SettingsService.Instance.Settings.MapCrsEpsg };
            foreach (var code in inUseCodes)
            {
                var crs = _crsRegistry.GetByEpsg(code);
                if (crs != null)
                {
                    if (SelectedTabFilter == CrsTabFilter.Vertical && !crs.IsVerticalOnly) continue;

                    layersNode.Children.Add(new CrsTreeNode
                    {
                        Name = $"{crs.AuthorityCode} - {crs.Name} ({crs.AreaOfUse})",
                        IsCategory = false,
                        Crs = crs
                    });
                }
            }
            if (layersNode.Children.Count > 0)
            {
                RootNodes.Add(layersNode);
            }

            // 3. Hierarchical Taxonomy from CategoryPath
            var categoryMap = new Dictionary<string, CrsTreeNode>();

            foreach (var crs in filteredCrs)
            {
                var path = crs.CategoryPath;
                if (string.IsNullOrEmpty(path))
                {
                    path = crs.IsVerticalOnly
                        ? "Vertical Coordinate Systems/Gravity-related"
                        : (crs.IsProjected ? "Projected Coordinate Systems/Other" : "Geographic Coordinate Systems/World");
                }

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
                if (SelectedTabFilter == CrsTabFilter.Horizontal && crs.IsVerticalOnly) continue;
                if (SelectedTabFilter == CrsTabFilter.Vertical && !crs.IsVerticalOnly) continue;

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
