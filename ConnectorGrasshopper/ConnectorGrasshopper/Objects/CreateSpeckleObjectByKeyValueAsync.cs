﻿using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ConnectorGrasshopper.Extras;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GrasshopperAsyncComponent;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using Utilities = ConnectorGrasshopper.Extras.Utilities;

namespace ConnectorGrasshopper.Objects
{
  public class CreateSpeckleObjectByKeyValueAsync : SelectKitAsyncComponentBase
  {
    public override Guid ComponentGuid => new Guid("C8D4DBEB-7CC5-45C0-AF5D-F374FA5DBFBB");

    protected override Bitmap Icon => Properties.Resources.CreateSpeckleObjectByKeyValue;
    
    public override GH_Exposure Exposure => GH_Exposure.primary;

    public CreateSpeckleObjectByKeyValueAsync() : base("Create Speckle Object by Key/Value", "K/V",
      "Creates a speckle object from key value pairs", "Speckle 2", "Object Management")
    {
      BaseWorker = new CreateSpeckleObjectByKeyValueWorker(this,Converter);
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddTextParameter("Keys", "K", "List of keys", GH_ParamAccess.list);
      pManager.AddGenericParameter("Values", "V", "List of values", GH_ParamAccess.tree);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddParameter(new SpeckleBaseParam("Object", "O", "Speckle object", GH_ParamAccess.item));
    }
  }

  public class CreateSpeckleObjectByKeyValueWorker : WorkerInstance
  {
    private GH_Structure<IGH_Goo> valueTree = new GH_Structure<IGH_Goo>();
    private List<string> keys = new List<string>();
    private Base speckleObj;
    private int iteration;
    public ISpeckleConverter Converter;

    public CreateSpeckleObjectByKeyValueWorker(GH_Component parent, ISpeckleConverter converter) : base(parent)
    {
      Converter = converter;
    }

    public override void DoWork(Action<string, double> ReportProgress, Action Done)
    {
      // 👉 Checking for cancellation!
      if (CancellationToken.IsCancellationRequested) return;
      Parent.Message = "Creating...";
      // Create a path from the current iteration
      var searchPath = new GH_Path(iteration);

      // Grab the corresponding subtree from the value input tree.
      var subTree = Utilities.GetSubTree(valueTree, searchPath);
      speckleObj = new Base();
      // Find the list or subtree belonging to that path
      if (valueTree.PathExists(searchPath) || valueTree.Paths.Count == 1)
      {
        var list = valueTree.Paths.Count == 1 ? valueTree.Branches[0] : valueTree.get_Branch(searchPath);
        // We got a list of values
        var ind = 0;
        keys.ForEach(key =>
        {
          if (ind < list.Count)
            speckleObj[key] = Utilities.TryConvertItemToSpeckle(list[ind], Converter);
          ind++;
        });
      }
      else
      {
        // We got a tree of values

        // Create the speckle object with the specified keys
        var index = 0;
        keys.ForEach(key =>
        {
          var itemPath = new GH_Path(index);
          
          var branch = subTree.get_Branch(itemPath);
          if (branch != null)
          {
            var objs = new List<object>();
            foreach (var goo in branch)
            {
              objs.Add(Utilities.TryConvertItemToSpeckle(goo, Converter));
            }

            if (objs.Count > 0)
              speckleObj[key] = objs;
          }

          index++;
        });
      }
      // --> Report progress if necessary
      // ReportProgress(Id, percentage);
      
      // Call Done() to signal it's finished.
      Done();
    }

    public override WorkerInstance Duplicate() => new CreateSpeckleObjectByKeyValueWorker(Parent, Converter);

    public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
    {
      DA.DisableGapLogic();
      // Use DA.GetData as usual...
      DA.GetDataList(0, keys);
      DA.GetDataTree(1, out valueTree);
      iteration = DA.Iteration;
    }

    public override void SetData(IGH_DataAccess DA)
    {
      // 👉 Checking for cancellation!
      if (CancellationToken.IsCancellationRequested) return;

      // Use DA.SetData as usual...
      DA.SetData(0, new GH_SpeckleBase {Value = speckleObj});
    }
  }
}
