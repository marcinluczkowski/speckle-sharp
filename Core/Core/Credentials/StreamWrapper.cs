﻿using System;
using System.Linq;

namespace Speckle.Core.Credentials
{
  public class StreamWrapper
  {
    public string AccountId { get; set; }
    public string ServerUrl { get; set; }
    public string StreamId { get; set; }
    public string CommitId { get; set; }
    public string BranchName { get; set; } // To be used later! 

    /// <summary>
    /// Determines if the current stream wrapper contains a valid stream.
    /// </summary>
    public bool IsValid => Type != StreamWrapperType.Undefined;
    
    public StreamWrapperType Type
    {
      // Quick solution to determine whether a wrapper points to a branch, commit or stream.
      get
      {
        if (!string.IsNullOrEmpty(BranchName)) return StreamWrapperType.Branch;
        if (!string.IsNullOrEmpty(CommitId)) return StreamWrapperType.Commit;
        if (!string.IsNullOrEmpty(StreamId)) return StreamWrapperType.Stream;
        // If we reach here, it means that the stream is invalid for some reason.
        return StreamWrapperType.Undefined;
      }
    }
    
    public StreamWrapper()
    {
    }

    /// <summary>
    /// Creates a StreamWrapper from a stream url
    /// </summary>
    /// <param name="streamUrl">Stream Url eg: http://speckle.server/streams/8fecc9aa6d/commits/76a23d7179</param>
    /// <exception cref="Exception"></exception>
    public StreamWrapper(string streamUrl)
    {
      Account account;
      Uri uri;
      if (streamUrl.Contains("?u="))
      {
        uri = new Uri(streamUrl.Split(new string[] {"?u="}, StringSplitOptions.None)[0]);
        ServerUrl = uri.GetLeftPart(UriPartial.Authority);

        AccountId = streamUrl.Split(new string[] {"?u="}, StringSplitOptions.None)[1];
        account = GetAccountForServer(AccountId);
      }
      else
      {
        uri = new Uri(streamUrl);
        ServerUrl = uri.GetLeftPart(UriPartial.Authority);
        account = GetAccountForServer();
      }

      if (account == null)
      {
        throw new Exception(
          $"You do not have an account for {ServerUrl}. Please create one or add it to the Speckle Manager.");
      }

      if (uri.Segments.Length < 3)
      {
        throw new Exception($"Cannot parse {uri} into a stream wrapper class.");
      }

      switch (uri.Segments.Length)
      {
        case 3: // ie http://speckle.server/streams/8fecc9aa6d
          if (uri.Segments[1].ToLowerInvariant() == "streams/")
          {
            StreamId = uri.Segments[2].Replace("/", "");
          }
          else
          {
            throw new Exception($"Cannot parse {uri} into a stream wrapper class.");
          }

          break;
        case 5: // ie http://speckle.server/streams/8fecc9aa6d/commits/76a23d7179
          if (uri.Segments[3].ToLowerInvariant() == "commits/")
          {
            StreamId = uri.Segments[2].Replace("/", "");
            CommitId = uri.Segments[4].Replace("/", "");
          }
          else if (uri.Segments[3].ToLowerInvariant() == "branches/")
          {
            StreamId = uri.Segments[2].Replace("/", "");
            BranchName = uri.Segments[4].Replace("/", "");
          }
          else
          {
            throw new Exception($"Cannot parse {uri} into a stream wrapper class.");
          }

          break;
      }
    }

    /// <summary>
    /// Tries to find the best matching account for a stream url
    /// If the default account is on that server returns that, otherwise it picks the first account on that server it finds
    /// </summary>
    /// <returns></returns>
    private Account GetAccountForServer(string accountId = null)
    {
      var accounts = AccountManager.GetAccounts(ServerUrl);

      //get by id
      if (!string.IsNullOrEmpty(accountId))
      {
        var matchingAccount = accounts.FirstOrDefault(x => x.id == accountId);
        if (matchingAccount != null)
          return matchingAccount;
      }

      //get default account, if on this server
      var defaultAccount = accounts.FirstOrDefault(x => x.isDefault);
      var account = defaultAccount;

      //get first account on this server
      if (account == null)
      {
        account = accounts.FirstOrDefault();
      }

      //store Id to avoid further guessing
      if (account != null)
      {
        AccountId = account.id;
      }

      return account;
    }

    /// <summary>
    /// Creates a StreamWrapper by streamId, accountId and serverUrl
    /// </summary>
    /// <param name="streamId"></param>
    /// <param name="accountId"></param>
    /// <param name="serverUrl"></param>
    public StreamWrapper(string streamId, string accountId, string serverUrl)
    {
      AccountId = accountId;
      ServerUrl = serverUrl;
      StreamId = streamId;
    }

    public override string ToString()
    {
      return
        $"{ServerUrl}/streams/{StreamId}{(CommitId != null ? "/commits/" + CommitId : "")}{(AccountId != null ? "?u=" + AccountId : "")}";
    }

    public Account GetAccount()
    {
      return GetAccountForServer(AccountId);
    }
  }
  
  public enum StreamWrapperType
  {
    Undefined,
    Stream,
    Commit,
    Branch
  }
}
