using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AniList.Configuration;
using System.Text.RegularExpressions;


namespace Jellyfin.Plugin.AniList.Filter
{
    public class BasicFilter
    {
        private readonly ILogger<AniListSeriesProvider> _log;

        public BasicFilter(ILogger<AniListSeriesProvider> logger)
        {
            _log = logger;
        }

        /// <summary>
        ///获取真实文件名
        ///get real file name
        /// </summary>
        /// <param name="searchName">文件名 searchName</param>
        /// <returns></returns>
        public string GetRealName(string searchName)
        {    
            _log.LogInformation("Start AniList ... before Searching ({Name})", searchName);   

            // 去除集名
            // quick remove info
            searchName = quickRemoveInfo(searchName);

            // 读取待过滤文字配置，以 , 分割
            // read words list from config to be filtered, split by ,
            PluginConfiguration config = Plugin.Instance.Configuration;
            string[] filterRemoveList = config.FilterRemoveList.Split(',');
            foreach(string c in filterRemoveList)
                searchName = Regex.Replace(searchName, c, "", RegexOptions.IgnoreCase);
            
            // 替换连接符，如：2010-2022 -> 20102022
            // replace connector, eg:2010-2022 -> 20102022
            searchName = searchName.Replace(".", " ");
            searchName = searchName.Replace("-", " ");
            searchName = searchName.Replace("_", " ");
            searchName = searchName.Replace("+", " ");
            searchName = searchName.Replace("`", "");
            searchName = searchName.Replace("'", "");
            searchName = searchName.Replace("&", " ");
            
            // 以 [ 和 ] 分割字符串，然后去除仅数字字符串，如：年份、编号
            // split the string with [ and ] , then remove only numeric strings, such as year and id
            string[] removeTime = searchName.Split(new char[2]{'[',']'});
            Regex onlyNum = new Regex(@"^[0-9]+$");
            searchName = "";
            foreach(string c in removeTime)
                if (!onlyNum.IsMatch(c))
                {
                    searchName += c;
                }
            
            // 替换分隔符
            // replace separator
            searchName = searchName.Replace("（", "");
            searchName = searchName.Replace("）", "");
            searchName = searchName.Replace("(", "");
            searchName = searchName.Replace(")", "");
            searchName = searchName.Replace("【", "");
            searchName = searchName.Replace("】", "");
            
            // 去除空格
            // Remove whitespace
            searchName = searchName.Trim();
   
            _log.LogInformation("Start AniList ... Searching the correct anime({Name})", searchName);  

            return searchName;
        }

        /// <summary>
        ///根据关键词，去除关键词及之后所有非必要部分，如：集名
        ///quick remove unnecesary info, eg:epside title
        /// </summary>
        /// <param name="searchName">文件名 searchName</param>
        /// <returns></returns>
        private string quickRemoveInfo(string searchName)
        {
            string[] quickRemoveEP = {"vol", "下巻", "上巻", "EPISODE", "第1話", "第一話", "第一章", "第一话", "#","上卷", "下卷"};
            
            foreach(string c in quickRemoveEP)
                Regex r = new Regex(c);
                if (r.Match(searchName).Success)
                {
                    searchName = Regex.Split(searchName, c, RegexOptions.IgnoreCase)[0];
                    return searchName;
                }
            
            return searchName;
        }


    }
}
