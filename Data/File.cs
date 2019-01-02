using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WFM.Data
{
    public struct ColumnArrayItem
    {
        public string ColumnName {get;set;}
        public string ColumnValue {get;set;}
    }

    public class File
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "Unzip")]
        public bool Unzip { get; set; }

        [XmlElement(ElementName = "Columns")]
        public CacheColumnCollection ColumnContainer { get; set; }

        private List<StringCommand> StringCommands { get; set; }

        public string FileName
        {
            get
            {
                if(!string.IsNullOrEmpty(Name))
                    return Name.Replace("/", @"\");

                return "";
            }
        }

        public List<ColumnArrayItem> GetFileColumnArray()
        {
            List<ColumnArrayItem> items = new List<ColumnArrayItem>();

            foreach(CacheColumn col in ColumnContainer.Items)
            {
                items.Add(new ColumnArrayItem { ColumnName = col.Name });
            }

            return items;
        }

        public List<StringCommand> SetStringCommands(FileInfo currentFile, FileInfo parentFile)
        {
            List<StringCommand> file_attributes = new List<StringCommand>();

            file_attributes.Add(new StringCommand
            {
                Name  = "%FileName%",
				//Value = parentFile == null ? currentFile.Name : parentFile.Name.Replace(parentFile.Extension, "") + @"\" + currentFile.Name
				Value = parentFile == null ? currentFile.Name : parentFile.Name.Replace(parentFile.Extension, "") + @"\" + currentFile.Name
			});

            file_attributes.Add(new StringCommand
            {
                Name = "%FileCreationTime%",
                Value = currentFile.CreationTime.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileCreationTimeUTC%",
                Value = currentFile.CreationTimeUtc.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileDirectoryName%",
                Value = currentFile.DirectoryName
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileExtension%",
                Value = currentFile.Extension
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileFullName%",
                Value = currentFile.FullName
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileIsReadonly%",
                Value = currentFile.IsReadOnly.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileLastAccessTime%",
                Value = currentFile.LastAccessTime.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileLastAccessTimeUTC%",
                Value = currentFile.LastAccessTimeUtc.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileLastWriteTime%",
                Value = currentFile.LastWriteTime.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileLastWriteTimeUTC%",
                Value = currentFile.LastWriteTimeUtc.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%FileLength%",
                Value = currentFile.Length.ToString()
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%ZippedParentFileName%",
                Value = parentFile == null ? null : parentFile.Name
            });

            file_attributes.Add(new StringCommand
            {
                Name = "%ZippedParentFileCreationTime%",
                Value = parentFile == null ? DateTime.MinValue.ToString() : parentFile.CreationTime.ToString()
            });

            return file_attributes;
        }

        private bool IsCompressed(FileInfo current_file)
        {
            switch(current_file.Extension.ToUpper())
            {
                case ".ZIP":
                { 
                    current_file.Attributes = FileAttributes.Compressed;
                    return true;
                }
            }

            return false;
        }
    }

    public class SourceFileContainer
    {
        [XmlElement(ElementName = "SourceFile")]
        public List<SourceFile> List { get; set; }
    }

    public class SourceFile
    {
        string folder = "";
        bool unzip = false;

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Folder")]
        public string Folder
        {
            get
            {
                return folder;
            }
            set
            {
                folder = value;
            }
        }

        [XmlAttribute(AttributeName = "Unzip")]
        public bool Unzip
        {
            get
            {
                return unzip;
            }
            set
            {
                unzip = value;
            }
        }

        [XmlElement(ElementName = "Filter")]
        public GenericExpression Filter { get; set; }
    }

    public class DirectoryContainer
    {
        [XmlElement(ElementName = "Directory")]
        public List<Directory> List { get; set; }
    }

    public class Directory
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Base")]
        public string Base { get; set; }

        [XmlAttribute(AttributeName = "Pattern")]
        public string Pattern { get; set; }
    }
}
