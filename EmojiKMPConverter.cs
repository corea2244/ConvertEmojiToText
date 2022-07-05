//using Emoji_exercise;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;  // 크롤링 하기 위해서
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Linq.Expressions; // Dictionary 사용을 위해

namespace EmojiKMPConverter
{
    public interface ICrawler
    {
        Dictionary<byte[], string> Extract(string emojiFullUrl, string emojiFullMSUrl);
    }

    public class UnicodeOrgCrawler : ICrawler
    {
        /// <summary>
        /// Extracts the specified target URL.
        /// </summary>
        /// <param name="emojiFullUrl"></param>
        /// <param name="emojiFullMSUrl"></param>
        /// <returns></returns>
        public Dictionary<byte[], string> Extract(string emojiFullUrl, string emojiFullMSUrl)
        {
            var emojiMap = new Dictionary<byte[], string>(new ByteArrayComparer());
            var emojiMapMS = new Dictionary<byte[], string>(new ByteArrayComparer());
            var doc = OpenSite(emojiFullUrl);
            var docMS = OpenSite(emojiFullMSUrl);
            var emojiAndTextIndex = GetEmojiInfoPos(doc);
            var emojiAndTextIndexMS = GetEmojiInfoPos(docMS);
            emojiMap = ExtractData(doc, emojiAndTextIndex);
            emojiMapMS = ExtractData(docMS, emojiAndTextIndexMS);
            var emojiMergeMap = MergeDic(emojiMap, emojiMapMS);
            return emojiMergeMap;
        }

