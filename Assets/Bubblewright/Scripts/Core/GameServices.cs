using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Lightweight, zero-dependency interfaces and default stubs for rewarded/interstitial ads
    /// and gameplay analytics events. Swap the static providers at launch without touching gameplay code.
    /// </summary>
    public interface IAdService
    {
        bool IsRewardedReady(string placement);
        void ShowRewarded(string placement, Action onReward);
        void ShowInterstitial(string placement);
    }

    public interface IAnalyticsService
    {
        void Log(string eventName, params (string key, object value)[] parameters);
    }

    public static class GameServices
    {
        public static IAdService Ads { get; set; } = new StubAdService();
        public static IAnalyticsService Analytics { get; set; } = new StubAnalyticsService();

        public static void ResetToStubs()
        {
            Ads = new StubAdService();
            Analytics = new StubAnalyticsService();
        }
    }

    /// <summary>MVP stub: immediately grants the reward and logs the placement so flows can be tested in-editor.</summary>
    public class StubAdService : IAdService
    {
        public int RewardedShownCount { get; private set; }
        public int InterstitialShownCount { get; private set; }
        public string LastPlacement { get; private set; }

        public bool IsRewardedReady(string placement) => true;

        public void ShowRewarded(string placement, Action onReward)
        {
            RewardedShownCount++;
            LastPlacement = placement;
            GameServices.Analytics.Log("ad_rewarded_shown", ("placement", placement));
            onReward?.Invoke();
        }

        public void ShowInterstitial(string placement)
        {
            InterstitialShownCount++;
            LastPlacement = placement;
            GameServices.Analytics.Log("ad_interstitial_shown", ("placement", placement));
        }
    }

    /// <summary>In-memory event ring buffer (up to 128 recent events) for automated tests and debug inspection.</summary>
    public class StubAnalyticsService : IAnalyticsService
    {
        public struct EventRecord
        {
            public string Name;
            public (string key, object value)[] Parameters;
        }

        public readonly List<EventRecord> Events = new List<EventRecord>();

        public void Log(string eventName, params (string key, object value)[] parameters)
        {
            if (Events.Count >= 128) Events.RemoveAt(0);
            Events.Add(new EventRecord { Name = eventName, Parameters = parameters });
        }
    }
}
