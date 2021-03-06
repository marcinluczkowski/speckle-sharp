﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;
using Speckle.Core.Api;
using Speckle.Core.Credentials;
using Speckle.Core.Logging;
using Speckle.DesktopUI.Accounts;
using Speckle.DesktopUI.Utils;
using Stylet;

namespace Speckle.DesktopUI.Streams
{
  public class StreamCreateDialogViewModel : StreamDialogBase,
    IHandle<RetrievedFilteredObjectsEvent>, IHandle<ApplicationEvent>
  {
    private readonly IEventAggregator _events;
    private ISnackbarMessageQueue _notifications = new SnackbarMessageQueue(TimeSpan.FromSeconds(5));

    public StreamCreateDialogViewModel(IEventAggregator events, StreamsRepository streamsRepo, AccountsRepository acctsRepo, ConnectorBindings bindings)
    {
      DisplayName = "Create Stream";
      _events = events;
      Bindings = bindings;
      FilterTabs = new BindableCollection<FilterTab>(Bindings.GetSelectionFilters().Select(f => new FilterTab(f)));
      _acctRepo = acctsRepo;

      SelectionCount = Bindings.GetSelectedObjects().Count;
      _events.Subscribe(this);
    }

    private readonly AccountsRepository _acctRepo;

    public override Account AccountToSendFrom
    {
      get => _accountToSendFrom;
      set
      {
        SetAndNotify(ref _accountToSendFrom, value);

        _latestStreams = null;
        StreamSearchResults?.Clear();
        SearchForStreams();
      }
    }

    public ISnackbarMessageQueue Notifications
    {
      get => _notifications;
      set => SetAndNotify(ref _notifications, value);
    }

    public List<string> StreamIds;

    private bool _createButtonLoading;

    public bool CreateButtonLoading
    {
      get => _createButtonLoading;
      set => SetAndNotify(ref _createButtonLoading, value);
    }

    private bool _addExistingButtonLoading;

    public bool AddExistingButtonLoading
    {
      get => _addExistingButtonLoading;
      set => SetAndNotify(ref _addExistingButtonLoading, value);
    }

    private Stream _streamToCreate = new Stream();

    public Stream StreamToCreate
    {
      get => _streamToCreate;
      set => SetAndNotify(ref _streamToCreate, value);
    }

    private StreamState _streamState = new StreamState();

    public StreamState StreamState
    {
      get => _streamState;
      set => SetAndNotify(ref _streamState, value);
    }

    public ObservableCollection<Account> Accounts
    {
      get => _acctRepo.LoadAccounts();
    }

    private System.Windows.Visibility _AccountSelectionVisibility = System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility AccountSelectionVisibility
    {
      get => _AccountSelectionVisibility;
      set
      {
        SetAndNotify(ref _AccountSelectionVisibility, value);
      }
    }

    public void ToggleAccountSelection()
    {
      AccountSelectionVisibility = AccountSelectionVisibility == System.Windows.Visibility.Visible ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    #region Searching Existing Streams

    private string _streamQuery;

    public string StreamQuery
    {
      get => _streamQuery;
      set
      {
        SetAndNotify(ref _streamQuery, value);

        if (value == "")
        {
          StreamSearchResults?.Clear();
        }
        SearchForStreams();
      }
    }



    private BindableCollection<Stream> _streamSearchResults;

    public BindableCollection<Stream> StreamSearchResults
    {
      get => _streamSearchResults;
      set
      {
        SetAndNotify(ref _streamSearchResults, value);
      }
    }

    public bool HasSearchResults
    {
      get => StreamSearchResults != null && StreamSearchResults.Count > 0 ? true : false;
    }

    private List<Stream> _latestStreams;

    private async Task<List<Stream>> GetLatestStreams()
    {
      if (_latestStreams == null)
      {
        var client = new Client(AccountToSendFrom);
        _latestStreams = await client.StreamsGet(3);
      }
      return _latestStreams;
    }

    private async void SearchForStreams()
    {
      //Show latest 3 streams if query field is empty
      if (string.IsNullOrEmpty(StreamQuery))
      {
        try
        {
          StreamSearchResults = new BindableCollection<Stream>(await GetLatestStreams());
        }
        catch
        {
          StreamSearchResults?.Clear();
        }
        return;
      }


      if (StreamQuery.Length <= 2)
        return;

      try
      {
        var client = new Client(AccountToSendFrom);
        var streams = await client.StreamSearch(StreamQuery);
        StreamSearchResults = new BindableCollection<Stream>(streams);
      }
      catch (Exception)
      {
        // search prob returned no results
        StreamSearchResults?.Clear();
      }
    }

    #endregion

    public async void AddNewStream()
    {
      CreateButtonLoading = true;
      Tracker.TrackPageview(Tracker.STREAM_CREATE);
      var client = new Client(AccountToSendFrom);
      try
      {
        var streamId = await client.StreamCreate(new StreamCreateInput()
        {
          name = StreamToCreate.name, description = StreamToCreate.description, isPublic = StreamToCreate.isPublic
        });

        if (Collaborators.Any())
        {
          Tracker.TrackPageview("stream", "collaborators");
        }

        foreach (var user in Collaborators)
        {
          var res = await client.StreamGrantPermission(new StreamGrantPermissionInput()
          {
            streamId = streamId,
            userId = user.id,
            role = "stream:contributor"
          });
        }

        StreamToCreate = await client.StreamGet(streamId);

        StreamState = new StreamState(client, StreamToCreate);
        StreamState.Branches = await client.StreamGetBranches(streamId);
        Bindings.AddNewStream(StreamState);

        _events.Publish(new StreamAddedEvent() { NewStream = StreamState });
        StreamState = new StreamState();
        CloseDialog();
      }
      catch (Exception e)
      {
        await client.StreamDelete(StreamToCreate.id);
        Log.CaptureException(e);
        Notifications.Enqueue($"Error: {e.Message}");
      }

      CreateButtonLoading = false;
    }


    public async void AddExistingStream(Stream SelectedStream)
    {
      if (StreamIds.Contains(SelectedStream.id))
      {
        Notifications.Enqueue("This stream already exists in this file");
        return;
      }

      AddExistingButtonLoading = true;
      Tracker.TrackPageview(Tracker.STREAM_GET);

      var client = new Client(AccountToSendFrom);
      StreamToCreate = await client.StreamGet(SelectedStream.id);

      StreamState = new StreamState(client, StreamToCreate);
      StreamState.Branches = await client.StreamGetBranches(StreamState.Stream.id);

      StreamState.IsSenderCard = false; // Assume we're creating a receiver

      Bindings.AddNewStream(StreamState);
      _events.Publish(new StreamAddedEvent() { NewStream = StreamState });

      AddExistingButtonLoading = false;
      CloseDialog();
    }

    public void AddSimpleStream()
    {
      CreateButtonLoading = true;
      Tracker.TrackPageview("stream", "from-selection");
      StreamToCreate.name = StreamQuery;

      AddNewStream();
    }

    public void AddStreamFromView()
    {
      Tracker.TrackPageview("stream", "from-view");
      SelectedFilterTab = FilterTabs.First(tab => tab.Filter.Name == "Selection");
      SelectedFilterTab.Filter.Selection = ActiveViewObjects;

      AddNewStream();
    }

    public void ChangeSlide(string slideIndex)
    {
      SelectedSlide = int.Parse(slideIndex);
    }

    public void Handle(RetrievedFilteredObjectsEvent message)
    {
      StreamState.SelectedObjectIds = message.Objects.Select(x => x.applicationId).ToList();
    }

    public void Handle(ApplicationEvent message)
    {
      switch (message.Type)
      {
        case ApplicationEvent.EventType.ViewActivated:
          {
            NotifyOfPropertyChange(nameof(ActiveViewName));
            NotifyOfPropertyChange(nameof(ActiveViewObjects));
            return;
          }
        case ApplicationEvent.EventType.DocumentClosed:
          {
            CloseDialog();
            return;
          }
        default:
          return;
      }
    }
  }
}
