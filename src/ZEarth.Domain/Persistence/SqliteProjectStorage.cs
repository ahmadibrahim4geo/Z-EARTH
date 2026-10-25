using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ZEarth.Domain;

namespace ZEarth.Domain.Persistence
{
    /// <summary>
    /// Authoritative SQLite project persistence provider conforming to ADR-004 and 02_PROJECT_PLACES_PERSISTENCE_AR.md.
    /// Manages the internal project.db database inside .zearth ZIP packages.
    /// </summary>
    public static class SqliteProjectStorage
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        public static void SaveToDatabase(ProjectDocument project, string sqliteFilePath)
        {
            if (File.Exists(sqliteFilePath))
            {
                File.Delete(sqliteFilePath);
            }

            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = sqliteFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString();

            using var conn = new SqliteConnection(connStr);
            conn.Open();

            using var trans = conn.BeginTransaction();

            // Create schema
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS project_metadata (
                        id TEXT PRIMARY KEY,
                        schema_version TEXT NOT NULL,
                        name TEXT NOT NULL,
                        description TEXT,
                        author TEXT,
                        created_date TEXT NOT NULL,
                        modified_date TEXT NOT NULL,
                        crs TEXT NOT NULL,
                        scene_mode TEXT NOT NULL,
                        camera_json TEXT
                    );

                    CREATE TABLE IF NOT EXISTS places (
                        id TEXT PRIMARY KEY,
                        collection_type TEXT NOT NULL,
                        parent_id TEXT,
                        order_index INTEGER NOT NULL,
                        name TEXT NOT NULL,
                        description TEXT,
                        item_type TEXT NOT NULL,
                        is_visible INTEGER NOT NULL,
                        is_expanded INTEGER NOT NULL,
                        latitude REAL,
                        longitude REAL,
                        altitude REAL,
                        color_hex TEXT,
                        style_url TEXT,
                        camera_json TEXT,
                        coordinates_json TEXT,
                        inner_rings_json TEXT,
                        extended_data_json TEXT,
                        preserved_xml_json TEXT,
                        item_extra_json TEXT
                    );

                    CREATE INDEX IF NOT EXISTS idx_places_collection ON places(collection_type);
                    CREATE INDEX IF NOT EXISTS idx_places_parent ON places(parent_id);
                ";
                cmd.ExecuteNonQuery();
            }

