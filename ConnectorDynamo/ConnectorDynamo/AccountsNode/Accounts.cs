﻿extern alias DynamoNewtonsoft;
using DNJ = DynamoNewtonsoft::Newtonsoft.Json;
using Dynamo.Graph.Nodes;
using ProtoCore.AST.AssociativeAST;
using Speckle.ConnectorDynamo.Functions;
using Speckle.Core.Credentials;
using System;
using System.Collections.Generic;
using System.Linq;
using Speckle.Core.Logging;
using Dynamo.Engine;
using ProtoCore.Mirror;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Speckle.Core.Models;
using Account = Speckle.Core.Credentials.Account;

namespace Speckle.ConnectorDynamo.AccountsNode
{
  /// <summary>
  /// List Accounts
  /// </summary>
  [NodeName("Select")]
  [NodeCategory("Speckle 2.Account.Create")]
  [NodeDescription("Select a Speckle account")]
  [NodeSearchTags("accounts", "speckle")]
  [IsDesignScriptCompatible]
  public class Accounts : NodeModel
  {
    public string SelectedAccountId = "";
    private ObservableCollection<Core.Credentials.Account> _accountList = new ObservableCollection<Account>();

    /// <summary>
    /// UI Binding
    /// </summary>
    [DNJ.JsonIgnore]
    public ObservableCollection<Core.Credentials.Account> AccountList
    {
      get => _accountList;
      set
      {
        _accountList = value;
        RaisePropertyChanged("AccountList");
      }
    }

    private Account _selectedAccount;

    /// <summary>
    /// UI Binding
    /// </summary>
    [DNJ.JsonIgnore]
    public Account SelectedAccount
    {
      get => _selectedAccount;
      set
      {
        _selectedAccount = value;
        RaisePropertyChanged("SelectedAccount");
      }
    }


    /// <summary>
    /// JSON constructor, called on file open
    /// </summary>
    /// <param name="inPorts"></param>
    /// <param name="outPorts"></param>
    [DNJ.JsonConstructor]
    private Accounts(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts)
    {
      if (outPorts.Count() == 0)
        AddOutputs();

      ArgumentLacing = LacingStrategy.Disabled;
    }

    /// <summary>
    /// Normal constructor, called when adding node to canvas
    /// </summary>
    public Accounts()
    {
      Tracker.TrackPageview(Tracker.ACCOUNT_LIST);

      AddOutputs();

      RegisterAllPorts();
      ArgumentLacing = LacingStrategy.Disabled;
    }

    private void AddOutputs()
    {
      OutPorts.Add(new PortModel(PortType.Output, this, new PortData("account", "Selected account")));
    }

    internal void RestoreSelection()
    {
      AccountList = new ObservableCollection<Account>(AccountManager.GetAccounts());
      if (AccountList.Count == 0)
      {
        Warning("No accounts found. Please use the Speckle Manager to manage your accounts on this computer.");
        SelectedAccount = null;
        SelectedAccountId = "";
        return;
      }
      
      SelectedAccount = !string.IsNullOrEmpty(SelectedAccountId) 
        ? AccountList.FirstOrDefault(x => x.id == SelectedAccountId) 
        : AccountList.FirstOrDefault(x => x.isDefault);
    }

    internal void SelectionChanged(Account account)
    {
      SelectedAccountId = account.id;
      OnNodeModified(true);
    }

    #region overrides

    /// <summary>
    /// Returns the selected account if found
    /// NOTE: if an account is deleted via the account manager after being selected in this node and saved
    /// upon open it will return null
    /// </summary>
    /// <param name="inputAstNodes"></param>
    /// <returns></returns>
    public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
    {
      var id = SelectedAccountId ?? "";
      var functionCall = AstFactory.BuildFunctionCall(
        new Func<string, Account>(Functions.Account.GetById),
        new List<AssociativeNode> {AstFactory.BuildStringNode(id)});

      return new[] {AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), functionCall)};
    }

    #endregion
  }
}
