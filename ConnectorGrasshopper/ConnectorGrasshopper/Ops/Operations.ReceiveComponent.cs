﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using ConnectorGrasshopper.Extras;
using ConnectorGrasshopper.Properties;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GrasshopperAsyncComponent;
using Rhino;
using Speckle.Core.Api;
using Speckle.Core.Api.SubscriptionModels;
using Speckle.Core.Credentials;
using Speckle.Core.Kits;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using Speckle.Core.Transports;
using Utilities = ConnectorGrasshopper.Extras.Utilities;

namespace ConnectorGrasshopper.Ops
{
  public class ReceiveComponent : GH_AsyncComponent
  {
    public ISpeckleConverter Converter;

    public ISpeckleKit Kit;

    public ReceiveComponent() : base("Receive", "Receive", "Receive data from a Speckle server", "Speckle 2",
      "   Send/Receive")
    {
      BaseWorker = new ReceiveComponentWorker(this);
      Attributes = new ReceiveComponentAttributes(this);
      SetDefaultKitAndConverter();
    }

    private Client ApiClient { get; set; }

    public bool AutoReceive { get; set; }

    public override Guid ComponentGuid => new Guid("{3D07C1AC-2D05-42DF-A297-F861CCEEFBC7}");

    public string CurrentComponentState { get; set; } = "needs_input";

    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override Bitmap Icon => Resources.Receiver;

    public string InputType { get; set; }

    public bool JustPastedIn { get; set; }

    public string LastCommitDate { get; set; }

    public string LastInfoMessage { get; set; }

    public double OverallProgress { get; set; }

    public string ReceivedCommitId { get; set; }

    public string ReceivedObjectId { get; set; }

    public StreamWrapper StreamWrapper { get; set; }

    public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
    {
      switch (context)
      {
        case GH_DocumentContext.Loaded:
        {
          // Will execute every time a document becomes active (from background or opening file.).
          if (StreamWrapper != null)
            Task.Run(() =>
            {
              // Ensure fresh instance of client.
              ResetApiClient(StreamWrapper);

              // Get last commit from the branch
              var b = ApiClient.BranchGet(BaseWorker.CancellationToken , StreamWrapper.StreamId, StreamWrapper.BranchName ?? "main", 1).Result;

              // Compare commit id's. If they don't match, notify user or fetch data if in auto mode
              if (b.commits.items[0].id != ReceivedCommitId)
                HandleNewCommit();
            });
          break;
        }
        case GH_DocumentContext.Unloaded:
          // Will execute every time a document becomes inactive (in background or closing file.)
          //Correctly dispose of the client when changing documents to prevent subscription handlers being called in background.
          CleanApiClient();
          break;
      }

      base.DocumentContextChanged(document, context);
    }

    private void HandleNewCommit()
    {
      Message = "Expired";
      CurrentComponentState = "expired";
      AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"There is a newer commit available for this {InputType}");

      RhinoApp.InvokeOnUiThread((Action) delegate
      {
        if (AutoReceive)
          ExpireSolution(true);
        else
          OnDisplayExpired(true);
      });
    }

    public override bool Write(GH_IWriter writer)
    {
      writer.SetBoolean("AutoReceive", AutoReceive);
      writer.SetString("CurrentComponentState", CurrentComponentState);
      writer.SetString("LastInfoMessage", LastInfoMessage);
      writer.SetString("LastCommitDate", LastCommitDate);
      writer.SetString("ReceivedObjectId", ReceivedObjectId);
      writer.SetString("ReceivedCommitId", ReceivedCommitId);
      writer.SetString("KitName", Kit.Name);
      var streamUrl = StreamWrapper != null ? StreamWrapper.ToString() : "";
      writer.SetString("StreamWrapper", streamUrl);

      return base.Write(writer);
    }

