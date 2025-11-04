using Content.Shared._NF.Bank.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Bank.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class MoneyAccountsComponent : Component
{
    // The amount of money this entity has in their bank account.
    // Should not be modified directly, may be out-of-date.
    [DataField, Access(typeof(SharedBankSystem))]
    [AutoNetworkedField]
    public Dictionary<string, MoneyAccount> MoneyAccounts { get; set; } = new();

    public bool TryGetAccount(string accountname, out MoneyAccount? account)
    {
        if (MoneyAccounts.TryGetValue(accountname, out var Account))
        {
            account = Account;
            return true;
        }
        else
        {
            account = null;
            return false;
        }
    }
    public void CreateAccount(string accountname, int balance = 0)
    {
        MoneyAccount newAccount = new MoneyAccount(accountname, balance);
        MoneyAccounts.Add(accountname, newAccount);
    }
}


[DataDefinition]
[Serializable]
public partial class MoneyAccount
{
    [DataField("_name")]
    public string Name = "Unnamed Money Account";
    [DataField("_balance")]
    public int Balance = 0;

    public MoneyAccount(string name, int balance)
    {
        Name = name;
        Balance = balance;
    }
}
