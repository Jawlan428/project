using UnityEngine;

/// <summary>
/// Singleton component that stores the current player's chosen name for the session.
/// Persists across scene loads using DontDestroyOnLoad.
/// </summary>
public class PlayerIdentity : MonoBehaviour
{
    private static PlayerIdentity _instance;
    private string _playerName = "Unknown";

    public static PlayerIdentity Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PlayerIdentity");
                _instance = go.AddComponent<PlayerIdentity>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets the current player name. Returns "Unknown" if not set.
    /// </summary>
    public string PlayerName => _playerName;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[PlayerIdentity] Duplicate instance found. Destroying this one.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Sets the player name for this session.
    /// </summary>
    /// <param name="name">The player's chosen name</param>
    public void SetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            _playerName = "Unknown";
        }
        else
        {
            _playerName = name;
        }
        Debug.Log($"[PlayerIdentity] Player name set to: {_playerName}");
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
