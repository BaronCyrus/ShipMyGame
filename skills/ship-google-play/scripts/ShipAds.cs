// ShipMyGame — AdMob through the Google Mobile Ads Unity plugin 11.x, with UMP consent first:
// consent (UMP) → MobileAds.Initialize → rewarded / interstitial / anchored adaptive banner.
// Copy into a runtime folder (e.g. Assets/Scripts/) and put it on a GameObject in the first scene.
// The production ad unit IDs below stay EMPTY for every test build: empty means Google's public test units, so
// closed testers who "help" by tapping ads cannot get the AdMob account flagged for invalid traffic. Fill them only
// for the production build, and set the matching App ID in Assets > Google Mobile Ads > Settings.
// Ad callbacks arrive on a background thread: anything touching Unity objects goes through ExecuteInUpdate.
// Verified: compiles (Roslyn, C# 9) against Unity 6000.3 and 2022.3 engine assemblies plus the plugin's sources
// from the same month as v11.6.0. NOT run on a device yet.
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

public class ShipAds : MonoBehaviour
{
    // Production ad unit IDs (AdMob → 应用 → 广告单元). Leave empty until the production build.
    static readonly string RewardedUnitId = "";
    static readonly string InterstitialUnitId = "";
    static readonly string BannerUnitId = "";

    // Google's public Android test ad units: https://developers.google.com/admob/unity/test-ads
    const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
    const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
    const string TestBanner = "ca-app-pub-3940256099942544/9214589741";   // anchored adaptive banner

    static string Pick(string real, string test) => string.IsNullOrEmpty(real) ? test : real;

    public static ShipAds Instance { get; private set; }
    RewardedAd _rewarded;
    InterstitialAd _interstitial;
    BannerView _banner;
    bool _initStarted;

    /// <summary>Hide every "watch an ad" button while this is false (no fill, no consent, account under review).</summary>
    public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();

    /// <summary>Show a "Privacy options" entry in settings only when this is true.</summary>
    public bool PrivacyOptionsRequired =>
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Every launch: refresh consent information, show the form if required, then initialize.
        var request = new ConsentRequestParameters();   // set TagForUnderAgeOfConsent for an audience under the age of consent
        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null) Debug.LogWarning("[Ads] UMP update: " + updateError.Message);
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null) Debug.LogWarning("[Ads] UMP form: " + formError.Message);
                if (ConsentInformation.CanRequestAds()) MobileAdsEventExecutor.ExecuteInUpdate(InitAds);
            });
        });
        // Consent from an earlier session counts right after Update() was called.
        if (ConsentInformation.CanRequestAds()) InitAds();
    }

    public void ShowPrivacyOptions()
    {
        ConsentForm.ShowPrivacyOptionsForm(err => { if (err != null) Debug.LogWarning("[Ads] UMP " + err.Message); });
    }

    void InitAds()
    {
        if (_initStarted) return;
        _initStarted = true;
        MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            LoadRewarded();
            LoadInterstitial();
        }));
    }

    // ---------- rewarded ----------
    void LoadRewarded()
    {
        if (_rewarded != null) { _rewarded.Destroy(); _rewarded = null; }
        RewardedAd.Load(Pick(RewardedUnitId, TestRewarded), new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null) { Debug.LogWarning("[Ads] rewarded load: " + error); return; }   // no tight retry loop
            _rewarded = ad;
            _rewarded.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(LoadRewarded);
            _rewarded.OnAdFullScreenContentFailed += e => MobileAdsEventExecutor.ExecuteInUpdate(LoadRewarded);
        });
    }

    /// <summary>Returns false when no ad is ready; grantReward runs on the main thread after the user earned it.</summary>
    public bool ShowRewarded(Action<double> grantReward)
    {
        if (!RewardedReady) return false;
        _rewarded.Show(reward => MobileAdsEventExecutor.ExecuteInUpdate(() => grantReward(reward.Amount)));
        return true;
    }

    // ---------- interstitial (natural breaks only, e.g. after a level) ----------
    void LoadInterstitial()
    {
        if (_interstitial != null) { _interstitial.Destroy(); _interstitial = null; }
        InterstitialAd.Load(Pick(InterstitialUnitId, TestInterstitial), new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null) { Debug.LogWarning("[Ads] interstitial load: " + error); return; }
            _interstitial = ad;
            _interstitial.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(LoadInterstitial);
            _interstitial.OnAdFullScreenContentFailed += e => MobileAdsEventExecutor.ExecuteInUpdate(LoadInterstitial);
        });
    }

    public void ShowInterstitial()
    {
        if (_interstitial != null && _interstitial.CanShowAd()) _interstitial.Show();
    }

    // ---------- banner ----------
    public void ShowBanner()
    {
        if (_banner != null || !_initStarted) return;
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(MobileAds.Utils.GetDeviceSafeWidth());
        _banner = new BannerView(Pick(BannerUnitId, TestBanner), size, AdPosition.Bottom);
        _banner.OnBannerAdLoadFailed += err => Debug.LogWarning("[Ads] banner: " + err);
        _banner.LoadAd(new AdRequest());
    }

    public void HideBanner()
    {
        if (_banner != null) { _banner.Destroy(); _banner = null; }
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        HideBanner();
        if (_rewarded != null) _rewarded.Destroy();
        if (_interstitial != null) _interstitial.Destroy();
    }
}
