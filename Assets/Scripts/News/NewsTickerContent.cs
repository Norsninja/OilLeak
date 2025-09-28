using System.Collections.Generic;

namespace OilLeak.News
{
    /// <summary>
    /// Data structure for news ticker content loaded from JSON
    /// </summary>
    [System.Serializable]
    public class NewsTickerContent
    {
        public string version;
        public Dictionary<string, TierContent> tiers;
        public List<BreakingNewsTemplate> breaking;
    }

    [System.Serializable]
    public class TierContent
    {
        public List<NewsMessage> messages;
    }

    [System.Serializable]
    public class NewsMessage
    {
        public string id;
        public string template;
        public int weight = 10;
    }

    [System.Serializable]
    public class BreakingNewsTemplate
    {
        public string id;
        public string template;
        public int weight = 10;
        public float cooldownSec = 60f;
        public float ttlSec = 30f;
    }
}