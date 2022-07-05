using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;  // 크롤링 하기 위해서
using System.Net;
using System.IO;
using System.Collections.Generic;  // Dictionary 사용을 위해

namespace EmojiProcessor
{
    public interface ICrawler
    {
        Dictionary<byte[], string> Extract(string emojiFullUrl);
    }

    public class UnicodeOrgCrawler : ICrawler
    {
        /// <summary>
        /// Opens the site and gets the Html Document.
        /// </summary>
        /// <param name="targetUrl">The target URL.</param>
        /// <returns> HtmlDocument </returns>
        private HtmlDocument OpenSite(string targetUrl)
        {
            var html = DownLoadString(targetUrl);
            if (string.IsNullOrEmpty(html))
            {
                return null;
            }

            var doc = LoadHtmlDocument(html);
            if (doc == null)
            {
                Console.Write("Html Document 를 불러오지 못했습니다. - ");

                return null;
            }

            Console.WriteLine("크롤링 완료!");

            return doc;
        }

        /// <summary>
        /// Download the requested resource to string
        /// </summary>
        /// <param name="targetUrl"></param>
        /// <returns> Html string </returns>
        private string DownLoadString(string targetUrl)
        {
            var wc = new WebClient();
            wc.Encoding = Encoding.UTF8;

            try
            {
                return wc.DownloadString(targetUrl);
            }
            catch (Exception e)
            {
                Console.Write("DownLoadString() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Load Html as String Resource
        /// </summary>
        /// <param name="html"></param>
        /// <returns> Html Document </returns>
        private HtmlDocument LoadHtmlDocument(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return doc;
        }

        /// <summary>
        /// Gets the emoji information position.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <returns>list[0] = emoji index, list[1] = text index</returns>
        private List<int> GetEmojiInfoPos(HtmlDocument doc)
        {
            var tableHead = FindHeadNode(doc);
            if (tableHead == null)
            {
                Console.Write("HeadNode의 위치가 올바르지 않습니다. - ");

                return null;
            }

            var tableHeadName = tableHead.InnerText;
            var tableHeadNames = SplitHeadString(tableHeadName);
            if (tableHeadNames == null)
            {
                return null;
            }

            var indexList = SearchEmojiAndTextIndex(tableHeadNames);
            if (indexList == null)
            {
                return null;
            }

            return indexList;
        }

        /// <summary>
        /// Finds the head node.
        /// </summary>
        /// <param name="doc"></param>
        /// <returns> head HtmlNode </returns>
        private HtmlNode FindHeadNode(HtmlDocument doc)
        {
            // table head = tr[3] : 웹 사이트 참고하여 임의로 지정
            return doc.DocumentNode.SelectSingleNode("//table/tr[3]");
        }

        /// <summary>
        /// Split head string
        /// </summary>
        /// <param name="tableHeadName"></param>
        /// <returns> head string[] array </returns>
        private string[] SplitHeadString(string tableHeadName)
        {
            try
            {
                // '\n'을 기준으로 문자를 나눔
                char[] delimiterChars = { '\n' };

                return tableHeadName.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception e)
            {
                Console.Write("SplitHeadString() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Search Emoji and Text index
        /// </summary>
        /// <param name="tableHeadNames"></param>
        /// <returns> List of emoji index and text index </returns>
        private List<int> SearchEmojiAndTextIndex(string[] tableHeadNames)
        {
            var emojiAndTextIndex = new List<int>();

            // 이모지와 대체 텍스트 col index 값 알기 위해
            var emojiIndex = 0;
            var textIndex = 0;

            // 홈페이지 head title 과 비교
            const string emojiTitle = "Code";
            const string textTitle = "CLDR Short Name";

            try
            {
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
            }
            catch (Exception e)
            {
                Console.Write("SearchIndex() - 에러 : " + e.Message + " - ");

                return null;
            }

            if (emojiIndex == 0 && textIndex == 0)
            {
                Console.Write("찾으려는 Emoji Title과 Text Title이 올바르지 않습니다. - ");

                return null;
            }

            emojiAndTextIndex.Add(emojiIndex);
            emojiAndTextIndex.Add(textIndex);

            return emojiAndTextIndex;
        }

        /// <summary>
        /// Extracts the data.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="emojiAndTextIndex">Index of the emoji and text.</param>
        /// <returns> key = emoji byte[], value = text string </returns>
        private Dictionary<string, string> ExtractData(HtmlDocument doc, List<int> emojiAndTextIndex)
        {
            var emojiStringMap = new Dictionary<string, string>();
            var rows = FindRowNodes(doc);
            if (rows == null)
            {
                Console.Write("웹 사이트에 tr 태그가 없습니다. - ");

                return null;
            }

            // 0번째 : emoji의 index, 1번째 : text의 index
            var emojiIndex = emojiAndTextIndex[0];
            var textIndex = emojiAndTextIndex[1];

            var rowCount = 1;

            foreach (var row in rows)
            {
                var emojiNode = FindEmojiNode(doc, emojiIndex, rowCount);
                var textNode = FindTextNode(doc, textIndex, rowCount);

                // table - tr - td 안에 emoji 와 대체 텍스트 값 존재
                if ((textNode != null) && (emojiNode != null))
                {
                    emojiStringMap = AddEmojiMap(emojiNode, textNode, emojiStringMap);
                }
                else if ((textNode == null) && (emojiNode != null))
                {
                    // colspan 을 이용하여 text 못 찾는 에러 해결
                    var cols = FindColNode(doc, rowCount);
                    if (cols == null)
                    {
                        Console.Write("Colspan 태그를 찾을 수 없습니다. - ");

                        return null;
                    }

                    var colIndex = cols.GetAttributeValue("colspan", 0);
                    if(colIndex > 0)
                    {
                        Console.Write("Colspan 태그를 찾을 수 없습니다. - ");

                        return null;
                    }

                    textNode = FindTextNode(doc, textIndex - (colIndex - 1), rowCount);
                    if (textNode == null)
                    {
                        Console.Write("Colspan 태그가 비었습니다. - ");

                        return null;
                    }

                    emojiStringMap = AddEmojiMap(emojiNode, textNode, emojiStringMap);
                }

                rowCount++;

                if (emojiStringMap == null)
                {
                    Console.Write("이모지와 텍스트 map이 비었습니다. - ");

                    return null;
                }
            }

            Console.WriteLine("성공적으로 추출했습니다.");

            return emojiStringMap;
        }

        /// <summary>
        /// Finds the row nodes.
        /// </summary>
        /// <param name="doc"></param>
        /// <returns> row HtmlNode </returns>
        private HtmlNodeCollection FindRowNodes(HtmlDocument doc)
        {
            return doc.DocumentNode.SelectNodes("//table/tr");
        }

        /// <summary>
        /// Finds the emoji node.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="emojiIndex">Index of the emoji.</param>
        /// <param name="rowCount">The row count.</param>
        /// <returns> emoji HtmlNode </returns>
        private HtmlNode FindEmojiNode(HtmlDocument doc, int emojiIndex, int rowCount)
        {
            return doc.DocumentNode.SelectSingleNode("//table/tr[" + Convert.ToString(rowCount) + "]/td[" + Convert.ToString(emojiIndex) + "]");
        }

        /// <summary>
        /// Finds the text node.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="textIndex">Index of the text.</param>
        /// <param name="rowCount">The row count.</param>
        /// <returns> text HtmlNode </returns>
        private HtmlNode FindTextNode(HtmlDocument doc, int textIndex, int rowCount)
        {
            return doc.DocumentNode.SelectSingleNode("//table/tr[" + Convert.ToString(rowCount) + "]/td[" + Convert.ToString(textIndex) + "]");
        }

        /// <summary>
        /// Add Emoji and Text to Emoji Map
        /// </summary>
        /// <param name="emojiNode"></param>
        /// <param name="textNode"></param>
        /// <returns> Emoji and Text map </returns>
        private Dictionary<string, string> AddEmojiMap(HtmlNode emojiNode, HtmlNode textNode, Dictionary<string, string> emojiStringMap)
        {
            try
            {
                var emoji = emojiNode.InnerText;
                var text = textNode.InnerText;
                text = "(" + text + ")";
                emojiStringMap.Add(emoji, text);

                return emojiStringMap;
            }
            catch (Exception e)
            {
                Console.Write("AddEmojiMap() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Finds the col node.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="rowCount"></param>
        /// <returns> col HtmlNode </returns>
        private HtmlNode FindColNode(HtmlDocument doc, int rowCount)
        {
            return doc.DocumentNode.SelectSingleNode("//table/tr[" + Convert.ToString(rowCount) + "]/td[@colspan]");
        }

        /// <summary>
        /// Converts emoji to byte.
        /// </summary>
        /// <param name="emojiMap">The emoji map.</param>
        /// <returns> byteMap </returns>
        private Dictionary<byte[], string> ConvertEmojiToByte(Dictionary<string, string> emojiStringMap)
        {
            try
            {
                var emojiByteMap = new Dictionary<byte[], string>();

                foreach (var pair in emojiStringMap)
                {
                    var byteList = new List<byte>();
                    var strArrKey = SplitStringKey(pair);
                    if (strArrKey == null)
                    {
                        return null;
                    }

                    for (var i = 0; i < strArrKey.Length; i++)
                    {
                        strArrKey[i] = ConvertUnicodeForm(strArrKey[i]);
                        if (strArrKey[i] == null)
                        {
                            return null;
                        }

                        var out16Byte = ConvertUnicodeToByte(strArrKey[i]);
                        if (out16Byte == null)
                        {
                            return null;
                        }

                        byteList = ExchangeSequence(out16Byte, byteList);
                        if (byteList == null)
                        {
                            return null;
                        }
                    }
                    var byteArr = byteList.ToArray();
                    emojiByteMap.Add(byteArr, pair.Value);
                }

                return emojiByteMap;
            }
            catch (Exception e)
            {
                Console.Write("ConvertEmojiToByte() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Split Key String
        /// </summary>
        /// <param name="pair"></param>
        /// <returns> splited string array </returns>
        private string[] SplitStringKey(KeyValuePair<string, string> pair)
        {
            try
            {
                char[] delimiterChars = { ' ' };

                return pair.Key.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception e)
            {
                Console.Write("SplitStringKey() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Convert Unicode Point to Unicode Form
        /// </summary>
        /// <param name="strArrKey"></param>
        /// <returns> converted string </returns>
        private string ConvertUnicodeForm(string strArrKey)
        {
            try
            {
                strArrKey = strArrKey.Trim().Substring(2);

                if (strArrKey.Length == 5)
                {
                    return "000" + strArrKey;
                }
                else if (strArrKey.Length == 4)
                {
                    return strArrKey;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                Console.Write("ConvertUnicodeForm() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Convert unicode string to byte
        /// </summary>
        /// <param name="strArrKey"></param>
        /// <returns> converted byte </returns>
        private byte[] ConvertUnicodeToByte(string strArrKey)
        {
            try
            {
                var uintVal = uint.Parse(strArrKey, System.Globalization.NumberStyles.AllowHexSpecifier);
                var inByte = BitConverter.GetBytes(uintVal);
                var out32String = Encoding.UTF32.GetString(inByte);
                var out16Byte = Encoding.Unicode.GetBytes(out32String);

                return out16Byte;
            }
            catch (Exception e)
            {
                Console.Write("ConvertToUnicodeByte() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        ///  Exchange Little Endian for Big Endian
        /// </summary>
        /// <param name="inputBuffer"></param>
        /// <param name="byteList"></param>
        /// <returns> Converted byte buffer </returns>
        private List<byte> ExchangeSequence(byte[] inputBuffer, List<byte> byteList)
        {
            try
            {
                for (var i = 0; i < inputBuffer.Length; i += 2)
                {
                    byteList.Add(inputBuffer[i + 1]);
                    byteList.Add(inputBuffer[i]);
                }

                return byteList;
            }
            catch (Exception e)
            {
                Console.Write("ExchangeSequence() - 에러 : " + e.Message + " - ");

                return null;
            }
        }

        /// <summary>
        /// Extracting with Crawling Data Map
        /// </summary>
        /// <param name="emojiFullUrl"></param>
        /// <returns> Crawling Data Map </returns>
        public Dictionary<byte[], string> Extract(string targetUrl)
        {
            var doc = OpenSite(targetUrl);
            if (doc == null)
            {
                Console.WriteLine("사이트 연결 실패.");

                return null;
            }

            var emojiAndTextIndex = GetEmojiInfoPos(doc);
            if (emojiAndTextIndex == null)
            {
                Console.WriteLine("(크롤링)이모지와 대체 텍스트의 index 검색 불가.");

                return null;
            }

            var emojiStringMap = ExtractData(doc, emojiAndTextIndex);
            if (emojiStringMap == null)
            {
                Console.WriteLine("(크롤링)이모지와 대체 텍스트 검색 불가.");

                return null;
            }

            var emojiByteMap = ConvertEmojiToByte(emojiStringMap);
            if(emojiByteMap == null)
            {
                Console.WriteLine("(크롤링)String 이모지를 byte로 변환 실패.");

                return null;
            }

            // 데이터 보여주는 부분(필수 아님)
            try
            {
                var emojiCount = 0;

                foreach (var pair in emojiByteMap)
                {
                    emojiCount++;
                    var result = BitConverter.ToString(pair.Key);

                    // 크롤링 과정 출력(필수 x)
                    Console.WriteLine("{0} {1} : {2}", emojiCount, result, pair.Value);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Extract() - 에러 : " + e.Message);
            }

            return emojiByteMap;
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

    public class EmojiRegexConverter
    {
        // 일반 글자를 찾았으면 1, 이모지 찾았으면 2, 결합중이면 3
        private enum RegexCase
        {
            NormalCase = 1,
            EmojiCase = 2,
            LinkCase = 3
        }
        private RegexCase _emojing = RegexCase.NormalCase;
        private int _countOfNotFound = 0;

        /// <summary>
        /// Replace Little Endian with Big Endian
        /// </summary>
        /// <param name="inputBufferSize"></param>
        /// <param name="inputBuffer"></param>
        private bool ExchangeSequence(byte[] inputBuffer)
        {
            try
            {
                for (var i = 0; i < inputBuffer.Length - 1; i += 2)
                {
                    var temp = inputBuffer[i];
                    inputBuffer[i] = inputBuffer[i + 1];
                    inputBuffer[i + 1] = temp;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.Write("ExchangeSequence() - 에러 : " + e.Message + " - ");

                return false;
            }
        }

        /// <summary>
        /// Exchange Little Endian for Big Endian
        /// </summary>
        /// <param name="inputBuffer"></param>
        /// <param name="endIndex"></param>
        /// <param name="startIndex"></param>
        private List<byte> ExchangeSequence(byte[] inputBuffer, int inputBufferSize, int endIndex, int startIndex, List<byte> _changedResult)
        {
            for (var i = endIndex + 1; i < startIndex; i += 2)
            {
                if (i >= inputBufferSize - 1)
                {
                    return null;
                }

                _changedResult.Add(inputBuffer[i + 1]);
                _changedResult.Add(inputBuffer[i]);
            }

            return _changedResult;
        }

        /// <summary>
        /// Exchange Little Endian for Big Endian
        /// </summary>
        /// <param name="inputBuffer"></param>
        /// <param name="endIndex"></param>
        private List<byte> ExchangeSequence(byte[] inputBuffer, int inputBufferSize, int endIndex, List<byte> _changedResult)
        {
            return ExchangeSequence(inputBuffer, inputBufferSize, endIndex, inputBufferSize, _changedResult);
        }

        /// <summary>
        /// A function that finds the starting index of an emoji
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <returns> array index i </returns>
        private int ProcessNormalText(int i, byte[] inputBuffer, int inputBufferSize, List<int> startIndexList, List<int> endIndexList)
        {
            // 00 일 경우
            if (IsEmojiStartZero(inputBuffer[i]))
            {
                i = ProcessZeroInitUnicode(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
            }
            // 20~30 일 경우
            else if (IsEmojiStartTwo(inputBuffer[i]))
            {
                i = ProcessTwoInitUnicode(i, inputBuffer, startIndexList, endIndexList);
            }
            // 32~33 일 경우
            else if (IsEmojiStartThree(inputBuffer[i]))
            {
                i = ProcessThreeInitUnicode(i, inputBuffer, startIndexList, endIndexList);
            }
            // D8 일 경우
            else if (IsEmojiStartD8(inputBuffer[i], i, inputBufferSize))
            {
                i = ProcessD8InitUnicode(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
            }
            // 일반 글자인 경우
            else
            {
                i ++;
            }

            return i;
        }

        /// <summary>
        /// Case of start 00
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiStartZero(byte code)
        {
            // 이모지가 0x00(=0)으로 시작하는 경우
            return code == 0;
        }

        /// <summary>
        /// Case of Unicode starting with 00
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <param name="inputBufferSize"></param>
        /// <param name="startIndexList"></param>
        /// <param name="endIndexList"></param>
        /// <returns> array index i </returns>
        private int ProcessZeroInitUnicode(int i, byte[] inputBuffer, int inputBufferSize, List<int> startIndexList, List<int> endIndexList)
        {
            if (IsEmojiStartCopyrightCode(inputBuffer[i + 1])) // A9 or AE 일 경우
            {
                if (_emojing != RegexCase.LinkCase)
                {
                    if (_emojing == RegexCase.EmojiCase)
                    {
                        endIndexList.Add(i - 1);
                    }

                    startIndexList.Add(i);
                }

                _emojing = RegexCase.EmojiCase;
                i ++;
            }
            else if (i < inputBufferSize - 5 && IsEmojiKeyCapCode(inputBuffer[i + 1], inputBuffer[i + 2]))
            {
                if (_emojing != RegexCase.LinkCase)
                {
                    if (_emojing == RegexCase.EmojiCase)
                    {
                        endIndexList.Add(i - 1);
                    }

                    startIndexList.Add(i);
                }

                endIndexList.Add(i + 5);
                _emojing = RegexCase.NormalCase;
                i += 5;
            }
            else
            {
                if (_emojing == RegexCase.EmojiCase)
                {
                    endIndexList.Add(i - 1);
                    _emojing = RegexCase.NormalCase;
                }

                i ++;
            }

            return i;
        }

        /// <summary>
        /// For special cases such as copyright.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiStartCopyrightCode(byte code)
        {
            // 이모지는 0xA9(=169)나 0xAE(=174)로 시작하는 경우이다.
            return code == 169 || code == 174;
        }

        /// <summary>
        /// For special cases such as keycap.
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <returns></returns>
        private bool IsEmojiKeyCapCode(byte firstCode, byte secondCode)
        {
            // 이모지는 0x23(=35)이거나 0x2A(=42)이거나 0x30(=48) ~ 0x39(=57) 으로 시작하고 0xFE(=254) 로 이어질 경우이다. 
            return (firstCode == 35 || firstCode == 42 || (firstCode >= 48 && firstCode <= 57)) && secondCode == 254;
        }

        /// <summary>
        /// Case of start 20 ~ 30
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiStartTwo(byte code)
        {
            // 이모지가 0x20(=32)이상이고 0x30(48)이하로 시작하는 경우
            return code >= 32 && code <= 48;
        }

        /// <summary>
        /// Case of Unicode starting with 20
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <param name="startIndexList"></param>
        /// <param name="endIndexList"></param>
        /// <returns> array index i </returns>
        private int ProcessTwoInitUnicode(int i, byte[] inputBuffer, List<int> startIndexList, List<int> endIndexList)
        {
            if (IsEmojiStartTwoInitCode(inputBuffer[i], inputBuffer[i + 1]))
            {
                if (_emojing != RegexCase.LinkCase)
                {
                    if (_emojing == RegexCase.EmojiCase)
                    {
                        endIndexList.Add(i - 1);
                    }

                    startIndexList.Add(i);
                }

                _emojing = RegexCase.EmojiCase;
                i ++;
            }
            else
            {
                if (_emojing == RegexCase.EmojiCase)
                {
                    endIndexList.Add(i - 1);
                    _emojing = RegexCase.NormalCase;
                }

                i ++;
            }

            return i;
        }

        /// <summary>
        /// Pick an emoji between 20 and 30.
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <returns></returns>
        private bool IsEmojiStartTwoInitCode(byte firstCode, byte secondCode)
        {
            // 이모지는 0x20(=32)로 시작하고 0x0E(=14)이상으로 끝나거나, 0x21(=33) ~ 0x2F(=47)로 시작하거나, 0x30(=48)로 시작하고 0x39(=57)이하로 끝나는 경우이다.
            return (firstCode == 32 && secondCode >= 14) || (firstCode >= 33 && firstCode <= 47) || (firstCode == 48 && secondCode <= 57);
        }

        /// <summary>
        /// Case of start 32 ~ 33
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiStartThree(byte code)
        {
            // 이모지가 0x32(=50)이상이고 0x33(=51)이하로 시작하는 경우
            return code >= 50 && code <= 51;
        }

        /// <summary>
        /// Case of Unicode starting with 30
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <param name="startIndexList"></param>
        /// <param name="endIndexList"></param>
        /// <returns> array index i </returns>
        private int ProcessThreeInitUnicode(int i, byte[] inputBuffer, List<int> startIndexList, List<int> endIndexList)
        {
            if (IsEmojiStartThreeInitCode(inputBuffer[i], inputBuffer[i + 1]))
            {
                if (_emojing != RegexCase.LinkCase)
                {
                    if (_emojing == RegexCase.EmojiCase)
                    {
                        endIndexList.Add(i - 1);
                    }

                    startIndexList.Add(i);
                }

                _emojing = RegexCase.EmojiCase;
                i ++;
            }
            else
            {
                if (_emojing == RegexCase.EmojiCase)
                {
                    endIndexList.Add(i - 1);
                    _emojing = RegexCase.NormalCase;
                }

                i ++;
            }

            return i;
        }

        /// <summary>
        /// Pick an emoji between 32 and 33
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <returns></returns>
        private bool IsEmojiStartThreeInitCode(byte firstCode, byte secondCode)
        {
            // 이모지는 0x32(=50) 로 시작하거나 0x33(=51)로 시작하고 0x00(=0)으로 끝난다.
            return firstCode == 50 || (firstCode == 51 && secondCode == 0);
        }

        /// <summary>
        /// Not emoji if it doesn't start with D8.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="i"></param>
        /// <param name="inputBufferSize"></param>
        /// <returns></returns>
        private bool IsEmojiStartD8(byte code, int i, int inputBufferSize)
        {
            // 이모지가 0xD8(=216)로 시작하는 경우
            return i < inputBufferSize - 3 && code == 216;
        }

        /// <summary>
        /// Case of Unicode starting with 1F(D8)
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <param name="inputBufferSize"></param>
        /// <param name="startIndexList"></param>
        /// <param name="endIndexList"></param>
        /// <returns> array index i </returns>
        private int ProcessD8InitUnicode(int i, byte[] inputBuffer, int inputBufferSize, List<int> startIndexList, List<int> endIndexList)
        {
            if (IsEmojiStart3cCode(inputBuffer[i + 1]))
            {
                if (IsEmojiStartRiCode(inputBuffer[i + 2], inputBuffer[i + 3]))
                {
                    // This is RI Emoji - 특수한 경우 : 2개만 나오면 끝
                    if (i < inputBufferSize - 7 && IsEmojiEndRiCode(inputBuffer[i + 4], inputBuffer[i + 5], inputBuffer[i + 6], inputBuffer[i + 7], i, inputBufferSize))
                    {
                        if (_emojing != RegexCase.LinkCase)
                        {
                            if (_emojing == RegexCase.EmojiCase)
                            {
                                endIndexList.Add(i - 1);
                            }

                            startIndexList.Add(i);
                        }

                        endIndexList.Add(i + 7);
                        _emojing = RegexCase.NormalCase;
                        i += 7;
                    }
                    else
                    {
                        if (_emojing == RegexCase.EmojiCase)
                        {
                            endIndexList.Add(i - 1);
                            _emojing = RegexCase.NormalCase;
                        }

                        i ++;
                    }
                }
                else if (IsEmojiStartModifiedCode(inputBuffer[i + 2], _emojing))
                {
                    if (IsEmojiEndModifiedCode(inputBuffer[i + 3]))
                    {
                        i += 3;
                    }
                    else
                    {
                        endIndexList.Add(i - 1);
                        _emojing = RegexCase.NormalCase;
                        i++;
                    }
                }
                else
                {
                    if (_emojing != RegexCase.LinkCase)
                    {
                        if (_emojing == RegexCase.EmojiCase)
                        {
                            endIndexList.Add(i - 1);
                        }

                        startIndexList.Add(i);
                    }

                    _emojing = RegexCase.EmojiCase;
                    i += 3;
                }
            }
            else if (IsEmojiStart3d3eCode(inputBuffer[i + 1]))
            {
                if (_emojing != RegexCase.LinkCase)
                {
                    if (_emojing == RegexCase.EmojiCase)
                    {
                        endIndexList.Add(i - 1);
                    }

                    startIndexList.Add(i);
                }

                _emojing = RegexCase.EmojiCase;
                i += 3;
            }
            else
            {
                if (_emojing == RegexCase.EmojiCase)
                {
                    endIndexList.Add(i - 1);
                    _emojing = RegexCase.NormalCase;
                }

                i ++;
            }

            return i;
        }

        /// <summary>
        /// If it starts with 3C, it filters out special cases.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiStart3cCode(byte code)
        {
            // 이모지는 0x3C(=60)로 시작한다.
            return code == 60;
        }

        /// <summary>
        /// Case of RI Emoji
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <returns></returns>
        private bool IsEmojiStartRiCode(byte firstCode, byte secondCode)
        {
            // RI 이모지(깃발 표시)는 0xDD(=221) 0xE6(=230) ~ 0xDD(=221) 0xFF(=255) 값을 갖는다.
            return firstCode == 221 && (secondCode >= 230 && secondCode <= 255);
        }

        /// <summary>
        /// Check for consecutive RI.
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <param name="thirdCode"></param>
        /// <param name="fourthCode"></param>
        /// <param name="i"></param>
        /// <param name="inputBufferSize"></param>
        /// <returns></returns>
        private bool IsEmojiEndRiCode(byte firstCode, byte secondCode, byte thirdCode, byte fourthCode, int i, int inputBufferSize)
        {
            // RI 이모지(깃발 표시)는 0xD8(=216) 0x3C(-60) 0xDD(=221) 0xE6(=230) ~ 0xD8(=216) 0x3C(-60) 0xDD(=221) 0xFF(=255) 값을 갖는다.
            return i < inputBufferSize - 7 && (firstCode == 216 && secondCode == 60 && thirdCode == 221 && fourthCode >= 230 && fourthCode <= 255);
        }

        /// <summary>
        /// Start Modified Emoji
        /// </summary>
        /// <param name="code"></param>
        /// <param name="emojing"></param>
        /// <returns></returns>
        private bool IsEmojiStartModifiedCode(byte code, RegexCase emojing)
        {
            // Modified Emoji(앞의 이모지에 색 추가)는 0xDF(=223)로 시작하고 '이모지 검색중(= emojing : 2)' 상태여야 한다.
            return code == 223 && emojing == RegexCase.EmojiCase;
        }

        /// <summary>
        /// Check Modified Emoji Cognition.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiEndModifiedCode(byte code)
        {
            // Modified Emoji(앞에 이모지에 색 추가)는 0xFB(=251) ~ 0xFF(=255) 값을 갖는다.
            return code >= 251 && code <= 255;
        }

        /// <summary>
        /// If you start with 3D or 3E, it's definitely an emoji.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private bool IsEmojiStart3d3eCode(byte code)
        {
            // 이모지는 0x3D(=61) 이거나 0x3E(=62)로 시작한다.
            return code == 61 || code == 62;
        }

        /// <summary>
        /// A function that finds the start and end index of an emoji
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <returns> array index i </returns>
        private int ProcessEmojiBeginOrEnd(int i, byte[] inputBuffer, int inputBufferSize, List<int> startIndexList, List<int> endIndexList)
        {
            // 00 일 경우
            if (IsEmojiStartZero(inputBuffer[i]))
            {
                i = ProcessZeroInitUnicode(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
            }
            // 200D 일 경우
            else if (IsEmojiLink(inputBuffer[i], inputBuffer[i + 1]))
            {
                _emojing = RegexCase.LinkCase;
                i ++;
            }
            // 20~30 일 경우
            else if (IsEmojiStartTwo(inputBuffer[i]))
            {
                i = ProcessTwoInitUnicode(i, inputBuffer, startIndexList, endIndexList);
            }
            // 32~33 일 경우
            else if (IsEmojiStartThree(inputBuffer[i]))
            {
                i = ProcessThreeInitUnicode(i, inputBuffer, startIndexList, endIndexList);
            }
            // D8 일 경우
            else if (IsEmojiStartD8(inputBuffer[i], i, inputBufferSize))
            {
                i = ProcessD8InitUnicode(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
            }
            // DB 일 경우
            else if (IsEmojiStartDB(inputBuffer[i], inputBuffer[i + 1], inputBuffer[i + 2], i, inputBufferSize))
            {
                if (IsEmojiMiddleDB(inputBuffer[i + 3], i, inputBufferSize))
                {
                    i += 3;
                }
                else if (IsEmojiEndDB(inputBuffer[i + 3], i, inputBufferSize))
                {
                    endIndexList.Add(i + 3);
                    i += 3;
                    _emojing = RegexCase.NormalCase;
                }
                else
                {
                    endIndexList.Add(i - 1);
                    _emojing = RegexCase.NormalCase;
                    i ++;
                }
            }
            // FE0F 일 경우
            else if (IsEmojiVSCode(inputBuffer[i], inputBuffer[i + 1]))
            {
                i ++;
            }
            // 일반 글자 일 경우
            else
            {
                endIndexList.Add(i - 1);
                _emojing = RegexCase.NormalCase;
                i ++;
            }

            return i;
        }

        /// <summary>
        /// If emoji is 200D.
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <returns></returns>
        private bool IsEmojiLink(byte firstCode, byte secondCode)
        {
            // 이모지가 0x20(=32)으로 시작해서 0x0D(=13)으로 끝나는 경우
            return firstCode == 32 && secondCode == 13;
        }

        /// <summary>
        /// To filter out flags when starting with DB.
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <param name="thirdCode"></param>
        /// <param name="i"></param>
        /// <param name="inputBufferSize"></param>
        /// <returns></returns>
        private bool IsEmojiStartDB(byte firstCode, byte secondCode, byte thirdCode, int i, int inputBufferSize)
        {
            // 이모지가 0xDB(=219) 0x40(=64) 0xDC(=220)로 시작하는 경우
            return firstCode == 219 && (i < inputBufferSize - 2 && (secondCode == 64 && thirdCode == 220));
        }

        /// <summary>
        /// The part that connects to the flag.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="i"></param>
        /// <param name="inputBufferSize"></param>
        /// <returns></returns>
        private bool IsEmojiMiddleDB(byte code, int i, int inputBufferSize)
        {
            // 이모지가 0x20(=32) ~ 0x7E(=126) 으로 끝나는 경우
            return i < inputBufferSize - 3 && (code >= 32 && code <= 126);
        }

        /// <summary>
        /// Ending part when flag.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="i"></param>
        /// <param name="inputBufferSize"></param>
        /// <returns></returns>
        private bool IsEmojiEndDB(byte code, int i, int inputBufferSize)
        {
            // 이모지가 0x7F(=127) 으로 끝나는 경우
            return i < inputBufferSize - 3 && (code == 127);
        }

        /// <summary>
        /// Case of FE0F 
        /// </summary>
        /// <param name="firstCode"></param>
        /// <param name="secondCode"></param>
        /// <returns></returns>
        private bool IsEmojiVSCode(byte firstCode, byte secondCode)
        {
            // 이모지가 0xFE(=254) 0x0F(=15) 로 시작하는 경우
            return firstCode == 254 && secondCode == 15;
        }

        /// <summary>
        /// Connecting emoji to emoji
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inputBuffer"></param>
        /// <returns> array index i </returns>
        private int ProcessLinkEmoji(int i, byte[] inputBuffer, int inputBufferSize, List<int> startIndexList, List<int> endIndexList)
        {
            // 00 일 경우
            if (IsEmojiStartZero(inputBuffer[i]))
            {
                i = ProcessZeroInitUnicode(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
            }
            // 20~30 일 경우
            else if (IsEmojiStartTwo(inputBuffer[i]))
            {
                i = ProcessTwoInitUnicode(i, inputBuffer, startIndexList, endIndexList);
            }
            // 32~33 일 경우
            else if (IsEmojiStartThree(inputBuffer[i]))
            {
                i = ProcessThreeInitUnicode(i, inputBuffer, startIndexList, endIndexList);
            }
            // D8 일 경우
            else if (IsEmojiStartD8(inputBuffer[i], i, inputBufferSize))
            {
                i = ProcessD8InitUnicode(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
            }

            return i;
        }

        /// <summary>
        /// Check Map for keys
        /// </summary>
        /// <param name="loadEmojiMap"></param>
        /// <param name="emojiByte"></param>
        /// <returns> textBuffer(Value) </returns>
        private byte[] SearchEmoji(Dictionary<byte[], string> loadEmojiMap, byte[] emojiByte)
        {
            try
            {
                if (loadEmojiMap.ContainsKey(emojiByte))
                {
                    var text = loadEmojiMap[emojiByte];
                    var textBuffer = Encoding.Unicode.GetBytes(text);

                    return textBuffer;
                }
                else
                {
                    var text = "(NO)";
                    var textBuffer = Encoding.Unicode.GetBytes(text);
                    _countOfNotFound++;
                    Console.WriteLine("{0} : 이모지를 찾지 못했습니다!", _countOfNotFound);

                    return textBuffer;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("SearchEmoji() - 에러 : " + e.Message);

                return null;
            }
        }

        /// <summary>
        /// Extract the start index and end index of emoji
        /// </summary>
        /// <param name="inputBuffer"></param>
        /// <param name="inputBufferSize"></param>
        public Dictionary<int, int> FindIndex(byte[] inputBuffer, int inputBufferSize)
        {
            var startIndexList = new List<int>();
            var endIndexList = new List<int>();
            var emojiIndexMap = new Dictionary<int, int>();
            ExchangeSequence(inputBuffer);

            for (var i = 0; i < inputBufferSize; i++)
            {
                if (i < inputBufferSize - 1)
                {
                    // 일반 글자 검색 중
                    if (_emojing == RegexCase.NormalCase)
                    {
                        i = ProcessNormalText(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
                    }
                    // 이모지 검색 중
                    else if (_emojing == RegexCase.EmojiCase)
                    {
                        i = ProcessEmojiBeginOrEnd(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
                    }
                    // 결합자 사용중
                    else if (_emojing == RegexCase.LinkCase)
                    {
                        i = ProcessLinkEmoji(i, inputBuffer, inputBufferSize, startIndexList, endIndexList);
                    }
                }
            }

            if (_emojing == RegexCase.EmojiCase)
            {
                endIndexList.Add(inputBufferSize - 1);
            }

            _emojing = RegexCase.NormalCase;

            if (startIndexList.Count != endIndexList.Count)
            {
                Console.WriteLine("FindIndex() : startindex와 endindex의 짝이 맞지 않습니다.");

                return null;
            }

            try
            {
                for (var i = 0; i < startIndexList.Count; i++)
                {
                    emojiIndexMap.Add(startIndexList[i], endIndexList[i]);
                }

                return emojiIndexMap;
            }
            catch (Exception e)
            {
                Console.WriteLine("FindIndex() : " + e.Message);

                return null;
            }
        }

        /// <summary>
        /// Exchange emoji to alternate text
        /// </summary>
        /// <param name="loadEmojiMap"></param>
        /// <param name="inputBuffer"></param>
        public bool ExchangeText(Dictionary<byte[], string> loadEmojiMap, byte[] inputBuffer, int inputBufferSize, Dictionary<int, int> emojiIndexMap)
        {
            try
            {
                var _changedResult = new List<byte>();
                var startIndex = 0;
                var endIndex = 0;
                var maxvalue = 0;

                if (emojiIndexMap == null)
                {
                    Console.WriteLine("ExchangeText() : 입력 문자열에서 이모지를 찾지 못했습니다.");

                    return false;
                }

                foreach (var pair in emojiIndexMap)
                {
                    startIndex = pair.Key;
                    // case 1 : 맨 앞에 이모지가 없을 경우
                    if (startIndex - endIndex > 0)
                    {
                        if (ExchangeSequence(inputBuffer, inputBufferSize, endIndex, startIndex, _changedResult) == null)
                        {
                            Console.WriteLine("Little endian 을 Big endian으로 바꾸기 실패.");

                            return false;
                        }
                    }
                    // 이모지를 찾을 경우
                    endIndex = pair.Value;
                    var emojiByte = new byte[endIndex - startIndex + 1];
                    Array.Copy(inputBuffer, startIndex, emojiByte, 0, endIndex - startIndex + 1);

                    var textByteArr = SearchEmoji(loadEmojiMap, emojiByte);
                    if (textByteArr == null)
                    {
                        Console.WriteLine("Text를 byte로 바꾸기 실패.");

                        return false;
                    }

                    foreach (var b in textByteArr)
                    {
                        _changedResult.Add(b);
                    }

                    if (endIndex >= maxvalue)
                    {
                        maxvalue = endIndex;
                    }
                }
                // case 2 : 맨 마지막이 이모지가 아닐 경우
                if (maxvalue <= inputBuffer.Length - 1)
                {
                    if (ExchangeSequence(inputBuffer, inputBufferSize, endIndex, _changedResult) == null)
                    {
                        Console.WriteLine("Little endian 을 Big endian으로 바꾸기 실패.");

                        return false;
                    }
                }
                var changedResultBuffer = _changedResult.ToArray();
                var changedString = Encoding.Unicode.GetString(changedResultBuffer);
                Console.WriteLine(changedString);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("ExchangeText() : " + e.Message);

                return false;
            }
        }
    }

    public class EmojiProcessor
    {
        private Dictionary<byte[], string> _emojiMap = new Dictionary<byte[], string>(new ByteArrayComparer());

        /// <summary>
        /// merge function for combining regular and Modified emojis
        /// </summary>
        /// <param name="emojiByteMap"></param>
        /// <param name="emojiMsByteMap"></param>
        /// <returns> Merged Bytes Array </returns>
        private Dictionary<byte[], string> MergeDic(Dictionary<byte[], string> emojiByteMap, Dictionary<byte[], string> emojiMsByteMap)
        {
            var emojiByteMergeMap = new Dictionary<byte[], string>(new ByteArrayComparer());

            try
            {
                var fullCount = 0;
                var fullMsCount = 0;

                foreach (var pair in emojiByteMap)
                {
                    emojiByteMergeMap.Add(pair.Key, pair.Value);
                    fullCount++;
                }

                foreach (var pair in emojiMsByteMap)
                {
                    emojiByteMergeMap.Add(pair.Key, pair.Value);
                    fullMsCount++;
                }

                var sumCount = fullCount + fullMsCount;
                Console.WriteLine("FullCount : {0}", fullCount);
                Console.WriteLine("FullMsCount : {0}", fullMsCount);
                Console.WriteLine("Total Count : {0}", sumCount);
                Console.WriteLine("In dictionary count : {0}", emojiByteMergeMap.Count);

                return emojiByteMergeMap;
            }
            catch (Exception e)
            {
                Console.WriteLine("MergeDic() - 에러 : " + e.Message);

                return null;
            }
        }

        /// <summary>
        /// Saves the specified binary path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="emojiMapCount"></param>
        /// <param name="emojiMap"></param>
        /// <returns> Return successful save </returns>
        private bool SerializeMap(string path, int emojiMapCount, Dictionary<byte[], string> emojiMap)
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

                Console.WriteLine("저장 성공!");

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("SerializeMap() - 에러 : " + e.Message);

                return false;
            }
        }

        /// <summary>
        /// Import Stored Data
        /// </summary>
        /// <param name="path"></param>
        /// <returns> Return Load Successful </returns>
        private bool DeserializeMap(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("파일이 존재하지 않습니다.");

                return false;
            }

            try
            {
                using (var reader = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    // 전체 이모지 개수 + (버퍼 사이즈 + 이모지 버퍼 + 대체 텍스트 버퍼)  - () 안은 반복해서 출력 
                    var emojiCount = reader.ReadInt32();

                    for (var i = 0; i < emojiCount; i++)
                    {
                        var bufferSize = reader.ReadInt32();
                        var bufferEmoji = reader.ReadBytes(bufferSize);
                        var stringText = reader.ReadString();

                        if (_emojiMap.ContainsKey(bufferEmoji) == false)
                        {
                            _emojiMap.Add(bufferEmoji, stringText);
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("DeserializeMap() - 에러 : " + e.Message);

                return false;
            }
        }

        /// <summary>
        /// Crawl the specified emoji site.
        /// </summary>
        /// <param name="emojiFullUrl"></param>
        /// <param name="emojiFullMsUrl"></param>
        /// <returns> bool : crawling successful </returns>
        public bool Crawl(string emojiFullUrl, string emojiFullMsUrl)
        {
            var unicodeOrgCrawler = new UnicodeOrgCrawler();
            var fullEmojiMap = unicodeOrgCrawler.Extract(emojiFullUrl);
            var fullMsEmojiMap = unicodeOrgCrawler.Extract(emojiFullMsUrl);

            if (fullEmojiMap == null || fullMsEmojiMap == null)
            {
                return false;
            }

            _emojiMap = MergeDic(fullEmojiMap, fullMsEmojiMap);

            if (_emojiMap == null)
            {
                Console.WriteLine("Merge 된 emoji map이 비었습니다.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Saves the specified binary path.
        /// </summary>
        /// <param name="binaryPath">The binary path.</param>
        public bool Save(string binaryPath)
        {
            var emojiMapCount = _emojiMap.Count;

            return SerializeMap(binaryPath, emojiMapCount, _emojiMap);
        }

        /// <summary>
        /// Loads the specified binary file path.
        /// </summary>
        /// <param name="binaryFilePath">The binary file path.</param>
        public bool Load(string binaryFilePath)
        {
            return DeserializeMap(binaryFilePath);
        }

        /// <summary>
        /// Emojis the convert.
        /// </summary>
        /// <param name="inputString">The input buffer.</param>
        public bool ConvertEmoji(string inputString)
        {
            try
            {
                var inputBuffer = Encoding.Unicode.GetBytes(inputString);
                var inputBufferSize = inputBuffer.Length;
                var emojiRegexConverter = new EmojiRegexConverter();
                var emojiIndexMap = emojiRegexConverter.FindIndex(inputBuffer, inputBufferSize);

                if (emojiIndexMap == null)
                {
                    Console.Write("Emoji index map 이 비었습니다. - ");

                    return false;
                }

                return emojiRegexConverter.ExchangeText(_emojiMap, inputBuffer, inputBufferSize, emojiIndexMap);
            }
            catch (Exception e)
            {
                Console.Write("ConvertEmoji() - 에러 : " + e.Message + " - ");

                return false;
            }
        }
    }
}
