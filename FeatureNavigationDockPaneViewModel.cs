using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.Geometry;
using ArcGIS.Core.Threading.Tasks;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FeatureNavigation
{
    internal class FeatureNavigationDockPaneViewModel : DockPane
    {
        private const string _dockPaneID = "FeatureNavigation_FeatureNavigationDockPane";

        protected FeatureNavigationDockPaneViewModel()
        {
            Layers = new ObservableCollection<BasicFeatureLayer>();
            OrderFields = new ObservableCollection<Field>();
            OrderTypes = new ObservableCollection<string> { "Ascending", "Descending" };

            _layerSelection = new ObservableCollection<long?>();
            _fieldAttributes = new ObservableCollection<FieldAttributeInfo>();

            _selectedLayerInfos = new ConcurrentDictionary<BasicFeatureLayer, LayerState>();

            // Initialize commands
            _nextFeatureCommand = new RelayCommand(
                async () => await ExecuteNextFeature(),
                () => CanExecuteNextFeature());

            _previousFeatureCommand = new RelayCommand(
                async () => await ExecutePreviousFeature(),
                () => CanExecutePreviousFeature());

            _zoomToFeatureCommand = new RelayCommand(() => ZoomToCurrentFeature(), () => CanExecuteZoomToFeature());

            _openLogFileCommand = new RelayCommand(OpenLogFile);

            // Subscribe to events
            _mapViewChangedEvent = ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
            MapSelectionChangedEvent.Subscribe(OnSelectionChanged);
            _layersAddedEvent = LayersAddedEvent.Subscribe(OnLayersAdded);
            _layersRemovedEvent = LayersRemovedEvent.Subscribe(OnLayersRemoved);
            MapRemovedEvent.Subscribe(OnMapRemoved);

            _activeMap = MapView.Active?.Map;
            _lastActiveMapView = MapView.Active;

            if (_activeMap != null)
            {
                UpdateForActiveMap();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No active map found at startup.");
            }
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        #region Properties

        private Map _activeMap;
        private MapView _lastActiveMapView;
        private SubscriptionToken _mapViewChangedEvent;
        private SubscriptionToken _layersAddedEvent;
        private SubscriptionToken _layersRemovedEvent;

        private ConcurrentDictionary<BasicFeatureLayer, LayerState> _selectedLayerInfos;

        public ObservableCollection<BasicFeatureLayer> Layers { get; private set; }

        private BasicFeatureLayer _selectedLayer;
        public BasicFeatureLayer SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                if (_selectedLayer != value)
                {
                    if (_selectedLayer != null)
                    {
                        CacheCurrentLayerState();
                    }

                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Changing SelectedLayer from {_selectedLayer?.Name ?? ""} to {value?.Name ?? ""}");

                    _selectedLayer = value;
                    SetProperty(ref _selectedLayer, value, () => SelectedLayer);

                    if (_selectedLayer == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[DEBUG] SelectedLayer is null after setting. Exiting setter.");
                        return;
                    }

                    InitializeSelectedLayer();
                }
            }
        }

        private async void InitializeSelectedLayer()
        {
            // Clear any previously stored layer information
            FeatureNavigationHelper.ClearLayer();

            // Set the selected layer and load order fields
            FeatureNavigationHelper.SelectedLayer = _selectedLayer;
            await LoadOrderFields();

            RestoreLayerState();

            // Ensure the first field is selected if none is restored
            if (SelectedOrderField == null && OrderFields.Any())
            {
                SelectedOrderField = OrderFields.First();
            }

            // Load the feature OIDs based on the selected order field
            if (SelectedOrderField != null)
            {
                await FeatureNavigationHelper.LoadFeatureOids(SelectedOrderField, IsAscendingOrder, FilterExpression);

                // Set `CurrentObjectId` to the first OID in the list to start from the first feature
                if (FeatureNavigationHelper.FeatureOids.Any())
                {
                    CurrentObjectId = FeatureNavigationHelper.FeatureOids.First().ToString();
                    FeatureNavigationHelper.SetCurrentOid(FeatureNavigationHelper.FeatureOids.First());
                }

                // Update the state of the navigation commands
                (_nextFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_previousFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_zoomToFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }


        private void RestoreLayerState()
        {
            if (_selectedLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] RestoreLayerState called but _selectedLayer is null. Exiting.");
                return;
            }

            var cachedState = FeatureNavigationHelper.RetrieveLayerState(_selectedLayer);
            if (cachedState != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Restoring cached state for {_selectedLayer.Name}");
                SelectedOrderField = OrderFields.FirstOrDefault(f => f.Name == cachedState.OrderFieldName);
                IsAscendingOrder = cachedState.IsAscendingOrder;
                SelectedOrderType = IsAscendingOrder ? "Ascending" : "Descending";
                CurrentObjectId = cachedState.CurrentObjectId;
                WhereClause = cachedState.WhereClause;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] No cached state found for {_selectedLayer.Name}. Using defaults.");
                SelectedOrderField = OrderFields.FirstOrDefault();
                IsAscendingOrder = true;
                SelectedOrderType = "Ascending";
                CurrentObjectId = string.Empty;
                WhereClause = string.Empty;
            }
        }

        private void CacheCurrentLayerState()
        {
            if (_selectedLayer != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Caching layer state for {_selectedLayer.Name}");
                FeatureNavigationHelper.CacheLayerState(_selectedLayer, SelectedOrderField?.Name, IsAscendingOrder, CurrentObjectId, WhereClause);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] CacheCurrentLayerState called but _selectedLayer is null.");
            }
        }

        private void ClearLayerData()
        {
            SelectedOrderField = null;
            IsAscendingOrder = true;
            CurrentObjectId = null;
            FeatureNavigationHelper.ClearLayer();
        }

        private ObservableCollection<Field> _orderFields;
        public ObservableCollection<Field> OrderFields
        {
            get { return _orderFields; }
            set { SetProperty(ref _orderFields, value, () => OrderFields); }
        }

        private ObservableCollection<string> _orderTypes;
        public ObservableCollection<string> OrderTypes
        {
            get { return _orderTypes; }
            set { SetProperty(ref _orderTypes, value, () => OrderTypes); }
        }

        private Field _selectedOrderField;
        public Field SelectedOrderField
        {
            get { return _selectedOrderField; }
            set
            {
                if (_selectedOrderField != value)
                {
                    var matchingField = OrderFields.FirstOrDefault(f => f.Name == value?.Name);
                    if (matchingField != null)
                    {
                        SetProperty(ref _selectedOrderField, matchingField, () => SelectedOrderField);
                        if (FeatureNavigationHelper.SelectedLayer != null)
                        {
                            LoadFeatureOidsAndUpdateCommands();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Invalid field selected, resetting to default.");
                        SetProperty(ref _selectedOrderField, OrderFields.FirstOrDefault(), () => SelectedOrderField);
                        if (FeatureNavigationHelper.SelectedLayer != null)
                        {
                            LoadFeatureOidsAndUpdateCommands();
                        }
                    }
                }
            }
        }

        private async void LoadFeatureOidsAndUpdateCommands()
        {
            await FeatureNavigationHelper.LoadFeatureOids(SelectedOrderField, IsAscendingOrder, FilterExpression);
            (_nextFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (_previousFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (_zoomToFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private string _selectedOrderType;
        public string SelectedOrderType
        {
            get { return _selectedOrderType; }
            set
            {
                SetProperty(ref _selectedOrderType, value, () => SelectedOrderType);
                IsAscendingOrder = SelectedOrderType == "Ascending";
                if (SelectedOrderField != null && FeatureNavigationHelper.SelectedLayer != null)
                {
                    LoadFeatureOidsAndUpdateCommands();
                }
            }
        }

        private string _filterExpression;
        public string FilterExpression
        {
            get => _filterExpression;
            set
            {
                if (SetProperty(ref _filterExpression, value, () => FilterExpression))
                {
                    ApplyFilter();
                }
            }
        }

        private async void ApplyFilter()
        {
            if (SelectedOrderField != null && FeatureNavigationHelper.SelectedLayer != null)
            {
                await FeatureNavigationHelper.LoadFeatureOids(SelectedOrderField, IsAscendingOrder, FilterExpression);
                (_nextFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_previousFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_zoomToFeatureCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }



        private bool _isAscendingOrder = true;
        public bool IsAscendingOrder
        {
            get { return _isAscendingOrder; }
            set { SetProperty(ref _isAscendingOrder, value, () => IsAscendingOrder); }
        }

        private string _currentObjectId;
        public string CurrentObjectId
        {
            get { return _currentObjectId; }
            set
            {
                if (SetProperty(ref _currentObjectId, value, () => CurrentObjectId))
                {
                    UpdateFeatureNavigationHelperIndex(value);
                    ZoomToCurrentFeature();
                    SelectCurrentFeature();
                }
            }
        }

        private float _bufferPercentage = 0.0f;
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

        private ICommand _nextFeatureCommand;
        public ICommand NextFeatureCommand
        {
            get { return _nextFeatureCommand; }
        }

        private ICommand _previousFeatureCommand;
        public ICommand PreviousFeatureCommand
        {
            get { return _previousFeatureCommand; }
        }

        private ICommand _zoomToFeatureCommand;
        public ICommand ZoomToFeatureCommand
        {
            get { return _zoomToFeatureCommand; }
        }

        private ICommand _openLogFileCommand;
        public ICommand OpenLogFileCommand
        {
            get { return _openLogFileCommand; }
        }

        private ICommand _enterKeyCommand;
        public ICommand EnterKeyCommand
        {
            get
            {
                if (_enterKeyCommand == null)
                {
                    _enterKeyCommand = new RelayCommand(() =>
                    {
                        ZoomToCurrentFeature();
                        SelectCurrentFeature();
                    });
                }
                return _enterKeyCommand;
            }
        }

        private ICommand _applyFilterCommand;
        public ICommand ApplyFilterCommand => _applyFilterCommand ??= new RelayCommand(ApplyFilter);

        private string _whereClause;
        public string WhereClause
        {
            get { return _whereClause; }
            set
            {
                if (SetProperty(ref _whereClause, value, () => WhereClause))
                {
                    if (SelectedOrderField != null && FeatureNavigationHelper.SelectedLayer != null)
                    {
                        LoadFeatureOidsAndUpdateCommands();
                    }
                }
            }
        }

        #endregion

        #region Methods

        private void UpdateForActiveMap()
        {
            if (_activeMap == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Active map is null in UpdateForActiveMap. Exiting.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Active map: {_activeMap?.Name}");

            UpdateLayers();
        }

        private void UpdateLayers()
        {
            if (_activeMap == null)
                return;

            var previousSelectedLayer = SelectedLayer;

            var mapLayers = _activeMap.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>().ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var layer in mapLayers)
                {
                    if (!Layers.Contains(layer))
                    {
                        Layers.Add(layer);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Layer added to drop-down: {layer.Name}");
                    }
                }

                var layersToRemove = Layers.Except(mapLayers).ToList();
                foreach (var layer in layersToRemove)
                {
                    Layers.Remove(layer);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Layer removed from drop-down: {layer.Name}");
                }

                if (previousSelectedLayer != null && Layers.Contains(previousSelectedLayer))
                {
                    SelectedLayer = previousSelectedLayer;
                }
                else if (Layers.Count > 0)
                {
                    SelectedLayer = Layers.First();
                }
                else
                {
                    SelectedLayer = null;
                }
            });
        }

        private void UpdateFeatureNavigationHelperIndex(string objectId)
        {
            if (long.TryParse(objectId, out long oid))
            {
                FeatureNavigationHelper.SetCurrentOid(oid);
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

        private bool CanExecuteZoomToFeature()
        {
            return !string.IsNullOrEmpty(CurrentObjectId);
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
                CurrentObjectId = nextOid.Value.ToString();
                LogCurrentObjectId();
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
                CurrentObjectId = previousOid.Value.ToString();
                LogCurrentObjectId();
            }
        }

        private void ZoomToCurrentFeature()
        {
            if (long.TryParse(CurrentObjectId, out long oid))
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Zooming to feature with OID {oid} in layer {SelectedLayer?.Name}");

                QueuedTask.Run(() =>
                {
                    ZoomToFeature(oid);
                });
            }
        }

        private async Task ZoomToFeature(long oid)
        {
            // Wait for MapView to be initialized and active
            MapView mapView = await GetInitializedMapView();
            if (mapView == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] MapView is not initialized or available. Cannot zoom to feature.");
                return;
            }

            if (FeatureNavigationHelper.SelectedLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] SelectedLayer is null in ZoomToFeature. Exiting.");
                return;
            }

            // Query for the feature's geometry
            var queryFilter = new QueryFilter { ObjectIDs = new List<long> { oid } };
            using (var rowCursor = FeatureNavigationHelper.SelectedLayer?.Search(queryFilter))
            {
                if (rowCursor == null || !rowCursor.MoveNext())
                {
                    System.Diagnostics.Debug.WriteLine($"Feature with OID {oid} not found.");
                    return;
                }

                using (var feature = rowCursor.Current as Feature)
                {
                    var geometry = feature.GetShape();
                    if (geometry == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Feature with OID {oid} has no geometry.");
                        return;
                    }

                    // Adjust zoom based on geometry type
                    await QueuedTask.Run(() =>
                    {
                        if (geometry.GeometryType == GeometryType.Point)
                        {
                            const double scaleFactor = 1000;
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

                            mapView.ZoomTo(envelope, new TimeSpan(0, 0, 0, 0, 100));
                        }
                        else
                        {
                            var extent = geometry.Extent;
                            var bufferDistance = CalculateBufferDistance(extent, BufferPercentage);
                            var buffer = GeometryEngine.Instance.Buffer(geometry, bufferDistance);
                            mapView.ZoomTo(buffer, new TimeSpan(0, 0, 0, 0, 100));
                        }
                    });
                }
            }
        }

        // Helper method to retry getting an active MapView
        private async Task<MapView> GetInitializedMapView()
        {
            MapView mapView = null;
            int retryCount = 5;
            int delayMilliseconds = 500;

            while (retryCount > 0)
            {
                mapView = MapView.Active;
                if (mapView != null) return mapView;

                await Task.Delay(delayMilliseconds); // Wait before retrying
                retryCount--;
            }

            return null; // MapView is not available even after retries
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
            string newLogEntry = $"{DateTime.Now}: OID {CurrentObjectId} in {SelectedLayer?.Name ?? "No Layer Selected"}, Order Field: {SelectedOrderField?.Name}, Order Type: {SelectedOrderType}";

            try
            {
                string[] existingLogEntries = File.Exists(logFilePath) ? File.ReadAllLines(logFilePath) : new string[0];
                string[] updatedLogEntries = new string[existingLogEntries.Length + 1];
                updatedLogEntries[0] = newLogEntry;
                existingLogEntries.CopyTo(updatedLogEntries, 1);

                File.WriteAllLines(logFilePath, updatedLogEntries);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private void OpenLogFile()
        {
            try
            {
                string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ArcGIS", "AddIns", "FeatureNavigationLog.txt");

                if (File.Exists(logFilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = logFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Log file not found.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open log file: {ex.Message}");
            }
        }

        private void SelectCurrentFeature()
        {
            if (!IsSelectFeatureChecked)
            {
                return;
            }

            if (long.TryParse(CurrentObjectId, out long oid))
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Selecting feature with OID {oid} in layer {SelectedLayer?.Name}");

                if (SelectedLayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Cannot select feature as SelectedLayer is null.");
                    return;
                }

                QueuedTask.Run(() =>
                {
                    try
                    {
                        SelectedLayer.ClearSelection();
                        var queryFilter = new QueryFilter { ObjectIDs = new List<long> { oid } };
                        SelectedLayer.Select(queryFilter, SelectionCombinationMethod.New);

                        var mapView = GetMapView();
                        if (mapView != null)
                        {
                            mapView.FlashFeature(SelectedLayer, oid);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[DEBUG] MapView is null in SelectCurrentFeature.");
                        }

                        System.Diagnostics.Debug.WriteLine("[DEBUG] Feature selected and flashed on map.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Error selecting feature: {ex.Message}");
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Invalid OID for feature selection.");
            }
        }

        private async Task LoadOrderFields()
        {
            if (SelectedLayer == null) return;

            try
            {
                var fields = await QueuedTask.Run(() =>
                {
                    var fieldList = new List<Field>();
                    var layerFields = SelectedLayer.GetTable().GetDefinition().GetFields();
                    foreach (var field in layerFields)
                    {
                        if (field.FieldType != FieldType.Geometry)
                        {
                            fieldList.Add(field);
                        }
                    }
                    return fieldList;
                });

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OrderFields.Clear();
                    foreach (var field in fields)
                    {
                        OrderFields.Add(field);
                    }

                    if (SelectedOrderField == null && OrderFields.Any())
                    {
                        SelectedOrderField = OrderFields.First();
                    }
                });

                if (!OrderFields.Any())
                {
                    SelectedOrderField = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading fields: {ex.Message}");
            }
        }

        private void ShowAttributes()
        {
            // Implement as needed
        }

        #endregion

        #region Event Handlers

        private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
        {
            if (args.IncomingView != null)
            {
                _activeMap = args.IncomingView.Map;
                _lastActiveMapView = args.IncomingView;

                UpdateForActiveMap();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Active map view changed to null.");
            }
        }

        private MapView GetMapView()
        {
            return MapView.Active ?? _lastActiveMapView;
        }

        private void OnSelectionChanged(MapSelectionChangedEventArgs args)
        {
            // Implementation of selection changed event handler if needed
        }

        private void OnLayersRemoved(LayerEventsArgs args)
        {
            if (_activeMap == null)
                return;

            var currentMapLayers = _activeMap.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>().ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var layer in args.Layers.OfType<BasicFeatureLayer>())
                {
                    if (layer.Map == _activeMap)
                    {
                        if (!currentMapLayers.Contains(layer) && Layers.Contains(layer))
                        {
                            Layers.Remove(layer);
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Layer removed from drop-down: {layer.Name}");
                        }
                    }
                }

                if (SelectedLayer == null && Layers.Count > 0)
                {
                    SelectedLayer = Layers.First();
                }
            });
        }

        private void OnLayersAdded(LayerEventsArgs args)
        {
            if (_activeMap == null)
                return;

            foreach (var layer in args.Layers.OfType<BasicFeatureLayer>())
            {
                if (layer.Map == _activeMap && !Layers.Contains(layer))
                {
                    Layers.Add(layer);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Layer added to drop-down: {layer.Name}");

                    if (SelectedLayer == null)
                    {
                        SelectedLayer = layer;
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Selected layer set to: {SelectedLayer?.Name}");
                    }
                }
            }
        }

        private void OnMapRemoved(MapRemovedEventArgs args)
        {
            // Implementation of map removed event handler if needed
        }

        #endregion

        private ObservableCollection<long?> _layerSelection;
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
            }
        }

        private ObservableCollection<FieldAttributeInfo> _fieldAttributes;
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
    }

    internal static class FeatureNavigationHelper
    {
        public static BasicFeatureLayer SelectedLayer { get; set; }

        public static List<long> FeatureOids { get; private set; } = new List<long>();

        private static int _currentIndex = -1;

        private static ConcurrentDictionary<BasicFeatureLayer, LayerState> _layerStates = new ConcurrentDictionary<BasicFeatureLayer, LayerState>();

        public static FeatureNavigationDockPaneViewModel FeatureNavigationDockPaneViewModelInstance { get; set; }

        public static async Task LoadFeatureOids(Field orderField, bool isAscendingOrder)
        {
            if (SelectedLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] SelectedLayer is null in LoadFeatureOids");
                return;
            }

            if (orderField == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] orderField is null in LoadFeatureOids");
                return;
            }

            await QueuedTask.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading feature OIDs for layer {SelectedLayer.Name}");

                    FeatureOids.Clear();

                    var table = SelectedLayer.GetTable();
                    var queryFilter = new QueryFilter()
                    {
                        SubFields = SelectedLayer.GetTable().GetDefinition().GetObjectIDField(),
                        WhereClause = "1=1",
                        PostfixClause = $"ORDER BY {orderField.Name} {(isAscendingOrder ? "ASC" : "DESC")}"
                    };

                    if (!string.IsNullOrEmpty(FeatureNavigationDockPaneViewModelInstance?.WhereClause))
                    {
                        queryFilter.WhereClause = FeatureNavigationDockPaneViewModelInstance.WhereClause;
                    }

                    using (var rowCursor = table.Search(queryFilter, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (var row = rowCursor.Current)
                            {
                                FeatureOids.Add(row.GetObjectID());
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Total Features Loaded: {FeatureOids.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading feature OIDs: {ex.Message}");
                }
            });
        }

        public static long? GetNextOid()
        {
            if (FeatureOids.Count == 0)
                return null;

            _currentIndex++;

            if (_currentIndex >= FeatureOids.Count)
            {
                _currentIndex = 0;
            }

            return FeatureOids[_currentIndex];
        }

        public static long? GetPreviousOid()
        {
            if (FeatureOids.Count == 0)
                return null;

            _currentIndex--;

            if (_currentIndex < 0)
            {
                _currentIndex = FeatureOids.Count - 1;
            }

            return FeatureOids[_currentIndex];
        }

        public static void SetCurrentOid(long oid)
        {
            _currentIndex = FeatureOids.IndexOf(oid);
        }

        public static void CacheLayerState(BasicFeatureLayer layer, string orderFieldName, bool isAscendingOrder, string currentObjectId, string whereClause)
        {
            if (layer == null)
                return;

            var layerState = new LayerState
            {
                Layer = layer,
                OrderFieldName = orderFieldName,
                IsAscendingOrder = isAscendingOrder,
                CurrentObjectId = currentObjectId,
                WhereClause = whereClause
            };

            _layerStates[layer] = layerState;
        }

        public static LayerState RetrieveLayerState(BasicFeatureLayer layer)
        {
            if (layer == null)
                return null;

            if (_layerStates.TryGetValue(layer, out var state))
            {
                return state;
            }
            else
            {
                return null;
            }
        }

        public static void ClearLayer()
        {
            SelectedLayer = null;
            FeatureOids.Clear();
            _currentIndex = -1;
        }


        public static async Task LoadFeatureOids(Field orderField, bool isAscendingOrder, string filterExpression = null)
        {
            if (SelectedLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] SelectedLayer is null in LoadFeatureOids");
                return;
            }

            if (orderField == null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] orderField is null in LoadFeatureOids");
                return;
            }

            await QueuedTask.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading feature OIDs for layer {SelectedLayer.Name}");

                    FeatureOids.Clear();

                    var table = SelectedLayer.GetTable();
                    var queryFilter = new QueryFilter()
                    {
                        SubFields = SelectedLayer.GetTable().GetDefinition().GetObjectIDField(),
                        WhereClause = "1=1",
                        PostfixClause = $"ORDER BY {orderField.Name} {(isAscendingOrder ? "ASC" : "DESC")}"
                    };

                    // Apply the filter expression if it’s provided
                    if (!string.IsNullOrEmpty(filterExpression))
                    {
                        queryFilter.WhereClause += $" AND ({filterExpression})";
                    }

                    using (var rowCursor = table.Search(queryFilter, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (var row = rowCursor.Current)
                            {
                                FeatureOids.Add(row.GetObjectID());
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Total Features Loaded: {FeatureOids.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading feature OIDs: {ex.Message}");
                }
            });
        }
    }

    public class LayerState
    {
        public BasicFeatureLayer Layer { get; set; }
        public string OrderFieldName { get; set; }
        public bool IsAscendingOrder { get; set; }
        public string CurrentObjectId { get; set; }
        public string WhereClause { get; set; }
    }

    internal class FieldAttributeInfo
    {
        public string FieldName { get; }
        public string FieldAlias { get; }
        public string FieldValue { get; }
        public FieldType FieldType { get; }

        internal FieldAttributeInfo(Field field, string fieldValue)
        {
            FieldName = field.Name;
            FieldAlias = field.AliasName;
            FieldValue = fieldValue;
            FieldType = field.FieldType;
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
            FeatureNavigationHelper.FeatureNavigationDockPaneViewModelInstance = FrameworkApplication.DockPaneManager.Find("FeatureNavigation_FeatureNavigationDockPane") as FeatureNavigationDockPaneViewModel;
        }
    }
}
    