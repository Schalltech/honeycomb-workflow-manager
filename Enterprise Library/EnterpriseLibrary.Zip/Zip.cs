using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseLibrary.Utilities
{
    public class Zip
    {
        public static void Compress(string source, string destination)
        {
            try
            {
                // Verify the source file exists.
                if (File.Exists(source))
                {
                    // Compress the file and write it out to the destination.
                    File.WriteAllBytes(destination, Compress(File.ReadAllBytes(source), Path.GetFileName(source)));
                }
                else
                {
                    throw new Exception("File not found [" + source + "].");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to compress file [" + source + "].", ex);
            }
        }

        public static void Compress(string[] files, string destination)
        {
            try
            {
                // Create a stream for the new zip file.
                using (var fileStream = new FileStream(destination, FileMode.OpenOrCreate))
                {
                    // Create the archive.
                    using (ZipArchive zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
                    {
                        foreach (string file in files)
                        {
                            // Verify the file we are attempting to compress exists.
                            if (File.Exists(file))
                            {
                                // Create a zip entry for the file.
                                ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal);

                                // Write the file to the archive.
                                using (Stream ZipFile = entry.Open())
                                {
                                    byte[] data = File.ReadAllBytes(file);
                                    ZipFile.Write(data, 0, data.Length);
                                }
                            }
                            else
                            {
                                throw new Exception("File not found [" + file + "].");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to create archive file [" + destination + "].", ex);
            }
        }

        public static byte[] Compress(byte[] source, string fileName)
        {
            try
            {
                using (var compressedFileStream = new MemoryStream())
                {
                    //Create an archive and store the stream in memory.
                    using (var zipArchive = new ZipArchive(compressedFileStream, ZipArchiveMode.Update, false))
                    {
                        //Create a zip entry.
                        var zipEntry = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal);

                        //Load the byte array into a stream.
                        using (var originalFileStream = new MemoryStream(source))
                        {
                            // Open a stream to the zip entry.
                            using (var zipEntryStream = zipEntry.Open())
                            {
                                //Copy the byte array stream to the zip entry stream.
                                originalFileStream.CopyTo(zipEntryStream);
                            }
                        }
                    }

                    return compressedFileStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to compress file [" + source + "].", ex);
            }
        }

        public static FileInfo[] Decompress(string source, string destination)
        {
            List<FileInfo> list = new List<FileInfo>();
            string zipPath = "";

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(source))
                {
                    // Create any folders included in the zip file.
                    System.IO.Directory.CreateDirectory(Path.Combine(destination));

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Standardize the slashes to make it easier to work with.
                        zipPath = entry.FullName.Replace(@"\", "/");

                        // If the zip path contains any sub directories make sure they are created.
                        if (entry.FullName.Contains('/'))
                            System.IO.Directory.CreateDirectory(Path.Combine(destination, zipPath.Substring(0, zipPath.LastIndexOf('/'))));

                        // It seems if this is a folder the Name will be empty.
                        if (!string.IsNullOrWhiteSpace(entry.Name))
                        {
                            // Extract the file to disk. (overwrite)
                            entry.ExtractToFile(Path.Combine(destination, entry.FullName), true);

                            // Store the extracted files info.
                            list.Add(new System.IO.FileInfo(destination + "\\" + entry.FullName));
                        }
                    }
                }

                return list.ToArray();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
