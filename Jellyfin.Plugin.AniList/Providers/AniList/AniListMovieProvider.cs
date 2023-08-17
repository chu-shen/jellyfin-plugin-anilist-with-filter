using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly IApplicationPaths _paths;
        private readonly ILogger _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListMovieProvider(IApplicationPaths appPaths, ILogger<AniListMovieProvider> logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
            _paths = appPaths;
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            Media media = null;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await _aniListApi.GetAnime(aid);
            }
            else
            {
                MediaSearchResult msr = null;
                string searchName;

                await AniListHelper.RequestLimiter.Tick().ConfigureAwait(false);
                await Task.Delay(Plugin.Instance.Configuration.AniDbRateLimit).ConfigureAwait(false);

                if (msr == null && info.OriginalTitle != null)
                {
                    searchName = AniListHelper.NameHelper(info.OriginalTitle, _log);

                    await AniListHelper.RequestLimiter.Tick().ConfigureAwait(false);
                    await Task.Delay(Plugin.Instance.Configuration.AniDbRateLimit).ConfigureAwait(false);

                    msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken);
                }

                if(msr == null && !String.Equals(info.OriginalTitle, info.Name, StringComparison.Ordinal))
                {
                    searchName = AniListHelper.NameHelper(info.Name, _log);

                    await AniListHelper.RequestLimiter.Tick().ConfigureAwait(false);
                    await Task.Delay(Plugin.Instance.Configuration.AniDbRateLimit).ConfigureAwait(false);

                    msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken);
                }

                if (msr == null)
                {
                    // get name from path
                    searchName = AniListHelper.NameHelper(Path.GetFileName(info.Path), _log);
                    // get media with correct year
                    var animeYear = new Jellyfin.Plugin.AniList.Anitomy.Anitomy(Path.GetFileName(info.Path)).ExtractAnimeYear();
                    if (animeYear != null)
                        msr = await _aniListApi.Search_GetSeries(searchName, animeYear, cancellationToken);
                    else
                        msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken);
                }

                if (msr != null)
                {
                    media = await _aniListApi.GetAnime(msr.id.ToString());
                }
            }

            if (media != null)
            {
                result.HasMetadata = true;
                result.Item = media.ToMovie();
                result.People = media.GetPeopleInfo();
                result.Provider = ProviderNames.AniList;
                StoreImageUrl(media.id.ToString(), media.GetImageUrl(), "image");
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await _aniListApi.GetAnime(aid).ConfigureAwait(false);
                if (aid_result != null)
                {
                    results.Add(aid_result.ToSearchResult());
                }
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<MediaSearchResult> name_results = await _aniListApi.Search_GetSeries_list(searchInfo.Name, cancellationToken).ConfigureAwait(false);
                foreach (var media in name_results)
                {
                    results.Add(media.ToSearchResult());
                }
            }

            return results;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anilist", type, series + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            await AniListHelper.RequestLimiter.Tick().ConfigureAwait(false);
            var httpClient = Plugin.Instance.GetHttpClient();
            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }
    }
}