        /// <summary>
        /// Opens the site.
        /// </summary>
        /// <param name="targetUrl">The target URL.</param>
        /// <returns>HtmlDocument</returns>
        private static HtmlDocument OpenSite(string targetUrl)
        {
            var wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            var html = string.Empty;

            try
            {
                html = wc.DownloadString(targetUrl);
            }
            catch (Exception e)
            {
                Console.WriteLine("link_website : Not found Web, Exception " + e.Message);
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }

        /// <summary>
        /// Gets the emoji information position.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <returns>list[0] = emoji index, list[1] = text index</returns>
        private static List<int> GetEmojiInfoPos(HtmlDocument doc)
        {
            var emojiAndTextIndex = new List<int>();

            // table head = tr[3] : 웹 사이트 참고하여 임의로 지정
            var tableHead = doc.DocumentNode.SelectSingleNode("//table/tr[3]");
            var tableHeadName = tableHead.InnerText;

            try
            {
                // '\n'을 기준으로 문자를 나눔
                string[] tableHeadNames = null;
                try
                {
                    char[] delimiterChars = { '\n' };
                    tableHeadNames = tableHeadName.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                }
                catch (Exception e)
                {
                    Console.WriteLine("crawling() : can't split into '\n'" + e.Message);
                }

                // 이모지와 대체 텍스트 col index 값 알기 위해
                var emojiIndex = 0;
                var textIndex = 0;

                // 홈페이지 head title 과 비교
                const string emojiTitle = "BrowserAppl";
                const string textTitle = "CLDR Short Name";

                for (var i = 0; i < tableHeadNames.Length; i++)
                {
                    tableHeadNames[i] = tableHeadNames[i].Trim();
                    if (tableHeadNames[i].Equals(emojiTitle))
                    {
                        // BrowserAppl 이 구분이 되지 않음 :  index + 1
                        emojiIndex = i + 1;
                    }
                    else if (tableHeadNames[i].Equals(textTitle))
                    {
                        // CLDR Short Name 앞에 빈 열이 존재 : index + 2
                        textIndex = i + 2;
                    }
                }

                emojiAndTextIndex.Add(emojiIndex);
                emojiAndTextIndex.Add(textIndex);
            }
            catch (Exception e)
            {
                Console.WriteLine("GetEmojiInfoPos() : can't split into '\n' : " + e.Message);
            }

            return emojiAndTextIndex;
        }

        /// <summary>
        /// Finds the emoji node.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="emojiIndex">Index of the emoji.</param>
        /// <param name="rowCount">The row count.</param>
        /// <returns>emoji HtmlNode</returns>
        private static HtmlNode FindEmojiNode(HtmlDocument doc, int emojiIndex, int rowCount)
        {
            return doc.DocumentNode.SelectSingleNode("//table/tr[" + Convert.ToString(rowCount) + "]/td[" + Convert.ToString(emojiIndex) + "]");
        }

        /// <summary>
        /// Finds the text node.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="textIndex">Index of the text.</param>
        /// <param name="rowCount">The row count.</param>
        /// <returns>text HtmlNode</returns>
        private static HtmlNode FindTextNode(HtmlDocument doc, int textIndex, int rowCount)
        {
            return doc.DocumentNode.SelectSingleNode("//table/tr[" + Convert.ToString(rowCount) + "]/td[" + Convert.ToString(textIndex) + "]");
        }

        /// <summary>
        /// Merge two Map
        /// </summary>
        /// <param name="emojiMap"></param>
        /// <param name="emojiMapMS"></param>
        /// <returns> Merged Map </returns>
        private static Dictionary<byte[], string> MergeDic(Dictionary<byte[], string> emojiMap, Dictionary<byte[], string> emojiMapMS)
        {
            var emojiMergeMap = new Dictionary<byte[], string>();

            foreach (var pair in emojiMap)
            {
                emojiMergeMap.Add(pair.Key, pair.Value);
            }

            foreach (var pair in emojiMapMS)
            {
                emojiMergeMap.Add(pair.Key, pair.Value);
            }
            return emojiMergeMap;
        }

        /// <summary>
        /// Extracts the data.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="emojiAndTextIndex">Index of the emoji and text.</param>
        /// <returns>key = emoji byte[], value = text string</returns>
        private static Dictionary<byte[], string> ExtractData(HtmlDocument doc, List<int> emojiAndTextIndex)
        {
            // 맵 구성 = 키: emoji, 값: text
            var emojiByteMap = new Dictionary<byte[], string>(new ByteArrayComparer());
            var rows = doc.DocumentNode.SelectNodes("//table/tr");

            // 0번째 : emoji의 index, 1번째 : text의 index
            var emojiIndex = emojiAndTextIndex[0];
            var textIndex = emojiAndTextIndex[1];

            // emoji, text 값 추출
            try
            {
                var rowCount = 0;
                foreach (var row in rows)
                {
                    // table - tr - td 안에 emoji 와 대체 텍스트 값 존재
                    var emojiNode = FindEmojiNode(doc, emojiIndex, rowCount);
                    var textNode = FindTextNode(doc, textIndex, rowCount);
                    var text = string.Empty;
                    var emoji = string.Empty;
                    if ((textNode != null) && (emojiNode != null))
                    {
                        emoji = emojiNode.InnerText;
                        text = textNode.InnerText;

                        text = "(" + text + ")";

                        // 이모지를 byte 로 변환해서 dictionary 에 저장
                        var emojiByteBuffer = Encoding.Unicode.GetBytes(emoji);

                        // 저장하는 과정 콘솔 출력 : 필수 아님!!
                        foreach (var b in emojiByteBuffer)
                        {
                            Console.Write(b);
                            Console.Write(" ");
                        }
                        Console.WriteLine(text);

                        emojiByteMap.Add(emojiByteBuffer, text);
                    }
                    rowCount++;
                }

                Console.WriteLine("Data Save Success \n\n\n\n");

                return emojiByteMap;
            }
            catch (Exception e)
            {
                Console.WriteLine("Data Save Failed!!" + e.Message);
                return emojiByteMap;
            }
        }
    }

