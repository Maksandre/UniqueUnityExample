using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts;
using Substrate.NetApi;
using Substrate.NetApi.Model.Extrinsics;
using Substrate.NetApi.Model.Rpc;
using Substrate.NetApi.Model.Types;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using Substrate.Unique.NET.NetApiExt.Generated;
using Substrate.Unique.NET.NetApiExt.Generated.Model.sp_runtime.multiaddress;
using Substrate.Unique.NET.NetApiExt.Generated.Storage;
using Substrate.Unique.NET.NetApiExt.Helper;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UniqueNetworkExample : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField mnemonicInput;
    [SerializeField] private TMP_InputField recipientAddressInput;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button sendButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Network Settings")]
    [SerializeField] private string wsEndpoint = "wss://ws.unique.network";

    private SubstrateClientExt _client;
    private bool _isConnected;
    private Account _account;
    private bool _isInitialized;

    private async void Start()
    {
        try
        {
            connectButton?.onClick.AddListener(ConnectToNetwork);
            sendButton?.onClick.AddListener(SendTransaction);
            mnemonicInput?.onValueChanged.AddListener(OnMnemonicChanged);

            OnMnemonicChanged(mnemonicInput?.text ?? "");

            if (statusText != null)
                statusText.text = "Enter mnemonic and click Connect";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    private void ConnectToNetwork()
    {
        _ = ConnectToNetworkAsync();
    }

    private async Task ConnectToNetworkAsync()
    {
        try
        {
            if (statusText != null)
                statusText.text = "Connecting...";

            // Get mnemonic from input field
            var accountMnemonic = mnemonicInput?.text;
            if (string.IsNullOrEmpty(accountMnemonic))
            {
                throw new Exception("Please enter a mnemonic phrase");
            }

            _account = Mnemonic.GetAccountFromMnemonic(accountMnemonic, "", KeyType.Sr25519);

            if (_account == null)
            {
                throw new Exception("Failed to create account");
            }

            // Initialize the SubstrateNetwork client
            _client = new SubstrateClientExt(new Uri(wsEndpoint), ChargeTransactionPayment.Default());
            await _client.ConnectAsync(true, true, destroyCancellationToken);

            if (!_client.IsConnected)
            {
                throw new Exception("Failed to connect to network");
            }

            _isConnected = true;
            _isInitialized = true;

            if (statusText != null)
                statusText.text = $"Connected to Unique Network! Account: {Utils.GetAddressFrom(_account.Bytes)}";

            InvokeRepeating(nameof(CheckConnection), 2.0f, 2.0f);
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            _isConnected = false;

            if (statusText != null)
                statusText.text = $"Failed: {ex.Message}";

            Debug.LogError($"Connection error: {ex}");
        }
    }

    private void CheckConnection()
    {
        if (_client == null || !_isInitialized)
        {
            _isConnected = false;
            if (statusText != null)
                statusText.text = "Not connected";
            return;
        }

        var isConnected = _client.IsConnected;
        if (isConnected != _isConnected)
        {
            _isConnected = isConnected;
            if (statusText != null)
                statusText.text = _isConnected ? "Connected" : "Disconnected";
        }
    }

    private void SendTransaction()
    {
        // recipient:
        var acc = new Account();
        acc.Create(KeyType.Sr25519, Utils.GetPublicKeyFrom(recipientAddressInput.text));

        var account32 = acc.ToAccountId32();

        var multiAddress = new EnumMultiAddress();
        multiAddress.Create(MultiAddress.Id, account32);

        // amount
        var amount = new BaseCom<U128>();
        try
        {
            amount.Create(ulong.Parse(amountInput.text));
        }
        catch (Exception e)
        {
            Debug.LogError($"Invalid amount: {e.Message}");
            if (statusText != null)
                statusText.text = "Invalid amount";
            return;
        }
        Debug.Log($"Sending {Utils.GetAddressFrom(acc.Bytes)}: {amount.Value.Value}");
        var transferKeepAlive = BalancesCalls.TransferKeepAlive(multiAddress, amount);

        _ = SendTransactionAsync(transferKeepAlive);
    }

    private async Task SendTransactionAsync(Method extrinsicMethod)
    {
        if (!_isInitialized || !_isConnected || _client == null || _account == null)
        {
            if (statusText != null)
                statusText.text = "Not properly initialized or connected";
            return;
        }

        await GenericExtrinsicAsync(_client, extrinsicMethod, destroyCancellationToken);
    }

    private async Task GenericExtrinsicAsync(SubstrateClientExt client, Method extrinsicMethod,
        CancellationToken token)
    {
        try
        {
            var subscription = await client.Author.SubmitAndWatchExtrinsicAsync(ActionExtrinsicUpdate, extrinsicMethod, _account, ChargeTransactionPayment.Default(), 64, token);
            if (subscription == null)
            {
                return;
            }

            Debug.Log($"Generic extrinsic sent {extrinsicMethod.ModuleName}_{extrinsicMethod.CallName} with {subscription}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    private void ActionExtrinsicUpdate(string subscriptionId, ExtrinsicStatus extrinsicUpdate)
    {
        var broadcast = extrinsicUpdate.Broadcast != null ? string.Join(",", extrinsicUpdate.Broadcast) : "";
        var hash = extrinsicUpdate.Hash != null ? extrinsicUpdate.Hash.Value : "";

        Debug.Log($"{subscriptionId} => {extrinsicUpdate.ExtrinsicState} [HASH: {hash}] [BROADCAST: {broadcast}]");

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            statusText.text = statusText.text + $"" +
                              $"\n{subscriptionId}" +
                              $"\n => {extrinsicUpdate.ExtrinsicState} {(hash.Length > 0 ? $"[{hash}]" : "")}{(broadcast.Length > 0 ? $"[{broadcast}]" : "")}";
        });
    }

    private void OnMnemonicChanged(string value)
    {
        // TODO: Validate mnemonic format if needed
        if (connectButton != null)
            connectButton.interactable = !string.IsNullOrWhiteSpace(value);
    }


    private void OnDestroy()
    {
        connectButton?.onClick.RemoveListener(ConnectToNetwork);
        sendButton?.onClick.RemoveListener(SendTransaction);
        mnemonicInput?.onValueChanged.RemoveListener(OnMnemonicChanged);

        _client = null;
        _account = null;
        _isInitialized = false;
        _isConnected = false;

        CancelInvoke(nameof(CheckConnection));
    }
}