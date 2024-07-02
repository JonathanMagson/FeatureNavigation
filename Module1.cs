
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Threading.Tasks;
using ArcGIS.Core.Data;

namespace FeatureNavigation
{

  internal class Module1 : Module
  {
    private static Module1 _this = null;
    private const string _dockPaneID = "FeatureNavigation_FeatureNavigationDockPane";

 
    public static Module1 Current
    {
      get
      {
        return _this ?? (_this = (Module1)FrameworkApplication.FindModule("FeatureNavigation_Module"));
      }
    }

    #region Set As Expression

    /// <summary>
    /// Returns true if a new expression can be set
    /// </summary>
    internal bool CanSetAsExpression
    {
      get
      {
        var vm = FeatureNavigationVM;
        if (vm == null || vm.SelectedRow == null)
          return false;

        return GetFormattedExpression(vm.SelectedRow) != null;
      }
    }

    /// <summary>
    /// Sets the new expression using the selected field and corresponding attribute
    /// </summary>
    internal void SetAsExpression()
    {
      var vm = FeatureNavigationVM;
      vm.WhereClause = GetFormattedExpression(vm.SelectedRow);
    }

    #endregion

    #region Append To Expression
    
    /// <summary>
    /// Returns true if a the expression can be appended to
    /// </summary>
    internal bool CanAddToExpression
    {
      get
      {
        var vm = FeatureNavigationVM;
        if (vm == null || vm.SelectedRow == null)
          return false;

        return GetFormattedExpression(vm.SelectedRow) != null;
      }
    }

    /// <summary>
    /// Appends to the expression using the selected field and corresponding attribute
    /// </summary>
    internal void AddToExpression()
    {
      var vm = FeatureNavigationVM;
      if (vm.WhereClause == "")
        vm.WhereClause = GetFormattedExpression(vm.SelectedRow);
      else
        vm.WhereClause += string.Format(" AND {0}", GetFormattedExpression(vm.SelectedRow));
    }

    #endregion

    /// <summary>
    /// Gets a string representing a new clause using the information defined in the FieldAttributeInfo
    /// </summary>
    /// <param name="fieldAttribute"></param>
    private string GetFormattedExpression(FieldAttributeInfo fieldAttribute)
    {
      switch (fieldAttribute.FieldType)
	    {
        case FieldType.Double:
        case FieldType.Integer:
        case FieldType.Single:
        case FieldType.SmallInteger:
        case FieldType.OID:
          if (fieldAttribute.FieldValue == null)
            return string.Format("{0} is NULL", fieldAttribute.FieldName);
          else
            return string.Format("{0} = {1}", fieldAttribute.FieldName, fieldAttribute.FieldValue);
        case FieldType.String:
          if (fieldAttribute.FieldValue == null)
            return string.Format("{0} is NULL", fieldAttribute.FieldName);
          else
            return string.Format("{0} = {1}", fieldAttribute.FieldName, string.Format("'{0}'", fieldAttribute.FieldValue));
        default:
          return null;
	    }
    }

    /// <summary>
    /// Stores the instance of the Feature Navigation dock pane viewmodel
    /// </summary>
    private static FeatureNavigationDockPaneViewModel _dockPane;
    internal static FeatureNavigationDockPaneViewModel FeatureNavigationVM
    {
      get
      {
        if (_dockPane == null)
        {
          _dockPane = FrameworkApplication.DockPaneManager.Find(_dockPaneID) as FeatureNavigationDockPaneViewModel;
        }
        return _dockPane;
      }
    }

    #region Overrides
    /// <summary>
    /// Called by Framework when ArcGIS Pro is closing
    /// </summary>
    /// <returns>False to prevent Pro from closing, otherwise True</returns>
    protected override bool CanUnload()
    {
      //TODO - add your business logic
      //return false to ~cancel~ Application close
      return true;
    }

    #endregion Overrides
  }
}
