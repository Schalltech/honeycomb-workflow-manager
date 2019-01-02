using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class LanPoster : BaseModule
    {
        bool overwrite = true;

        [XmlElement(ElementName = "DrivingModule")]
        public CacheTable DrivingModule { get; set; }

        [XmlElement(ElementName = "FileName")]
        public string FileName { get; set; }

        [XmlElement(ElementName = "ErrorOnFileNotFound")]
        public bool ErrorOnFileNotFound { get; set; }

        [XmlElement(ElementName = "ZipFile")]
        public bool ZipFile { get; set; }

        [XmlElement(ElementName = "MinFileSizeToZip")]
        public int MinFileSizeToZip { get; set; }

        [XmlElement(ElementName = "Destinations")]
        public DestinationContainer DestinationContainer { get; set; }

        [XmlElement(ElementName = "OverWrite")]
        public bool OverWrite
        {
            get
            {
                return overwrite;
            }
            set
            {
                overwrite = value;
            }
        }

        [XmlElement(ElementName = "MaxDocuments")]
        public int MaxDocuments { get; set; }

        public LanPoster()
        {}

        public LanPoster(Cache shared_data, LanPoster configuration) 
            : base(shared_data, configuration)
        {
            ErrorOnFileNotFound  = configuration.ErrorOnFileNotFound;
            ZipFile              = configuration.ZipFile;
            MinFileSizeToZip     = configuration.MinFileSizeToZip;
            DestinationContainer = configuration.DestinationContainer;
            OverWrite            = configuration.OverWrite;
            MaxDocuments         = configuration.MaxDocuments;
            FileName             = configuration.FileName;

            DrivingModule = new CacheTable(SharedData, DrivingData, configuration.DrivingModule);
        }

        protected void PostFromDisk(string LocalFileName, Destination Destination)
        {
            FileInfo LocalFileInfo;
            string LocalFileFullPath = "";
            string RemoteFileName = "";
            string RemoteDirectory = "";
            string PartialPath = "";
            long LocalFileSize;

            //Parse the local file's name.
            LocalFileName = TextParser.Parse(LocalFileName, DrivingData, SharedData, ModuleCommands);

            //Set the local file's base path as the processes temporary directory.
            //If required strip the base path from the LocalFileName.
            if(!LocalFileName.Contains(TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands)))
            {
                //If only the file name was provided, add the processes temporary directory to create the full path.
                LocalFileFullPath = TextParser.Parse(SharedData.TempFileDirectory + LocalFileName, DrivingData, SharedData, ModuleCommands);
            }
            else
            {
                LocalFileFullPath = LocalFileName;
                LocalFileName = LocalFileName.Replace(TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands), "");
            }

            //Get the local file's information and store it in the modules commands.
            LocalFileInfo = new System.IO.FileInfo(LocalFileFullPath);
            AddOriginalModuleVariables(LocalFileInfo);

            //Check the LocalFileName for a partial path.
            if(LocalFileName.Contains(@"\"))
            {
                PartialPath = LocalFileName.Substring(0, LocalFileName.LastIndexOf(@"\") + 1);
                LocalFileName = LocalFileName.Replace(PartialPath, "");
            }

            //Concat the destination url and the partial path
            RemoteDirectory = (TextParser.Parse(Destination.URL, DrivingData, SharedData, ModuleCommands) + PartialPath).Replace("/", @"\");
            System.IO.Directory.CreateDirectory(RemoteDirectory);

            //Check to see if the local file will be renamed when it is posted.
            if(!string.IsNullOrEmpty(Destination.NewFileName))
            {
                //Get the new name of the file.
                RemoteFileName = TextParser.Parse(Destination.NewFileName, DrivingData, SharedData, ModuleCommands);
                Logger.WriteLine("LanPoster.PostFromDisk", "        RENAMED FILE: " + RemoteFileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            else
                RemoteFileName = LocalFileName;

            //Get the size of the current file.
            LocalFileSize = LocalFileInfo.Length;

            if (ZipFile && MinFileSizeToZip <= 0 || (LocalFileSize >= (MinFileSizeToZip * 1000) && MinFileSizeToZip > 0))
            {
                Logger.WriteLine("LanPoster.PostFromDisk", "    COMPRESSING FILE: " + RemoteFileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                //Change the remote filename extension.
                RemoteFileName = RemoteFileName.Substring(0, RemoteFileName.LastIndexOf(".")) + ".zip";

                //Compress the local file.
                EnterpriseLibrary.Utilities.Zip.Compress(LocalFileFullPath, TextParser.Parse(SharedData.TempFileDirectory + RemoteFileName, DrivingData, SharedData, ModuleCommands));

                //Since the original local file was compressed, its name and path must be updated 
                //to reflect the new compressed file's information.
                LocalFileFullPath = TextParser.Parse(SharedData.TempFileDirectory + RemoteFileName, DrivingData, SharedData, ModuleCommands);
                LocalFileName = RemoteFileName;
            }

            try
            {
                //Upload the file to the LAN directory.
                Logger.WriteLine("LanPoster.PostFromDisk", "      UPLOADING FILE: " + RemoteFileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                System.IO.File.Copy(LocalFileFullPath, RemoteDirectory + RemoteFileName, OverWrite);
                Logger.WriteLine("LanPoster.PostFromDisk", "       UPLOAD STATUS: SUCCESSFULL", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                //Get the posted file's information and store it in the module's commands.
                LocalFileInfo = new System.IO.FileInfo(RemoteDirectory + RemoteFileName);
                AddPostedModuleVariables(LocalFileInfo);

                //Add the current file to the results table.
                AddResults();
            }
            catch(Exception ex)
            {
                Logger.WriteLine("LanPoster.PostFromDisk", "       UPLOAD STATUS: FAILED", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                throw ex;
            }
        }

        protected void PostFromMemory(string LocalFileName, Byte[] ReportBuffer, Destination Destination)
        {
            long ReportFileSize = 0;
            string RemoteDirectory;
            string PartialPath = "";
            string RemoteFileName = "";
            System.IO.FileInfo LocalFileInfo;

            try
            {
                //Get the size of the current file.
                ReportFileSize = ReportBuffer.LongLength;

                //Check to see if the file will be renamed.
                if(!string.IsNullOrEmpty(Destination.NewFileName))
                {

                    //Get the new name of the file.
                    RemoteFileName = TextParser.Parse(Destination.NewFileName, DrivingData, SharedData, ModuleCommands);
                    Logger.WriteLine("LanPoster.PostFromMemory", "        RENAMED FILE: " + RemoteFileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
                else
                    RemoteFileName = LocalFileName;

                //Remove the partial path from the file name if exists.
                if(RemoteFileName.Contains(@"\"))
                {
                    PartialPath = RemoteFileName.Substring(0, RemoteFileName.LastIndexOf(@"\") + 1);
                    RemoteFileName = RemoteFileName.Replace(PartialPath, "");
                }

                RemoteDirectory = TextParser.Parse(Destination.URL, DrivingData, SharedData, ModuleCommands) + PartialPath;

                //Ensure the target directory exist.
                if(!System.IO.Directory.Exists(RemoteDirectory))
                {
                    //Creates directory and all subdirectories needed.
                    System.IO.Directory.CreateDirectory(RemoteDirectory);
                }

                //Check to see if the file is suppose to be zipped, and if it meets the minimum file size to be zipped.
                if(ZipFile && (MinFileSizeToZip <= 0 || ReportFileSize >= (MinFileSizeToZip * 1000)))
                {
                    Logger.WriteLine("LanPoster.PostFromMemory", "    COMPRESSING FILE: " + RemoteFileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    //Zip the file buffer.
                    ReportBuffer = EnterpriseLibrary.Utilities.Zip.Compress(ReportBuffer, RemoteFileName);

                    //Change the files extension.
                    RemoteFileName = RemoteFileName.Substring(0, RemoteFileName.LastIndexOf(".")) + ".zip";
                }

                //Upload the file to the LAN directory.
                Logger.WriteLine("LanPoster.PostFromMemory", "      UPLOADING FILE: " + RemoteFileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                try
                {
                    System.IO.File.WriteAllBytes(RemoteDirectory + RemoteFileName, ReportBuffer);
                    Logger.WriteLine("LanPoster.PostFromMemory", "       UPLOAD STATUS: SUCCESSFULL", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    //Update the variable values for the current file.
                    LocalFileInfo = new System.IO.FileInfo(RemoteDirectory + RemoteFileName);
                    AddPostedModuleVariables(LocalFileInfo);

                    //Add the current file to the results table.
                    AddResults();
                }
                catch(Exception ex)
                {
                    Logger.WriteLine("LanPoster.PostFromMemory", "       UPLOAD STATUS: FAILED", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    throw ex;
                }
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        protected void AddOriginalModuleVariables(System.IO.FileInfo CurrentFile)
        {
            SetModuleCommand("%OrigFileName%",		CurrentFile.Name);
            SetModuleCommand("%OrigFileExtension%", CurrentFile.Extension);
            SetModuleCommand("%OrigFileLength%",	CurrentFile.Length.ToString());
        }

        protected void AddPostedModuleVariables(System.IO.FileInfo CurrentFile)
        {
            SetModuleCommand("%PostedFileName%",              CurrentFile.Name);
            SetModuleCommand("%PostedFileCreationTime%",      CurrentFile.CreationTime.ToString());
            SetModuleCommand("%PostedFileCreationTimeUTC%",   CurrentFile.CreationTimeUtc.ToString());
            SetModuleCommand("%PostedFileDirectoryName%",     CurrentFile.DirectoryName);
            SetModuleCommand("%PostedFileExtension%",         CurrentFile.Extension);
            SetModuleCommand("%PostedFileFullName%",          CurrentFile.FullName);
            SetModuleCommand("%PostedFileIsReadonly%",        CurrentFile.IsReadOnly.ToString());
            SetModuleCommand("%PostedFileLastAccessTime%",    CurrentFile.LastAccessTime.ToString());
            SetModuleCommand("%PostedFileLastAccessTimeUTC%", CurrentFile.LastAccessTimeUtc.ToString());
            SetModuleCommand("%PostedFileLastWriteTime%",     CurrentFile.LastWriteTime.ToString());
            SetModuleCommand("%PostedFileLastWriteTimeUTC%",  CurrentFile.LastWriteTimeUtc.ToString());
            SetModuleCommand("%PostedFileLength%",            CurrentFile.Length.ToString());
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
            string LocalFileName;
            System.Data.DataTable DrivingTable;

            try
            {
                //Make sure at least one target directory has been specified in the configuration file.
                if(DestinationContainer == null || DestinationContainer.Count == 0)
                {
                    Logger.WriteLine("LanPoster.OnProcess", "Module [" + Name + "]: No destinations are defined for the poster.", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    return;
                }

                //Upload all listed files to each destination.
                foreach(Destination Destination in DestinationContainer.List)
                {
                    //Get the driving module from the shared data.
                    if(SharedData.Data.Contains(DrivingModule.Name))
                    {
                        DrivingTable = DrivingModule.Process();

                        Logger.WriteLine("LanPoster.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        if(Destination.URL.Contains(DrivingModule.Name))
                        {
                            Logger.WriteLine("LanPoster.OnProcess", "    REMOTE DIRECTORY: " + Destination.URL, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.WriteLine("LanPoster.OnProcess", "          FILE COUNT: " + DrivingTable.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        }
                        else
                        {
                            Logger.WriteLine("LanPoster.OnProcess", "    REMOTE DIRECTORY: " + TextParser.Parse(Destination.URL, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.WriteLine("LanPoster.OnProcess", "          FILE COUNT: " + DrivingTable.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        }

                        Logger.WriteLine("LanPoster.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        foreach(System.Data.DataRow row in DrivingTable.Rows)
                        {
                            DrivingData = row;

                            if(Destination.URL.Contains(DrivingModule.Name))
                                Logger.WriteLine("LanPoster.OnProcess", "         PARSED PATH: " + TextParser.Parse(Destination.URL, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                            LocalFileName = TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands);

                            //Retrieve the file from the temp directory or from the table.
                            if(DrivingTable.Columns.Contains("_Memory_Management_"))
                            {
                                if(DrivingData["_Memory_Management_"].ToString() == "disk")
                                {
                                    PostFromDisk(LocalFileName, Destination);
                                }
                                else if(DrivingData["_Memory_Management_"].ToString() == "stream")
                                {
                                    PostFromMemory(LocalFileName, (byte[])DrivingData["_Raw_Report_"], Destination);
                                }
                                else
                                    throw new Exception("Error retrieving report data buffer from memory.");
                            }
                            else
                            {
                                //The referenced table was not created by a GAH module!
                                //Assume the file was saved in the temp directory...
                                PostFromDisk(LocalFileName, Destination);
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Module [" + Name + "]: The driving module [" + DrivingModule.Name + "] does not exist.");
                    }
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        protected override void OnLoadVariables(object sender, EventArgs e)
        {
            //Create the default variables that will hold the file's values before it is posted.
            AddDefaultModuleVariable("OrigFileName", "String", "%OrigFileName%");
            AddDefaultModuleVariable("OrigFileLength", "String", "%OrigFileLength%");
            AddDefaultModuleVariable("OrigFileExtension", "String", "%OrigFileExtension%");

            //Create the default variables that will hold the file's values after it is posted.
            AddDefaultModuleVariable("PostedFileName", "String", "%PostedFileName%");
            AddDefaultModuleVariable("PostedFileLength", "String", "%PostedFileLength%");
            AddDefaultModuleVariable("PostedFileCreationTime", "String", "%PostedFileCreationTime%");
            AddDefaultModuleVariable("PostedFileCreationTimeUTC", "String", "%PostedFileCreationTimeUTC%");
            AddDefaultModuleVariable("PostedFileDirectoryName", "String", "%PostedFileDirectoryName%");
            AddDefaultModuleVariable("PostedFileExtension", "String", "%PostedFileExtension%");
            AddDefaultModuleVariable("PostedFileFullName", "String", "%PostedFileFullName%");
            AddDefaultModuleVariable("PostedFileIsReadonly", "String", "%PostedFileIsReadonly%");
            AddDefaultModuleVariable("PostedFileLastAccessTime", "String", "%PostedFileLastAccessTime%");
            AddDefaultModuleVariable("PostedFileLastAccessTimeUTC", "String", "%PostedFileLastAccessTimeUTC%");
            AddDefaultModuleVariable("PostedFileLastWriteTime", "String", "%PostedFileLastWriteTime%");
            AddDefaultModuleVariable("PostedFileLastWriteTimeUTC", "String", "%PostedFileLastWriteTimeUTC%");

            base.OnLoadVariables(sender, e);
        }
    }

    public class DestinationContainer
    {
        [XmlElement(ElementName = "Destination")]
        public List<Destination> List { get; set; }

        public int Count
        {
            get
            {
                if (List != null)
                    return List.Count;
                else
                    return 0;
            }
        }

        public int Length
        {
            get
            {
                if (List != null)
                    return List.Count - 1;
                else
                    return -1;
            }
        }
    }

    public class Destination
    {
        [XmlAttribute(AttributeName = "Path")]
        public string Path { get; set; }

        [XmlAttribute(AttributeName = "Folder")]
        public string Folder { get; set; }

        [XmlAttribute(AttributeName = "NewFileName")]
        public string NewFileName { get; set; }

        public string URL
        {
            get
            {
                if (Folder == null)
                    Folder = "";

                Path.Replace("/", @"\");
                Folder.Replace("/", @"\");

                return (Path.EndsWith(@"\") ? Path : Path + @"\") + (Folder == "" ? Folder : (Folder.EndsWith(@"\") ? Folder : Folder + @"\"));
            }
        }
    }
}
