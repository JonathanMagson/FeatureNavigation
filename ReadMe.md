## FeatureNavigation
Download the add-in installer here: https://github.com/JonathanMagson/FeatureNavigation/blob/master/FeatureNavigation.esriAddinX

## Overview
The FeatureNavigation Add-in for ArcGIS Pro provides a custom tool and dock pane to manage feature navigation within a map. It enhances navigation through selected features and provides functionalities for zooming into features with a specified buffer size. Additionally, it records the object ID of the last feature you navigated to in a logfile:  
`C:\Users\<username>\Documents\ArcGIS\AddIns\FeatureNavigationLog.txt`

![image](https://github.com/JonathanMagson/FeatureNavigation/assets/162064664/36b30d45-b882-4d9a-9da2-03c1d05ced3c)

## Features

### Layer Selection
- **Layer Combo Box**: Allows the user to select a layer from the currently active map.

### Buffer or Scale
- **Buffer Size/Scale Input**: Users can specify a buffer size percentage 
  - Enter a percentage (e.g., `5` or `10`).

### Current Object ID
- **Current Object ID Display**: Displays the Object ID of the currently selected feature.
- **Manual Input**: Users can manually input an Object ID and press Enter to zoom to that feature.

### Navigation Buttons
- **Previous Feature Button**: Navigate to the previous feature in the selection set.
- **Next Feature Button**: Navigate to the next feature in the selection set.

### Order Fields
- **Order Field Dropdown**: Allows the user to choose a field to sort the features by.
- **Order Type Dropdown**: Allows toggling between ascending or descending order for navigation.

### Log File Button
- **Open Log File Button**: Opens the log file directly from the dock pane.

## Usage

1. **Select a Layer**:
   - Open the FeatureNavigation dock pane.
   - Select a layer from the "Layer" combo box.

2. **Set Buffer Size or Fixed Scale**:
   - Enter a buffer size percentage (e.g., `5%`) 

3. **Navigate Features**:
   - Use the "Previous" and "Next" buttons to navigate through the selected features.
   - The current Object ID of the selected feature will be displayed and can also be manually entered.

4. **Sort by Field**:
   - Select an attribute field from the "Order Field" dropdown to sort features by a specific attribute.

5. **Set Sort Order**:
   - Choose the sort order (Ascending or Descending) from the "Order Type" dropdown.

6. **Zoom to Feature**:
   - Manually enter an Object ID in the "Current Object ID" text box and press Enter to zoom to that feature.

7. **View Log File**:
   - Use the "Log File" button to open the log file and review navigation history.

## Logging
- The Add-in logs the timestamp, Object ID, selected layer, and the order field and type (ascending/descending) of the feature being navigated to in a log file located at:  
  `C:\Users\<user_name>\Documents\ArcGIS\AddIns\FeatureNavigationLog.txt`

## Installation
1. Download the Add-in package.
2. Double-click the Add-in file to install it in ArcGIS Pro.


