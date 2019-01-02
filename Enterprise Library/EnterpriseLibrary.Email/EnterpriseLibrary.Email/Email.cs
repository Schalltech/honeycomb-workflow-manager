using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMail = System.Net.Mail;
using Schalltech.EnterpriseLibrary;
using Schalltech.EnterpriseLibrary.Configuration.Email;
using System.Collections;
using System.Net;

namespace EnterpriseLibrary.Email
{
    public class SMTPClient : IDisposable
    {
        EmailConfiguration Model { get; set; }

        NetMail.MailMessage Message { get; set; }

        public string To
        {
            get
            {
                return Message.To.ToString();
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if(value.Contains(";"))
                    {
                        AddAddressWithSemiColons(Message.To, value);
                    }
                    else
                    {
                        Message.To.Add(value.Trim());
                    }
                }
                else
                    Message.To.Clear();
            }
        }

        public string From
        {
            get
            {
                if (Message.From == null && !string.IsNullOrEmpty(Model.DefaultFromAddress))
                    Message.From = new NetMail.MailAddress(Model.DefaultFromAddress);

                return Message.From.ToString();
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Message.From = new NetMail.MailAddress(value.Trim());
                }
                else if (!string.IsNullOrEmpty(Model.DefaultFromAddress))
                {
                    Message.From = new NetMail.MailAddress(Model.DefaultFromAddress);
                }
                else
                    Message.From = null;
            }
        }

        public string ReplyTo
        {
            get
            {
                if (Message.ReplyToList == null && !string.IsNullOrEmpty(Model.DefaultReplyToAddress))
                {
                    if (Model.DefaultReplyToAddress.Contains(";"))
                    {
                        AddAddressWithSemiColons(Message.ReplyToList, Model.DefaultReplyToAddress);
                    }
                    else
                        Message.ReplyToList.Add(Model.DefaultReplyToAddress.Trim());
                }
                
                return Message.ReplyToList.ToString();
            }
            set
            {
                Message.ReplyToList.Clear();

                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Contains(";"))
                    {
                        AddAddressWithSemiColons(Message.ReplyToList, value);
                    }
                    else
                    {
                        Message.ReplyToList.Add(value.Trim());
                    }
                }
                else if(!string.IsNullOrEmpty(Model.DefaultReplyToAddress))
                {
                    if (Model.DefaultReplyToAddress.Contains(";"))
                    {
                        AddAddressWithSemiColons(Message.ReplyToList, Model.DefaultReplyToAddress);
                    }
                    else
                        Message.ReplyToList.Add(Model.DefaultReplyToAddress.Trim());
                }
            }
        }

        public string CC
        {
            get
            {
                return Message.CC.ToString();
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Contains(";"))
                    {
                        AddAddressWithSemiColons(Message.CC, value);
                    }
                    else
                    {
                        Message.CC.Add(value.Trim());
                    }
                }
                else
                    Message.CC.Clear();
            }
        }

        public string BCC
        {
            get
            {
                return Message.Bcc.ToString();
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Contains(";"))
                    {
                        AddAddressWithSemiColons(Message.Bcc, value);
                    }
                    else
                    {
                        Message.Bcc.Add(value.Trim());
                    }
                }
                else
                    Message.Bcc.Clear();
            }
        }

        public string Body
        {
            get
            {
                return Message.Body;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Message.Body = value;
                }
                else
                    Message.Body = "";
            }
        }

        public string Subject
        {
            get
            {
                return Message.Subject;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Message.Subject = value;
                }
                else
                    Message.Subject = "";
            }
        }

        public bool IsBodyHtml
        {
            get
            {
                return Message.IsBodyHtml;
            }
            set
            {
                Message.IsBodyHtml = value;
            }
        }

        //public IList Attachments
        //{
        //    get
        //    {
        //        return Message.Attachments;
        //    }
        //}

        public SMTPClient()
        {
            Model = Schalltech.EnterpriseLibrary.Configuration.ConfigurationManager.GetConfiguration<EmailConfiguration>("EmailConfiguration");

            Initialize();
        }

        private void Initialize()
        {
            if(Message == null)
            {
                Message = new NetMail.MailMessage();
                Message.BodyEncoding = System.Text.Encoding.Default;
                Message.ReplyToList.Clear();
            }
        }

        public void Send()
        {
            if(Message.Attachments.Count > 0)
                Message.Body = Message.Body + Environment.NewLine + Environment.NewLine;

            ValidateMessage();

            SendMail();
        }
        
        private void SendMail()
        {
            System.Net.Mail.SmtpClient client = null;
			
            foreach (string server in Model.ServerCollection.Items)
            {
                try
                {
                    client = new System.Net.Mail.SmtpClient(server);
					client.UseDefaultCredentials = false;
					client.Credentials = new NetworkCredential("67eff5ec5d237501277e229007766be2", "0bf10b9b770586d90c02c96aa99ab751");
					client.EnableSsl = true;
					//client.UseDefaultCredentials = true;
					//client.Credentials = new NetworkCredential("eschall@schalltech.com", "xxxx");

					if (!string.IsNullOrEmpty(Model.Port))
                        client.Port = Convert.ToInt32(Model.Port);
                    
                    client.Send(Message);
                    return;
                }
                catch (Exception ex)
                {
                    if(!ex.GetBaseException().Message.Contains("The transport failed to connect to the server"))
                        throw ex;
                }
            }

            throw new EmailException("The transport failed to connect to the server. Please verify the email server configurations.");
        }

        public void ValidateMessage()
        {
            if (Message.To.Count > 0)
            {
                if (!ValidateAddress(Message.To.ToString()))
                    throw new EmailException("The 'To' email address '" + Message.To.ToString() + "' is not valid.");
            }
            else
                throw new EmailException("The 'To' address for the email was not provided.");

            if (string.IsNullOrEmpty(Message.Subject))
                throw new EmailException("A subject for the email was not provided.");

            if (Message.From != null && !string.IsNullOrEmpty(Message.From.ToString()))
            {
                if (!ValidateAddress(From))
                    throw new EmailException("The 'From' email address '" + From + "' is not valid.");
            }
            else if (!string.IsNullOrEmpty(Model.DefaultFromAddress))
            {
                //if(ValidateAddress(Model.DefaultFromAddress))
                //    Message.From.
            }
            else
                throw new EmailException("The 'From' address for the email was not provided.");

            if (Message.CC != null && !string.IsNullOrEmpty(Message.CC.ToString()))
            {
                if (!ValidateAddress(Message.CC.ToString()))
                    throw new EmailException("The 'CC' email address '" + Message.CC.ToString() + "' is not valid.");
            }

            if (Message.Bcc != null && !string.IsNullOrEmpty(Message.Bcc.ToString()))
            {
                if (!ValidateAddress(Message.Bcc.ToString()))
                    throw new EmailException("The 'BCC' email address '" + Message.Bcc.ToString() + "' is not valid.");
            }
        }

        private bool ValidateAddress(string address)
        {
            List<string> collection = new List<string>();

            try
            {
                if (address.Contains(","))
                {
                    collection = address.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    address = "";
                }
                else if (address.Contains(";"))
                {
                    collection = address.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    address = "";
                }

                if (collection.Count > 0)
                {
                    foreach (string entry in collection)
                    {
                        if (!ValidateAddress(entry.Trim()))
                            return false;
                    }
                }
                else
                {
                    if (address.Contains("@"))
                        return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new EmailException("Unable to validate the email address '" + address + "'.", ex);
            }
        }

        public void AddAttachment(string file_name, string format = System.Net.Mime.MediaTypeNames.Application.Octet)
        {
            try
            {
                if (System.IO.File.Exists(file_name))
                {
                    Message.Attachments.Add(new NetMail.Attachment(file_name, format));
                }
                else
                    throw new Exception("Attachment '" + file_name + "' was not found.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void AddAddressWithSemiColons(NetMail.MailAddressCollection collection, string addresses)
        {
            foreach (string address in addresses.Trim().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrEmpty(address))
                    collection.Add(address.Trim());
            }
        }

        public void Dispose()
        {
            if(Message != null)
            {
                Message.Dispose();
                Message = null;
            }
        }
    }

    public class EmailException : Exception
    {
        public EmailException (string message) 
            : base(message)
        {}

        public EmailException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
