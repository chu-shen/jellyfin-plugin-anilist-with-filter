using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AniList.Filter;
using System.Text.RegularExpressions;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListResultProvider
    {
        private readonly ILogger _log;
        private readonly AniListApi _aniListApi;
        
        // NOTE: AniDB has very low request rate limits, a minimum of 2 seconds between requests, and an average of 4 seconds between requests
        // NOTE: anilist 90 requests per minute, more info -> https://anilist.gitbook.io/anilist-apiv2-docs/overview/rate-limiting
        // formula：speed = series Task + movie Task + image Task < 90 Task/per minute  eg: 1min/1sec = 60 < 90
        // 每分钟的请求总数为60，每个请求间的间隔时间必须在500ms以上。具体请求时间自行记录观察当前三种请求的时间点
        // The total number of requests per minute is 60, and the interval between requests must be more than 500ms
        // 三种请求均使用AniListResultProvider中同一个RequestLimiter
        public static readonly RateLimiter RequestLimiter = new RateLimiter(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));


        public AniListResultProvider(ILogger logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
        }

        public async Task<Media> GetMedia(ItemLookupInfo info, CancellationToken cancellationToken)
        {
            Media media = null;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await _aniListApi.GetAnime(aid);
            }
            else
            {
                //https://github.com/jellyfin/jellyfin/blob/master/Emby.Naming/TV/SeriesInfo.cs
                //https://github.com/jellyfin/jellyfin/blob/f863ca1f2d00839fd78a7655c759e25e9815483f/Emby.Naming/TV/SeriesResolver.cs#L43
                // use true file name(without extension) ,not info.Name. because it will change to something else 
                string searchName = Path.GetFileNameWithoutExtension(info.Path);
                _log.LogDebug("Start AniList ... before Searching ({Name})", searchName); 
                
                BasicFilter basicFilter = new BasicFilter(_log);
                searchName = basicFilter.GetRealName(searchName);
                
                _log.LogInformation("Start AniList ... Searching the correct anime({Name})", searchName);  
                            
                _log.LogTrace(System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fff")+":series requet time");
                
                
                MediaSearchResult msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken);
                
                //过滤包含歧义的词语
                //try another filter(Ambiguous words)
                if(msr == null)
                {
                    string searchStrictName = basicFilter.GetStrictName(searchName);
                    _log.LogInformation("Retry AniList:  ... Searching strict name ({Name})", searchStrictName);  
                    msr = await _aniListApi.Search_GetSeries(searchStrictName, cancellationToken);
                }
                
                // 截取部分标题自动重试
                // get part of title and try again automatically
                // TODO a better retry
                byte countRetry = 0;
                while(msr == null && countRetry<1)
                {
                    countRetry++;
                    string searchPartName = basicFilter.GetPartName(searchName);
                    _log.LogInformation("Retry AniList: ({Count}) ... Searching part name ({Name})", countRetry, searchPartName);  
                    msr = await _aniListApi.Search_GetSeries(searchPartName, cancellationToken);
                }                
                
                if (msr != null)
                {
                    media = await _aniListApi.GetAnime(msr.id.ToString());
                }
            }

            return media;
        }

    }
}
