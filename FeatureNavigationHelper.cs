using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Diagnostics;

public static class FeatureNavigationHelper
{
    public static BasicFeatureLayer SelectedLayer { get; private set; }
    public static List<long> FeatureOids { get; private set; } = new List<long>();
    private static int CurrentIndex { get; set; } = -1;

    // Dictionary to cache the selected layer's state
    private static Dictionary<BasicFeatureLayer, LayerState> _layerStateCache = new Dictionary<BasicFeatureLayer, LayerState>();

    // In-memory cache for selected layer name
    private static string _cachedLayerName = null;



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


    public static void CacheLayerState(BasicFeatureLayer layer, Field orderField, bool isAscending, string currentObjectId)
    {
        if (layer == null) return;

        if (_layerStateCache.ContainsKey(layer))
        {
            _layerStateCache[layer].OrderField = orderField;
            _layerStateCache[layer].IsAscendingOrder = isAscending;
            _layerStateCache[layer].CurrentObjectId = currentObjectId;
        }
        else
        {
            _layerStateCache[layer] = new LayerState
            {
                OrderField = orderField,
                IsAscendingOrder = isAscending,
                CurrentObjectId = currentObjectId
            };
        }
    }

    public static LayerState RetrieveLayerState(BasicFeatureLayer layer)
    {
        if (layer != null && _layerStateCache.ContainsKey(layer))
        {
            return _layerStateCache[layer];
        }
        return null;
    }

    // Cache selected layer name in memory
    public static void CacheSelectedLayerName(string layerName)
    {
        _cachedLayerName = layerName; // Use an in-memory cache instead of Properties.Settings
    }

    // Get cached layer name from memory
    public static string GetCachedLayerName()
    {
        return _cachedLayerName; // Return the in-memory cached value
    }
}

// LayerState class to hold the state of each layer
public class LayerState
{
    public Field OrderField { get; set; }
    public bool IsAscendingOrder { get; set; }
    public string CurrentObjectId { get; set; }
}
