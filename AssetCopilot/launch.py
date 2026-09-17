# -*- coding: utf-8 -*-
import os
import Rhino
import System
path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dist", "AssetCopilot.rhp")
result, plugin_id = Rhino.PlugIns.PlugIn.LoadPlugIn(path)
print("AssetCopilot load: " + str(result))
if plugin_id != System.Guid.Empty:
    Rhino.RhinoApp.RunScript("_AssetCopilot", False)
else:
    print("Please close Rhino and run the Remy Asset Copilot shortcut again.")
