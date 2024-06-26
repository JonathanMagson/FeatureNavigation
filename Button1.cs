using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace FeatureSelection
{
	internal class Button1 : Button
    {
        protected override void OnClick()
        {
            var layer = MapView.Active.Map.FindLayers("YourLayerName").FirstOrDefault() as BasicFeatureLayer;
            if (layer == null) return;

            FeatureNavigationHelper.InitializeLayer(layer).ContinueWith(t =>
            {
                QueuedTask.Run(() =>
                {
                    var nextOid = FeatureNavigationHelper.GetNextOid();
                    if (nextOid.HasValue)
                        ZoomToFeature(nextOid.Value);
                });
            });
        }

        private void ZoomToFeature(long oid)
        {
            var mapView = MapView.Active;
            if (mapView == null || FeatureNavigationHelper.SelectedLayer == null)
                return;

            var queryFilter = new QueryFilter { ObjectIDs = new List<long> { oid } };
            using (var rowCursor = FeatureNavigationHelper.SelectedLayer.Search(queryFilter))
            {
                if (rowCursor.MoveNext())
                {
                    using (var feature = (Feature)rowCursor.Current)
                    {
                        mapView.ZoomTo(feature.GetShape(), new TimeSpan(0, 0, 0, 0, 100)); // Faster zoom
                    }
                }
            }
        }
    }
}