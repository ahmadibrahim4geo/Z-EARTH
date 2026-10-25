using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ZEarth.Domain
{
    public class ProjectDocument
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "2.0.0";

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Untitled Project";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "Z EARTH Desktop Project";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "Ahmad Ibrahim";

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("modifiedDate")]
        public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("crs")]
        public string Crs { get; set; } = "EPSG:4326";

        [JsonPropertyName("sceneMode")]
        public SceneMode SceneMode { get; set; } = SceneMode.Scene3D;

        [JsonPropertyName("camera")]
        public CameraState Camera { get; set; } = CameraState.CreateHome();

        [JsonPropertyName("myPlaces")]
        public ObservableCollection<PlaceItem> MyPlaces { get; set; } = new ObservableCollection<PlaceItem>();

        [JsonPropertyName("temporaryPlaces")]
        public ObservableCollection<PlaceItem> TemporaryPlaces { get; set; } = new ObservableCollection<PlaceItem>();

        [JsonPropertyName("savedScenes")]
        public ObservableCollection<PlaceItem> SavedScenes { get; set; } = new ObservableCollection<PlaceItem>();

        public static ProjectDocument CreateDefault()
        {
            var doc = new ProjectDocument();

            // Built-in child folder: Saved Scenes inside My Places (SIDEBAR-01 Scope, Section 2)
            var savedScenesFolder = new PlaceItem("Saved Scenes", PlaceItemType.Folder)
            {
                Id = "folder_saved_scenes",
                IsProtected = true,
                IsExpanded = true
            };

            doc.MyPlaces.Add(savedScenesFolder);

            // Temporary Places is a real root folder in the same My Places tree.
            // Its children are still mirrored to the legacy session store on shutdown,
            // but the UI must never expose a second places tree.
            doc.MyPlaces.Add(new PlaceItem("Temporary Places", PlaceItemType.Folder)
            {
                Id = "folder_temporary_places",
                IsProtected = true,
                IsExpanded = true
            });

            // Synchronize legacy SavedScenes collection for backwards-compatibility
            doc.SyncSavedScenes();

            return doc;
        }

        public PlaceItem EnsureSavedScenesFolder()
        {
            var folder = MyPlaces.FirstOrDefault(p => p.ItemType == PlaceItemType.Folder &&
                (p.Id == "folder_saved_scenes" || string.Equals(p.Name, "Saved Scenes", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Saved Scenes")));

            if (folder == null)
            {
                folder = new PlaceItem("Saved Scenes", PlaceItemType.Folder)
                {
                    Id = "folder_saved_scenes",
                    IsProtected = true,
                    IsExpanded = true
                };
                if (SavedScenes.Count > 0)
                {
                    foreach (var s in SavedScenes)
                    {
                        folder.AddChild(s);
                    }
                }
                MyPlaces.Insert(0, folder);
            }
            else
            {
                folder.IsProtected = true;
                // Ensure it is at index 0 of MyPlaces
                int idx = MyPlaces.IndexOf(folder);
                if (idx > 0)
                {
                    MyPlaces.Move(idx, 0);
                }
            }

            SyncSavedScenes();
            return folder;
        }

        /// <summary>
        /// Returns the protected Temporary Places root folder and migrates data written by
        /// older builds from the separate TemporaryPlaces collection into that folder.
        /// </summary>
        public PlaceItem EnsureTemporaryPlacesFolder()
        {
            var folder = MyPlaces.FirstOrDefault(p => p.ItemType == PlaceItemType.Folder &&
                (p.Id == "folder_temporary_places" || string.Equals(p.Name, "Temporary Places", StringComparison.OrdinalIgnoreCase)));

            if (folder == null)
            {
                folder = new PlaceItem("Temporary Places", PlaceItemType.Folder)
                {
                    Id = "folder_temporary_places",
                    IsProtected = true,
                    IsExpanded = true
                };
                MyPlaces.Insert(Math.Min(1, MyPlaces.Count), folder);
            }
            else
            {
                folder.Id = "folder_temporary_places";
                folder.Name = "Temporary Places";
                folder.IsProtected = true;

                int targetIndex = Math.Min(1, MyPlaces.Count - 1);
                int currentIndex = MyPlaces.IndexOf(folder);
                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    MyPlaces.Move(currentIndex, targetIndex);
                }
            }

            // Backwards-compatible one-time migration from projects produced by older builds.
            if (!ReferenceEquals(TemporaryPlaces, folder.Children) && TemporaryPlaces.Count > 0)
            {
                foreach (var item in TemporaryPlaces.ToList())
                {
                    if (!folder.Children.Any(existing => existing.Id == item.Id))
                    {
                        folder.AddChild(item);
                    }
                }
                TemporaryPlaces.Clear();
            }

            return folder;
        }

        [Obsolete("Use EnsureTemporaryPlacesFolder(). The folder now belongs to the My Places tree.")]
        public PlaceItem CreateTemporaryPlacesViewFolder() => EnsureTemporaryPlacesFolder();

        /// <summary>
        /// Move a saved scene inside the dedicated Saved Scenes folder.
        /// The item at index zero is always the project startup scene.
        /// System folders and ordinary placemarks cannot be reordered by this method.
        /// </summary>
        public bool MoveSavedScene(PlaceItem? scene, int direction)
        {
            if (scene == null || scene.ItemType != PlaceItemType.SavedScene ||
                (direction != -1 && direction != 1))
                return false;

            var folder = EnsureSavedScenesFolder();
            var oldIndex = folder.Children.IndexOf(scene);
            var newIndex = oldIndex + direction;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= folder.Children.Count)
                return false;

            folder.Children.Move(oldIndex, newIndex);
            SyncSavedScenes(); // Reassigns the ★ startup marker to the new first scene.
            return true;
        }

        public void SyncSavedScenes()
        {
            var folder = MyPlaces.FirstOrDefault(p => p.ItemType == PlaceItemType.Folder &&
                (p.Id == "folder_saved_scenes" || string.Equals(p.Name, "Saved Scenes", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Saved Scenes")));

            if (folder != null)
            {
                SavedScenes.Clear();
                for (int i = 0; i < folder.Children.Count; i++)
                {
                    var child = folder.Children[i];
                    child.IsStartupScene = (i == 0);
                    SavedScenes.Add(child);
                }
            }
        }
    }
}
