using Content.Server.Clothing.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.KillTracking;
using Content.Server.Preferences.Managers;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Bank.Events;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Points;
using Content.Shared.Preferences;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem : SharedBankSystem
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private ISawmill _log = default!;

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("bank");
        InitializeATM();
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        EnsureAccount(ev.Profile.Name, 0);
    }
    public MoneyAccountsComponent? GetMoneyAccountsComponent()
    {
        var target = _map.GetMapOrInvalid(_gameTicker.DefaultMap);
        if (!EntityManager.TryGetComponent<MoneyAccountsComponent>(target, out var moneyComp))
        {
            _log.Info($"GetMoneyAccountsComponent: No MoneyAccountsComponent found.");
        }

        return moneyComp;
    }
    
    public void EnsureAccount(string name, int balance = 0)
    {
        var accName = name;
        var component = GetMoneyAccountsComponent();
        if (component == null)
        {
            return;
        }
        MoneyAccountsComponent? accounts = component;
        if (!accounts!.TryGetAccount(accName, out var account))
        {
            accounts.CreateAccount(accName, balance);
        }
    }

    /// <summary>
    /// Attempts to remove money from a character's bank account.
    /// This should always be used instead of attempting to modify the BankAccountComponent directly.
    /// When successful, the entity's BankAccountComponent will be updated with their current balance.
    /// </summary>
    /// <param name="mobUid">The UID that the bank account is attached to, typically the player controlled mob</param>
    /// <param name="amount">The integer amount of which to decrease the bank account</param>
    /// <returns>true if the transaction was successful, false if it was not</returns>
    ///
    public bool TryBankWithdraw(EntityUid mobUid, int amount)
    {
        if (amount <= 0)
        {
            _log.Info($"TryBankWithdraw: {amount} is invalid");
            return false;
        }
        var accName = Name(mobUid);
        var component = GetMoneyAccountsComponent();
        if (component == null)
        {
            return false;
        }
        MoneyAccountsComponent? accounts = component;
        if (!accounts!.TryGetAccount(accName, out var account))
        {
            _log.Info($"TryBankWithdraw: {mobUid} has no bank account");
            return false;
        }

        if (account!.Balance >= amount)
        {
            account.Balance -= amount;
            _log.Info($"{mobUid} withdrew {amount}");
            return true;

        }
        return false;
    }

    /// <summary>
    /// Attempts to add money to a character's bank account. This should always be used instead of attempting to modify the bankaccountcomponent directly
    /// </summary>
    /// <param name="mobUid">The UID that the bank account is connected to, typically the player controlled mob</param>
    /// <param name="amount">The amount of spesos to remove from the bank account</param>
    /// <returns>true if the transaction was successful, false if it was not</returns>
    public bool TryBankDeposit(EntityUid mobUid, int amount)
    {
        if (amount <= 0)
        {
            _log.Info($"TryBankDeposit: {amount} is invalid");
            return false;
        }

        var component = GetMoneyAccountsComponent();
        if (component == null)
        {
            return false;
        }
        MoneyAccountsComponent? accounts = component;
        var accName = Name(mobUid);
        if (!accounts!.TryGetAccount(accName, out var account))
        {
            _log.Info($"TryBankDeposit: {mobUid} has no bank account");
            return false;
        }
        account!.Balance += amount;
        _log.Info($"{mobUid} deposited {amount}");
        return true;
    }

    /// <summary>
    /// Retrieves a character's balance via its in-game entity, if it has one.
    /// </summary>
    /// <param name="ent">The UID that the bank account is connected to, typically the player controlled mob</param>
    /// <param name="balance">When successful, contains the account balance in spesos. Otherwise, set to 0.</param>
    /// <returns>true if the account was successfully queried.</returns>
    public bool TryGetBalance(EntityUid ent, out int balance)
    {
        balance = 0;
        var component = GetMoneyAccountsComponent();
        if (component == null)
        {
            return false;
        }
        MoneyAccountsComponent? accounts = component;
        var accName = Name(ent);
        if (!accounts!.TryGetAccount(accName, out var account))
        {
            _log.Info($"TryGetBalance: {ent} has no bank account");
            return false;
        }

        balance = account!.Balance;
        return true;
    }

}
