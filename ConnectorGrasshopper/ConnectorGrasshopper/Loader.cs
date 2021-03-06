﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Speckle.Core.Kits;
using Speckle.Core.Logging;

namespace ConnectorGrasshopper
{
  public class Loader : GH_AssemblyPriority
  {
    public bool MenuHasBeenAdded = false;

    public IEnumerable<ISpeckleKit> loadedKits;
    public ISpeckleKit selectedKit;
    private ToolStripMenuItem speckleMenu;
    private IEnumerable<ToolStripItem> kitMenuItems;

    public override GH_LoadingInstruction PriorityLoad()
    {
      Setup.Init(Applications.Grasshopper);
      Grasshopper.Instances.DocumentServer.DocumentAdded += CanvasCreatedEvent;
      Grasshopper.Instances.ComponentServer.AddCategoryIcon("Speckle 2", Properties.Resources.speckle_logo);
      Grasshopper.Instances.ComponentServer.AddCategorySymbolName("Speckle 2", 'S');
      Grasshopper.Instances.ComponentServer.AddCategoryIcon("Speckle 2 Dev", Properties.Resources.speckle_logo);
      Grasshopper.Instances.ComponentServer.AddCategorySymbolName("Speckle 2 Dev", 'S');

      return GH_LoadingInstruction.Proceed;
    }

    private void CanvasCreatedEvent(GH_DocumentServer server, GH_Document doc)
    {
      AddSpeckleMenu(null, null);
    }

    private void HandleKitSelectedEvent(object sender, EventArgs args)
    {
      var clickedItem = (ToolStripMenuItem)sender;

      // Update the selected kit
      selectedKit = loadedKits.First(kit => clickedItem.Text.Trim() == kit.Name);

      var key = "Speckle2:kit.default.name";
      Grasshopper.Instances.Settings.SetValue(key, selectedKit.Name);
      Grasshopper.Instances.Settings.WritePersistentSettings();
      // Update the check status of all
      foreach (var item in kitMenuItems)
      {
        if (item is ToolStripMenuItem menuItem)
          menuItem.CheckState =
            clickedItem.Text.Trim() == selectedKit.Name
              ? CheckState.Checked
              : CheckState.Unchecked;
      }
    }

    private void AddSpeckleMenu(object sender, ElapsedEventArgs e)
    {
      if (Grasshopper.Instances.DocumentEditor == null || MenuHasBeenAdded) return;

      speckleMenu = new ToolStripMenuItem("Speckle 2");

      var kitHeader = speckleMenu.DropDown.Items.Add("Select the converter you want to use.");
      kitHeader.Enabled = false;

      loadedKits = KitManager.GetKitsWithConvertersForApp(Applications.Rhino);

      var kitItems = new List<ToolStripItem>();
      loadedKits.ToList().ForEach(kit =>
      {
        var item = speckleMenu.DropDown.Items.Add("  " + kit.Name);

        item.Click += HandleKitSelectedEvent;
        kitItems.Add(item);
      });
      kitMenuItems = kitItems;

      speckleMenu.DropDown.Items.Add(new ToolStripSeparator());

      speckleMenu.DropDown.Items.Add("Open Speckle Manager", Properties.Resources.speckle_logo);

      try
      {
        var mainMenu = Grasshopper.Instances.DocumentEditor.MainMenuStrip;
        Grasshopper.Instances.DocumentEditor.Invoke(new Action(() =>
        {
          if (!MenuHasBeenAdded)
          {
            mainMenu.Items.Add(speckleMenu);
            // Select the first kit by default.
            if (speckleMenu.DropDown.Items.Count > 0)
              HandleKitSelectedEvent(kitMenuItems.FirstOrDefault(k => k.Text.Trim() == "Objects"), null);
          }
        }));
        MenuHasBeenAdded = true;
      }
      catch (Exception err)
      {
        Debug.WriteLine(err.Message);
      }
    }
  }
}