            // Insert project metadata
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;
                cmd.CommandText = @"
                    INSERT INTO project_metadata (
                        id, schema_version, name, description, author,
                        created_date, modified_date, crs, scene_mode, camera_json
                    ) VALUES (
                        $id, $schemaVersion, $name, $description, $author,
                        $createdDate, $modifiedDate, $crs, $sceneMode, $cameraJson
                    );
                ";
                cmd.Parameters.AddWithValue("$id", project.Id);
                cmd.Parameters.AddWithValue("$schemaVersion", project.SchemaVersion);
                cmd.Parameters.AddWithValue("$name", project.Name);
                cmd.Parameters.AddWithValue("$description", project.Description ?? string.Empty);
                cmd.Parameters.AddWithValue("$author", project.Author ?? "Ahmad Ibrahim");
                cmd.Parameters.AddWithValue("$createdDate", project.CreatedDate.ToString("O"));
                cmd.Parameters.AddWithValue("$modifiedDate", project.ModifiedDate.ToString("O"));
                cmd.Parameters.AddWithValue("$crs", project.Crs ?? "EPSG:4326");
                cmd.Parameters.AddWithValue("$sceneMode", project.SceneMode.ToString());
                cmd.Parameters.AddWithValue("$cameraJson", JsonSerializer.Serialize(project.Camera, _jsonOptions));
                cmd.ExecuteNonQuery();
            }

            // Normalize both protected root folders before writing. This also migrates
            // Temporary Places created by builds that persisted it as a separate collection.
            // Preserve scenes inserted via the legacy SavedScenes collection. Ensuring the
            // protected folder calls SyncSavedScenes(), which would otherwise clear this
            // legacy collection *before* pending additions can be written to SQLite.
            var newlyAddedScenes = project.SavedScenes.ToList();
            project.EnsureSavedScenesFolder();
            project.EnsureTemporaryPlacesFolder();

            // If MyPlaces has a Saved Scenes folder, ensure any newly added scenes in project.SavedScenes are attached to it
            var scenesFolderOnSave = project.MyPlaces.FirstOrDefault(p => p.ItemType == PlaceItemType.Folder &&
                (p.Id == "folder_saved_scenes" || string.Equals(p.Name, "Saved Scenes", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Saved Scenes")));

            if (scenesFolderOnSave != null)
            {
                foreach (var scene in newlyAddedScenes)
                {
                    if (!scenesFolderOnSave.Children.Any(c => c.Id == scene.Id || c.Name == scene.Name))
                    {
                        scenesFolderOnSave.AddChild(scene);
                    }
                }
                // Keep the migration/legacy view synchronized with the authoritative tree.
                project.SyncSavedScenes();
            }

            // Insert Places collections
            var insertedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            InsertPlaceCollection(conn, trans, project.MyPlaces, "my_places", null, insertedIds);
            InsertPlaceCollection(conn, trans, project.TemporaryPlaces, "temporary_places", null, insertedIds);
            InsertPlaceCollection(conn, trans, project.SavedScenes, "saved_scenes", null, insertedIds);

            trans.Commit();
            conn.Close();
            SqliteConnection.ClearPool(conn);
        }

        private static void InsertPlaceCollection(SqliteConnection conn, SqliteTransaction trans, IEnumerable<PlaceItem> items, string collectionType, string? parentId, HashSet<string> insertedIds)
        {
            int order = 0;
            foreach (var item in items)
            {
                if (!insertedIds.Add(item.Id))
                {
                    continue;
                }

                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;
                cmd.CommandText = @"
                    INSERT INTO places (
                        id, collection_type, parent_id, order_index, name, description,
                        item_type, is_visible, is_expanded, latitude, longitude, altitude,
                        color_hex, style_url, camera_json, coordinates_json, inner_rings_json,
                        extended_data_json, preserved_xml_json, item_extra_json
                    ) VALUES (
                        $id, $collectionType, $parentId, $orderIndex, $name, $description,
                        $itemType, $isVisible, $isExpanded, $latitude, $longitude, $altitude,
                        $colorHex, $styleUrl, $cameraJson, $coordsJson, $innerRingsJson,
                        $extendedDataJson, $preservedXmlJson, $itemExtraJson
                    );
                ";

                cmd.Parameters.AddWithValue("$id", item.Id);
                cmd.Parameters.AddWithValue("$collectionType", collectionType);
                cmd.Parameters.AddWithValue("$parentId", (object?)parentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$orderIndex", order++);
                cmd.Parameters.AddWithValue("$name", item.Name ?? "Unnamed");
                cmd.Parameters.AddWithValue("$description", item.Description ?? string.Empty);
                cmd.Parameters.AddWithValue("$itemType", item.ItemType.ToString());
                cmd.Parameters.AddWithValue("$isVisible", item.IsVisible ? 1 : 0);
                cmd.Parameters.AddWithValue("$isExpanded", item.IsExpanded ? 1 : 0);
                cmd.Parameters.AddWithValue("$latitude", (object?)item.Latitude ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$longitude", (object?)item.Longitude ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$altitude", (object?)item.Altitude ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$colorHex", item.ColorHex ?? "#00c8ff");
                cmd.Parameters.AddWithValue("$styleUrl", (object?)item.StyleUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$cameraJson", item.Camera != null ? JsonSerializer.Serialize(item.Camera, _jsonOptions) : DBNull.Value);
                cmd.Parameters.AddWithValue("$coordsJson", item.Coordinates.Count > 0 ? JsonSerializer.Serialize(item.Coordinates, _jsonOptions) : DBNull.Value);
                cmd.Parameters.AddWithValue("$innerRingsJson", item.InnerRings.Count > 0 ? JsonSerializer.Serialize(item.InnerRings, _jsonOptions) : DBNull.Value);
                cmd.Parameters.AddWithValue("$extendedDataJson", item.ExtendedData.Count > 0 ? JsonSerializer.Serialize(item.ExtendedData, _jsonOptions) : DBNull.Value);
                cmd.Parameters.AddWithValue("$preservedXmlJson", item.PreservedXmlFragments.Count > 0 ? JsonSerializer.Serialize(item.PreservedXmlFragments, _jsonOptions) : DBNull.Value);
                // Avoid silently dropping KML TimeSpan and KMZ assets on .zearth Save As.
                cmd.Parameters.AddWithValue("$itemExtraJson", JsonSerializer.Serialize(new PlacePersistedExtras
                {
                    RadiusMeters = item.RadiusMeters,
                    IsProtected = item.IsProtected,
                    IsStartupScene = item.IsStartupScene,
                    TimeSpanBegin = item.TimeSpanBegin,
                    TimeSpanEnd = item.TimeSpanEnd,
                    KmzResourcesBase64 = item.KmzResourcesBase64
                }, _jsonOptions));

                cmd.ExecuteNonQuery();

                if (item.Children.Count > 0)
                {
                    InsertPlaceCollection(conn, trans, item.Children, collectionType, item.Id, insertedIds);
                }
            }
        }

        private sealed class PlacePersistedExtras
        {
            public double? RadiusMeters { get; set; }
            public bool IsProtected { get; set; }
            public bool IsStartupScene { get; set; }
            public string? TimeSpanBegin { get; set; }
            public string? TimeSpanEnd { get; set; }
            public Dictionary<string, string> KmzResourcesBase64 { get; set; } = new();
        }

        public static ProjectDocument LoadFromDatabase(string sqliteFilePath)
        {
            if (!File.Exists(sqliteFilePath))
            {
                throw new FileNotFoundException($"SQLite database not found: {sqliteFilePath}");
            }

            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = sqliteFilePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();

            using var conn = new SqliteConnection(connStr);
            conn.Open();

            var project = new ProjectDocument();

            // Load Metadata
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, schema_version, name, description, author, created_date, modified_date, crs, scene_mode, camera_json FROM project_metadata LIMIT 1;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    project.Id = reader.GetString(0);
                    project.SchemaVersion = reader.GetString(1);
                    project.Name = reader.GetString(2);
                    project.Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    project.Author = reader.IsDBNull(4) ? "Ahmad Ibrahim" : reader.GetString(4);
                    if (DateTime.TryParse(reader.GetString(5), out var cd)) project.CreatedDate = cd;
                    if (DateTime.TryParse(reader.GetString(6), out var md)) project.ModifiedDate = md;
                    project.Crs = reader.IsDBNull(7) ? "EPSG:4326" : reader.GetString(7);
                    if (Enum.TryParse<SceneMode>(reader.GetString(8), out var sm)) project.SceneMode = sm;
                    if (!reader.IsDBNull(9))
                    {
                        var camJson = reader.GetString(9);
                        project.Camera = JsonSerializer.Deserialize<CameraState>(camJson, _jsonOptions) ?? CameraState.CreateHome();
                    }
                }
            }

            // Prior project.db schemas do not contain item_extra_json; preserve
            // backwards compatibility when opening an older .zearth project.
            bool hasItemExtras = false;
            using (var schema = conn.CreateCommand())
            {
                schema.CommandText = "PRAGMA table_info(places);";
                using var columns = schema.ExecuteReader();
                while (columns.Read())
                    if (columns.GetString(1).Equals("item_extra_json", StringComparison.OrdinalIgnoreCase))
                        hasItemExtras = true;
            }

            // Load Places
            var allItems = new Dictionary<string, (PlaceItem Item, string? ParentId, string CollectionType, int Order)>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, collection_type, parent_id, order_index, name, description,
                           item_type, is_visible, is_expanded, latitude, longitude, altitude,
                           color_hex, style_url, camera_json, coordinates_json, inner_rings_json,
                           extended_data_json, preserved_xml_json" +
                    (hasItemExtras ? ", item_extra_json" : "") + @"
                    FROM places ORDER BY order_index ASC;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string colType = reader.GetString(1);
                    string? parentId = reader.IsDBNull(2) ? null : reader.GetString(2);
                    int order = reader.GetInt32(3);

                    var item = new PlaceItem
                    {
                        Id = id,
                        Name = reader.GetString(4),
                        Description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        ItemType = Enum.TryParse<PlaceItemType>(reader.GetString(6), out var it) ? it : PlaceItemType.Placemark,
                        IsVisible = reader.GetInt32(7) != 0,
                        IsExpanded = reader.GetInt32(8) != 0,
                        Latitude = reader.IsDBNull(9) ? null : reader.GetDouble(9),
                        Longitude = reader.IsDBNull(10) ? null : reader.GetDouble(10),
                        Altitude = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                        ColorHex = reader.IsDBNull(12) ? "#00c8ff" : reader.GetString(12),
                        StyleUrl = reader.IsDBNull(13) ? null : reader.GetString(13)
                    };

                    if (!reader.IsDBNull(14))
                    {
                        item.Camera = JsonSerializer.Deserialize<CameraState>(reader.GetString(14), _jsonOptions);
                    }
                    if (!reader.IsDBNull(15))
                    {
                        item.Coordinates = JsonSerializer.Deserialize<List<CoordinatePoint>>(reader.GetString(15), _jsonOptions) ?? new();
                    }
                    if (!reader.IsDBNull(16))
                    {
                        item.InnerRings = JsonSerializer.Deserialize<List<List<CoordinatePoint>>>(reader.GetString(16), _jsonOptions) ?? new();
                    }
                    if (!reader.IsDBNull(17))
                    {
                        item.ExtendedData = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(17), _jsonOptions) ?? new();
                    }
                    if (!reader.IsDBNull(18))
                    {
                        item.PreservedXmlFragments = JsonSerializer.Deserialize<List<string>>(reader.GetString(18), _jsonOptions) ?? new();
                    }

                    if (hasItemExtras && !reader.IsDBNull(19))
                    {
                        var extra = JsonSerializer.Deserialize<PlacePersistedExtras>(reader.GetString(19), _jsonOptions);
                        if (extra != null)
                        {
                            item.RadiusMeters = extra.RadiusMeters;
                            item.IsProtected = extra.IsProtected;
                            item.IsStartupScene = extra.IsStartupScene;
                            item.TimeSpanBegin = extra.TimeSpanBegin;
                            item.TimeSpanEnd = extra.TimeSpanEnd;
                            item.KmzResourcesBase64 = extra.KmzResourcesBase64 ?? new();
                        }
                    }

                    allItems[id] = (item, parentId, colType, order);
                }
            }

            // Reconstruct Tree
            project.MyPlaces.Clear();
            project.TemporaryPlaces.Clear();
            project.SavedScenes.Clear();

            // First pass: attach children to parents
            foreach (var kvp in allItems.Values)
            {
                if (kvp.ParentId != null && allItems.TryGetValue(kvp.ParentId, out var parentEntry))
                {
                    parentEntry.Item.AddChild(kvp.Item);
                }
            }

            // Second pass: add root items to respective collections
            foreach (var kvp in allItems.Values)
            {
                if (kvp.ParentId == null)
                {
                    if (kvp.CollectionType == "my_places")
                    {
                        project.MyPlaces.Add(kvp.Item);
                    }
                    else if (kvp.CollectionType == "temporary_places")
                    {
                        project.TemporaryPlaces.Add(kvp.Item);
                    }
                    else if (kvp.CollectionType == "saved_scenes")
                    {
                        project.SavedScenes.Add(kvp.Item);
                    }
                }
            }

            // Reconstruct Saved Scenes as a built-in child folder of My Places (SIDEBAR-01 Scope)
            var scenesFolder = project.MyPlaces.FirstOrDefault(p => p.ItemType == PlaceItemType.Folder &&
                (p.Id == "folder_saved_scenes" || string.Equals(p.Name, "Saved Scenes", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Saved Scenes")));

            if (scenesFolder != null)
            {
                scenesFolder.IsProtected = true;
                project.SyncSavedScenes();
            }

            project.EnsureSavedScenesFolder();
            project.EnsureTemporaryPlacesFolder();

            conn.Close();
            SqliteConnection.ClearPool(conn);

            return project;
        }
    }
}
