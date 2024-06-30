using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Framework;
using System.Collections.ObjectModel;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.CIM;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using ArcGIS.Core.Internal.Geometry;

namespace FeatureNavigation
{
    internal class FeatureNavigationDockPaneViewModel : DockPane, INotifyPropertyChanged
    {
        private const string _dockPaneID = "FeatureNavigation_FeatureNavigationDockPane";
        private const string _selectToolID = "FeatureNavigation_FeatureNavigationTool";
        private object _lock = new object();
        private Dictionary<Map, SelectedLayerInfo> _selectedLayerInfos = new Dictionary<Map, SelectedLayerInfo>();
        private Map _activeMap;

        private RelayCommand _nextFeatureCommand;
        private RelayCommand _previousFeatureCommand;
        private RelayCommand _zoomToFeatureCommand;

        private bool _selectToolActive = false;
        public bool SelectToolActive
        {
            get { return _selectToolActive; }
            set
            {
                SetProperty(ref _selectToolActive, value, () => SelectToolActive);
            }
        }

        protected FeatureNavigationDockPaneViewModel()
        {
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(_layers, _lock);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(_layerSelection, _lock);
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(_fieldAttributes, _lock);
            LayersAddedEvent.Subscribe(OnLayersAdded);
            LayersRemovedEvent.Subscribe(OnLayersRemoved);
            ActiveToolChangedEvent.Subscribe(OnActiveToolChanged);
            MapSelectionChangedEvent.Subscribe(OnSelectionChanged);
            ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
            MapRemovedEvent.Subscribe(OnMapRemoved);
        }

        ~FeatureNavigationDockPaneViewModel()
        {
            LayersAddedEvent.Unsubscribe(OnLayersAdded);
            LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
            ActiveToolChangedEvent.Unsubscribe(OnActiveToolChanged);
            MapSelectionChangedEvent.Unsubscribe(OnSelectionChanged);
            ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChanged);
        }

