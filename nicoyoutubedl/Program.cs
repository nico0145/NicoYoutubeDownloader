
using System;
using VideoLibrary;
using MediaToolkit;
using MediaToolkit.Model;
using System.Linq;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace nicoyoutubedl
{
    static class Nicoyoutubedl
    {
        public static void DownloadFile(string sIn, string sOut)
        {
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(sIn), sOut);
            }
        }
        static void Main(string[] args)
        {
            string sReturnMessage;
            if (args.Any())
                sReturnMessage = args[0];
            else
            {
                Console.WriteLine("Please provide a youtube video URL:");
                sReturnMessage = Console.ReadLine();
            }
            var youtube = YouTube.Default;
            var vid = youtube.GetVideo(sReturnMessage);
            string sFileName;
            if (vid.Title.ToLower().Trim() == "youtube")
            {
                sFileName = GetVideoTitle(sReturnMessage.Split('=').Last());
            }
            else
            {
                sFileName = SanatizeFileName(vid.Title);
            }
            File.WriteAllBytes("video" + vid.FileExtension, vid.GetBytes());
            var inputFile = new MediaFile { Filename = "video" + vid.FileExtension };
            var outputFile = new MediaFile { Filename = $"{sFileName}.mp3" };

            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);
                engine.Convert(inputFile, outputFile);
            }
            TagLib.File oF = TagLib.File.Create(outputFile.Filename);
            var oMBData = MusicBrainz.Search.Recording(recording: sFileName.Replace("youtube", "")); // ver detalles api aca http://musicbrainz.org/ws/2/recording/?query=recording:
            OrganizeMetadata(oF.Tag, oMBData?.Data, vid.Title);
            string path = "tempcover.jpg";
            DownloadFile("https://img.youtube.com/vi/" + sReturnMessage.Split('=').LastOrDefault() + "/default.jpg", path);
            using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(path)))
            {
                oF.Tag.Pictures = new TagLib.IPicture[]
                {
                                            new TagLib.Picture(TagLib.ByteVector.FromStream(ms))
                                            {
                                                Type = TagLib.PictureType.FrontCover,
                                                Description = "Cover",
                                                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg
                                            }
                };
            }
            oF.Save();
            File.Delete(inputFile.Filename);
            Console.WriteLine($"File downloaded: {outputFile.Filename}");
        }
        private static string GetVideoTitle(string sId)
        {
            try
            {
                using (var client = new WebClient())
                {
                    string sRetu = client.DownloadString($"https://www.youtube.com/oembed?url=http://www.youtube.com/watch?v={sId}&format=json");
                    return JsonConvert.DeserializeObject<YoutubeInfo>(sRetu)?.title;
                }
            }
            catch
            { //sarasa
                return "";
            }
        }
        private static void OrganizeMetadata(TagLib.Tag Tag, IEnumerable<MusicBrainz.Data.RecordingData> recordings, string Title)
        {
            Console.WriteLine("Please select the correct metadata:");
            int iAux = 0;
            var recs = recordings.ToArray();
            foreach (var recording in recs)
            {
                Console.WriteLine($"[{iAux}]:");
                DisplayRecording(recording);
                iAux++;
            }
            Console.WriteLine($"[{iAux}]:");
            Console.WriteLine($"Title: {Title}");
            int iOption = GetOption(iAux);
            if (iOption == recs.Count()) //custom
            {
                Console.Write($"Title [{Title}]: ");
                string sAux = Console.ReadLine();
                Tag.Title = string.IsNullOrWhiteSpace(sAux) ? Title : sAux;
                Console.Write("Artist: ");
                Tag.AlbumArtists = Tag.Performers = new string[] { Console.ReadLine() };
                Console.Write("Album: ");
                Tag.Album = Console.ReadLine();
            }
            else
            {
                Tag.Title = recs[iOption].Title;
                Tag.MusicBrainzArtistId = recs[iOption].Artistcredit.FirstOrDefault()?.Artist.Id;
                Tag.AlbumArtists = new string[] { recs[iOption].Artistcredit.FirstOrDefault()?.Artist.Name };
                Tag.Performers = Tag.AlbumArtists;
                Tag.Album = recs[iOption].Releaselist.FirstOrDefault().Title;
            }
        }
        private static int GetOption(int iAux)
        {
            Console.Write($"Select an option [0-{iAux}]: ");
            string sRetu = Console.ReadLine();
            int iRetu;
            while (!int.TryParse(sRetu, out iRetu) || iRetu < 0 || iRetu > iAux)
            {
                Console.Write($"Select an option [0-{iAux}]: ");
                sRetu = Console.ReadLine();
            }
            return iRetu;
        }
        private static void DisplayRecording(MusicBrainz.Data.RecordingData oRec)
        {
            Console.WriteLine($"Title: {oRec.Title}");
            Console.WriteLine($"ArtistID: {oRec.Artistcredit.FirstOrDefault()?.Artist.Id}");
            Console.WriteLine($"Album Artist/Performers: {oRec.Artistcredit.FirstOrDefault()?.Artist.Name}");
            Console.WriteLine($"Album: {oRec.Releaselist.FirstOrDefault()?.Title}");
        }
        private static string SanatizeFileName(string sIn)
        {
            List<char> InvalidCharacters = (new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).ToList();
            InvalidCharacters.ForEach(character => sIn = sIn.Replace(character, ' '));
            return sIn;
        }
    }
    public class YoutubeInfo
    {
        public string author_url { get; set; }
        public int height { get; set; }
        public string version { get; set; }
        public int thumbnail_height { get; set; }
        public string type { get; set; }
        public string provider_url { get; set; }
        public string provider_name { get; set; }
        public int width { get; set; }
        public int thumbnail_width { get; set; }
        public string thumbnail_url { get; set; }
        public string html { get; set; }
        public string author_name { get; set; }
        public string title { get; set; }
    }
}
