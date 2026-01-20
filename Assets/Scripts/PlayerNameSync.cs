using UnityEngine;
using XRMultiplayer;

/// <summary>
/// Automatically syncs the player name from XRINetworkGameManager to PlayerIdentity
/// Add this to any persistent GameObject in your scene
/// </summary>
public class PlayerNameSync : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How often to check for name changes (seconds)")]
    public float syncInterval = 1f;
    
    private string _lastKnownName = "";
    private float _syncTimer = 0f;

    void Start()
    {
        Debug.Log("[PlayerNameSync] Started - will sync player name from Unity Creator UI to audit system");
        SyncPlayerName();
    }

    void Update()
    {
        _syncTimer += Time.deltaTime;
        if (_syncTimer >= syncInterval)
        {
            _syncTimer = 0f;
            SyncPlayerName();
        }
    }

    void SyncPlayerName()
    {
        string currentName = GetNetworkPlayerName();
        
        if (!string.IsNullOrEmpty(currentName) && currentName != _lastKnownName && currentName != "Unknown" && currentName != "Player")
        {
            _lastKnownName = currentName;
            
            // Sync to PlayerIdentity
            if (PlayerIdentity.Instance != null)
            {
                PlayerIdentity.Instance.SetPlayerName(currentName);
                Debug.Log($"[PlayerNameSync] Synced player name: {currentName}");
            }
        }
    }
    
    string GetNetworkPlayerName()
    {
        try
        {
            return XRINetworkGameManager.LocalPlayerName?.Value;
        }
        catch
        {
            return null;
        }
    }
}

