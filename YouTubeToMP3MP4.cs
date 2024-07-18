// forked from leandro-almeida/Main.cs

using MediaToolkit;
using MediaToolkit.Model;
using VideoLibrary;
using System.Text.RegularExpressions;
using System.Drawing;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace GttrcrGist
{
    public class YouTubeToMP3MP4
    {
        enum Type
        {
            None,
            Video,
            Playlist
        }

        public static void Run(string[] args)
        {
            string exportPath = "./";
            Console.Write("Video or playlist URL: ");
            string? url = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(url))
            {
                Console.WriteLine("Invalid URL. Exiting...");
                return;
            }

            if (!url.ToLower().Contains("youtube.com"))
            {
                Console.WriteLine("Isn't a YouTube URL. Exiting...");
                return;
            }

            Type urlType = Type.None;
            if (url.ToLower().Contains("playlist?list="))
                urlType = Type.Playlist;

            if (url.ToLower().Contains("watch?v="))
                urlType = Type.Video;

            if (urlType == Type.None)
            {
                Console.WriteLine("Cannot find a valid video or playlist for this url. Exiting...");
                return;
            }

            string pathUrl = url
                .Replace("https", "")
                .Replace("http", "")
                .Replace("://", "")
                .Replace("www.", "")
                .Replace("youtube.com/", "").Trim();

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://www.youtube.com");
            var result = client.GetAsync(pathUrl).Result;
            var content = result.Content.ReadAsStringAsync().Result;
            List<string> links = new List<string>();
            if (urlType == Type.Playlist)
                links = GetLinksPlaylist(content);
            else if (urlType == Type.Video)
                links.Add(url);

            if (links.Count == 0)
            {
                Console.WriteLine("None video found in the playlist. Exiting...");
                return;
            }

            string title = GetTitle(content);
            string baseDir = (urlType == Type.Playlist ? exportPath + "\\" + title + "\\" : exportPath);
            if (!Directory.Exists(baseDir) && urlType == Type.Playlist)
                Directory.CreateDirectory(baseDir);

            var youtube = YouTube.Default;
            Engine engine = new Engine();

            Mutex writeMtx = new Mutex();
            const int maxAttempt = 4;
            Parallel.ForEach(links, link =>
            {
                Point pt = new Point();
                for (int attempt = 0; attempt < maxAttempt; attempt++)
                {
                    try
                    {
                        var vid = youtube.GetVideo(link);
                        string completeFilePath = baseDir + (links.IndexOf(link) + 1) + " - " + vid.FullName;
                        writeMtx.WaitOne();
                        Console.Write(completeFilePath);
                        if (attempt == 0)
                            pt = new Point(Console.CursorLeft, Console.CursorTop);
                        Console.WriteLine();
                        if (File.Exists(completeFilePath))
                        {
                            if (attempt == 0)
                            {
                                Console.SetCursorPosition(pt.X + 1, pt.Y);
                                Console.WriteLine("DONE!");
                                writeMtx.ReleaseMutex();
                                break;
                            }
                            else if (attempt > 0)
                                File.Delete(completeFilePath);
                        }
                        writeMtx.ReleaseMutex();

                        File.WriteAllBytes(completeFilePath, vid.GetBytes());
                        MediaFile inputFile = new MediaFile { Filename = completeFilePath };
                        MediaFile outputFile = new MediaFile { Filename = baseDir + Path.GetFileNameWithoutExtension(completeFilePath) + ".mp3" };
                        engine.GetMetadata(inputFile);
                        engine.Convert(inputFile, outputFile);
                        Console.SetCursorPosition(pt.X + 1, pt.Y);
                        Console.WriteLine("DONE!");
                        break;
                    }
                    catch
                    {
                    }
                }
            });

            engine.Dispose();
        }

        private static string GetTitle(string content)
        {
            try
            {
                var t = "<title>";
                var ini = content.IndexOf(t) + t.Length;
                var fim = content.IndexOf("</title>");
                var title = content.Substring(ini, fim - ini);
                title = title.EndsWith(" - YouTube") ? title.Remove(title.LastIndexOf(" - YouTube")) : title;
                return title.Trim();
            }
            catch (Exception)
            {
                return "Youtube";
            }
        }

        private static List<string> GetLinksPlaylist(string html)
        {
            List<string> ret = new List<string>();
            List<Match> matchs = Regex.Matches(html, @"index=\d+").Cast<Match>().ToList();
            for (int i = 0; i < matchs.Count; i++)
            {
                Console.WriteLine("Found " + matchs[i].Value + " at " + matchs[i].Index);
                char ch = ' ';
                int index = matchs[i].Index;
                while (ch != '"')
                    ch = html[index--];
                string url = html.Substring(index + 2, matchs[i].Index - index + matchs[i].Value.Length - 2);
                Console.WriteLine(url);
                ret.Add(url);
            }

            return ret;
        }
    }
}