﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Speckle.Core.Logging;
using Speckle.Core.Models;

namespace Speckle.Core.Transports
{
  /// <summary>
  /// An in memory storage of speckle objects.
  /// </summary>
  public class MemoryTransport : ITransport
  {
    public Dictionary<string, string> Objects;

    public CancellationToken CancellationToken { get; set; }

    public string TransportName { get; set; } = "Memory";

    public Action<string, int> OnProgressAction { get; set; }

    public Action<string, Exception> OnErrorAction { get; set; }

    public int SavedObjectCount { get; set; } = 0;

    public MemoryTransport()
    {
      Log.AddBreadcrumb("New Memory Transport");

      Objects = new Dictionary<string, string>();
    }

    public void BeginWrite()
    {
      SavedObjectCount = 0;
    }

    public void EndWrite() { }

    public void SaveObject(string hash, string serializedObject)
    {
      if (CancellationToken.IsCancellationRequested) return; // Check for cancellation

      Objects[hash] = serializedObject;
      
      SavedObjectCount++;
      OnProgressAction?.Invoke(TransportName, 1);
    }

    public void SaveObject(string id, ITransport sourceTransport)
    {
      throw new NotImplementedException();
    }

    public string GetObject(string hash)
    {
      if (CancellationToken.IsCancellationRequested) return null; // Check for cancellation

      if (Objects.ContainsKey(hash)) return Objects[hash];
      else
      {
        Log.CaptureException(new SpeckleException("No object found in this memory transport."), level: Sentry.Protocol.SentryLevel.Warning);
        throw new SpeckleException("No object found in this memory transport.");
      }
    }

    public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport, Action<int> onTotalChildrenCountKnown = null)
    {
      throw new NotImplementedException();
    }

    public bool GetWriteCompletionStatus()
    {
      return true; // can safely assume it's always true, as ops are atomic?
    }

    public Task WriteComplete()
    {
      return Utilities.WaitUntil(() => true);
    }

    public override string ToString()
    {
      return $"Memory Transport {TransportName}";
    }
  }

}
