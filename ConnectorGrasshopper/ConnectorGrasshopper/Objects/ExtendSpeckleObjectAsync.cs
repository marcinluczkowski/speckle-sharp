﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ConnectorGrasshopper.Extras;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GrasshopperAsyncComponent;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using Utilities = ConnectorGrasshopper.Extras.Utilities;

namespace ConnectorGrasshopper.Objects
{
  public class ExtendSpeckleObjectAsync : SelectKitAsyncComponentBase
  {
    public override Guid ComponentGuid => new Guid("00287364-F725-466E-9E38-FDAD270D87D3");
    protected override Bitmap Icon => Properties.Resources.ExtendSpeckleObject;

    public override GH_Exposure Exposure => GH_Exposure.secondary;

    public ExtendSpeckleObjectAsync() : base("Extend Speckle Object", "ESO",
      "Extend a current object with key/value pairs", "Speckle 2", "Object Management")
    {
      BaseWorker = new ExtendSpeckleObjectWorker(this, Converter);
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddParameter(new SpeckleBaseParam("Speckle Object", "O",
        "Speckle object to deconstruct into it's properties.", GH_ParamAccess.item));
      pManager.AddTextParameter("Keys", "K", "List of keys", GH_ParamAccess.list);
      pManager.AddGenericParameter("Values", "V", "List of values", GH_ParamAccess.tree);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddParameter(new SpeckleBaseParam("Speckle Object", "O",
        "Speckle object to deconstruct into it's properties.", GH_ParamAccess.item));
    }
  }

  public class ExtendSpeckleObjectWorker : WorkerInstance
  {
    private Base @base;
    private List<string> keys;
    private GH_Structure<IGH_Goo> valueTree;
    private int iteration;
    public ISpeckleConverter Converter;

    public ExtendSpeckleObjectWorker(GH_Component _parent, ISpeckleConverter converter) : base(_parent)
    {
      Converter = converter;
      keys = new List<string>();
      valueTree = new GH_Structure<IGH_Goo>();
    }

    public override WorkerInstance Duplicate()
    {
      return new ExtendSpeckleObjectWorker(Parent, Converter);
    }

    private void AssignToObject(Base b, List<string> keys, List<IGH_Goo> values)
    {
      var index = 0;
      keys.ForEach(key =>
      {
        if (b[key] != null)
        {
          Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Object {b.id} - Property {key} has been overwritten");
        }

        b[key] = Utilities.TryConvertItemToSpeckle(values[index++], Converter);
      });
    }

    public override void DoWork(Action<string, double> ReportProgress, Action Done)
    {
      Parent.Message = "Extending...";
      var path = new GH_Path(iteration);
      if (valueTree.PathExists(path))
      {
        var values = valueTree.get_Branch(path) as List<IGH_Goo>;
        // Input is a list of values. Assign them directly
        if (keys.Count != values?.Count)
        {
          Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Key and Value lists are not the same length.");
          Done();
        }

        AssignToObject(@base, keys, values);
      }
      else if (valueTree.Branches.Count == 1)
      {
        var values = valueTree.Branches[0];
        if (keys.Count != values.Count)
        {
          Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Key and Value lists are not the same length.");
          Done();
        }

        // Input is just one list, so use it.
        AssignToObject(@base, keys, values);
      }
      else
      {
        // Input is a tree, meaning it's values are either lists or trees.
        var subTree = Utilities.GetSubTree(valueTree, path);
        var index = 0;
        keys.ForEach(key =>
        {
          var subPath = new GH_Path(index);
          if (subTree.PathExists(subPath))
          {
            // Value is a list, convert and assign.
            var list = subTree.get_Branch(subPath) as List<IGH_Goo>;
            if (list?.Count > 0)
            {
              @base[key] = list.Select(goo => Utilities.TryConvertItemToSpeckle(goo, Converter)).ToList();
            };
          }
          else
          {
            // TODO: Handle tree conversions
            Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Cannot handle trees yet");
          }

          index++;
        });
      }

      Done();
    }

    public override void SetData(IGH_DataAccess DA)
    {
      DA.SetData(0, new GH_SpeckleBase { Value = @base });
    }

    public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
    {
      DA.DisableGapLogic();
      GH_SpeckleBase ghBase = null;
      DA.GetData(0, ref ghBase);
      DA.GetDataList(1, keys);
      DA.GetDataTree(2, out valueTree);
      iteration = DA.Iteration;
      if (ghBase == null)
      {
        return;
      }

      @base = ghBase.Value.ShallowCopy();
    }
  }
}
