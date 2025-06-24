using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Threading.Tasks;
using System.Threading;
using static Substrate.NetApi.Mnemonic;
using Substrate.NetApi;
using Substrate.NetApi.Model.Extrinsics;
using Substrate.NetApi.Model.Rpc;
using Substrate.NetApi.Model.Types;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using Substrate.Unique.NET.NetApiExt.Generated;
using Substrate.Unique.NET.NetApiExt.Generated.Storage;
using Substrate.Unique.NET.NetApiExt.Generated.Model.sp_runtime.multiaddress;
using Substrate.Unique.NET.NetApiExt.Helper;

namespace Assets.Scripts
{
    public class UniqueNetworkExample : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField recipientAddressInput;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Network Settings")]
        [SerializeField] private string wsEndpoint = "wss://ws-opal.unique.network";
        [SerializeField] private string accountMnemonic = "more permit wear stand bamboo veteran guide toward daughter dragon scheme surprise"; // This is used as a mnemonic for account generation
        
        private SubstrateClientExt _client;
        private bool _isConnected;
        private Account _account;
        private CancellationTokenSource _cts;
        private bool _isInitialized;

        private async void Start()
        {
            try
            {
                if (sendButton != null)
                    sendButton.onClick.AddListener(SendTransaction);
            
                _cts = new CancellationTokenSource();
                await ConnectToNetwork();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private async Task ConnectToNetwork()
        {
            try
            {
                if (statusText != null)
                    statusText.text = "Connecting...";

                _account = GetAccountFromMnemonic(accountMnemonic, "", KeyType.Sr25519);
                
                if (_account == null)
                {
                    throw new Exception("Failed to create account");
                }

                // Initialize the SubstrateNetwork client
                _client = new SubstrateClientExt(new Uri(wsEndpoint), ChargeTransactionPayment.Default());
                await _client.ConnectAsync(true, true, _cts.Token);
                
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
            amount.Create(1);
            
            
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

            await GenericExtrinsicAsync(_client, extrinsicMethod, CancellationToken.None);
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

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            _client = null;
            _account = null;
            _isInitialized = false;
            _isConnected = false;
            
            CancelInvoke(nameof(CheckConnection));
        }
    }
}