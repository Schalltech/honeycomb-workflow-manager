using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class EmailPoster : BaseModule
    {
        [XmlElement(ElementName = "To")]
        public string To { get; set; }

        [XmlElement(ElementName = "Cc")]
        public string Cc { get; set; }

        [XmlElement(ElementName = "Bcc")]
        public string Bcc { get; set; }

        [XmlElement(ElementName = "From")]
        public string From { get; set; }

        [XmlElement(ElementName = "Subject")]
        public string Subject { get; set; }

        [XmlElement(ElementName = "Body")]
        public string Body { get; set; }

        [XmlElement(ElementName = "Attachment")]
        public Attachment Attachment { get; set; }

        protected List<StringCommand> StringCommands { get; set; }

        public EmailPoster()
        { }

        public EmailPoster(Cache sharedData, EmailPoster configuration) 
            : base(sharedData, configuration)
        {
            Enabled = configuration.Enabled;
            To      = configuration.To;
            Cc      = configuration.Cc;
            Bcc     = configuration.Bcc;
            From    = configuration.From;
            Subject = configuration.Subject;
            Body    = configuration.Body;

            if (configuration.Attachment != null)
                Attachment = new Attachment(SharedData, configuration.Attachment);
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
            DataTable source_table = null;

            // Verify a body is defined for the email.
            if(!string.IsNullOrEmpty(Body))
            {
                // Check to see if the body is referencing another module.
                if (TextParser.IsTableCommand(Body, SharedData))
                {
                    source_table = TextParser.GetCommandTable(Body, SharedData);

                    if (source_table.Rows.Count > 0)
                    {
                        foreach (DataRow row in source_table.Rows)
                        {
                            DrivingData = row;
                            GenerateMail();
                        }
                    }
                    else
                    {
                        Logger.WriteLine("EmailPoster.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("EmailPoster.Process", "  NO EMAILS DETECTED ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    }
                }
                else
                    GenerateMail();
            }
        }

        protected void GenerateMail()
        {
            EnterpriseLibrary.Email.SMTPClient email = new EnterpriseLibrary.Email.SMTPClient();

            Logger.WriteLine("EmailPoster.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logger.WriteLine("EmailPoster.Process", "       SENDING EMAIL: " + TextParser.Parse(Subject, DrivingData, SharedData, StringCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

            if (!string.IsNullOrEmpty(To))
            {
                email.To = TextParser.Parse(To, DrivingData, SharedData, StringCommands);
                Logger.WriteLine("EmailPoster.Process", "                  To: " + email.To, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            else
                throw new Exception("");

            if (!string.IsNullOrEmpty(Cc))
            {
                email.CC = TextParser.Parse(Cc, DrivingData, SharedData, StringCommands);
                Logger.WriteLine("EmailPoster.Process", "                  Cc: " + email.CC, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }

            if (!string.IsNullOrEmpty(Bcc))
            {
                email.BCC = TextParser.Parse(Bcc, DrivingData, SharedData, StringCommands);
                Logger.WriteLine("EmailPoster.Process", "                 Bcc: " + email.BCC, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }

            if (!string.IsNullOrEmpty(From))
            {
                email.From = TextParser.Parse(From, DrivingData, SharedData, StringCommands);
                Logger.WriteLine("EmailPoster.Process", "                From: " + email.From, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }

            if (!string.IsNullOrEmpty(Subject))
            {
                email.Subject = TextParser.Parse(Subject, DrivingData, SharedData, StringCommands);
                Logger.WriteLine("EmailPoster.Process", "             Subject: " + email.Subject, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            else
                throw new Exception("");

            if (DrivingData != null && TextParser.GetCommandColumnValue(Body, DrivingData).GetType().Name.ToUpper().Equals("BYTE[]"))
            {
                email.Body = new System.Text.ASCIIEncoding().GetString(TextParser.GetCommandColumnValue(Body, DrivingData) as byte[]);
            }
            else
            {
                //email.Body = TextParser.GetCommandColumnValue(Body, DrivingData).ToString();
                email.Body = TextParser.Parse(Body, DrivingData, SharedData, StringCommands);
            }

            // Add attachments if present.
            if (Attachment != null && Attachment.Collection != null && Attachment.Collection.Count > 0)
            {
                foreach (string file in Attachment.Collection)
                {
                    if (file == Attachment.Collection[0])
                        Logger.WriteLine("EmailPoster.Process", "         ATTACHMENTS: " + file.Substring(file.LastIndexOf("\\") + 1), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    else
                        Logger.WriteLine("EmailPoster.Process", "                      " + file.Substring(file.LastIndexOf("\\") + 1), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    email.AddAttachment(file);
                }
            }
            else
            {
                Logger.WriteLine("EmailPoster.Process", "         ATTACHMENTS: None", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }

            // I dont like this being here, but I have been unable to figure out where the
            // three ?'s are coming from in the transformation.
            if (email.Body.StartsWith("???"))
                email.Body = email.Body.Remove(0, 3);

            // Send the email.
            email.IsBodyHtml = true;
            email.Send();
        }
    }

    public class Attachment
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Path")]
        public string Path { get; set; }

        [XmlAttribute(AttributeName = "Zip")]
        public string Zip { get; set; }

        public List<string> Collection { get; set; }

        protected Cache SharedData { get; set; }
        protected DataRow DrivingData { get; set; }
        protected List<StringCommand> ModuleCommands { get; set; }

        public Attachment()
        { }

        public Attachment(Cache sharedData, Attachment configuration)
        {
            SharedData = sharedData;
            Name       = configuration.Name;
            Path       = configuration.Path;
            Zip        = configuration.Zip;

            Initialize();
        }

        protected void Initialize()
        {
            string temp_directory = null;
            string file_name = null;

            try
            {
                // Initialize the collection.
                Collection = new List<string>();

                // Verify the name of the attachment is defined.
                if ((!string.IsNullOrEmpty(Name)))
                {
                    // Verify the path to the attachment(s) is defined.
                    if (!string.IsNullOrEmpty(Path))
                    {
                        // Parse the temp directory path.
                        temp_directory = TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands).Replace("/", @"\");

                        // Check to see if the attachment file name is referenced from a table command.
                        if (TextParser.IsTableCommand(Name, SharedData))
                        {
                            // First we need to check to see if the path command references a table suitable for providing driving data.
                            // Second check to see if the tables column the command is referencing is an array.
                            if (TextParser.IsTableCommand(Path, SharedData) &&
                               TextParser.GetCommandTable(Path, SharedData).Columns[TextParser.GetCommandColumnName(Path)].DataType.IsArray)
                            {
                                // Verify the column array is an array of type 'byte'.
                                if (TextParser.GetCommandTable(Path, SharedData).Columns[TextParser.GetCommandColumnName(Path)].DataType.GetElementType() == typeof(byte))
                                {
                                    // The command references a column of type Byte[].
                                    // We will attempt to write the bytes to the temp directory.
                                    foreach (DataRow row in TextParser.GetCommandTable(Name, SharedData).Rows)
                                    {
                                        // Update the current driving data.
                                        DrivingData = row;

                                        // Parse the file name.
                                        file_name = TextParser.Parse(Name, DrivingData, SharedData, ModuleCommands);

                                        // Write the file to the processes temp directory.
                                        System.IO.File.WriteAllBytes(temp_directory + @"\" + file_name, TextParser.GetCommandColumnValue(Path, DrivingData) as byte[]);

                                        // Store the processed files full path.
                                        Collection.Add(temp_directory + @"\" + file_name);
                                    }
                                }
                                else
                                    throw new Exception("The column '" + TextParser.GetCommandColumnName(Path) + "' from table '" + TextParser.GetCommandTable(Path, SharedData).TableName + "' cannot be used for email attachments as the data type 'ArrayOf(" + TextParser.GetCommandTable(Path, SharedData).Columns[TextParser.GetCommandColumnName(Path)].DataType.GetElementType().Name + ")' is not supported. The supported data types are 'String' and 'ArrayOf(Byte)'.");
                            }
                            else
                            {
                                // The name command references a column of type string.
                                // Process through each entry in the referenced table.
                                foreach (DataRow row in TextParser.GetCommandTable(Name, SharedData).Rows)
                                {
                                    // Update the current driving data.
                                    DrivingData = row;

                                    // Copy the file to the temp directory.
                                    CopyAttachment(DrivingData, temp_directory);
                                }
                            }
                        }
                        else
                        {
                            // Copy the file to the temp directory.
                            CopyAttachment(DrivingData, temp_directory);
                        }

                        if (!string.IsNullOrEmpty(Zip))
                        {
                            if (TextParser.Parse(Zip, DrivingData, SharedData, ModuleCommands) == "Y")
                            {
                                for (int i = 0; i < Collection.Count - 1; i++)
                                {
                                    file_name = Collection[i].Substring(0, Collection[i].LastIndexOf(".")) + ".zip";

                                    EnterpriseLibrary.Utilities.Zip.Compress(Collection[i], file_name);
                                }
                            }
                            else if (TextParser.Parse(Zip, DrivingData, SharedData, ModuleCommands) != "N")
                            {
                                throw new Exception("The value '" + TextParser.Parse(Zip, DrivingData, SharedData, ModuleCommands) + "' is not supported for the zip attribute on email attachments. Supported values are 'Y' for true and 'N' for false.");
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Unable to initialize email attachments. The path to the file(s) being attached must be defined. This can be as an actual file system path or an array of bytes.");
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected void CopyAttachment(DataRow data, string target)
        {
            string file_name, source_path;

            // Parse the file name.
            file_name = TextParser.Parse(Name, data, SharedData, ModuleCommands);

            // Parse the source path.
            source_path = TextParser.Parse(Path, data, SharedData, ModuleCommands).Replace("/", @"\");

            if (!string.IsNullOrEmpty(source_path))
            {
                if(source_path + @"\" + file_name != target + @"\" + file_name)
                {
                    // Copy the file to the target directory.
                    System.IO.File.Copy(source_path + @"\" + file_name, target + @"\" + file_name, true);
                }

                // Store the processed files full path.
                Collection.Add(target + @"\" + file_name);
            }
            else
                throw new Exception("Source path for file '" + file_name + "' is not defined.");
        }
    }
}
