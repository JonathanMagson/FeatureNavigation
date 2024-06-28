## FeatureNavigation


## Overview
The FeatureNavigation Add-in for ArcGIS Pro provides a custom tool and dock pane to manage feature navigation within a map. It enhances navigation through selected features and provides functionalities for zooming into features with a specified buffer size.

## Features

### Layer Selection
- **Layer Combo Box**: Allows the user to select a layer from the currently active map.

### Buffer Size
- **Buffer Size Input**: Users can specify a buffer size to be applied when zooming into a feature. The buffer size can be a decimal value and the units are derived from the selected layer's spatial reference.

### Current Object ID
- **Current Object ID Display**: Displays the Object ID of the currently selected feature.
- **Manual Input**: Users can manually input an Object ID and press Enter to zoom to that feature.

### Navigation Buttons
- **Previous Feature Button**: Navigates to the previous feature in the selection set.
- **Next Feature Button**: Navigates to the next feature in the selection set.

## Usage

1. **Select a Layer**:
   - Open the FeatureNavigation dock pane.
   - Select a layer from the "Layer" combo box.

2. **Set Buffer Size**:
   - Enter a buffer size in the "Buffer Size" text box. The value can be a decimal.

3. **Navigate Features**:
   - Use the "Previous" and "Next" buttons to navigate through the selected features.
   - The current Object ID of the selected feature will be displayed and can also be manually entered.

4. **Zoom to Feature**:
   - Manually enter an Object ID in the "Current Object ID" text box and press Enter to zoom to that feature.

## Logging
- The Add-in logs the timestamp and Object ID of the feature being navigated to in a log file located at `C:\Users\<user_name>\Documents\ArcGIS\AddIns\FeatureNavigationLog.txt`.

## Installation
1. Download the Add-in package.
2. Double-click the Add-in file to install it in ArcGIS Pro.

## Support
For any issues or questions, please contact jonathan.magson@environment.nsw.gov.au