        protected override Task InitializeAsync()
        {
            if (MapView.Active == null)
                return Task.FromResult(0);

            _activeMap = MapView.Active.Map;
            SelectedLayer = null; // Set to null initially
            return UpdateForActiveMap();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        #region Bindable Properties

        private ObservableCollection<BasicFeatureLayer> _layers = new ObservableCollection<BasicFeatureLayer>();
        public ObservableCollection<BasicFeatureLayer> Layers
        {
            get { return _layers; }
        }

        private BasicFeatureLayer _selectedLayer;
        public BasicFeatureLayer SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                SetProperty(ref _selectedLayer, value, () => SelectedLayer);
                if (_selectedLayer == null)
                {
                    ClearLayerSelection();
                    FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
                    return;
                }
                FeatureNavigationHelper.InitializeLayer(_selectedLayer);  // Initialize the FeatureNavigationHelper with the selected layer
                _selectedLayerInfos[_activeMap].SelectedLayer = _selectedLayer;
                SelectedLayerChanged();
            }
        }

        private string _whereClause = "";
        public string WhereClause
        {
            get { return _whereClause; }
            set
            {
                SetProperty(ref _whereClause, value, () => WhereClause);
                IsValid = false;
                HasError = false;
            }
        }

        private ObservableCollection<long?> _layerSelection = new ObservableCollection<long?>();
        public ObservableCollection<long?> LayerSelection
        {
            get { return _layerSelection; }
        }

        private long? _selectedOID;
        public long? SelectedOID
        {
            get { return _selectedOID; }
            set
            {
                SetProperty(ref _selectedOID, value, () => SelectedOID);
                _selectedLayerInfos[_activeMap].SelectedOID = _selectedOID;
                if (_selectedOID.HasValue && MapView.Active != null)
                    MapView.Active.FlashFeature(SelectedLayer, _selectedOID.Value);
                ShowAttributes();
            }
        }

        private ObservableCollection<FieldAttributeInfo> _fieldAttributes = new ObservableCollection<FieldAttributeInfo>();
        public ObservableCollection<FieldAttributeInfo> FieldAttributes
        {
            get { return _fieldAttributes; }
        }

        private FieldAttributeInfo _selectedRow;
        public FieldAttributeInfo SelectedRow
        {
            get { return _selectedRow; }
            set
            {
                SetProperty(ref _selectedRow, value, () => SelectedRow);
            }
        }

        private bool _hasError = false;
        public bool HasError
        {
            get { return _hasError; }
            set
            {
                SetProperty(ref _hasError, value, () => HasError);
            }
        }

        private bool _isValid = false;
        public bool IsValid
        {
            get { return _isValid; }
            set
            {
                SetProperty(ref _isValid, value, () => IsValid);
            }
        }

        private string _currentObjectId;
        public string CurrentObjectId
        {
            get { return _currentObjectId; }
            set
            {
                SetProperty(ref _currentObjectId, value, () => CurrentObjectId);
            }
        }

        private float _bufferPercentage = 0.0f; // Default buffer percentage
        public float BufferPercentage
        {
            get { return _bufferPercentage; }
            set { SetProperty(ref _bufferPercentage, value, () => BufferPercentage); }
        }

        private bool _isSelectFeatureChecked;
        public bool IsSelectFeatureChecked
        {
            get { return _isSelectFeatureChecked; }
            set { SetProperty(ref _isSelectFeatureChecked, value, () => IsSelectFeatureChecked); }
        }

        public System.Windows.Controls.ContextMenu RowContextMenu
        {
            get { return FrameworkApplication.CreateContextMenu("FeatureNavigation_RowContextMenu"); }
        }

        #region Commands

        public ICommand NextFeatureCommand
        {
            get
            {
                if (_nextFeatureCommand == null)
                {
                    _nextFeatureCommand = new RelayCommand(
                        async () => await ExecuteNextFeature(),
                        () => CanExecuteNextFeature());
                }
                return _nextFeatureCommand;
            }
        }

        public ICommand PreviousFeatureCommand
        {
            get
            {
                if (_previousFeatureCommand == null)
                {
                    _previousFeatureCommand = new RelayCommand(
                        async () => await ExecutePreviousFeature(),
                        () => CanExecutePreviousFeature());
                }
                return _previousFeatureCommand;
            }
        }

        public ICommand ZoomToFeatureCommand
        {
            get
            {
                if (_zoomToFeatureCommand == null)
                {
                    _zoomToFeatureCommand = new RelayCommand(() => ZoomToFeature(), () => true);
                }
                return _zoomToFeatureCommand;
            }
        }

        private bool CanExecuteNextFeature()
        {
            return FeatureNavigationHelper.SelectedLayer != null && FeatureNavigationHelper.FeatureOids.Count > 0;
        }

        private bool CanExecutePreviousFeature()
        {
            return FeatureNavigationHelper.SelectedLayer != null && FeatureNavigationHelper.FeatureOids.Count > 0;
        }

        private async Task ExecuteNextFeature()
        {
            var nextOid = FeatureNavigationHelper.GetNextOid();
            if (nextOid.HasValue)
            {
                await QueuedTask.Run(() =>
                {
                    ZoomToFeature(nextOid.Value);
                });
                CurrentObjectId = nextOid.Value.ToString(); // Update the CurrentObjectId property
                LogCurrentObjectId(); // Log the current object ID
                if (IsSelectFeatureChecked)
                {
                    SelectCurrentFeature(); // Select the feature if the checkbox is checked
                }
            }
        }

        private async Task ExecutePreviousFeature()
        {
            var previousOid = FeatureNavigationHelper.GetPreviousOid();
            if (previousOid.HasValue)
            {
                await QueuedTask.Run(() =>
                {
                    ZoomToFeature(previousOid.Value);
                });
                CurrentObjectId = previousOid.Value.ToString(); // Update the CurrentObjectId property
                LogCurrentObjectId(); // Log the current object ID
                if (IsSelectFeatureChecked)
                {
                    SelectCurrentFeature(); // Select the feature if the checkbox is checked
                }
            }
        }

        private void ZoomToFeature()
        {
            if (long.TryParse(CurrentObjectId, out long oid))
            {
                QueuedTask.Run(() =>
                {
                    ZoomToFeature(oid);
                });
                LogCurrentObjectId(); // Log the current object ID
            }
        }

        private void ZoomToFeature(long oid)
        {
            var mapView = MapView.Active;
            if (mapView == null || FeatureNavigationHelper.SelectedLayer == null)
                return;

            var queryFilter = new QueryFilter { ObjectIDs = new List<long> { oid } };
            using (var rowCursor = FeatureNavigationHelper.SelectedLayer?.Search(queryFilter))
            {
                if (rowCursor == null || !rowCursor.MoveNext())
                {
                    // Handle the case where the feature is not found
                    System.Diagnostics.Debug.WriteLine($"Feature with OID {oid} not found.");
                    return;
                }

                using (var feature = (Feature)rowCursor.Current)
                {
                    var geometry = feature.GetShape();
                    if (geometry.GeometryType == GeometryType.Point)
                    {
                        // Create an envelope around the point to achieve the desired scale
                        const double scaleFactor = 1000; // Desired scale 1:1000
                        var center = geometry as MapPoint;
                        var halfWidth = scaleFactor / 2.0;
                        var halfHeight = scaleFactor / 2.0;

                        var envelope = new EnvelopeBuilder(center.SpatialReference)
                        {
                            XMin = center.X - halfWidth,
                            XMax = center.X + halfWidth,
                            YMin = center.Y - halfHeight,
                            YMax = center.Y + halfHeight
                        }.ToGeometry();

                        mapView.ZoomTo(envelope, new TimeSpan(0, 0, 0, 0, 100)); // Faster zoom
                    }
                    else
                    {
                        var extent = geometry.Extent;
                        var bufferDistance = CalculateBufferDistance(extent, BufferPercentage);
                        var buffer = GeometryEngine.Instance.Buffer(geometry, bufferDistance);
                        mapView.ZoomTo(buffer, new TimeSpan(0, 0, 0, 0, 100)); // Faster zoom
                    }
                }
            }
        }


        private double CalculateBufferDistance(Envelope extent, float bufferPercentage)
        {
            var width = extent.Width;
            var height = extent.Height;
            var maxDimension = Math.Max(width, height);
            return maxDimension * (bufferPercentage / 100.0);
        }

        private void LogCurrentObjectId()
        {
            string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ArcGIS", "AddIns", "FeatureNavigationLog.txt");
            string logEntry = $"{DateTime.Now}: OID {CurrentObjectId} in {SelectedLayer?.Name ?? "No Layer Selected"}";

            try
            {
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Handle any exceptions here (e.g., log to a different location, show a message to the user, etc.)
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private bool ValidateExpression(bool showValidationSuccessMsg)
        {
            try
            {
                var qf = new QueryFilter() { WhereClause = WhereClause };
                SelectedLayer?.Search(qf);
            }
            catch (Exception)
            {
                HasError = true;
                IsValid = false;
                return false;
            }

            if (showValidationSuccessMsg)
                IsValid = true;
            HasError = false;
            return true;
        }

        #endregion

        #endregion

        #region Private Methods

        private Task UpdateForActiveMap(bool activeMapChanged = true, Dictionary<MapMember, List<long>> mapSelection = null)
        {
            return QueuedTask.Run(() =>
            {
                SelectedLayerInfo selectedLayerInfo = null;
                if (!_selectedLayerInfos.ContainsKey(_activeMap))
                {
                    selectedLayerInfo = new SelectedLayerInfo();
                    _selectedLayerInfos.Add(_activeMap, selectedLayerInfo);
                }
                else
                    selectedLayerInfo = _selectedLayerInfos[_activeMap];

                if (activeMapChanged)
                {
                    RefreshLayerCollection();

                    SetProperty(ref _selectedLayer, selectedLayerInfo.SelectedLayer, () => SelectedLayer);
                    if (_selectedLayer == null)
                    {
                        FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
                        return;
                    }
                    selectedLayerInfo.SelectedLayer = SelectedLayer;
                }

                if (SelectedLayer == null)
                    RefreshSelectionOIDs(new List<long>());
                else
                {
                    List<long> oids = new List<long>();
                    if (mapSelection != null)
                    {
                        if (mapSelection.ContainsKey(SelectedLayer))
                            oids.AddRange(mapSelection[SelectedLayer]);
                    }
                    else
                    {
                        oids.AddRange(SelectedLayer.GetSelection().GetObjectIDs());
                    }
                    RefreshSelectionOIDs(oids);
                }

                SetProperty(ref _selectedOID, (selectedLayerInfo.SelectedOID != null && LayerSelection.Contains(selectedLayerInfo.SelectedOID)) ? selectedLayerInfo.SelectedOID : LayerSelection.FirstOrDefault(), () => SelectedOID);
                selectedLayerInfo.SelectedOID = SelectedOID;
                ShowAttributes();
            });
        }

        private void RefreshLayerCollection()
        {
            Layers.Clear();
            if (_activeMap == null)
                return;

            var layers = _activeMap.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>();
            lock (_lock)
            {
                foreach (var layer in layers)
                {
                    Layers.Add(layer);
                }
            }
        }

        private Task SelectedLayerChanged()
        {
            return QueuedTask.Run(() =>
            {
                if (SelectedLayer == null)
                    RefreshSelectionOIDs(new List<long>());
                else
                {
                    var selection = SelectedLayer.GetSelection();
                    RefreshSelectionOIDs(selection.GetObjectIDs());
                }
                SelectedOID = LayerSelection.FirstOrDefault();
            });
        }

        private void RefreshSelectionOIDs(IEnumerable<long> oids)
        {
            FieldAttributes.Clear();
            SetProperty(ref _selectedOID, null, () => SelectedOID);
            LayerSelection.Clear();
            lock (_lock)
            {
                foreach (var oid in oids)
                {
                    LayerSelection.Add(oid);
                }
            }
        }

        private Task ShowAttributes()
        {
            return QueuedTask.Run(() =>
            {
                try
                {
                    _fieldAttributes.Clear();
                    if (SelectedLayer == null || SelectedOID == null)
                        return;

                    var oidField = SelectedLayer.GetTable().GetDefinition().GetObjectIDField();
                    var qf = new QueryFilter() { WhereClause = string.Format("{0} = {1}", oidField, SelectedOID) };
                    var cursor = SelectedLayer.Search(qf);
                    Row row = null;

                    if (!cursor.MoveNext()) return;

                    using (row = cursor.Current)
                    {
                        var fields = row.GetFields();
                        lock (_lock)
                        {
                            foreach (ArcGIS.Core.Data.Field field in fields)
                            {
                                if (field.FieldType == FieldType.Geometry)
                                    continue;
                                var val = row[field.Name];
                                FieldAttributeInfo fa = new FieldAttributeInfo(field, (val is DBNull || val == null) ? null : val.ToString());
                                _fieldAttributes.Add(fa);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        private Task ModifyLayerSelection(SelectionCombinationMethod method)
        {
            return QueuedTask.Run(() =>
            {
                if (MapView.Active == null || SelectedLayer == null || WhereClause == null)
                    return;

                if (!ValidateExpression(false))
                    return;

                SelectedLayer.Select(new QueryFilter() { WhereClause = WhereClause }, method);
            });
        }

        private void SelectCurrentFeature()
        {
            if (long.TryParse(CurrentObjectId, out long oid))
            {
                QueuedTask.Run(() =>
                {
                    // Clear the previous selection
                    SelectedLayer.ClearSelection();

                    // Create a query filter to select the current feature
                    var queryFilter = new QueryFilter
                    {
                        ObjectIDs = new List<long> { oid }
                    };

                    // Select the current feature
                    SelectedLayer.Select(queryFilter, SelectionCombinationMethod.New);
                });
            }
        }

        private void ClearLayerSelection()
        {
            if (SelectedLayer != null)
            {
                QueuedTask.Run(() =>
                {
                    SelectedLayer.ClearSelection();
                });
            }
        }

        #endregion

        #region Event Handlers

        private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
        {
            if (args.IncomingView == null)
            {
                SetProperty(ref _selectedLayer, null, () => SelectedLayer);
                _layers.Clear();
                SetProperty(ref _selectedOID, null, () => SelectedOID);
                _layerSelection.Clear();
                _fieldAttributes.Clear();
                return;
            }

            _activeMap = args.IncomingView.Map;
            UpdateForActiveMap();
        }

        private void OnSelectionChanged(MapSelectionChangedEventArgs args)
        {
            if (args.Map != _activeMap)
                return;

            UpdateForActiveMap(false, args.Selection.ToDictionary());
        }

        private void OnLayersRemoved(LayerEventsArgs args)
        {
            foreach (var layer in args.Layers)
            {
                if (layer.Map == _activeMap)
                {
                    if (Layers.Contains(layer))
                        Layers.Remove((BasicFeatureLayer)layer);
                }
            }

            if (SelectedLayer == null)
            {
                SelectedLayer = Layers.FirstOrDefault();
                SelectedLayerChanged();
            }
        }

        private void OnLayersAdded(LayerEventsArgs args)
        {
            foreach (var layer in args.Layers)
            {
                if (layer.Map == _activeMap && layer is BasicFeatureLayer)
                {
                    Layers.Add((BasicFeatureLayer)layer);
                    if (SelectedLayer == null)
                        SelectedLayer = (BasicFeatureLayer)layer;
                }
            }
        }

        private void OnMapRemoved(MapRemovedEventArgs args)
        {
            var map = _selectedLayerInfos.Where(kvp => kvp.Key.URI == args.MapPath).FirstOrDefault().Key;
            if (map != null)
                _selectedLayerInfos.Remove(map);
        }

        private void OnActiveToolChanged(ToolEventArgs args)
        {
            SetProperty(ref _selectToolActive, (args.CurrentID == _selectToolID), () => SelectToolActive);
        }

        internal bool ValidateExpresion(bool v)
        {
            throw new NotImplementedException();
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, System.Linq.Expressions.Expression<Func<T>> propertyExpression)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            var memberExpression = (System.Linq.Expressions.MemberExpression)propertyExpression.Body;
            OnPropertyChanged(memberExpression.Member.Name);
            return true;
        }
    }

    internal class FieldAttributeInfo
    {
        private string _fieldName;
        private string _fieldAlias;
        private string _fieldValue;
        private FieldType _fieldType;

        internal FieldAttributeInfo(Field field, string fieldValue)
        {
            _fieldName = field.Name;
            _fieldAlias = field.AliasName;
            _fieldValue = fieldValue;
            _fieldType = field.FieldType;
        }

        public string FieldName
        {
            get { return _fieldName; }
        }

        public string FieldAlias
        {
            get { return _fieldAlias; }
        }

        public string FieldValue
        {
            get { return _fieldValue; }
        }

        public FieldType FieldType
        {
            get { return _fieldType; }
        }
    }

    internal class SelectedLayerInfo
    {
        public SelectedLayerInfo() { }
        public SelectedLayerInfo(BasicFeatureLayer selectedLayer, long? selectedOID)
        {
            SelectedLayer = selectedLayer;
            SelectedOID = selectedOID;
        }

        public BasicFeatureLayer SelectedLayer { get; set; }

        public long? SelectedOID { get; set; }
    }

    internal class FeatureNavigationDockPane_ShowButton : Button
    {
        protected override void OnClick()
        {
            FeatureNavigationDockPaneViewModel.Show();
        }
    }
}
