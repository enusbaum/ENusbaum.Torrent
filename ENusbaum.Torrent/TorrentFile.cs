using BencodeNET.Torrents;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Cryptography;

namespace ENusbaum.Torrent
{
    public class TorrentFile
    {
        /// <summary>
        ///     Creates a Torrent File based on the specified folder of files
        /// </summary>
        /// <param name="inputPath">Path to files to be added to the torrent</param>
        /// <param name="torrentName">Name used for the Torrent and the folder name in which the BitTorrent client will store the files</param>
        /// <param name="trackerAnnounceUrl">The Tracker URL to use in your Torrent File</param>
        /// <param name="pieceSize">Piece Size (Auto, or 8KiB -> 16MiB)</param>
        public ReadOnlySpan<byte> Create(string inputPath, string torrentName, string trackerAnnounceUrl, PieceSize pieceSize = PieceSize.Auto)
        {
            //Input Validation
            if(string.IsNullOrEmpty(inputPath) || !Directory.Exists(inputPath))
                throw new DirectoryNotFoundException($"Input Path does not exist: {inputPath}");

            if(string.IsNullOrEmpty(torrentName))
                throw new ArgumentNullException(nameof(torrentName), "Torrent Name is required");

            if(string.IsNullOrEmpty(trackerAnnounceUrl) || !Uri.IsWellFormedUriString(trackerAnnounceUrl, UriKind.Absolute))
                throw new ArgumentException("Tracker URL is required and must be a valid URL", nameof(trackerAnnounceUrl));

            if (pieceSize == PieceSize.Auto)
            {
                var totalTorrentSize = new DirectoryInfo(inputPath).GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                pieceSize = CalculateOptimalPieceSize(totalTorrentSize);
            }

            var torrent = new BencodeNET.Torrents.Torrent
            {
                // Setting the Announce URL to your tracker
                Trackers = new List<IList<string>> { new List<string> { trackerAnnounceUrl } },

                // Setting creation date and created by (optional)
                CreationDate = DateTime.UtcNow,
                PieceSize = (int)pieceSize,
                Files = new MultiFileInfoList(torrentName)
            };

            var msPiecesHash = new MemoryStream();
            var buffer = new byte[(int)pieceSize];
            var bufferIndex = 0; // Tracks where in the buffer we are

            using var sha1 = SHA1.Create();

            foreach (var file in Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories))
            {
                var fileInfo = new FileInfo(file);
                torrent.Files.Add(new MultiFileInfo
                {
                    FileSize = fileInfo.Length,
                    FullPath = string.IsNullOrEmpty(fileInfo.DirectoryName?.Replace(inputPath, string.Empty)) ? fileInfo.Name : fileInfo.FullName.Replace(inputPath, string.Empty),
                    Md5Sum = CalculateFileMD5(file)
                });

                // Read Pieces
                using var stream = File.OpenRead(file);
                int read;
                while ((read = stream.Read(buffer, bufferIndex, buffer.Length - bufferIndex)) > 0)
                {
                    bufferIndex += read; // Increment bufferIndex by the number of bytes read

                    if (bufferIndex != buffer.Length) continue; // If buffer is not full, continue to next file

                    msPiecesHash.Write(sha1.ComputeHash(buffer, 0, buffer.Length));
                    bufferIndex = 0; // Reset buffer index
                }
            }

            // Handle the last piece if buffer is not empty
            if (bufferIndex > 0)
                msPiecesHash.Write(sha1.ComputeHash(buffer, 0, bufferIndex));

            // Assuming you want to do something with the pieces hash
            torrent.Pieces = msPiecesHash.ToArray();

            //Return the stream
            var msOutput = new MemoryStream();
            torrent.EncodeTo(msOutput);
            return msOutput.ToArray();
        }

