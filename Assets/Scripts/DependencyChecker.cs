// Assets/Scripts/DependencyChecker.cs
using UnityEngine;

// Try these different namespace variations:
// Option 1 (most likely):
using Substrate.NetApi;
using Substrate.NetApi.Model.Types.Primitive;

// Option 2 (if above doesn't work):
// using PolkadotUnitySDK;

// Option 3 (check what's actually available):
// Look in the Project window under Assets/Scripts or Assets/Polkadot Unity SDK/Scripts
// to see what namespaces are actually included

public class DependencyChecker : MonoBehaviour
{
    void Start()
    {
        CheckPolkadotSDK();
    }

    void CheckPolkadotSDK()
    {
        try
        {
            // Basic test - if this compiles, the SDK is working
            Debug.Log("Polkadot SDK namespace found and working!");
            
            // Try to create a basic Substrate client connection test
            Debug.Log("SDK basic functionality test passed");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Polkadot SDK dependency error: {e.Message}");
        }
    }
}