    public class EmojiLoad
    {
        /// <summary>
        /// Loads the specified binary file path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>key : emoji byte[], value : text string</returns>
        public Dictionary<byte[], string> LoadTo(string path)
        {
            var dic = new Dictionary<byte[], string>(new ByteArrayComparer());
            try
            {
                if (File.Exists(path))
                {
                    using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
                    {
                        // 전체 이모지 개수 + (버퍼 사이즈 + 이모지 버퍼 + 대체 텍스트 버퍼)  - () 안은 반복해서 출력 
                        var emojiCount = reader.ReadInt32();
                        for (var i = 0; i < emojiCount; i++)
                        {
                            var bufferSize = reader.ReadInt32();
                            var bufferEmoji = reader.ReadBytes(bufferSize);
                            var stringText = reader.ReadString();
                            dic.Add(bufferEmoji, stringText);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Load Data : Don't connect to binary file" + e.Message);
            }

            return dic;
        }
    }

    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.SequenceEqual(right);
        }
        public int GetHashCode(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            return key.Sum(b => b);
        }
    }

    // KMP 알고리즘 사용 : 찾은 이모지의 start_index - Key, end_index - Value 로 구성되어 있는 Dictionary 반환
    public class KMP
    {
        /// <summary>
        /// KMPs the index of the search emoji.
        /// </summary>
        /// <param name="pat">The pat.</param>
        /// <param name="txt">The text.</param>
        /// <param name="emojiIndex">Index of the emoji.</param>
        /// <returns>key : start index, value : end index</returns>
        public Dictionary<int, int> KmpSearchEmojiIndex(byte[] pat, byte[] txt, Dictionary<int, int> emojiIndex)
        {
            var M = pat.Length;
            var N = txt.Length;
            var lps = new int[M];
            var j = 0; // index for pat[]

            ComputeLPSArray(pat, M, lps);

            var i = 0;
            while (i < N)
            {
                if (pat[j] == txt[i])
                {
                    j++;
                    i++;
                }

                if (j == M)
                {
                    // dictionary 에 key 가 이미 존재하는 경우 예외 처리
                    if (!emojiIndex.ContainsKey(i - j))
                    {
                        emojiIndex.Add((i - j), (i - j + M));
                    }

                    j = lps[j - 1];
                }
                else if (i < N && pat[j] != txt[i])
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i = i + 1;
                    }
                }
            }

            return emojiIndex;
        }

        /// <summary>
        /// Computes the LPS array.
        /// </summary>
        /// <param name="pat">The pat.</param>
        /// <param name="M">The m.</param>
        /// <param name="lps">The LPS.</param>
        private static void ComputeLPSArray(byte[] pat, int M, int[] lps)
        {
            var len = 0;
            var i = 1;
            lps[0] = 0;

            while (i < M)
            {
                if (pat[i] == pat[len])
                {
                    len++;
                    lps[i] = len;
                    i++;
                }
                else
                {
                    if (len != 0)
                    {
                        len = lps[len - 1];
                    }
                    else
                    {
                        lps[i] = len;
                        i++;
                    }
                }
            }
        }
    }

