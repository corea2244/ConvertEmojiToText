using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;  // 크롤링 하기 위해서
using System.Net;
using System.IO;
using System.Collections.Generic;  // Dictionary 사용을 위해 
using System.Text.RegularExpressions;
using EmojiProcessor;

namespace ConvertEmoji
{
    public class Program
    {
        static void Main(string[] args)
        {
            // keyMode = 1 : 크롤링&데이터저장 (Use regex)
            // keyMode = 2 : 데이터로드&이모지변환 (Use regex)
            // keyMode = 3 : 크롤링&데이터저장 (Use KMP)
            // keyMode = 4 : 데이터로드&이모지변환 (Use KMP)
            const int keyMode = 2;

            // 원하는 입력 집어넣기 (inputPath : inputString1.txt , inputString2.txt , inputString3.txt)
            var inputString = ReadFileText("inputString1.txt");

            // 크롤링 타켓 주소 ( emojiFullUrl : Modified 를 제외한 전체 이모지, emojiFullMsUrl : Modified 이모지)
            const string emojiFullUrl = "https://unicode.org/emoji/charts/full-emoji-list.html";
            const string emojiFullMsUrl = "https://unicode.org/emoji/charts/full-emoji-modifiers.html";

            // 바이너리 데이터 저장 경로
            var binaryFilePath = string.Empty;
            if (keyMode == 1 || keyMode == 2)
            {
                binaryFilePath = "./emojiRegexData.dll";
            }
            else if (keyMode == 3 || keyMode == 4)
            {
                binaryFilePath = "./emojiKMPData.dll";
            }
            else
            {
                Console.WriteLine("올바른 keyMode를 선택하세요.");
            }

            // EmojiConverter 객체 생성
            var emojiProcessor = new EmojiProcessor.EmojiProcessor();
            var emojiKMPConverter = new EmojiKMPConverter.EmojiKMPConverter();

            // EmojiRegexConverter
            if (keyMode == 1)
            {
                // 초기 한 번만 실행 : 크롤링 & 데이터 저장
                if (emojiProcessor.Crawl(emojiFullUrl, emojiFullMsUrl))
                {
                    Console.WriteLine("크롤링 성공\n");
                    if (!emojiProcessor.Save(binaryFilePath))
                    {
                        Console.WriteLine("\n프로그램을 종료합니다.");
                    }
                }
                else
                {
                    Console.WriteLine("크롤링 실패");
                    Console.WriteLine("프로그램을 종료합니다.");
                }
            }
            else if (keyMode == 2)
            {
                // 데이터 로드
                if (emojiProcessor.Load(binaryFilePath))
                {
                    Console.WriteLine("데이터 불러오기 완료!\n");
                    if (!emojiProcessor.ConvertEmoji(inputString))
                    {
                        Console.WriteLine("\n이모지 변환 실패!");
                    }
                    else
                    {
                        Console.WriteLine("\n이모지 변환 성공!");
                    }
                }
                else
                {
                    Console.WriteLine("데이터 불러오기 실패!");
                }
            }
            else if (keyMode == 3)
            {
                // 초기 한 번만 실행 : 크롤링 & 데이터 저장
                if (emojiKMPConverter.Crawl(emojiFullUrl, emojiFullMsUrl))
                {
                    emojiKMPConverter.Save(binaryFilePath);
                }
                else
                {
                    Console.WriteLine("Fail Crawling!");
                }
                Console.ReadLine();
            }
            else if (keyMode == 4)
            {
                // 데이터 로드
                emojiKMPConverter.Load(binaryFilePath);

                // Convert Emoji to Text
                //for (var i = 0; i < 10000; i++)
                //{
                emojiKMPConverter.EmojiConvert(inputString);
                //}
                Console.ReadLine();
            }

            Console.ReadLine();
        }

        /// <summary>
        /// Read input text file
        /// </summary>
        /// <param name="path"></param>
        /// <returns> Input string </returns>
        static string ReadFileText(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Console.WriteLine("파일 내용을 확인할 수 없습니다. 에러: " + e.Message);

                return string.Empty;
            }            
        }
    }
}
