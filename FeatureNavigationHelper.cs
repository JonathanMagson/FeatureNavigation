using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;

public static class FeatureNavigationHelper
{
    public static BasicFeatureLayer SelectedLayer { get; private set; }
    public static List<long> FeatureOids { get; private set; } = new List<long>();
    private static int CurrentIndex { get; set; } = -1;

    public static async Task InitializeLayer(BasicFeatureLayer layer)
    {
        SelectedLayer = layer;
        await LoadFeatureOids();
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

    private static async Task LoadFeatureOids()
    {
        await QueuedTask.Run(() =>
        {
            FeatureOids.Clear();
            var query = new QueryFilter { WhereClause = "1=1" };
            using (var cursor = SelectedLayer.Search(query))
            {
                while (cursor.MoveNext())
                {
                    using (var record = cursor.Current)
                    {
                        FeatureOids.Add(record.GetObjectID());
                    }
                }
            }
        });
    }
}