    public class EmojiToText
    {
        /// <summary>
        /// Converts the emoji.
        /// </summary>
        /// <param name="emojiIndex">Index of the emoji.</param>
        /// <param name="inputBuffer">The input buffer.</param>
        /// <param name="emojiMap">The emoji map.</param>
        /// <param name="bufferSize">Size of the buffer.</param>
        /// <returns>Final output string</returns>
        public string ConvertEmoji(Dictionary<int, int> emojiIndex, byte[] inputBuffer, Dictionary<byte[], string> emojiMap, int bufferSize)
        {
            var result = string.Empty;

            try
            {
                // 0 : 맨 마지막에 이모지가 없음, 1: 맨 마지막에 이모지가 있음
                var emojiPlaceFlag = 0;

                var finalResultByteList = new List<byte>();
                var lastEndIndex = 0;

                // start_index 오름차순 정렬
                foreach (var item in emojiIndex.OrderBy(i => i.Key))
                {
                    var startIndex = item.Key;

                    // 예외처리 : 이모지 안에 다른 이모지가 포함되어 있을 경우
                    if (startIndex < lastEndIndex)
                    {
                        continue;
                    }

                    var endIndex = item.Value;
                    var emojiByteBufferSize = endIndex - startIndex;

                    // 맨 처음에 텍스트가 나올 경우
                    if (startIndex != 0)
                    {
                        // 이모지 앞에 내용 빼내기
                        var splitOrgText = new byte[startIndex - lastEndIndex];
                        Buffer.BlockCopy(inputBuffer, lastEndIndex, splitOrgText, 0, startIndex - lastEndIndex);

                        foreach (var b in splitOrgText)
                        {
                            finalResultByteList.Add(b);
                        }
                    }

                    // 이모지 부분 빼내기
                    var splitOrgEmoji = new byte[endIndex - startIndex];
                    Buffer.BlockCopy(inputBuffer, startIndex, splitOrgEmoji, 0, emojiByteBufferSize);

                    // 이모지 부분 대체 텍스트의 byte 가져오기
                    var splitChangedEmojiToText = Encoding.Unicode.GetBytes(emojiMap[splitOrgEmoji]);

                    foreach (var b in splitChangedEmojiToText)
                    {
                        finalResultByteList.Add(b);
                    }
                    lastEndIndex = endIndex;

                    // 이모지가 마지막에 있는 경우
                    if (endIndex == bufferSize)
                    {
                        emojiPlaceFlag = 1;
                    }
                }

                // 마지막에 텍스트가 있는 경우
                if (emojiPlaceFlag == 0)
                {
                    var splitOrgText = new byte[bufferSize - lastEndIndex];
                    Buffer.BlockCopy(inputBuffer, lastEndIndex, splitOrgText, 0, bufferSize - lastEndIndex);

                    foreach (var b in splitOrgText)
                    {
                        finalResultByteList.Add(b);
                    }
                }

                result = Encoding.Unicode.GetString(finalResultByteList.ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine("EmojiToText : Exception catch!!" + e.Message);
            }
            return result;
        }
    }

    public class EmojiKMPConverter
    {
        private Dictionary<byte[], string> _saveEmojiMap = new Dictionary<byte[], string>(new ByteArrayComparer());
        private Dictionary<byte[], string> _loadEmojiMap = new Dictionary<byte[], string>(new ByteArrayComparer());
        private Dictionary<byte[], string> _sortedEmojiMap = new Dictionary<byte[], string>(new ByteArrayComparer());
        private Dictionary<int, int> _emojiIndexMap = new Dictionary<int, int>();

        /// <summary>
        /// Saves the specified binary path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="emojiMapCount">The emoji map count.</param>
        /// <param name="emojiMap">The emoji map.</param>
        private static void SaveTo(string path, int emojiMapCount, Dictionary<byte[], string> emojiMap)
        {
            try
            {
                var fileName = path;
                using (var writer = new BinaryWriter(File.Open(fileName, FileMode.Create)))
                {
                    // 전체 이모지 개수 + (버퍼 사이즈 + 이모지 버퍼 + 대체 텍스트 버퍼)  - () 안은 반복해서 입력 
                    writer.Write(emojiMapCount);
                    foreach (var pair in emojiMap)
                    {
                        var bufferSize = pair.Key.Length;
                        var bufferEmoji = pair.Key;
                        var bufferText = pair.Value;
                        writer.Write(bufferSize);
                        writer.Write(bufferEmoji);
                        writer.Write(bufferText);
                    }
                }
                Console.WriteLine("Save binary file!");

                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Save file : Don't connect to binary file" + e.Message);
            }
        }

