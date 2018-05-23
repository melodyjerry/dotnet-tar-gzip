using System;
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

// https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples
namespace targzip
{
    class Program
    {
        public void ExtractTGZ(String gzArchiveName, String destFolder)
        {

            Stream inStream = File.OpenRead(gzArchiveName);
            Stream gzipStream = new GZipInputStream(inStream);

            TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
            tarArchive.ExtractContents(destFolder);
            tarArchive.Close();

            gzipStream.Close();
            inStream.Close();
        }

        public string ExtractGZipFile(string gzipFileName, string targetDir)
        {

            // Use a 4K buffer. Any larger is a waste.    
            byte[] dataBuffer = new byte[4096];

            using (System.IO.Stream fs = new FileStream(gzipFileName, FileMode.Open, FileAccess.Read))
            {
                using (GZipInputStream gzipStream = new GZipInputStream(fs))
                {

                    // Change this to your needs
                    string fnOut = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(gzipFileName));

                    using (FileStream fsOut = File.Create(fnOut))
                    {
                        StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                    }

                    return fnOut;
                }
            }
        }

        public void ExtractTar(String tarFileName, String destFolder)
        {

            Stream inStream = File.OpenRead(tarFileName);
            TarArchive tarArchive = TarArchive.CreateInputTarArchive(inStream);
            tarArchive.ExtractContents(destFolder);
            tarArchive.Close();
            inStream.Close();
        }

        private void CopyWithAsciiTranslate(TarInputStream tarIn, Stream outStream)
        {
            byte[] buffer = new byte[4096];
            bool isAscii = true;
            bool cr = false;

            int numRead = tarIn.Read(buffer, 0, buffer.Length);
            int maxCheck = Math.Min(200, numRead);
            for (int i = 0; i < maxCheck; i++)
            {
                byte b = buffer[i];
                if (b < 8 || (b > 13 && b < 32) || b == 255)
                {
                    isAscii = false;
                    break;
                }
            }
            while (numRead > 0)
            {
                if (isAscii)
                {
                    // Convert LF without CR to CRLF. Handle CRLF split over buffers.
                    for (int i = 0; i < numRead; i++)
                    {
                        byte b = buffer[i]; // assuming plain Ascii and not UTF-16
                        if (b == 10 && !cr)     // LF without CR
                            outStream.WriteByte(13);
                        cr = (b == 13);

                        outStream.WriteByte(b);
                    }
                }
                else
                {
                    outStream.Write(buffer, 0, numRead);
                }
                numRead = tarIn.Read(buffer, 0, buffer.Length);
            }
        }

