using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class LanCollector : BaseFileCollector
    {
        [XmlAttributeAttribute(AttributeName = "Method")]
        public string Method = "Copy";

        public LanCollector()
        { }

        public LanCollector(Cache shared_data, LanCollector configuration)
            : base(shared_data, configuration)
        {
            Method = configuration.Method;
        }

        protected override DataTable GetRemoteFileList(Data.SourceFile SourceFile)
        {
            string pattern;
            DirectoryInfo directory_info;
            List<FileInfo> file_list;

            pattern = SourceFile.Name;

            directory_info = new DirectoryInfo(TextParser.Parse(Directory, DrivingData, SharedData, ModuleCommands));

            file_list = directory_info.GetFiles(pattern, SearchOption.TopDirectoryOnly).ToList();

            return ConvertFileInfo(file_list);
        }

        protected override FileInfo CollectRemoteFile(System.Data.DataRow remote_file)
        {
            FileInfo collected_file;
            string local_full_file_name;

            try
            {
                if (!StopCollecting)
                {
                    local_full_file_name = TextParser.Parse(SharedData.TempFileDirectory + @"\" + remote_file["FileName"].ToString(), DrivingData, SharedData, ModuleCommands);

                    switch (Method.ToUpper())
                    {
                        case "COPY":
                            {
                                // Copy the file to the temp directory.
                                Logger.WriteLine("BaseFileCollector.OnProcess", "        COPYING FILE: " + remote_file["FileName"].ToString(), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                                System.IO.File.Copy(remote_file["FileFullName"].ToString(), local_full_file_name, true);
                                break;
                            }
                        case "MOVE":
                            {
                                // Move the file to the temp directory.
                                Logger.WriteLine("BaseFileCollector.OnProcess", "        MOVEING FILE: " + remote_file["FileName"].ToString(), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                                System.IO.File.Move(remote_file["FileFullName"].ToString(), local_full_file_name);
                                break;
                            }
                        default:
                            {
                                throw new Exception(Name + ": Collection method [" + Method + "] is not a valid operation.");
                            }
                    }

                    collected_file = new FileInfo(local_full_file_name);
                    return collected_file;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected DataTable ConvertFileInfo(List<FileInfo> file_list)
        {
            DataTable remote_file_list;
            DataRow row;

            try
            {
                remote_file_list = new DataTable("FileList");
                remote_file_list.Columns.Add("FileName");
                remote_file_list.Columns.Add("FileLength");
                remote_file_list.Columns.Add("FileCreationTime");
                remote_file_list.Columns.Add("FileCreationTimeUTC");
                remote_file_list.Columns.Add("FileDirectoryName");
                remote_file_list.Columns.Add("FileExtension");
                remote_file_list.Columns.Add("FileFullName");
                remote_file_list.Columns.Add("FileIsReadonly");
                remote_file_list.Columns.Add("FileLastAccessTime");
                remote_file_list.Columns.Add("FileLastAccessTimeUTC");
                remote_file_list.Columns.Add("FileLastWriteTime");
                remote_file_list.Columns.Add("FileLastWriteTimeUTC");
                remote_file_list.Columns.Add("FileNameWithoutExtension");

                // Pass each file's information into the data table.
                foreach (FileInfo File in file_list)
                {
                    row                          = remote_file_list.NewRow();
                    row["FileName"]              = File.Name;
                    row["FileLength"]            = File.Length;
                    row["FileCreationTime"]      = File.CreationTime;
                    row["FileCreationTimeUTC"]   = File.CreationTimeUtc;
                    row["FileDirectoryName"]     = File.DirectoryName;
                    row["FileExtension"]         = File.Extension;
                    row["FileFullName"]          = File.FullName;
                    row["FileIsReadonly"]        = File.IsReadOnly;
                    row["FileLastAccessTime"]    = File.LastAccessTime;
                    row["FileLastAccessTimeUTC"] = File.LastAccessTimeUtc;
                    row["FileLastWriteTime"]     = File.LastWriteTime;
                    row["FileLastWriteTimeUTC"]  = File.LastWriteTimeUtc;

                    if (!string.IsNullOrEmpty(File.Extension) && File.Extension.Length > 0)
                        row["FileNameWithoutExtension"] = File.Name.Substring(0, File.Name.Length - File.Extension.Length);
                    else
                        row["FileNameWithoutExtension"] = "";

                    remote_file_list.Rows.Add(row);
                }

                remote_file_list.AcceptChanges();

                return remote_file_list;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
