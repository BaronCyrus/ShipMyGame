// ShipMyGame — Google Play in-app review and in-app update for Unity.
// Needs com.google.play.review and com.google.play.appupdate (github.com/google/play-in-app-reviews-unity,
// play-in-app-updates-unity; they pull com.google.play.common and com.google.play.core). Both packages only exist
// on Android, so everything sits behind #if UNITY_ANDROID. If game code lives in an asmdef, reference
// Google.Play.Review, Google.Play.AppUpdate and Google.Play.Common.
// Review: call PrefetchReview when the player gets close to a good moment (e.g. the result screen of a level),
// then LaunchReview at that moment. Never put it behind a button: Play rations the dialog with a hidden quota.
// Update: checks once at startup; priority comes from the release's inAppUpdatePriority (Play Developer API, 0..5).
// Verified: compiles (Roslyn, C# 9) against Unity 6000.3 and 2022.3 engine assemblies plus the packages' runtime
// sources (2026-11 main), with and without UNITY_ANDROID. NOT run on a device yet.
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Google.Play.AppUpdate;
using Google.Play.Common;
using Google.Play.Review;
#endif

public class ShipPlayReview : MonoBehaviour
{
#pragma warning disable CS0414   // only read on Android
    [Tooltip("Releases with this inAppUpdatePriority or higher ask for an immediate update; lower ones a flexible one.")]
    [SerializeField] int immediateFromPriority = 4;
#pragma warning restore CS0414

#if UNITY_ANDROID
    ReviewManager _reviewManager;
    PlayReviewInfo _reviewInfo;
    AppUpdateManager _updateManager;
#endif

    void Awake()
    {
#if UNITY_ANDROID
        _reviewManager = new ReviewManager();
        _updateManager = new AppUpdateManager();
#endif
    }

    void Start() { StartCoroutine(CheckForUpdate()); }

    public IEnumerator PrefetchReview()
    {
#if UNITY_ANDROID
        var request = _reviewManager.RequestReviewFlow();
        yield return request;
        if (request.Error == ReviewErrorCode.NoError) _reviewInfo = request.GetResult();
        else Debug.Log("[Review] request: " + request.Error);
#else
        yield break;
#endif
    }

    /// <summary>The API never says whether the dialog showed or what the player did: just continue the game.</summary>
    public IEnumerator LaunchReview()
    {
#if UNITY_ANDROID
        if (_reviewInfo == null) yield break;
        var launch = _reviewManager.LaunchReviewFlow(_reviewInfo);
        yield return launch;
        _reviewInfo = null;
#else
        yield break;
#endif
    }

    IEnumerator CheckForUpdate()
    {
#if UNITY_ANDROID
        PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> infoOp = _updateManager.GetAppUpdateInfo();
        yield return infoOp;
        if (!infoOp.IsSuccessful) yield break;

        AppUpdateInfo info = infoOp.GetResult();
        if (info.UpdateAvailability != UpdateAvailability.UpdateAvailable) yield break;

        AppUpdateOptions options = info.UpdatePriority >= immediateFromPriority
            ? AppUpdateOptions.ImmediateAppUpdateOptions()
            : AppUpdateOptions.FlexibleAppUpdateOptions();
        if (!info.IsUpdateTypeAllowed(options)) yield break;

        AppUpdateRequest request = _updateManager.StartUpdate(info, options);
        yield return request;   // AppUpdateRequest is a CustomYieldInstruction

        if (request.Status == AppUpdateStatus.Downloaded)
        {
            // Flexible flow: the download finished in the background; installing restarts the app.
            var complete = _updateManager.CompleteUpdate();
            yield return complete;
            Debug.LogWarning("[Update] complete failed: " + complete.Error);   // only reached when it did not restart
        }
#else
        yield break;
#endif
    }
}
