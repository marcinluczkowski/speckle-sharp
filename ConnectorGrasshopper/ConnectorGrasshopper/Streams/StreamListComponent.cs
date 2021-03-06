﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ConnectorGrasshopper.Extras;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Speckle.Core.Api;
using Speckle.Core.Credentials;
using Speckle.Core.Logging;

namespace ConnectorGrasshopper.Streams
{
  public class StreamListComponent : GH_Component
  {
    public StreamListComponent() : base("Stream List", "sList", "Lists all the streams for this account", "Speckle 2",
      "Streams")
    {
    }

    public override Guid ComponentGuid => new Guid("BE790AF4-1834-495B-BE68-922B42FD53C7");
    protected override Bitmap Icon => Properties.Resources.StreamList;

    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      var acc = pManager.AddTextParameter("Account", "A", "Account to get streams from", GH_ParamAccess.item);
      pManager.AddIntegerParameter("Limit", "L", "Max number of streams to fetch", GH_ParamAccess.item, 10);
      
      Params.Input[acc].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddParameter(new SpeckleStreamParam("Streams", "S", "List of streams for the provided account.",
        GH_ParamAccess.list));
    }

    private List<StreamWrapper> streams;
    private Exception error;

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      if (error != null)
      {
        Message = null;
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.Message);
        error = null;
        streams = null;
      }
      else if (streams == null)
      {
        Message = "Fetching";
        string accountId = null;
        var limit = 10;

        DA.GetData(1, ref limit); // Has default value so will never be empty.

        var account = !DA.GetData(0, ref accountId)
          ? AccountManager.GetDefaultAccount()
          : AccountManager.GetAccounts().FirstOrDefault(a => a.id == accountId);

        if (accountId == null)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No account ID was provided");
          Message = null;
          return;
        }

        if (account == null)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not find default account in this machine. Use the Speckle Manager to add an account.");
          return;
        }

        Params.Input[0].AddVolatileData(new GH_Path(0), 0, account.id);

        Task.Run(async () =>
        {
          try
          {
            var client = new Client(account);
            // Save the result
            var result = await client.StreamsGet(limit);
            streams = result
              .Select(stream => new StreamWrapper(stream.id, account.id, account.serverInfo.url))
              .ToList();
          }
          catch (Exception e)
          {
            error = e;
          }
          finally
          {
            Rhino.RhinoApp.InvokeOnUiThread((Action)delegate { ExpireSolution(true); });
          }
        });
      }
      else
      {
        Message = "Done";
        if (streams != null)
          DA.SetDataList(0, streams.Select(item => new GH_SpeckleStream(item)));
        streams = null;
      }
    }

    protected override void BeforeSolveInstance()
    {
      Tracker.TrackPageview("stream", "list");
      base.BeforeSolveInstance();
    }
  }
}