        public void UpdateTar(string tarFileName, string targetFile, bool asciiTranslate)
        {
            using (FileStream fsIn = new FileStream(tarFileName, FileMode.Open, FileAccess.Read))
            {
                string tmpTar = Path.Combine(Path.GetDirectoryName(tarFileName), "tmp.tar");
                using (FileStream fsOut = new FileStream(tmpTar, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    TarOutputStream tarOutputStream = new TarOutputStream(fsOut);
                    TarInputStream tarIn = new TarInputStream(fsIn);
                    TarEntry tarEntry;
                    while ((tarEntry = tarIn.GetNextEntry()) != null)
                    {

                        if (tarEntry.IsDirectory)
                        {
                            continue;
                        }
                        // Converts the unix forward slashes in the filenames to windows backslashes
                        //
                        string name = tarEntry.Name.Replace('/', Path.DirectorySeparatorChar);
                        string sourceFileName = Path.GetFileName(targetFile);
                        string targetFileName = Path.GetFileName(tarEntry.Name);

                        if (sourceFileName.Equals(targetFileName))
                        {
                            using (Stream inputStream = File.OpenRead(targetFile))
                            {

                                long fileSize = inputStream.Length;
                                TarEntry entry = TarEntry.CreateTarEntry(tarEntry.Name);

                                // Must set size, otherwise TarOutputStream will fail when output exceeds.
                                entry.Size = fileSize;

                                // Add the entry to the tar stream, before writing the data.
                                tarOutputStream.PutNextEntry(entry);

                                // this is copied from TarArchive.WriteEntryCore
                                byte[] localBuffer = new byte[32 * 1024];
                                while (true)
                                {
                                    int numRead = inputStream.Read(localBuffer, 0, localBuffer.Length);
                                    if (numRead <= 0)
                                    {
                                        break;
                                    }
                                    tarOutputStream.Write(localBuffer, 0, numRead);
                                }
                            }
                            tarOutputStream.CloseEntry();
                        }
                        else
                        {
                            tarOutputStream.PutNextEntry(tarEntry);

                            if (asciiTranslate)
                            {
                                CopyWithAsciiTranslate(tarIn, tarOutputStream);
                            }
                            else
                            {
                                tarIn.CopyEntryContents(tarOutputStream);
                            }

                            tarOutputStream.CloseEntry();
                        }
                    }
                    tarIn.Close();
                    tarOutputStream.Close();
                }

                File.Delete(tarFileName);
                File.Move(tmpTar, tarFileName);
            }
        }

        public void UpdateGZipFile(string gzipFileName, string targetFile, bool asciiTranslate)
        {
            if (!File.Exists(gzipFileName) || !File.Exists(targetFile))
            {
                Console.WriteLine("Please input valid file");
                return;
            }

            // Extract gzip to tar
            string tarFileName = ExtractGZipFile(gzipFileName, Path.GetDirectoryName(gzipFileName));
            // Update tar
            UpdateTar(tarFileName, targetFile, asciiTranslate);
            // Create a new tar.gz
            UpdateTarGZ(gzipFileName, tarFileName);
        }

        private void AddDirectoryFilesToTar(TarArchive tarArchive, string sourceDirectory, bool recurse, bool isRoot)
        {

            // Optionally, write an entry for the directory itself.
            // Specify false for recursion here if we will add the directory's files individually.
            //
            TarEntry tarEntry;

            if (!isRoot)
            {
                tarEntry = TarEntry.CreateEntryFromFile(sourceDirectory);
                tarArchive.WriteEntry(tarEntry, false);
            }

            // Write each file to the tar.
            //
            string[] filenames = Directory.GetFiles(sourceDirectory);
            foreach (string filename in filenames)
            {
                tarEntry = TarEntry.CreateEntryFromFile(filename);
                Console.WriteLine(tarEntry.Name);
                tarArchive.WriteEntry(tarEntry, true);
            }

            if (recurse)
            {
                string[] directories = Directory.GetDirectories(sourceDirectory);
                foreach (string directory in directories)
                    AddDirectoryFilesToTar(tarArchive, directory, recurse, false);
            }
        }

        private void CreateTarGZ(string tgzFilename, string sourceDirectory)
        {
            Stream outStream = File.Create(tgzFilename);
            Stream gzoStream = new GZipOutputStream(outStream);
            TarArchive tarArchive = TarArchive.CreateOutputTarArchive(gzoStream);

            // Note that the RootPath is currently case sensitive and must be forward slashes e.g. "c:/temp"
            // and must not end with a slash, otherwise cuts off first char of filename
            // This is scheduled for fix in next release
            tarArchive.RootPath = sourceDirectory.Replace('\\', '/');
            if (tarArchive.RootPath.EndsWith("/"))
                tarArchive.RootPath = tarArchive.RootPath.Remove(tarArchive.RootPath.Length - 1);

            AddDirectoryFilesToTar(tarArchive, sourceDirectory, true, true);

            tarArchive.Close();
        }

        private void UpdateTarGZ(string tgzFilename, string tarFileName)
        {
            Stream gzoStream = new GZipOutputStream(File.Create(tgzFilename));

            using (FileStream source = File.Open(tarFileName,
            FileMode.Open))
            {

                byte[] localBuffer = new byte[32 * 1024];
                while (true)
                {
                    int numRead = source.Read(localBuffer, 0, localBuffer.Length);
                    if (numRead <= 0)
                    {
                        break;
                    }
                    gzoStream.Write(localBuffer, 0, numRead);
                }
            }

            gzoStream.Close();

            File.Delete(tarFileName);
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                string usage = @"
Name
    targzip

Synopsis
    targzip [c file.tar.gz directory] [e file.tar.gz directory] [u file.tar.gz file]

Description
    c create a tar.gz file
    e extract a tar.gz file
    u update a file
                                ";

                Console.WriteLine(usage);
                return;
            }

            string opt = args[0];
            Program app = new Program();
            switch (opt)
            {
                case "c":
                    {
                        app.CreateTarGZ(args[1], args[2]);
                    }
                    break;
                case "e":
                    {
                        app.ExtractTGZ(args[1], args[2]);
                    }
                    break;
                case "u":
                    {
                        app.UpdateGZipFile(args[1], args[2], true);
                    }
                    break;
                default:
                    Console.WriteLine("Invalid options");
                    break;
            }

            Console.WriteLine("Done.");
        }
    }
}
