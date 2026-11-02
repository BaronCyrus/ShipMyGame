// ShipMyGame — Play Games Services v2 for Unity: sign-in, achievements, leaderboards, saved games, friends.
// Copy into a runtime folder of the game (e.g. Assets/Scripts/) and put it on a GameObject in the first scene.
// Needs the Google Play Games plugin for Unity v2.x (github.com/playgameservices/play-games-plugin-for-unity).
// Use the constants that ShipPgsSetup writes (GPGSIds.achievement_xxx, GPGSIds.leaderboard_xxx) as IDs.
// The plugin's runtime only exists on Android, so every call sits behind #if UNITY_ANDROID and other build targets
// (WebGL for the mini-game channels) get no-ops. If game code lives in an asmdef, reference Google.Play.Games.
// Verified: compiles (Roslyn, C# 9) against Unity 6000.3 and 2022.3 engine assemblies plus the plugin's runtime
// sources from the same month as v2.3.0, with and without UNITY_ANDROID. NOT run on a device yet.
using System;
using System.Text;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif

public class ShipPlayGames : MonoBehaviour
{
    public static ShipPlayGames Instance { get; private set; }
    public bool IsSignedIn { get; private set; }
    /// <summary>Raised after every sign-in attempt; show a "Sign in with Play Games" button while it is false.</summary>
    public event Action<bool> SignInChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
#if UNITY_ANDROID
        // v2 signs in automatically when the game starts; this only fetches that result. Never block the game on it.
        PlayGamesPlatform.Instance.Authenticate(OnSignIn);
#else
        SignInChanged?.Invoke(false);
#endif
    }

    /// <summary>For the sign-in button (only after the automatic attempt failed).</summary>
    public void ManualSignIn()
    {
#if UNITY_ANDROID
        PlayGamesPlatform.Instance.ManuallyAuthenticate(OnSignIn);
#endif
    }

#if UNITY_ANDROID
    void OnSignIn(SignInStatus status)
    {
        IsSignedIn = status == SignInStatus.Success;
        Debug.Log("[PGS] sign-in: " + status);   // Canceled on an emulator without a Google account; the game goes on
        SignInChanged?.Invoke(IsSignedIn);
    }
#endif

    // ---------- achievements ----------
    public void Unlock(string achievementId)
    {
#if UNITY_ANDROID
        if (IsSignedIn) PlayGamesPlatform.Instance.UnlockAchievement(achievementId, ok => Debug.Log("[PGS] unlock " + ok));
#endif
    }

    /// <summary>Incremental achievement: adds steps.</summary>
    public void Increment(string achievementId, int steps)
    {
#if UNITY_ANDROID
        if (IsSignedIn) PlayGamesPlatform.Instance.IncrementAchievement(achievementId, steps, ok => Debug.Log("[PGS] increment " + ok));
#endif
    }

    /// <summary>Incremental achievement: sets an absolute count (safe to call repeatedly with the running total).</summary>
    public void SetStepsAtLeast(string achievementId, int steps)
    {
#if UNITY_ANDROID
        if (IsSignedIn) PlayGamesPlatform.Instance.SetStepsAtLeast(achievementId, steps, ok => Debug.Log("[PGS] steps " + ok));
#endif
    }

    public void ShowAchievements()
    {
#if UNITY_ANDROID
        if (IsSignedIn) PlayGamesPlatform.Instance.ShowAchievementsUI();
#endif
    }

    // ---------- leaderboards ----------
    /// <summary>Lower scores than the player's best are discarded by the server, so submit freely.</summary>
    public void SubmitScore(string leaderboardId, long score)
    {
#if UNITY_ANDROID
        if (IsSignedIn) PlayGamesPlatform.Instance.ReportScore(score, leaderboardId, ok => Debug.Log("[PGS] score " + ok));
#endif
    }

    /// <summary>Null shows all leaderboards.</summary>
    public void ShowLeaderboard(string leaderboardId = null)
    {
#if UNITY_ANDROID
        if (!IsSignedIn) return;
        if (string.IsNullOrEmpty(leaderboardId)) PlayGamesPlatform.Instance.ShowLeaderboardUI();
        else PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId);
#endif
    }

    // ---------- saved games (turn on 「游戏存档」 in the Play Console first; at most 3 MB per file) ----------
    public void SaveToCloud(string slot, string json, TimeSpan totalPlayTime, Action<bool> done = null)
    {
#if UNITY_ANDROID
        if (!IsSignedIn) { done?.Invoke(false); return; }
        ISavedGameClient client = PlayGamesPlatform.Instance.SavedGame;
        client.OpenWithAutomaticConflictResolution(slot, DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseLongestPlaytime, (status, meta) =>
            {
                if (status != SavedGameRequestStatus.Success) { Debug.LogWarning("[PGS] open " + status); done?.Invoke(false); return; }
                SavedGameMetadataUpdate update = new SavedGameMetadataUpdate.Builder()
                    .WithUpdatedPlayedTime(totalPlayTime)
                    .WithUpdatedDescription("Saved " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                    .Build();
                client.CommitUpdate(meta, update, Encoding.UTF8.GetBytes(json), (writeStatus, written) =>
                {
                    Debug.Log("[PGS] save " + writeStatus);
                    done?.Invoke(writeStatus == SavedGameRequestStatus.Success);
                });
            });
#else
        done?.Invoke(false);
#endif
    }

    /// <summary>Calls back with the saved text, or null when not signed in, missing or failed.</summary>
    public void LoadFromCloud(string slot, Action<string> onLoaded)
    {
#if UNITY_ANDROID
        if (!IsSignedIn) { onLoaded(null); return; }
        ISavedGameClient client = PlayGamesPlatform.Instance.SavedGame;
        client.OpenWithAutomaticConflictResolution(slot, DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseLongestPlaytime, (status, meta) =>
            {
                if (status != SavedGameRequestStatus.Success) { onLoaded(null); return; }
                client.ReadBinaryData(meta, (readStatus, data) =>
                    onLoaded(readStatus == SavedGameRequestStatus.Success && data != null && data.Length > 0
                        ? Encoding.UTF8.GetString(data) : null));
            });
#else
        onLoaded(null);
#endif
    }

    // ---------- friends ----------
    /// <summary>Calls back with the number of friends loaded, or -1 when the player has not shared the list yet.</summary>
    public void LoadFriends(Action<int> onCount)
    {
#if UNITY_ANDROID
        if (!IsSignedIn) { onCount(-1); return; }
        PlayGamesPlatform.Instance.LoadFriends(50, false, status =>
        {
            if (status == LoadFriendsStatus.ResolutionRequired)
            {
                // Shows the system "share your friends list" dialog; call LoadFriends again when the player agreed.
                PlayGamesPlatform.Instance.AskForLoadFriendsResolution(ui => Debug.Log("[PGS] friends consent " + ui));
                onCount(-1);
                return;
            }
            var friends = PlayGamesPlatform.Instance.localUser.friends;
            onCount(friends == null ? 0 : friends.Length);
        });
#else
        onCount(-1);
#endif
    }
}
