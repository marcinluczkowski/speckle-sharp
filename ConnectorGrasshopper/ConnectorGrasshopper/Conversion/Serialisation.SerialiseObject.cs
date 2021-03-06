﻿using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GrasshopperAsyncComponent;
using Speckle.Core.Api;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using System;
using System.Linq;
using ConnectorGrasshopper.Extras;

namespace ConnectorGrasshopper.Conversion
{
  public class SerializeObject : GH_AsyncComponent
  {
    public override Guid ComponentGuid { get => new Guid("EDEBF1F4-3FC3-4E01-95DD-286FF8804EB0"); }

    protected override System.Drawing.Bitmap Icon => Properties.Resources.Serialize;

    public override GH_Exposure Exposure => GH_Exposure.secondary;

    public SerializeObject() : base("Serialize", "SRL", "Serializes a Speckle Base object to JSON", "Speckle 2 Dev", "Conversion")
    {
      BaseWorker = new SerializeWorker(this);
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddParameter(new SpeckleBaseParam("Speckle Object", "O", "Speckle objects to serialize.", GH_ParamAccess.tree));
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("S", "S", "Serialized objects in JSON format.", GH_ParamAccess.tree);
    }

    protected override void BeforeSolveInstance()
    {
      Tracker.TrackPageview("serialization", "serialize");
      base.BeforeSolveInstance();
    }
  }

  public class SerializeWorker : WorkerInstance
  {
    GH_Structure<GH_SpeckleBase> Objects;
    GH_Structure<GH_String> ConvertedObjects;

    public SerializeWorker(GH_Component parent) : base(parent)
    {
      Objects = new GH_Structure<GH_SpeckleBase>();
      ConvertedObjects = new GH_Structure<GH_String>();
    }

    public override void DoWork(Action<string, double> ReportProgress, Action Done)
    {
      if (CancellationToken.IsCancellationRequested) return;

      int branchIndex = 0, completed = 0;
      foreach (var list in Objects.Branches)
      {
        var path = Objects.Paths[branchIndex];
        foreach (var item in list)
        {
          if (CancellationToken.IsCancellationRequested) return;
          
          if (item != null)
          {
            var serialised = Operations.Serialize(item.Value);
            ConvertedObjects.Append(new GH_String { Value = serialised }, path);
          }
          else
          {
            ConvertedObjects.Append(new GH_String { Value = null }, path);
            Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Object at {Objects.Paths[branchIndex]} is not a Speckle object.");
          }

          ReportProgress(Id, ((completed++ + 1) / (double)Objects.Count()));
        }

        branchIndex++;
      }

      Done();
    }

    public override WorkerInstance Duplicate() => new SerializeWorker(Parent);

    public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
    {
      if (CancellationToken.IsCancellationRequested) return;

      GH_Structure<GH_SpeckleBase> _objects;
      DA.GetDataTree(0, out _objects);

      int branchIndex = 0;
      foreach (var list in _objects.Branches)
      {
        var path = _objects.Paths[branchIndex];
        foreach (var item in list)
        {
          Objects.Append(item, path);
        }
        branchIndex++;
      }
    }

    public override void SetData(IGH_DataAccess DA)
    {
      if (CancellationToken.IsCancellationRequested) return;
      DA.SetDataTree(0, ConvertedObjects);
    }
  }
}