    public override bool Read(GH_IReader reader)
    {
      AutoReceive = reader.GetBoolean("AutoReceive");
      CurrentComponentState = reader.GetString("CurrentComponentState");
      LastInfoMessage = reader.GetString("LastInfoMessage");
      LastCommitDate = reader.GetString("LastCommitDate");
      ReceivedObjectId = reader.GetString("ReceivedObjectId");
      ReceivedCommitId = reader.GetString("ReceivedCommitId");

      var swString = reader.GetString("StreamWrapper");
      if (!string.IsNullOrEmpty(swString)) StreamWrapper = new StreamWrapper(swString);

      JustPastedIn = true;

      var kitName = "";
      reader.TryGetString("KitName", ref kitName);

      if (kitName != "")
        try
        {
          SetConverterFromKit(kitName);
        }
        catch (Exception e)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            $"Could not find the {kitName} kit on this machine. Do you have it installed? \n Will fallback to the default one.");
          SetDefaultKitAndConverter();
        }
      else
        SetDefaultKitAndConverter();

      return base.Read(reader);
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddGenericParameter("Stream", "S",
        "The Speckle Stream to receive data from. You can also input the Stream ID or it's URL as text.",
        GH_ParamAccess.tree);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddGenericParameter("Data", "D", "Data received.", GH_ParamAccess.tree);
      pManager.AddTextParameter("Info", "I", "Commit information.", GH_ParamAccess.item);
    }

    protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
    {
      Menu_AppendSeparator(menu);
      Menu_AppendItem(menu, "Select the converter you want to use:");
      var kits = KitManager.GetKitsWithConvertersForApp(Applications.Rhino);

      foreach (var kit in kits)
        Menu_AppendItem(menu, $"{kit.Name} ({kit.Description})", (s, e) => { SetConverterFromKit(kit.Name); }, true,
          kit.Name == Kit.Name);

      Menu_AppendSeparator(menu);

      if (InputType == "Stream" || InputType == "Branch")
      {
        var autoReceiveMi = Menu_AppendItem(menu, "Receive automatically", (s, e) =>
        {
          AutoReceive = !AutoReceive;
          RhinoApp.InvokeOnUiThread((Action) delegate { OnDisplayExpired(true); });
        }, true, AutoReceive);
        autoReceiveMi.ToolTipText =
          "Toggle automatic receiving. If set, any upstream change will be pulled instantly. This only is applicable when receiving a stream or a branch.";
      }
      else
      {
        var autoReceiveMi = Menu_AppendItem(menu,
          "Automatic receiving is disabled because you have specified a direct commit.");
        autoReceiveMi.ToolTipText =
          "To enable automatic receiving, you need to input a stream rather than a specific commit.";
      }

      base.AppendAdditionalComponentMenuItems(menu);
    }

    public void SetConverterFromKit(string kitName)
    {
      if (kitName == Kit.Name) return;

      Kit = KitManager.Kits.FirstOrDefault(k => k.Name == kitName);
      Converter = Kit.LoadConverter(Applications.Rhino);

      Message = $"Using the {Kit.Name} Converter";
      ExpireSolution(true);
    }

    private void SetDefaultKitAndConverter()
    {
      Kit = KitManager.GetDefaultKit();
      try
      {
        Converter = Kit.LoadConverter(Applications.Rhino);
        Converter.SetContextDocument(RhinoDoc.ActiveDoc);
      }
      catch
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No default kit found on this machine.");
      }
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      // We need to call this always in here to be able to react and set events :/
      ParseInput(DA);

      if ((AutoReceive || CurrentComponentState == "primed_to_receive" || CurrentComponentState == "receiving") &&
          !JustPastedIn)
      {
        CurrentComponentState = "receiving";

        // Delegate control to parent async component.
        base.SolveInstance(DA);
        return;
      }

      if (!JustPastedIn)
      {
        CurrentComponentState = "expired";
        Message = "Expired";
        OnDisplayExpired(true);
      }

      // Set output data in a "first run" event. Note: we are not persisting the actual "sent" object as it can be very big.
      if (JustPastedIn)
      {
        DA.SetData(1, LastInfoMessage);
        // This ensures that we actually do a run. The worker will check and determine if it needs to pull an existing object or not.
        base.SolveInstance(DA);
      }
    }

    public override void DisplayProgress(object sender, ElapsedEventArgs e)
    {
      if (Workers.Count == 0) return;

      Message = "";
      var total = 0.0;
      foreach (var kvp in ProgressReports)
      {
        Message += $"{kvp.Key}: {kvp.Value:0.00%}\n";
        total += kvp.Value;
      }

      OverallProgress = total / ProgressReports.Keys.Count();

      RhinoApp.InvokeOnUiThread((Action) delegate { OnDisplayExpired(true); });
    }

    public override void RemovedFromDocument(GH_Document document)
    {
      CleanApiClient();
      base.RemovedFromDocument(document);
    }

    private void ParseInput(IGH_DataAccess DA)
    {
      var check = DA.GetDataTree(0, out GH_Structure<IGH_Goo> DataInput);
      if (DataInput.IsEmpty)
      {
        StreamWrapper = null;
        TriggerAutoSave();
        return;
      }

      var ghGoo = DataInput.get_DataItem(0);
      if (ghGoo == null) return;

      var input = ghGoo.GetType().GetProperty("Value")?.GetValue(ghGoo);

      var inputType = "Invalid";
      StreamWrapper newWrapper = null;

      if (input is StreamWrapper wrapper)
      {
        newWrapper = wrapper;
        inputType = GetStreamTypeMessage(newWrapper);
      }
      else if (input is string s)
      {
        newWrapper = new StreamWrapper(s);
        inputType = GetStreamTypeMessage(newWrapper);
      }


      InputType = inputType;
      Message = inputType;
      HandleInputType(newWrapper);
    }

    private string GetStreamTypeMessage(StreamWrapper newWrapper)
    {
      string inputType = null;
      switch (newWrapper?.Type)
      {
        case StreamWrapperType.Undefined:
          inputType = "Invalid";
          break;
        case StreamWrapperType.Stream:
          inputType = "Stream";
          break;
        case StreamWrapperType.Commit:
          inputType = "Commit";
          break;
        case StreamWrapperType.Branch:
          inputType = "Branch";
          break;
      }

      return inputType;
    }

    private void HandleInputType(StreamWrapper wrapper)
    {
      if (wrapper.Type == StreamWrapperType.Commit)
      {
        AutoReceive = false;
        StreamWrapper = wrapper;
        return;
      }

      if (wrapper.Type == StreamWrapperType.Branch)
      {
        // TODO: 
      }

      if (StreamWrapper != null && wrapper.StreamId == StreamWrapper.StreamId && !JustPastedIn) return;

      StreamWrapper = wrapper;

      ResetApiClient(wrapper);
    }

    private void ResetApiClient(StreamWrapper wrapper)
    {
      CleanApiClient();
      ApiClient = new Client(wrapper.GetAccount());
      ApiClient.SubscribeCommitCreated(StreamWrapper.StreamId);
      ApiClient.OnCommitCreated += ApiClient_OnCommitCreated;
    }

    private void CleanApiClient()
    {
      ApiClient?.Dispose();
    }

    private void ApiClient_OnCommitCreated(object sender, CommitInfo e)
    {
      // Break if wrapper is branch type and branch name is not equal.
      if (StreamWrapper.Type == StreamWrapperType.Branch && e.branchName != StreamWrapper.BranchName) return;
      HandleNewCommit();
    }

    protected override void BeforeSolveInstance()
    {
      Tracker.TrackPageview("receive", AutoReceive ? "auto" : "manual");
      base.BeforeSolveInstance();
    }
  }

  public class ReceiveComponentWorker : WorkerInstance
  {
    private GH_Structure<IGH_Goo> DataInput;
    private Action<string, Exception> ErrorAction;

    private Action<ConcurrentDictionary<string, int>> InternalProgressAction;

    public ReceiveComponentWorker(GH_Component p) : base(p)
    {
    }

    private StreamWrapper InputWrapper { get; set; }

    public Commit ReceivedCommit { get; set; }

    public Base ReceivedObject { get; set; }

    private List<(GH_RuntimeMessageLevel, string)> RuntimeMessages { get; } =
      new List<(GH_RuntimeMessageLevel, string)>();

    public int TotalObjectCount { get; set; } = 1;

    public override WorkerInstance Duplicate()
    {
      return new ReceiveComponentWorker(Parent);
    }

    public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
    {
      InputWrapper = ((ReceiveComponent) Parent).StreamWrapper;
    }

    public override void DoWork(Action<string, double> ReportProgress, Action Done)
    {
      InternalProgressAction = dict =>
      {
        foreach (var kvp in dict) ReportProgress(kvp.Key, (double) kvp.Value / TotalObjectCount);
      };

      ErrorAction = (transportName, exception) =>
      {
        RuntimeMessages.Add((GH_RuntimeMessageLevel.Warning, $"{transportName}: {exception.Message}"));
        var asyncParent = (GH_AsyncComponent) Parent;
        asyncParent.CancellationSources.ForEach(source => source.Cancel());
      };

      var client = new Client(InputWrapper?.GetAccount());
      var remoteTransport = new ServerTransport(InputWrapper?.GetAccount(), InputWrapper?.StreamId);
      remoteTransport.TransportName = "R";

      if (((ReceiveComponent) Parent).JustPastedIn &&
          !string.IsNullOrEmpty(((ReceiveComponent) Parent).ReceivedObjectId))
      {
        Task.Run(async () =>
        {
          ReceivedObject = await Operations.Receive(
            ((ReceiveComponent) Parent).ReceivedObjectId,
            CancellationToken,
            remoteTransport,
            new SQLiteTransport {TransportName = "LC"}, // Local cache!
            InternalProgressAction,
            ErrorAction,
            count => TotalObjectCount = count
          );

          Done();
        });
        return;
      }

      // Means it's a copy paste of an empty non-init component; set the record and exit fast.
      if (((ReceiveComponent) Parent).JustPastedIn)
      {
        ((ReceiveComponent) Parent).JustPastedIn = false;
        return;
      }

      Task.Run(async () =>
      {
        Commit myCommit;
        if (InputWrapper.CommitId != null)
          try
          {
            myCommit = await client.CommitGet(CancellationToken, InputWrapper.StreamId, InputWrapper.CommitId);
          }
          catch (Exception e)
          {
            RuntimeMessages.Add((GH_RuntimeMessageLevel.Error, e.Message));
            Done();
            return;
          }
        else
          try
          {
            var branches = await client.StreamGetBranches(InputWrapper.StreamId);
            var mainBranch = branches.FirstOrDefault(b => b.name == (InputWrapper.BranchName ?? "main"));
            myCommit = mainBranch.commits.items[0];
          }
          catch (Exception e)
          {
            RuntimeMessages.Add((GH_RuntimeMessageLevel.Warning,
              "Could not get any commits from the stream's \"main\" branch."));
            Done();
            return;
          }

        ReceivedCommit = myCommit;

        if (CancellationToken.IsCancellationRequested) return;

        ReceivedObject = await Operations.Receive(
          myCommit.referencedObject,
          CancellationToken,
          remoteTransport,
          new SQLiteTransport {TransportName = "LC"}, // Local cache!
          InternalProgressAction,
          ErrorAction,
          count => TotalObjectCount = count
        );

        if (CancellationToken.IsCancellationRequested) return;

        Done();
      });
    }

    public override void SetData(IGH_DataAccess DA)
    {
      if (CancellationToken.IsCancellationRequested) return;

      foreach (var (level, message) in RuntimeMessages) Parent.AddRuntimeMessage(level, message);

      ((ReceiveComponent) Parent).CurrentComponentState = "up_to_date";

      if (ReceivedCommit != null)
      {
        ((ReceiveComponent) Parent).LastInfoMessage =
          $"{ReceivedCommit.authorName} @ {ReceivedCommit.createdAt}: {ReceivedCommit.message} (id:{ReceivedCommit.id})";

        ((ReceiveComponent) Parent).ReceivedCommitId = ReceivedCommit.id;
      }

      ((ReceiveComponent) Parent).JustPastedIn = false;

      DA.SetData(1, ((ReceiveComponent) Parent).LastInfoMessage);

      if (ReceivedObject == null) return;

      ((ReceiveComponent) Parent).ReceivedObjectId = ReceivedObject.id;

      //the active document may have changed
      ((ReceiveComponent) Parent).Converter.SetContextDocument(RhinoDoc.ActiveDoc);

      // case 1: it's an item that has a direct conversion method, eg a point
      if (((ReceiveComponent) Parent).Converter.CanConvertToNative(ReceivedObject))
      {
        DA.SetData(0, Utilities.TryConvertItemToNative(ReceivedObject, ((ReceiveComponent) Parent).Converter));
        return;
      }

      // case 2: it's a wrapper Base
      //       2a: if there's only one member unpack it
      //       2b: otherwise return dictionary of unpacked members

      var members = ReceivedObject.GetDynamicMembers();

      if (members.Count() == 1)
      {
        var treeBuilder = new TreeBuilder(((ReceiveComponent) Parent).Converter);
        var tree = treeBuilder.Build(ReceivedObject[members.ElementAt(0)]);

        DA.SetDataTree(0, tree);
        return;
      }

      // TODO: the base object has multiple members,
      // therefore create a matching structure via the output ports, similar to 
      // running the expando object
      // then run the treebuilder for each port


      DA.SetData(0, new GH_SpeckleBase {Value = ReceivedObject});
    }
  }

  public class ReceiveComponentAttributes : GH_ComponentAttributes
  {
    private bool _selected;

    public ReceiveComponentAttributes(GH_Component owner) : base(owner)
    {
    }

    private Rectangle ButtonBounds { get; set; }

    public override bool Selected
    {
      get => _selected;
      set
      {
        Owner.Params.ToList().ForEach(p => p.Attributes.Selected = value);
        _selected = value;
      }
    }

    protected override void Layout()
    {
      base.Layout();

      var baseRec = GH_Convert.ToRectangle(Bounds);
      baseRec.Height += 26;

      var btnRec = baseRec;
      btnRec.Y = btnRec.Bottom - 26;
      btnRec.Height = 26;
      btnRec.Inflate(-2, -2);

      Bounds = baseRec;
      ButtonBounds = btnRec;
    }

    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
      base.Render(canvas, graphics, channel);

      var state = ((ReceiveComponent) Owner).CurrentComponentState;

      if (channel == GH_CanvasChannel.Objects)
      {
        if (((ReceiveComponent) Owner).AutoReceive)
        {
          var autoSendButton =
            GH_Capsule.CreateTextCapsule(ButtonBounds, ButtonBounds, GH_Palette.Blue, "Auto Receive", 2, 0);

          autoSendButton.Render(graphics, Selected, Owner.Locked, false);
          autoSendButton.Dispose();
        }
        else
        {
          var palette = state == "expired" || state == "up_to_date" ? GH_Palette.Black : GH_Palette.Transparent;
          var text = state == "receiving" ? $"{((ReceiveComponent) Owner).OverallProgress:0.00%}" : "Receive";

          var button = GH_Capsule.CreateTextCapsule(ButtonBounds, ButtonBounds, palette, text, 2,
            state == "expired" ? 10 : 0);
          button.Render(graphics, Selected, Owner.Locked, false);
          button.Dispose();
        }
      }
    }

    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
      if (e.Button != MouseButtons.Left) return base.RespondToMouseDown(sender, e);
      if (!((RectangleF) ButtonBounds).Contains(e.CanvasLocation)) return base.RespondToMouseDown(sender, e);

      if (((ReceiveComponent) Owner).CurrentComponentState == "receiving") return GH_ObjectResponse.Handled;

      if (((ReceiveComponent) Owner).AutoReceive)
      {
        ((ReceiveComponent) Owner).AutoReceive = false;
        Owner.OnDisplayExpired(true);
        return GH_ObjectResponse.Handled;
      }

      ((ReceiveComponent) Owner).CurrentComponentState = "primed_to_receive";
      Owner.ExpireSolution(true);
      return GH_ObjectResponse.Handled;
    }
  }
}
