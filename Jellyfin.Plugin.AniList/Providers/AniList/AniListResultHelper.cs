using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListResultHelper
    {
        private readonly ILogger _log;
        private readonly AniListApi _aniListApi;
        
        // NOTE: AniDB has very low request rate limits, a minimum of 2 seconds between requests, and an average of 4 seconds between requests
        // NOTE: anilist 90 requests per minute, more info -> https://anilist.gitbook.io/anilist-apiv2-docs/overview/rate-limiting
        // formula：speed = series Task + movie Task + image Task < 90 Task/per minute  eg: 1min/1sec = 60 < 90
        // 每分钟的请求总数为60，每个请求间的间隔时间必须在500ms以上。具体请求时间自行记录观察当前三种请求的时间点
        // The total number of requests per minute is 60, and the interval between requests must be more than 500ms
        // 三种请求均使用AniListResultHelper中同一个RequestLimiter
        public static readonly RateLimiter RequestLimiter = new RateLimiter(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));


        public AniListResultHelper(ILogger logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
        }

    }
}