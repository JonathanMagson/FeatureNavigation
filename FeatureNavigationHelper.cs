using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Diagnostics;
using System;

public static class FeatureNavigationHelper
{
    public static BasicFeatureLayer SelectedLayer { get; private set; }
    public static List<long> FeatureOids { get; private set; } = new List<long>();
    private static int CurrentIndex { get; set; } = -1;

    public static async Task InitializeLayer(BasicFeatureLayer layer)
    {
        SelectedLayer = layer;
        await LoadFeatureOids(null, true);  // Load with default ordering (OBJECTID ASC)
    }

    public static void ClearLayer()
    {
        SelectedLayer = null;
        FeatureOids.Clear();
        CurrentIndex = -1;
    }

    public static long? GetNextOid()
    {
        if (FeatureOids.Count == 0)
            return null;

        CurrentIndex = (CurrentIndex + 1) % FeatureOids.Count;
        return FeatureOids[CurrentIndex];
    }

    public static long? GetPreviousOid()
    {
        if (FeatureOids.Count == 0)
            return null;

        CurrentIndex = (CurrentIndex - 1 + FeatureOids.Count) % FeatureOids.Count;
        return FeatureOids[CurrentIndex];
    }

    public static void SetCurrentOid(long oid)
    {
        CurrentIndex = FeatureOids.IndexOf(oid);
    }

    // Public method to load and order features by a selected field and order type (ascending/descending)
    private static readonly object _layerLock = new object();

    public static async Task LoadFeatureOids(Field selectedOrderField, bool isAscendingOrder)
    {
        await QueuedTask.Run(() =>
        {
            lock (_layerLock)  // Ensure no other thread modifies SelectedLayer while this is running
            {
                try
                {
                    // Double-check the SelectedLayer before proceeding
                    if (SelectedLayer == null)
                    {
                        System.Diagnostics.Debug.WriteLine("FeatureNavigationHelper.SelectedLayer is null. Exiting LoadFeatureOids.");
                        return;
                    }

                    // Clear the existing OIDs
                    FeatureOids.Clear();

                    // Prepare the order by clause based on the selected field and order type
                    string orderDirection = isAscendingOrder ? "ASC" : "DESC";
                    string orderByClause = selectedOrderField != null
                        ? $"{selectedOrderField.Name} {orderDirection}"
                        : "OBJECTID ASC"; // Default to OBJECTID if no field is selected

                    System.Diagnostics.Debug.WriteLine($"Ordering by field: {selectedOrderField?.Name ?? "OBJECTID"}, Order: {orderDirection}");

                    // Ensure the table is not null before proceeding
                    var table = SelectedLayer?.GetTable();
                    if (table == null)
                    {
                        System.Diagnostics.Debug.WriteLine("SelectedLayer's table is null. Exiting LoadFeatureOids.");
                        return;
                    }

                    var query = new QueryFilter
                    {
                        WhereClause = "1=1", // General clause
                        PostfixClause = $"ORDER BY {orderByClause}" // Apply ordering
                    };

                    // Perform the search and iterate over the results
                    using (var cursor = SelectedLayer?.Search(query))
                    {
                        // Additional null check before iterating
                        if (cursor == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Cursor is null during Search. Exiting LoadFeatureOids.");
                            return;
                        }

                        // Iterate through the features in the cursor
                        while (cursor.MoveNext())
                        {
                            using (var record = cursor.Current)
                            {
                                if (record == null)
                                {
                                    System.Diagnostics.Debug.WriteLine("Record is null during cursor iteration.");
                                    continue; // Skip if record is null
                                }

                                // Add the OID to the list
                                FeatureOids.Add(record.GetObjectID());
                                System.Diagnostics.Debug.WriteLine($"Loaded Feature OID: {record.GetObjectID()}");
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Total Features Loaded: {FeatureOids.Count}");
                }
                catch (NullReferenceException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NullReferenceException in LoadFeatureOids: {ex.Message}. Ensure layer and table exist.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in LoadFeatureOids: {ex.Message}");
                }
            }
        });
    }

}