        /// <summary>
        /// Sorts the dictionary. (Sort Bytes Longer order)
        /// </summary>
        /// <param name="emojiMap">The emoji map.</param>
        /// <returns>sortedEmojiMap</returns>
        private static Dictionary<byte[], string> SortDictionary(Dictionary<byte[], string> emojiMap)
        {
            var maxLength = 0;
            foreach (var item in emojiMap)
            {
                if (item.Key.Length > maxLength)
                {
                    maxLength = item.Key.Length;
                }
            }

            var dictionaryListEmoji = new List<byte[]>();
            var dictionaryListText = new List<string>();

            for (int i = maxLength; i > 0; i -= 2)
            {
                foreach (var item in emojiMap)
                {
                    if (item.Key.Length == i)
                    {
                        dictionaryListEmoji.Add(item.Key);
                        dictionaryListText.Add(item.Value);
                    }
                }
            }

            try
            {
                var sortedEmojiTextMap = dictionaryListEmoji.Zip(dictionaryListText, (k, v) => new { k, v }).ToDictionary(a => a.k, a => a.v);
                return sortedEmojiTextMap;
            }
            catch (Exception e)
            {
                var sortedEmojiTextMap = new Dictionary<byte[], string>();
                Console.WriteLine("sort_dictionary : ArgumentNullException!" + e.Message);
                return sortedEmojiTextMap;
            }
        }

        /// <summary>
        /// Extracts the index of the emoji using KMP algorithms
        /// </summary>
        /// <param name="sortedEmojiMap">The sorted emoji map.</param>
        /// <param name="inputBuffer">The input buffer.</param>
        /// <returns>key : start index, value = end index</returns>
        private static Dictionary<int, int> ExtractEmojiIndex(Dictionary<byte[], string> sortedEmojiMap, byte[] inputBuffer)
        {
            var kmp = new KMP();
            var emojiIndex = new Dictionary<int, int>();

            // KMP 알고리즘 사용
            foreach (var item in sortedEmojiMap)
            {
                // 찾은 emoji의 start_index : Key,  end_index : Value
                emojiIndex = kmp.KmpSearchEmojiIndex(item.Key, inputBuffer, emojiIndex);
            }

            return emojiIndex;
        }

        /// <summary>
        /// Crawls the specified target URL.
        /// </summary>
        /// <param name="targetUrl">The target URL.</param>
        /// <returns>If success crawling : true, else fail crawling : false</returns>
        public bool Crawl(string emojiFullUrl, string emojiFullMSUrl)
        {
            var unicodeOrgCrawler = new UnicodeOrgCrawler();
            _saveEmojiMap = unicodeOrgCrawler.Extract(emojiFullUrl, emojiFullMSUrl);
            var saveFlag = _saveEmojiMap.Count != 0;

            return saveFlag;
        }

        /// <summary>
        /// Saves the specified binary path.
        /// </summary>
        /// <param name="binaryPath">The binary path.</param>
        public void Save(string binaryPath)
        {
            var emojiMapCount = _saveEmojiMap.Count;
            SaveTo(binaryPath, emojiMapCount, _saveEmojiMap);
        }

        /// <summary>
        /// Loads the specified binary file path.
        /// </summary>
        /// <param name="binaryFilePath">The binary file path.</param>
        public void Load(string binaryFilePath)
        {
            var emojiBinaryFileLoad = new EmojiLoad();
            _loadEmojiMap = emojiBinaryFileLoad.LoadTo(binaryFilePath);
        }

        /// <summary>
        /// Emojis the convert.
        /// </summary>
        /// <param name="inputString">The input buffer.</param>
        public void EmojiConvert(string inputString)
        {
            var inputBuffer = Encoding.Unicode.GetBytes(inputString);
            var inputBufferSize = inputBuffer.Length;
            var emojiToText = new EmojiToText();
            _sortedEmojiMap = SortDictionary(_loadEmojiMap);
            _emojiIndexMap = ExtractEmojiIndex(_sortedEmojiMap, inputBuffer);
            var result = emojiToText.ConvertEmoji(_emojiIndexMap, inputBuffer, _loadEmojiMap, inputBufferSize);

            Console.WriteLine("Final string : " + result);
            Console.ReadLine();
        }
    }
}