        /// <summary>
        ///    Creates a Torrent File based on the specified folder of files
        /// </summary>
        /// <param name="inputPath">Path to files to be added to the torrent</param>
        /// <param name="torrentName">Name used for the Torrent and the folder name in which the BitTorrent client will store the files</param>
        /// <param name="trackerAnnounceUrl">The Tracker URL to use in your Torrent File</param>
        /// <param name="outputFile">Output Torrent file to be created</param>
        /// <param name="pieceSize">Piece Size (Auto, or 8KiB -> 16MiB)</param>
        /// <returns></returns>
        public bool CreateFile(string inputPath, string torrentName, string trackerAnnounceUrl, string outputFile,
            PieceSize pieceSize = PieceSize.Auto)
        {
            //Input Validation
            if (string.IsNullOrEmpty(outputFile) || !Directory.Exists(Path.GetDirectoryName(outputFile)))
                throw new DirectoryNotFoundException($"Output Path does not exist: {outputFile}");

            File.WriteAllBytes(outputFile, Create(inputPath, torrentName, trackerAnnounceUrl, pieceSize));
            return true;
        }

        /// <summary>
        ///     Calculates optimal piece size for a torrent based on the total size of the files and a target piece count (Default: 2000)
        /// </summary>
        /// <param name="totalTorrentSize">Total Size of Files included in the Torrent</param>
        /// <param name="targetPieceCount">Target number of Pieces for the Torrent (Default: 2000)</param>
        /// <returns></returns>
        public static PieceSize CalculateOptimalPieceSize(long totalTorrentSize, int targetPieceCount = 2000)
        {
            //Input Validation
            if (totalTorrentSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalTorrentSize), "Total Torrent Size must be greater than 0");

            if (targetPieceCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetPieceCount), "Target Piece Count must be greater than 0");

            // Initialize variables to hold the best piece size and the smallest difference found so far
            var optimalPieceSize = (int)Enum.GetValues<PieceSize>()[1];
            var smallestDifference = long.MaxValue;

            foreach (int pieceSize in Enum.GetValues<PieceSize>().OrderBy(x => (int)x))
            {
                //Skip the Auto piece size
                if (pieceSize == (int)PieceSize.Auto)
                    continue;

                // Calculate the number of pieces for the current piece size
                var pieceCount = totalTorrentSize / pieceSize + (totalTorrentSize % pieceSize > 0 ? 1 : 0);

                // Calculate the difference from the target
                var difference = Math.Abs(pieceCount - targetPieceCount);

                //If this piece size is the same as the previous
                //Both sizes are equidistant from 2000, we default to the smaller (previous) piece
                if (difference == smallestDifference)
                    break;

                // If this piece size gives us a closer piece count to the target, update optimalPieceSize and smallestDifference
                if (difference < smallestDifference)
                {
                    optimalPieceSize = pieceSize;
                    smallestDifference = difference;
                }

                // If we've hit the target exactly, we can't do better, so break
                if (difference == 0)
                    break;

            }

            return (PieceSize)optimalPieceSize;
        }

        /// <summary>
        ///     Calculates the number of pieces for a torrent based on the total size of the files in the input path
        /// </summary>
        /// <param name="inputPath">Path to files to be calculated for the Torrent</param>
        /// <param name="pieceSize"></param>
        /// <returns></returns>
        public static int CalculatePieces(string inputPath, PieceSize pieceSize)
        {
            //Input Validation
            if(string.IsNullOrEmpty(inputPath) || !Directory.Exists(inputPath))
                throw new DirectoryNotFoundException($"Input Path does not exist: {inputPath}");

            if(pieceSize == PieceSize.Auto)
                throw new ArgumentException("Piece Size must be specified when calculating pieces (Non-Auto)", nameof(pieceSize));

            var totalTorrentSize = new DirectoryInfo(inputPath).GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            return (int)(totalTorrentSize / (int)pieceSize + (totalTorrentSize % (int)pieceSize > 0 ? 1 : 0));
        }

        /// <summary>
        ///     Calculates the MD5 hash of a file using a MemoryMappedFile to avoid loading the entire file into memory
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string CalculateFileMD5(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new ArgumentException("Invalid file path.", nameof(filePath));

            using var md5 = MD5.Create();
            using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null);
            using var stream = mmf.CreateViewStream();

            var buffer = new byte[1048576]; // 1MiB buffer
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }

            // Finalize the hash computation
            md5.TransformFinalBlock([], 0, 0);

            // Return the MD5 hash as a hex string
            return Convert.ToHexString(md5.Hash);
        }
    }
}
