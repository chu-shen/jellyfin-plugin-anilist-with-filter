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
        private readonly ILogger<BasicFilter> _log;
        
        public BasicFilter(){}
        
        public BasicFilter(ILogger<BasicFilter> logger)
        {
            _log=logger;
        }

        /// <summary>
        ///获取真实文件名
        ///get real file name
        /// </summary>
        /// <param name="searchName">文件名 searchName</param>
        /// <returns></returns>
        public string GetRealName(string searchName)
        {    

            // 去除集名
            // quick remove info
            searchName = quickRemoveInfo(searchName);
            _log.LogInformation("step 1 ({Name})", searchName);   
            // 读取待过滤文字配置，以 , 分割
            // read words list from config to be filtered, split by ,
            PluginConfiguration config = Plugin.Instance.Configuration;
            string[] filterRemoveList = config.FilterRemoveList.Split(',');
            foreach(string c in filterRemoveList)
                searchName = Regex.Replace(searchName, c, "", RegexOptions.IgnoreCase);
            
            _log.LogInformation("step 2 ({Name})", searchName);   
            // 替换连接符，如：2010-2022 -> 20102022
            // replace connector, eg:2010-2022 -> 20102022
            searchName = searchName.Replace(".", " ");
            searchName = searchName.Replace("-", " ");
            searchName = searchName.Replace("_", " ");
            searchName = searchName.Replace("+", " ");
            searchName = searchName.Replace("`", "");
            searchName = searchName.Replace("'", "");
            searchName = searchName.Replace("&", " ");
            
            _log.LogInformation("step 3 ({Name})", searchName);   
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
            
            _log.LogInformation("step 4 ({Name})", searchName);   
            // 替换分隔符
            // replace separator
            searchName = searchName.Replace("（", "");
            searchName = searchName.Replace("）", "");
            searchName = searchName.Replace("(", "");
            searchName = searchName.Replace(")", "");
            searchName = searchName.Replace("【", "");
            searchName = searchName.Replace("】", "");
            
            _log.LogInformation("step 5 ({Name})", searchName);   
            // 去除空格
            // Remove whitespace
            searchName = searchName.Trim();

            _log.LogDebug("step 6 final ({Name})", searchName);   
            return searchName;
        }
        
        
        /// <summary>
        ///获取部分文件名
        ///get part of file name
        ///如果搜索结果为空，不直接返回，尝试改变标题重新搜索
        ///do not return directly, try to change the title and search again
        ///TODO a better strategy
        /// </summary>
        /// <param name="searchName">文件名 searchName</param>
        /// <returns></returns>
        public string GetPartName(string searchName)
        {   
            // 通常，动画是罗马音或原日文标题，比较规范。而里番大概率是罗马音与日文混用或标题名与集名混用，这种情况基本搜索失败
            // 因此判断为非仅字母数字组成的文件名搜索失败时，尝试只使用部分文件名进行搜索，以空格分割
            // anime(title)->romaji/japanese; R18 anime(title + episode)->japanese + romaji/english
            string numAndLetter = @"^[A-Za-z0-9]+$";
            Regex numAndLetterRegex = new Regex(numAndLetter);                
            string onlyLetter = @"^[a-zA-Z]+$";
            Regex onlyLetterRegex = new Regex(onlyLetter);
            if (!numAndLetterRegex.IsMatch(searchName) && !onlyLetterRegex.IsMatch(searchName))
            {
                // return string before first space
                searchName = searchName.Split(' ')[0];
            }
            
            _log.LogDebug("step 7 part name ({Name})", searchName);  
            return searchName;
        }

        /// <summary>
        ///根据关键词，去除关键词及之后所有非必要部分，如：集名
        ///quick remove unnecesary info, eg:epside title
        ///TODO filter regex
        /// </summary>
        /// <param name="searchName">文件名 searchName</param>
        /// <returns></returns>
        private string quickRemoveInfo(string searchName)
        {
            string[] quickRemoveEP = {"vol", "#", "下巻", "上巻","上卷", "下卷", "EPISODE", "第1話", "第2話", "第一話", "第二話", "第一章", "第二章", "第一话", "第二话"};
            
            foreach(string c in quickRemoveEP)
            {
                Regex epsideRegex = new Regex(c);
                if (epsideRegex.Match(searchName).Success)
                {
                    searchName = Regex.Split(searchName, c, RegexOptions.IgnoreCase)[0];
                    return searchName;
                }
            }
            
            return searchName;
        }


    }
}
